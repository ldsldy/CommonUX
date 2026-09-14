using System;
using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// 한 UIDocument 안에서 활성 모달 바깥으로 향하는 포인터·키보드 입력을 차단합니다.
    /// 전역 입력이나 다른 UIDocument의 입력 소유권은 프로젝트의 정책으로 연결해야 합니다.
    /// </summary>
    public sealed class ModalBarrier : IDisposable
    {
        private readonly VisualElement documentRoot;
        private readonly Func<ScreenViewAdapter> getActiveModal;
        private readonly Action restoreFocus;
        private bool restoring;
        private bool disposed;

        public ModalBarrier(VisualElement documentRoot, Func<ScreenViewAdapter> getActiveModal,
            Action restoreFocus)
        {
            this.documentRoot = documentRoot ?? throw new ArgumentNullException(nameof(documentRoot));
            this.getActiveModal = getActiveModal ?? throw new ArgumentNullException(nameof(getActiveModal));
            this.restoreFocus = restoreFocus ?? throw new ArgumentNullException(nameof(restoreFocus));
            Register<PointerDownEvent>();
            Register<PointerUpEvent>();
            Register<PointerMoveEvent>();
            Register<ClickEvent>();
            Register<WheelEvent>();
            Register<KeyDownEvent>();
            Register<KeyUpEvent>();
            Register<NavigationMoveEvent>();
            Register<NavigationSubmitEvent>();
            documentRoot.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
        }

        private void Register<T>() where T : EventBase<T>, new()
        {
            documentRoot.RegisterCallback<T>(OnInput, TrickleDown.TrickleDown);
        }

        private void Unregister<T>() where T : EventBase<T>, new()
        {
            documentRoot.UnregisterCallback<T>(OnInput, TrickleDown.TrickleDown);
        }

        private bool IsOutside(EventBase evt)
        {
            var modal = getActiveModal();
            if (modal == null) return false;
            var target = evt.target as VisualElement;
            return target == null || (target != modal.Root && !modal.Root.Contains(target));
        }

        private void OnInput<T>(T evt) where T : EventBase<T>, new()
        {
            if (IsOutside(evt)) evt.StopImmediatePropagation();
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (restoring || !IsOutside(evt)) return;
            evt.StopImmediatePropagation();
            restoring = true;
            try
            {
                // Tab 이동이나 외부 코드의 Focus 요청도 모달 안으로 되돌립니다.
                (evt.target as VisualElement)?.Blur();
                restoreFocus();
            }
            finally { restoring = false; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Unregister<PointerDownEvent>();
            Unregister<PointerUpEvent>();
            Unregister<PointerMoveEvent>();
            Unregister<ClickEvent>();
            Unregister<WheelEvent>();
            Unregister<KeyDownEvent>();
            Unregister<KeyUpEvent>();
            Unregister<NavigationMoveEvent>();
            Unregister<NavigationSubmitEvent>();
            documentRoot.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
        }
    }
}
