using CommonUX.UIToolkit;
using UnityEngine.UIElements;

namespace CommonUX.Samples.TypedNavigation
{
    /// <summary>이 샘플에서 사용하는 레이어 이름입니다. 패키지 자체는 이 enum을 알지 못합니다.</summary>
    public enum UiLayer
    {
        Menu,
        Modal
    }

    /// <summary>샘플 화면을 코드에서 구별하기 위한 프로젝트 소유 식별자입니다.</summary>
    public enum ScreenId
    {
        Home,
        Settings
    }

    /// <summary>UI Builder에서 샘플의 레이어 enum을 선택할 수 있게 하는 프로젝트 확장 요소입니다.</summary>
    [UxmlElement]
    public partial class SampleStackView : ScreenStackView
    {
        [UxmlAttribute("stack-id")]
        public UiLayer StackId { get; set; }
    }

    /// <summary>공통 화면 요소에 프로젝트의 화면 enum만 추가합니다. 화면 전환 상태는 호스트가 관리합니다.</summary>
    [UxmlElement]
    public partial class SampleScreenView : ScreenView
    {
        [UxmlAttribute("screen-id")]
        public ScreenId ScreenId { get; set; }
    }
}
