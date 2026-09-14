# 구조와 연결 계약

CommonUX는 UI Toolkit을 대체하는 렌더러가 아닙니다. UI Toolkit은 레이아웃, 스타일, 기본 컨트롤, 포인터와 내비게이션 이벤트를 처리하고, CommonUX는 여러 화면 사이의 상태와 입력 권한을 관리합니다.

## 세 계층

```text
프로젝트: 화면 enum · 게임 상태 · 표시 모델 · 저장 · 씬 전환
                       ↓
CommonUX.Core: UiContext · ScreenStack · ScreenHandle · UiCommand
                       ↕
CommonUX.UIToolkit: CommonUxHost · ScreenStackView · ScreenView
                       ↕
Unity: UIDocument · VisualElement · Button · FocusController

CommonUX.InputSystem: 장치 및 프로젝트 액션과 공통 UI 정책의 연결
```

Core는 시각 트리를 직접 수정하지 않습니다. UIToolkit 계층은 Core의 상태를 기존 요소의 표시·입력·포커스에 반영합니다. Input System 계층은 게임 프로젝트의 입력 설정과 연결되는 별도 어댑터입니다.

## 논리 객체와 실제 요소

`ScreenStack`은 화면 순서와 Back 처리 같은 논리 상태를 가집니다. `ScreenStackView`는 그 스택에 연결할 수 있는 `VisualElement` 컨테이너입니다. `ScreenHandle`은 등록된 한 화면을 나타내며, `ScreenView`는 UXML에서 화면의 기본 옵션을 지정할 수 있는 요소입니다.

일반 요소를 호스트에 직접 등록하는 방식과 커스텀 UXML 요소를 사용하는 방식은 같은 Core 객체에 연결됩니다. 두 방식이 별도 스택이나 별도 입력 시스템을 만들지 않습니다. 커스텀 요소를 선언했다는 이유만으로 Unity가 화면 스택 기능을 실행하는 것도 아닙니다. 문서와 화면을 등록한 CommonUX 호스트가 상태를 반영합니다.

`ScreenStackView`와 `ScreenView`에는 범용 문자열 ID를 두지 않습니다. 프로젝트는 enum, 객체 참조, 자체 카탈로그 등 필요한 식별 체계를 선택합니다. 샘플의 `UiLayer`, `ScreenId` 및 UXML 속성은 이 확장 지점의 예시입니다.

## 문서 수명과 화면 수명

권장 연결 순서는 다음과 같습니다.

1. `UIDocument`의 자산과 패널을 구성합니다.
2. `CommonUxHost`의 `Ready`를 구독하고 호스트를 초기화합니다.
3. 실제 트리에서 스택 컨테이너와 화면 루트를 찾아 한 번씩 등록합니다.
4. 화면 데이터와 명령을 연결한 뒤 초기 화면을 Push합니다.
5. 문서를 해제할 때 프로젝트가 만든 바인딩과 외부 구독을 정리합니다.

`Initialize`는 같은 문서에 반복 호출해도 중복 초기화하지 않는 진입점입니다. 프로젝트의 등록 코드는 같은 컨텍스트에 중복 실행하지 않도록 관리합니다. 문서 트리가 교체되면 이전 `VisualElement` 참조에 계속 연결하지 말고 새 트리를 대상으로 다시 바인딩합니다.

화면 등록과 활성화는 다릅니다. 화면은 트리에 연결된 채 숨겨질 수 있고, 모달 아래에서 표시 데이터를 유지할 수 있습니다. 비활성화를 영구 Dispose로 연결하지 않습니다. 영구 해제 시에는 버튼 콜백뿐 아니라 프로젝트 이벤트, 예약 작업, 코루틴 등 프로젝트가 만든 자원도 정리해야 합니다.

첫 적용에서 기존 화면이 먼저 생성되고 게임 이벤트를 받던 방식은 그대로 유지하는 편이 안전합니다. 표시 요청을 받은 뒤 화면을 처음 만들도록 바꾸려면, 그 전에 발생한 게임 이벤트를 대신할 현재 상태 전달 경로도 필요합니다.

## 화면 우선순위와 모달

프로젝트가 스택의 우선순위와 Back이 마지막 화면을 닫을 수 있는지(`allowEmpty`)를 정합니다. 명시적인 Pop/Clear는 이 Back 정책과 별도로 마지막 화면도 제거합니다. 예제는 메뉴 스택 0, 모달 스택 100을 사용하지만 이 이름과 숫자는 패키지의 고정 규칙이 아닙니다.

