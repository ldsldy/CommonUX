using System;
using System.Collections.Generic;
using CommonUX.Core;
using UnityEngine.InputSystem;

namespace CommonUX.InputSystem
{
    /// <summary>
    /// 정렬·새로고침 등 추가 Button 액션을 현재 화면의 명령에 연결합니다.
    /// 액션의 활성화와 해제는 호출자가 소유하며, 기본 UI 탐색은 UI Toolkit에 맡깁니다.
    /// </summary>
    public sealed class InputCommandBinding : IDisposable
    {
        /// <summary>
        /// Context와 액션당 하나의 구독으로 처리합니다. 같은 눌림이 새로 열린 화면까지 실행되는 것을 막습니다.
        /// 현재 화면의 명령 우선순위에서 첫 바인딩 하나만 선택하고, 실행 불가이면 그 입력을 종료합니다.
        /// </summary>
        private sealed class Dispatcher
        {
            internal readonly UiContext Context;
            internal readonly InputAction Action;
            internal readonly List<InputCommandBinding> Bindings = new List<InputCommandBinding>();

            internal Dispatcher(UiContext context, InputAction action)
            {
                Context = context;
                Action = action;
                Action.performed += OnPerformed;
                Context.Changed += OnContextChanged;
            }

            private void OnPerformed(InputAction.CallbackContext callback)
            {
                if (Context.IsDisposed) return;
                var commands = Context.GetActiveCommands();
                for (var i = 0; i < commands.Count; i++)
                {
                    for (var j = 0; j < Bindings.Count; j++)
                    {
                        var binding = Bindings[j];
                        if (binding._disposed || !ReferenceEquals(binding._command, commands[i])) continue;
                        // 사용자 콜백 후에는 소유 화면과 등록 목록이 바뀔 수 있으므로 재탐색하지 않습니다.
                        Context.TryExecute(commands[i]);
                        return;
                    }
                }
            }

            internal void Remove(InputCommandBinding binding)
            {
                Bindings.Remove(binding);
                if (Bindings.Count != 0) return;
                Detach();
            }

            private void OnContextChanged()
            {
                if (!Context.IsDisposed) return;
                // Context 수명이 끝난 경우 외부 액션이 살아 있어도 정적 연결 목록을 남기지 않습니다.
                for (var i = 0; i < Bindings.Count; i++) Bindings[i]._disposed = true;
                Bindings.Clear();
                Detach();
            }

            private void Detach()
            {
                Action.performed -= OnPerformed;
                Context.Changed -= OnContextChanged;
                Dispatchers.Remove((Context, Action));
            }
        }

        private static readonly Dictionary<(UiContext, InputAction), Dispatcher> Dispatchers =
            new Dictionary<(UiContext, InputAction), Dispatcher>();
        private readonly InputAction _action;
        private readonly UiCommand _command;
        private readonly Dispatcher _dispatcher;
        private bool _disposed;

        public InputCommandBinding(UiContext context, InputAction action, UiCommand command)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.IsDisposed) throw new ObjectDisposedException(nameof(context));
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _command = command ?? throw new ArgumentNullException(nameof(command));

            if (IsStandardUiActionName(action.name))
                throw new ArgumentException("Standard UI actions belong to UI Toolkit. Bind an extra command action instead.", nameof(action));
            if (action.type != InputActionType.Button)
                throw new ArgumentException("Extra commands require an InputAction of type Button.", nameof(action));

            if (!Dispatchers.TryGetValue((context, action), out _dispatcher))
            {
                _dispatcher = new Dispatcher(context, action);
                Dispatchers.Add((context, action), _dispatcher);
            }
            _dispatcher.Bindings.Add(this);
        }

        /// <summary>
        /// Unity 기본 UI 액션 이름을 검사합니다. 별칭으로 이름을 바꾼 기본 액션도 연결하면 안 됩니다.
        /// 이름 검사만으로 동일 물리 키에 대한 사용자 정의 중복 바인딩까지 판별할 수는 없습니다.
        /// </summary>
        public static bool IsStandardUiActionName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            switch (name.Trim().ToLowerInvariant())
            {
                case "submit": case "cancel": case "navigate": case "navigation":
                case "point": case "click": case "leftclick": case "rightclick":
                case "middleclick": case "scrollwheel": case "scroll":
                case "trackeddeviceposition": case "trackeddeviceorientation":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>이 연결의 콜백만 제거합니다. 공유 액션을 Disable하거나 Dispose하지 않습니다.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _dispatcher.Remove(this);
        }
    }
}
