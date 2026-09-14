using System;
using CommonUX.Core;
using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// 논리적 화면 핸들과 기존 VisualElement를 연결합니다.
    /// 요소를 재부모화하지 않으므로 USS 경로와 상속된 dataSource를 유지합니다.
    /// Dispose는 연결을 해제하고 등록 전 UI 속성을 복원하며, 요소 자체를 파괴하지 않습니다.
    /// </summary>
    public sealed class ScreenViewAdapter : IDisposable
    {
        private readonly StyleEnum<DisplayStyle> originalDisplay;
        private readonly bool originalEnabled;
        private readonly bool originalFocusable;
        private readonly int originalTabIndex;
        private bool disposed;

        public ScreenHandle Handle { get; }
        public VisualElement Root { get; }
        public Func<VisualElement> DefaultFocus { get; }
        public bool IsModal { get; }
        internal VisualElement LastFocused { get; set; }

        internal ScreenViewAdapter(ScreenHandle handle, VisualElement root, bool isModal,
            Func<VisualElement> defaultFocus)
        {
            Handle = handle;
            Root = root ?? throw new ArgumentNullException(nameof(root));
            IsModal = isModal;
            DefaultFocus = defaultFocus;
            originalDisplay = root.style.display;
            originalEnabled = root.enabledSelf;
            originalFocusable = root.focusable;
            originalTabIndex = root.tabIndex;
        }

        internal void Apply(bool visible, bool interactive)
        {
            if (disposed) return;
            // 활성 여부는 런타임이 소유하므로 USS의 초기 display:none보다 우선합니다.
            Root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            // Covered는 살아 있는 화면입니다. 자식의 개별 enabledSelf는 변경하지 않고
            // 루트에서만 상호작용을 제한하여 복귀 시 선택 상태와 비활성 버튼을 보존합니다.
            Root.SetEnabled(interactive && originalEnabled);
            Root.EnableInClassList("commonux-screen--active", interactive);
            Root.EnableInClassList("commonux-screen--covered", visible && !interactive);
            Root.EnableInClassList("commonux-screen--inactive", !visible);
        }

        internal void FocusRoot()
        {
            if (!Root.enabledInHierarchy) return;
            Root.focusable = true;
            Root.tabIndex = -1;
            Root.Focus();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Root.style.display = originalDisplay;
            Root.SetEnabled(originalEnabled);
            Root.focusable = originalFocusable;
            Root.tabIndex = originalTabIndex;
            Root.RemoveFromClassList("commonux-screen--active");
            Root.RemoveFromClassList("commonux-screen--covered");
            Root.RemoveFromClassList("commonux-screen--inactive");
            LastFocused = null;
        }
    }
}