Core는 등록된 스택을 기준으로 활성 화면을 결정합니다. UIToolkit 연결은 비활성 화면에서 사용자 입력이 실행되지 않게 하고, 활성 화면의 포커스 진입점과 이전 포커스 복구를 관리합니다. 모달 아래 화면의 데이터 모델을 삭제하거나 게임 시간을 자동 변경하지 않습니다.

처음 선택할 요소는 UXML의 `default-focus` 속성(C#의 `ScreenView.DefaultFocus`) 또는 `RegisterScreen`의 포커스 선택 함수로 지정합니다. 포커스를 복구할 대상이 제거되었거나 더 이상 사용 가능하지 않으면 해당 화면의 유효한 진입점으로 돌아가도록 처리합니다. 프로젝트가 화면 콘텐츠를 동적으로 교체하는 경우에도 이 조건을 고려해야 합니다.

## 명령과 UI Toolkit 기본 입력

`UiCommand`는 사용자에게 표시할 이름, 실행 함수, 실행 가능 여부를 제공합니다. 프로젝트는 이를 화면에 등록하고, 현재 화면에 해당하는 명령만 실행하도록 컨텍스트를 사용합니다.

`CommonUxHost.BindButton`은 명령 등록과 `Button.clicked` 구독을 함께 생성하고 `IDisposable`을 반환합니다. 같은 명령을 다시 직접 등록하거나 같은 기능을 별도 Submit 콜백에서도 실행하지 않습니다. 반환된 바인딩을 해제하면 두 연결 모두 정리됩니다.

포인터 클릭과 게임패드 Submit은 UI Toolkit 이벤트 수준에서 서로 다릅니다. 기본 `Button.clicked`를 공통 실행 지점으로 사용하면 두 입력 모두 컨트롤의 기본 동작을 통과합니다. 커스텀 액션은 별도 명령으로 연결하되 기본 Navigate / Submit / Cancel 이벤트를 이중으로 합성하지 않습니다.

UI의 이벤트 전파 중지는 별도 gameplay `InputAction`의 실행을 취소하지 않습니다. `CommonUxHost.GameplayInputBlockedChanged`, `BlocksGameplayInput`, 또는 Core의 `Snapshot.BlocksGameplayInput`은 프로젝트에 차단 필요 여부를 전달합니다. 이 패키지에는 게임 맵을 직접 바꾸는 Gate가 없습니다. 프로젝트가 이 정책을 자체 입력 맵이나 명령 실행 조건에 반영합니다. 이미 실행된 게임 콜백을 UI가 소급 취소할 수 있다고 가정하지 않습니다.

`InputCommandBinding`은 추가 Button 액션만 명령에 연결하며 액션 수명을 소유하지 않습니다. 기본 Toolkit 액션을 다시 전달하지 않습니다. `InputDeviceTracker`는 실제 입력으로 장치 종류를 판정하고, `ActionHintResolver`는 명령과 액션의 명시적인 대응 관계로 표시 문자열을 구합니다. `GlyphSet`은 외부 그림의 매핑 자산이며 장치 이름만으로 존재하지 않는 아이콘을 만들어 내지 않습니다.

`ActionBar`는 같은 컨텍스트의 활성 명령을 표시합니다. 힌트 resolver와 액션바를 연결한 쪽에서 `Changed` 구독을 해제해야 합니다. 바인딩 변경, 장치 변경, 외부 실행 조건 변경은 각각 resolver 또는 컨텍스트 갱신 경로를 통해 UI에 반영합니다.

## 프로젝트 어댑터의 경계

패키지는 `GameStarted`, `QuizCompleted`, `SettingsChanged` 같은 프로젝트 이벤트를 알지 못합니다. 기존 UIManager는 이 이벤트를 CommonUX 스택의 전환 요청으로 바꾸는 facade로 남길 수 있습니다. 기존 관리자가 방문 이력을 소유해야 한다면 `SetPresentation(inputOwner, visibleScreens)`으로 표시·입력 상태만 연결합니다. 이때 Back의 이동 대상도 외부가 결정합니다. 두 경로를 한 Context에서 동시에 사용하지 않습니다.

기존의 `GameScreenShown` 같은 이벤트가 새 게임 초기화까지 수행한다면, 화면의 모든 재활성화에서 같은 이벤트를 발행해서는 안 됩니다. `새 게임 시작`, `모달에서 돌아옴`, `화면이 처음 생성됨`은 서로 다른 의미이며 프로젝트 어댑터가 구분합니다. 게임 완료 직후 결과 화면을 열지, 피드백을 확인한 다음 열지도 프로젝트가 결정합니다.

CommonUX는 이런 경계를 제공하는 작은 UI 기반 계층입니다. 게임 상태 머신, 고정 레이어 정책, 씬 로더, 저장 서비스나 데이터 바인딩 프레임워크까지 대신 소유하지 않습니다.
