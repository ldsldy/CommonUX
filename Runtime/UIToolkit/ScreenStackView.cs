using UnityEngine.UIElements;

namespace CommonUX.UIToolkit
{
    /// <summary>
    /// UI Builder에서 화면 스택의 표시 영역을 구성하는 요소입니다.
    /// ID와 레이어 정책은 외부 구현이 소유하며, 순서는 Core.ScreenStack이 관리합니다.
    /// </summary>
    [UxmlElement]
    public partial class ScreenStackView : VisualElement
    {
        public ScreenStackView()
        {
            AddToClassList("commonux-stack");
            style.flexGrow = 1;
            pickingMode = PickingMode.Ignore;
        }
    }
}
