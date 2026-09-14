using System;
using System.Collections.Generic;
using CommonUX.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// 한 UIDocument와 CommonUX 런타임을 연결하는 수명 소유자입니다.
    /// 화면 ID와 레이어 구성은 외부 바인더가 RegisterStack/RegisterScreen으로 지정합니다.
    /// 비활성화하면 연결을 정리하며, 재활성화 시 외부 바인더가 새 Context에 다시 등록해야 합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CommonUX/Common UX Host")]
    public sealed class CommonUxHost : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        private readonly List<ScreenViewAdapter> screens = new List<ScreenViewAdapter>();
        private readonly Dictionary<ScreenHandle, ScreenViewAdapter> byHandle =
            new Dictionary<ScreenHandle, ScreenViewAdapter>();
        private readonly Dictionary<VisualElement, ScreenStack> stacks =
            new Dictionary<VisualElement, ScreenStack>();
        private readonly List<IDisposable> bindings = new List<IDisposable>();
        private readonly Dictionary<ScreenHandle, List<IDisposable>> screenBindings =
            new Dictionary<ScreenHandle, List<IDisposable>>();
        private readonly FocusService focus = new FocusService();
        private UiContext context;
        private VisualElement root;
        private ModalBarrier modalBarrier;
        private ScreenViewAdapter activeView;
        private bool shuttingDown;
        private bool initializationRequested;
        private bool gameplayBlocked;

        public event Action Ready;

        /// <summary>
        /// 게임 측에서 이 알림을 받아 자체 입력 맵이나 폴링 처리의 허용 여부를 전환합니다.
        /// CommonUX 자체는 게임을 정지하거나 프로젝트 InputActionMap을 변경하지 않습니다.
        /// </summary>
        public event Action<bool> GameplayInputBlockedChanged;

        public UIDocument Document
        {
            get => document;
            set
            {
                if (context != null) throw new InvalidOperationException("Shut down the host before changing its document.");
                document = value;
            }
        }

        public UiContext Context => context;
        public bool IsInitialized => context != null;
        public bool BlocksGameplayInput => gameplayBlocked;
        public IReadOnlyList<ScreenViewAdapter> ScreenViews => screens.AsReadOnly();

        private void OnEnable()
        {
            initializationRequested = true;
            if (document == null) document = GetComponent<UIDocument>();
            if (document != null && document.isActiveAndEnabled) Initialize();
        }

        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();

        private void Update()
        {
            if (!initializationRequested) return;
            if (document == null || !document.isActiveAndEnabled)
            {
                ShutdownCore();
                return;
            }
            if (context != null && root != document.rootVisualElement) ShutdownCore();
            if (context == null && document.rootVisualElement?.panel != null) Initialize();
        }

        public void Initialize()
        {
            if (document == null) throw new InvalidOperationException("Assign a UIDocument before initializing CommonUX.");
            if (!document.isActiveAndEnabled)
                throw new InvalidOperationException("Enable the UIDocument before initializing CommonUX.");
            initializationRequested = true;
            if (context != null && root == document.rootVisualElement) return;
            if (context != null) ShutdownCore();
            root = document.rootVisualElement;
            if (root == null) throw new InvalidOperationException("The UIDocument visual tree is not ready.");
            context = new UiContext();
            context.Changed += Synchronize;
            // 버블 단계에서만 Back을 처리하여 TextField 등 내부 컨트롤의 Cancel 처리를 존중합니다.
            root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            root.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
            modalBarrier = new ModalBarrier(root, GetActiveModal, RestoreFocus);
            try { Ready?.Invoke(); }
            catch { Shutdown(); throw; }
        }

        public ScreenStack RegisterStack(VisualElement container, int priority = 0, bool allowEmpty = true)
        {
            RequireInitialized();
            RequireInDocument(container);
            if (stacks.ContainsKey(container)) throw new InvalidOperationException("This container is already registered.");
            var stack = context.CreateStack(priority, allowEmpty);
            stacks.Add(container, stack);
            return stack;
        }

        public ScreenHandle RegisterScreen(VisualElement element, ScreenOptions options = null,
            Func<VisualElement> defaultFocus = null)
        {
            RequireInitialized();
            RequireInDocument(element);
            foreach (var existing in screens)
            {
                if (element == existing.Root || element.Contains(existing.Root) || existing.Root.Contains(element))
                    throw new InvalidOperationException("Registered screens must have separate, non-overlapping roots.");
            }

            if (element is ScreenView view)
            {
                if (options == null) options = new ScreenOptions(view.IsModal, view.BlocksGameplayInput);
                if (defaultFocus == null)
                    defaultFocus = () => string.IsNullOrEmpty(view.DefaultFocus) ? null : view.Q(view.DefaultFocus);
            }
            options = options ?? new ScreenOptions();
            var handle = context.RegisterScreen(options);
            var adapter = new ScreenViewAdapter(handle, element, options.IsModal, defaultFocus);
            screens.Add(adapter);
            byHandle.Add(handle, adapter);
            // 명령 콜백 도중 등록하면 Core에는 아직 예약된 핸들입니다.
            // 최초 표시를 끄고 다음 확정 Changed에서 상태를 읽습니다.
            adapter.Apply(false, false);
            return handle;
        }

        public void UnregisterScreen(ScreenHandle handle)
        {
            RequireInitialized();
            if (!byHandle.TryGetValue(handle, out var adapter))
                throw new ArgumentException("This screen is not registered with this host.", nameof(handle));
            byHandle.Remove(handle);
            screens.Remove(adapter);
            var errors = new List<Exception>();
            try
            {
                if (screenBindings.TryGetValue(handle, out var owned))
                {
                    screenBindings.Remove(handle);
                    foreach (var binding in owned)
                    {
                        bindings.Remove(binding);
                        try { binding.Dispose(); }
                        catch (Exception error) { errors.Add(error); }
                    }
                }
                try { context.UnregisterScreen(handle); }
                catch (Exception error) { errors.Add(error); }
            }
            finally { adapter.Dispose(); }
            if (errors.Count > 0) throw new AggregateException("Screen cleanup completed with callback errors.", errors);
        }

        public IDisposable BindButton(Button button, ScreenHandle owner, UiCommand command)
        {
            RequireInitialized();
            if (!byHandle.TryGetValue(owner, out var adapter) || !adapter.Root.Contains(button))
                throw new ArgumentException("The button must belong to the registered screen.", nameof(button));
            var binding = new CommandBinding(context, button, owner, command);
            bindings.Add(binding);
            if (!screenBindings.TryGetValue(owner, out var owned))
                screenBindings.Add(owner, owned = new List<IDisposable>());
            owned.Add(binding);
            return binding;
        }

        public void Refresh() => context?.Refresh();

        private void OnCancel(NavigationCancelEvent evt)
        {
            if (context != null && context.TryGoBack()) evt.StopPropagation();
        }

        private void OnRootDetached(DetachFromPanelEvent evt)
        {
            if (evt.target == root) ShutdownCore();
        }

        private ScreenViewAdapter GetActiveModal() => activeView != null && activeView.IsModal ? activeView : null;
        private void RestoreFocus() => focus.Restore(activeView);

        private void Synchronize()
        {
            if (context == null || shuttingDown) return;
            byHandle.TryGetValue(context.ActiveScreen, out var next);
            var changed = next != activeView;
            if (changed) focus.Capture(activeView);
            // 반영 도중 이전 모달로 포커스를 되돌리지 않도록 입력 소유자를 먼저 갱신합니다.
            activeView = next;
            foreach (var screen in screens)
            {
                context.TryGetScreenState(screen.Handle, out var state);
                screen.Apply(state != ScreenState.Inactive, state == ScreenState.Active);
            }

            // 같은 부모를 공유하는 화면은 논리적인 스택 순서와 그리기 순서를 맞춥니다.
            // 부모가 다른 레이어의 레이아웃은 프로젝트가 구성하며 여기서 재부모화하지 않습니다.
            foreach (var pair in stacks)
            {
                foreach (var handle in pair.Value.Entries)
                    if (byHandle.TryGetValue(handle, out var screen) && screen.Root.parent == pair.Key)
                        screen.Root.BringToFront();
            }
            if (activeView != null)
            {
                activeView.Root.BringToFront();
                foreach (var container in stacks.Keys)
                    if (container.Contains(activeView.Root)) container.BringToFront();
            }

            if (changed)
            {
                if (activeView != null)
                {
                    var expected = activeView;
                    // 표시와 레이아웃 갱신 후 복원합니다. 이전 예약이 새 화면의 포커스를 빼앗지 못하게 합니다.
                    expected.Root.schedule.Execute(() =>
                    {
                        if (context != null && activeView == expected) focus.Restore(expected);
                    });
                }
                else (root.panel?.focusController.focusedElement as VisualElement)?.Blur();
            }

            var blocked = context.Snapshot.BlocksGameplayInput;
            if (blocked == gameplayBlocked) return;
            gameplayBlocked = blocked;
            GameplayInputBlockedChanged?.Invoke(blocked);
        }

        public void Shutdown()
        {
            initializationRequested = false;
            ShutdownCore();
        }

        private void ShutdownCore()
        {
            if (context == null || shuttingDown) return;
            shuttingDown = true;
            var errors = new List<Exception>();
            void Clean(Action action)
            {
                try { action(); }
                catch (Exception error) { errors.Add(error); }
            }

            context.Changed -= Synchronize;
            Clean(() => modalBarrier?.Dispose());
            root.UnregisterCallback<NavigationCancelEvent>(OnCancel);
            root.UnregisterCallback<DetachFromPanelEvent>(OnRootDetached);
            foreach (var binding in bindings) Clean(binding.Dispose);
            foreach (var screen in screens) Clean(screen.Dispose);
            foreach (var bar in root.Query<ActionBar>().ToList()) Clean(bar.Unbind);
            Clean(context.Dispose);
            screens.Clear();
            byHandle.Clear();
            stacks.Clear();
            bindings.Clear();
            screenBindings.Clear();
            context = null;
            root = null;
            activeView = null;
            modalBarrier = null;
            var wasBlocked = gameplayBlocked;
            gameplayBlocked = false;
            shuttingDown = false;
            if (wasBlocked) Clean(() => GameplayInputBlockedChanged?.Invoke(false));
            foreach (var error in errors) Debug.LogException(error, this);
        }

        private void RequireInitialized()
        {
            if (context == null) throw new InvalidOperationException("Initialize the CommonUX host first.");
        }

        private void RequireInDocument(VisualElement element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (element != root && !root.Contains(element))
                throw new ArgumentException("The element must belong to this host's UIDocument.", nameof(element));
        }
    }
}
