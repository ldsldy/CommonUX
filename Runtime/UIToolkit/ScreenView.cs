using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// 하나의 활성화 단위를 표현합니다. 화면이 닫혀도 요소와 데이터는 유지됩니다.
    /// 프로젝트는 이 클래스를 상속하여 enum, 태그, 에셋 등의 ID를 노출할 수 있습니다.
    /// </summary>
    [UxmlElement]
    public partial class ScreenView : VisualElement
    {
        [UxmlAttribute("default-focus")]
        public string DefaultFocus { get; set; }

        [UxmlAttribute("modal")]
        public bool IsModal { get; set; }

        [UxmlAttribute("blocks-gameplay-input")]
        public bool BlocksGameplayInput { get; set; } = true;

        public ScreenView()
        {
            AddToClassList("commonux-screen");
            style.flexGrow = 1;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            // 포커스를 받을 자식이 없는 화면도 프로그램으로 포커스를 복원할 수 있습니다.
            focusable = true;
            tabIndex = -1;
        }
    }
}
