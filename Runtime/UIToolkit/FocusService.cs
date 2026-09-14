using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// 화면별 마지막 포커스를 보관하고 복귀 시 유효성을 다시 검사합니다.
    /// 동적으로 삭제되거나 숨겨진 대상 대신 기본 대상, 첫 컨트롤, 화면 루트 순으로 복원합니다.
    /// </summary>
    public sealed class FocusService
    {
        public void Capture(ScreenViewAdapter screen)
        {
            if (screen == null) return;
            var focused = screen.Root.panel?.focusController.focusedElement as VisualElement;
            if (focused != null && (focused == screen.Root || screen.Root.Contains(focused)))
                screen.LastFocused = focused;
        }

        public void Restore(ScreenViewAdapter screen)
        {
            if (screen == null || screen.Root.panel == null) return;
            var target = screen.LastFocused;
            if (!CanFocus(target, screen.Root)) target = screen.DefaultFocus?.Invoke();
            if (!CanFocus(target, screen.Root)) target = FindFirst(screen.Root);
            if (CanFocus(target, screen.Root)) target.Focus();
            else screen.FocusRoot();
        }

        internal static bool CanFocus(VisualElement element, VisualElement root)
        {
            if (element == null || element.panel == null || !element.focusable ||
                !element.enabledInHierarchy || (element != root && !root.Contains(element)))
                return false;

            // 자식의 display가 Flex여도 조상이 None이면 화면에 표시되지 않습니다.
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None ||
                    current.resolvedStyle.visibility == Visibility.Hidden)
                    return false;
            }
            return true;
        }

        private static VisualElement FindFirst(VisualElement root)
        {
            for (var i = 0; i < root.hierarchy.childCount; i++)
            {
                var child = root.hierarchy[i];
                if (child.tabIndex >= 0 && CanFocus(child, root)) return child;
                var descendant = FindFirst(child);
                if (descendant != null) return descendant;
            }
            return null;
        }
    }
}
