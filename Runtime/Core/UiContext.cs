using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace CommonUX.Core
{
    /// <summary>
    /// 화면의 활성 상태와 명령 소유권을 관리하는 단일 스레드 Core입니다.
    /// View, focus, Input System, 게임 입력 차단의 실제 적용은 외부 adapter가 담당합니다.
    /// </summary>
    public sealed class UiContext : IDisposable
    {
        /// <summary>화면 등록과 stack 소속, 등록 명령의 내부 수명 정보를 보관합니다.</summary>
        sealed class ScreenRecord
        {
            internal readonly ScreenHandle Handle;
            internal readonly ScreenOptions Options;
            internal ScreenStack Stack;
            internal ScreenStack PlannedStack;
            internal ScreenState State;
            internal readonly List<CommandRegistration> Commands = new List<CommandRegistration>();
            internal readonly List<CommandRegistration> PlannedCommands = new List<CommandRegistration>();

            internal ScreenRecord(ScreenHandle handle, ScreenOptions options)
            {
                Handle = handle;
                Options = options;
            }
        }

        /// <summary>명령 구독 하나의 해제 handle입니다. Dispose를 여러 번 호출해도 한 번만 해제합니다.</summary>
        sealed class CommandRegistration : IDisposable
        {
            readonly UiContext m_context;
            internal readonly ScreenRecord Screen;
            internal readonly UiCommand Command;
            internal readonly int Priority;
            internal readonly long Order;
            internal bool DisposeRequested;

            internal CommandRegistration(UiContext context, ScreenRecord screen, UiCommand command, int priority, long order)
            {
                m_context = context;
                Screen = screen;
                Command = command;
                Priority = priority;
                Order = order;
            }

            public void Dispose() => m_context.UnregisterCommand(this);
        }

        readonly Dictionary<int, ScreenRecord> m_screens = new Dictionary<int, ScreenRecord>();
        readonly Dictionary<int, ScreenRecord> m_plannedScreens = new Dictionary<int, ScreenRecord>();
        readonly List<ScreenStack> m_stacks = new List<ScreenStack>();
        readonly HashSet<ScreenStack> m_plannedStacks = new HashSet<ScreenStack>();
        readonly List<ScreenHandle> m_presentedScreens = new List<ScreenHandle>();
        readonly Queue<Action> m_mutations = new Queue<Action>();
        readonly List<Exception> m_errors = new List<Exception>();
        int m_nextScreenId;
        int m_nextStackId;
        long m_nextOrder;
        long m_version;
        bool m_processing;
        bool m_disposeRequested;
        bool m_hasPresentation;
        bool m_plannedHasPresentation;
        ScreenHandle m_presentationOwner;

        /// <summary>확정된 상태 변경을 알립니다. 구독자 안에서 요청한 변경은 구독자 호출 종료 뒤 처리됩니다.</summary>
        public event Action Changed;
        public bool IsDisposed { get; private set; }
        public UiStateSnapshot Snapshot { get; private set; }
        public ScreenHandle ActiveScreen => Snapshot.ActiveScreen;

        public UiContext()
        {
            Snapshot = new UiStateSnapshot(default, false, Array.Empty<UiCommand>(), 0);
        }

        public ScreenStack CreateStack(int priority = 0, bool allowEmpty = true)
        {
            ThrowIfUnavailable();
            var stack = new ScreenStack(this, new StackHandle(this, checked(++m_nextStackId)), priority, allowEmpty);
            m_plannedStacks.Add(stack);
            CommitOwnedRegistration(() => m_stacks.Add(stack), () => DisposeStack(stack));
            return stack;
        }

        public ScreenHandle RegisterScreen(ScreenOptions options = null)
        {
            ThrowIfUnavailable();
            var handle = new ScreenHandle(this, checked(++m_nextScreenId));
            var record = new ScreenRecord(handle, options ?? new ScreenOptions());
            m_plannedScreens.Add(handle.Id, record);
            CommitOwnedRegistration(() => m_screens.Add(handle.Id, record), () =>
            {
                if (!m_disposeRequested && m_plannedScreens.ContainsKey(handle.Id))
                    UnregisterScreen(handle);
            });
            return handle;
        }

        /// <summary>화면을 등록 해제하고 stack에서도 제거합니다. Pop과 달리 handle과 명령 등록을 재사용할 수 없습니다.</summary>
        public void UnregisterScreen(ScreenHandle screen)
        {
            ThrowIfUnavailable();
            ScreenRecord record = GetPlannedScreen(screen);
            m_plannedScreens.Remove(screen.Id);
            record.PlannedStack?.PlannedEntries.Remove(screen);
            record.PlannedStack = null;
            foreach (CommandRegistration registration in record.PlannedCommands)
                registration.DisposeRequested = true;
            record.PlannedCommands.Clear();
            Enqueue(() =>
            {
                record.Stack?.MutableEntries.Remove(screen);
                record.Stack = null;
                record.Commands.Clear();
                m_screens.Remove(screen.Id);
                m_presentedScreens.Remove(screen);
                if (m_presentationOwner == screen)
                    m_presentationOwner = default;
            });
        }

        /// <summary>
        /// 외부 UIManager가 결정한 표시 화면과 입력 소유자를 적용합니다. 화면 순서/history는 저장하지 않습니다.
        /// Stack 표시와 동시에 사용할 수 없으며, 입력 소유자가 있으면 표시 목록에 포함되어야 합니다.
        /// </summary>
        public void SetPresentation(ScreenHandle inputOwner, IReadOnlyList<ScreenHandle> visibleScreens)
        {
            ThrowIfUnavailable();
            if (visibleScreens == null)
                throw new ArgumentNullException(nameof(visibleScreens));
            foreach (ScreenStack stack in m_plannedStacks)
                if (stack.PlannedEntries.Count > 0)
                    throw new InvalidOperationException("Clear all stacks before setting an external presentation.");

            var copy = new ScreenHandle[visibleScreens.Count];
            var unique = new HashSet<ScreenHandle>();
            for (int i = 0; i < copy.Length; i++)
            {
                ScreenHandle screen = visibleScreens[i];
                GetPlannedScreen(screen);
                if (!unique.Add(screen))
                    throw new ArgumentException("A screen cannot appear twice in a presentation.", nameof(visibleScreens));
                copy[i] = screen;
            }
            if (inputOwner.IsValid)
            {
                GetPlannedScreen(inputOwner);
                if (!unique.Contains(inputOwner))
                    throw new ArgumentException("The input owner must be included in the visible screens.", nameof(inputOwner));
            }

            m_plannedHasPresentation = true;
            Enqueue(() =>
            {
                m_presentedScreens.Clear();
                m_presentedScreens.AddRange(copy);
                m_presentationOwner = inputOwner;
                m_hasPresentation = true;
            });
        }

        /// <summary>외부 표시 상태를 비우고 stack을 사용할 수 있게 합니다. 화면 등록과 명령은 유지합니다.</summary>
        public void ClearPresentation()
        {
            ThrowIfUnavailable();
            if (!m_plannedHasPresentation)
                return;
            m_plannedHasPresentation = false;
            Enqueue(() =>
            {
                m_presentedScreens.Clear();
                m_presentationOwner = default;
                m_hasPresentation = false;
            });
        }

        public ScreenState GetScreenState(ScreenHandle screen)
        {
            ThrowIfDisposed();
            return GetCommittedScreen(screen).State;
        }

        /// <summary>확정된 화면 상태를 조회합니다. 미확정 등록, 해제된 화면, 다른 context의 handle과 종료된 context에는 false를 반환합니다.</summary>
        public bool TryGetScreenState(ScreenHandle screen, out ScreenState state)
        {
            state = ScreenState.Inactive;
            if (IsDisposed || !screen.IsValid || !ReferenceEquals(screen.Owner, this) ||
                !m_screens.TryGetValue(screen.Id, out ScreenRecord record))
                return false;
            state = record.State;
            return true;
        }

        public bool IsVisible(ScreenHandle screen) => GetScreenState(screen) != ScreenState.Inactive;
        public bool IsInteractive(ScreenHandle screen) => GetScreenState(screen) == ScreenState.Active;

        /// <summary>현재 입력 소유 stack에서 뒤로 갑니다. AllowEmpty=false인 stack의 마지막 화면은 유지합니다.</summary>
        public bool TryGoBack()
        {
            ThrowIfUnavailable();
            if (!ActiveScreen.IsValid)
                return false;
            ScreenRecord record = GetCommittedScreen(ActiveScreen);
            ScreenStack stack = record.Stack;
            if (stack == null || !m_plannedStacks.Contains(stack))
                return false;
            if (stack.PlannedEntries.Count <= (stack.AllowEmpty ? 0 : 1))
                return false;
            return Pop(stack);
        }

        public IDisposable RegisterCommand(ScreenHandle owner, UiCommand command, int priority = 0)
        {
            ThrowIfUnavailable();
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            ScreenRecord record = GetPlannedScreen(owner);
            foreach (CommandRegistration existing in record.PlannedCommands)
                if (ReferenceEquals(existing.Command, command))
                    throw new InvalidOperationException("The command is already registered to this screen.");
            var registration = new CommandRegistration(this, record, command, priority, ++m_nextOrder);
            record.PlannedCommands.Add(registration);
            CommitOwnedRegistration(() => record.Commands.Add(registration), registration.Dispose);
            return registration;
        }

        /// <summary>현재 입력 소유자의 명령을 높은 priority, 등록 순서로 반환합니다. 실행 불가능한 명령도 안내용으로 포함합니다.</summary>
        public IReadOnlyList<UiCommand> GetActiveCommands() => Snapshot.Commands;

        /// <summary>
        /// 호출 시점 입력 소유자의 명령을 실행합니다. callback이 요청한 화면 변경은 실행 뒤 처리하므로
        /// 같은 입력으로 새 화면 명령까지 실행하지 않습니다. predicate/execute 예외도 변경 처리 후 다시 전달합니다.
        /// </summary>
        public bool TryExecute(UiCommand command)
        {
            ThrowIfUnavailable();
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            bool eligible = false;
            foreach (UiCommand candidate in Snapshot.Commands)
                if (ReferenceEquals(candidate, command)) { eligible = true; break; }
            if (!eligible)
                return false;

            if (m_processing)
                return ExecuteCaptured(command);

            bool executed = false;
            m_processing = true;
            try
            {
                try { executed = ExecuteCaptured(command); }
                catch (Exception error) { m_errors.Add(error); }
                DrainMutations();
            }
            finally { m_processing = false; }
            ThrowCollectedErrors();
            return executed;
        }

        /// <summary>외부 데이터의 변경으로 CanExecute 등이 달라졌을 때 안내와 view의 갱신을 요청합니다.</summary>
        public void Refresh()
        {
            ThrowIfUnavailable();
            Enqueue(() => { });
        }

        internal void Push(ScreenStack stack, ScreenHandle screen)
        {
            ValidateStack(stack);
            if (m_plannedHasPresentation)
                throw new InvalidOperationException("Clear the external presentation before pushing screens to a stack.");
            ScreenRecord record = GetPlannedScreen(screen);
            if (record.PlannedStack != null)
                throw new InvalidOperationException("A screen can belong to only one stack and cannot be pushed twice.");
            record.PlannedStack = stack;
            stack.PlannedEntries.Add(screen);
            long order = ++m_nextOrder;
            Enqueue(() =>
            {
                record.Stack = stack;
                stack.MutableEntries.Add(screen);
                stack.LastInteraction = order;
            });
        }

        internal bool Pop(ScreenStack stack)
        {
            ValidateStack(stack);
            if (stack.PlannedEntries.Count == 0)
                return false;
            int top = stack.PlannedEntries.Count - 1;
            ScreenHandle screen = stack.PlannedEntries[top];
            ScreenRecord record = GetPlannedScreen(screen);
            stack.PlannedEntries.RemoveAt(top);
            record.PlannedStack = null;
            long order = ++m_nextOrder;
            Enqueue(() =>
            {
                stack.MutableEntries.Remove(screen);
                record.Stack = null;
                stack.LastInteraction = order;
            });
            return true;
        }

        internal void Clear(ScreenStack stack)
        {
            ValidateStack(stack);
            if (stack.PlannedEntries.Count == 0)
                return;
            foreach (ScreenHandle screen in stack.PlannedEntries)
                GetPlannedScreen(screen).PlannedStack = null;
            stack.PlannedEntries.Clear();
            long order = ++m_nextOrder;
            Enqueue(() =>
            {
                foreach (ScreenHandle screen in stack.MutableEntries)
                    m_screens[screen.Id].Stack = null;
                stack.MutableEntries.Clear();
                stack.LastInteraction = order;
            });
        }

        internal void DisposeStack(ScreenStack stack)
        {
            if (m_disposeRequested || !m_plannedStacks.Contains(stack))
                return;
            m_plannedStacks.Remove(stack);
            foreach (ScreenHandle screen in stack.PlannedEntries)
                GetPlannedScreen(screen).PlannedStack = null;
            stack.PlannedEntries.Clear();
            Enqueue(() =>
            {
                foreach (ScreenHandle screen in stack.MutableEntries)
                    m_screens[screen.Id].Stack = null;
                stack.MutableEntries.Clear();
                stack.IsDisposed = true;
                m_stacks.Remove(stack);
            });
        }

        void UnregisterCommand(CommandRegistration registration)
        {
            if (registration.DisposeRequested || m_disposeRequested)
                return;
            registration.DisposeRequested = true;
            registration.Screen.PlannedCommands.Remove(registration);
            Enqueue(() => registration.Screen.Commands.Remove(registration));
        }

        public void Dispose()
        {
            if (m_disposeRequested)
                return;
            m_disposeRequested = true;
            m_plannedScreens.Clear();
            m_plannedStacks.Clear();
            m_plannedHasPresentation = false;
            Enqueue(() =>
            {
                foreach (ScreenStack stack in m_stacks)
                {
                    stack.MutableEntries.Clear();
                    stack.PlannedEntries.Clear();
                    stack.IsDisposed = true;
                }
                foreach (ScreenRecord record in m_screens.Values)
                {
                    record.Commands.Clear();
                    record.PlannedCommands.Clear();
                    record.Stack = null;
                    record.PlannedStack = null;
                }
                m_stacks.Clear();
                m_screens.Clear();
                m_presentedScreens.Clear();
                m_presentationOwner = default;
                m_hasPresentation = false;
                IsDisposed = true;
            });
        }

        static bool ExecuteCaptured(UiCommand command)
        {
            if (!command.CanExecute)
                return false;
            command.Execute();
            return true;
        }

        void ValidateStack(ScreenStack stack)
        {
            ThrowIfUnavailable();
            if (stack == null)
                throw new ArgumentNullException(nameof(stack));
            if (!ReferenceEquals(stack.Handle.Owner, this))
                throw new ArgumentException("The stack belongs to another UiContext.", nameof(stack));
            if (!m_plannedStacks.Contains(stack))
                throw new ObjectDisposedException(nameof(ScreenStack));
        }

        void ValidateOwner(ScreenHandle screen)
        {
            if (!screen.IsValid || !ReferenceEquals(screen.Owner, this))
                throw new ArgumentException("The screen handle is empty or belongs to another UiContext.", nameof(screen));
        }

        ScreenRecord GetPlannedScreen(ScreenHandle screen)
        {
            ValidateOwner(screen);
            if (!m_plannedScreens.TryGetValue(screen.Id, out ScreenRecord record))
                throw new InvalidOperationException("The screen handle has been unregistered.");
            return record;
        }

        ScreenRecord GetCommittedScreen(ScreenHandle screen)
        {
            ValidateOwner(screen);
            if (!m_screens.TryGetValue(screen.Id, out ScreenRecord record))
                throw new InvalidOperationException("The screen is unregistered or its queued registration has not committed yet.");
            return record;
        }

        void ThrowIfUnavailable()
        {
            if (m_disposeRequested)
                throw new ObjectDisposedException(nameof(UiContext));
        }

        void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(UiContext));
        }

        void Enqueue(Action mutation)
        {
            m_mutations.Enqueue(mutation);
            if (m_processing)
                return;
            m_processing = true;
            try { DrainMutations(); }
            finally { m_processing = false; }
            ThrowCollectedErrors();
        }

        // 알림 실패로 소유권 handle을 반환하지 못한 경우에는 그 등록만 정리합니다.
        // Callback에서 이미 반환된 deferred 등록과 다른 구독자가 완료한 변경은 되돌리지 않습니다.
        void CommitOwnedRegistration(Action registration, Action cleanup)
        {
            try { Enqueue(registration); }
            catch (Exception originalError)
            {
                try { cleanup(); }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("Registration failed and its ownership cleanup also reported an error.",
                        originalError, cleanupError);
                }
                throw;
            }
        }

        void DrainMutations()
        {
            while (m_mutations.Count > 0)
            {
                Action mutation = m_mutations.Dequeue();
                mutation();
                RebuildSnapshot();
                // 개별 구독자의 실패가 다른 구독자 및 이미 요청된 변경을 누락시키지 않게 합니다.
                Action changed = Changed;
                if (changed != null)
                {
                    foreach (Action callback in changed.GetInvocationList())
                    {
                        try { callback(); }
                        catch (Exception error) { m_errors.Add(error); }
                    }
                }
                if (IsDisposed)
                    Changed = null;
            }
        }

        void RebuildSnapshot()
        {
            foreach (ScreenRecord record in m_screens.Values)
                record.State = ScreenState.Inactive;

            if (m_hasPresentation)
            {
                foreach (ScreenHandle screen in m_presentedScreens)
                    m_screens[screen.Id].State = ScreenState.Covered;
                PublishOwnerSnapshot(m_presentationOwner);
                return;
            }

            ScreenStack ownerStack = null;
            foreach (ScreenStack stack in m_stacks)
            {
                if (stack.MutableEntries.Count == 0)
                    continue;
                if (ownerStack == null || stack.Priority > ownerStack.Priority ||
                    (stack.Priority == ownerStack.Priority && stack.LastInteraction > ownerStack.LastInteraction))
                    ownerStack = stack;

                // Modal은 아래 화면을 그리는 정책이며, 아래 화면에 입력을 허용한다는 의미는 아닙니다.
                for (int i = stack.MutableEntries.Count - 1; i >= 0; i--)
                {
                    ScreenRecord visible = m_screens[stack.MutableEntries[i].Id];
                    visible.State = ScreenState.Covered;
                    if (!visible.Options.IsModal)
                        break;
                }
            }

            ScreenHandle active = ownerStack == null ? default : ownerStack.MutableEntries[ownerStack.MutableEntries.Count - 1];
            PublishOwnerSnapshot(active);
        }

        void PublishOwnerSnapshot(ScreenHandle active)
        {
            if (!active.IsValid)
            {
                Snapshot = new UiStateSnapshot(default, false, Array.Empty<UiCommand>(), ++m_version);
                return;
            }

            ScreenRecord activeRecord = m_screens[active.Id];
            activeRecord.State = ScreenState.Active;
            var registrations = new List<CommandRegistration>(activeRecord.Commands);
            registrations.Sort((left, right) =>
            {
                int priority = right.Priority.CompareTo(left.Priority);
                return priority != 0 ? priority : left.Order.CompareTo(right.Order);
            });
            var commands = new UiCommand[registrations.Count];
            for (int i = 0; i < registrations.Count; i++)
                commands[i] = registrations[i].Command;
            Snapshot = new UiStateSnapshot(active, activeRecord.Options.BlocksGameplayInput, commands, ++m_version);
        }

        void ThrowCollectedErrors()
        {
            if (m_errors.Count == 0)
                return;
            Exception[] errors = m_errors.ToArray();
            m_errors.Clear();
            if (errors.Length == 1)
                ExceptionDispatchInfo.Capture(errors[0]).Throw();
            throw new AggregateException("One or more CommonUX callbacks failed after queued changes were committed.", errors);
        }
    }
}
