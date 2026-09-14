using System;
using CommonUX.Core;
using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// 기본 Button을 화면 명령에 연결하고 등록·해제 수명을 함께 소유합니다.
    /// clicked를 사용하므로 UI Toolkit의 포인터 클릭과 NavigationSubmit 경로를 공유합니다.
    /// 연결 중 버튼의 enabledSelf는 명령의 CanExecute가 결정합니다.
    /// </summary>
    public sealed class CommandBinding : IDisposable
    {
        private readonly UiContext context;
        private readonly Button button;
        private readonly UiCommand command;
        private readonly IDisposable registration;
        private readonly bool originalEnabled;
        private bool disposed;

        public CommandBinding(UiContext context, Button button, ScreenHandle owner, UiCommand command)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.button = button ?? throw new ArgumentNullException(nameof(button));
            this.command = command ?? throw new ArgumentNullException(nameof(command));
            originalEnabled = button.enabledSelf;
            registration = context.RegisterCommand(owner, command);
            button.clicked += OnClicked;
            context.Changed += Refresh;
            try { Refresh(); }
            catch { Dispose(); throw; }
        }

        public void Refresh()
        {
            if (!disposed) button.SetEnabled(command.CanExecute);
        }

        private void OnClicked()
        {
            if (!disposed) context.TryExecute(command);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            button.clicked -= OnClicked;
            context.Changed -= Refresh;
            try { registration.Dispose(); }
            finally { button.SetEnabled(originalEnabled); }
        }
    }
}
