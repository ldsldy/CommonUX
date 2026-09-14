# CommonUX

Unity UI Toolkit에 화면 스택, 모달 입력 제어, 포커스 복구, 화면별 명령을 연결하는 Unity 패키지입니다. CommonUI의 사용 목적을 참고했으며, Unreal/Epic 소스나 자산을 포함하지 않습니다.

패키지 이름은 `com.commonux.ui`, 버전은 `0.1.0`입니다. 대상 환경은 **Unity 6000.5 / Input System 1.20.0**입니다.

## 이 패키지가 맡는 것

- 화면의 표시 상태와 스택의 Push / Pop / Back 처리
- 여러 스택의 우선순위와 활성 화면 판정
- 모달에 가려진 화면의 입력 차단과 화면 간 포커스 복구
- 화면별 명령 등록과 `Button.clicked` 연결
- 현재 화면의 명령을 보여 주는 액션바와 선택적인 장치별 입력 힌트
- UI Toolkit 요소와 일반 C# 화면 상태 사이의 연결
- Input System을 사용하는 프로젝트를 위한 별도 입력 연동 계층

레이어 이름, 화면 ID, 게임 시작/종료, 저장, 씬 전환은 프로젝트가 정합니다. Lyra의 게임 흐름이나 고정 레이어 구성을 강제하지 않습니다. `ScreenStackView`와 `ScreenView`에도 프로젝트 식별자 속성을 내장하지 않습니다.

## 설치

### 로컬 폴더에서 설치

1. Unity의 Package Manager를 엽니다.
2. `+` 메뉴에서 **Install package from disk**를 선택합니다.
3. 이 폴더의 `package.json`을 선택합니다.

UPM 패키지 루트는 현재 문서와 `package.json`이 있는 `CommonUX` 폴더입니다. 기존 Quiz 프로젝트의 `Assets`를 이 폴더에 복사할 필요가 없습니다.

배포용 `.tgz` 파일을 받았다면 같은 메뉴의 **Install package from tarball**로 설치할 수도 있습니다.

### Git 저장소에서 설치

Package Manager의 **Install package from git URL**에 다음 주소를 입력합니다.

```text
https://github.com/ldsldy/CommonUX.git#v0.1.0
```

`v0.1.0` 태그에 고정해 같은 버전을 설치합니다. 저장소 루트에 `package.json`이 있으므로 별도의 `?path=`는 필요하지 않습니다.

프로젝트의 `Packages/manifest.json`을 직접 편집한다면 `dependencies` 안에 다음 항목을 추가할 수도 있습니다.

```json
"com.commonux.ui": "https://github.com/ldsldy/CommonUX.git#v0.1.0"
```

[GitHub 저장소](https://github.com/ldsldy/CommonUX)에서 소스와 버전 태그를 확인할 수 있습니다. Unity가 사용할 수 있는 Git 실행 파일이 설치되어 있어야 합니다.

## 실행 가능한 샘플

Package Manager에서 CommonUX의 **Samples > Typed Navigation > Import**를 선택합니다.

1. 빈 씬에 빈 GameObject를 하나 만듭니다.
2. **Typed Navigation Sample** 컴포넌트를 추가합니다. Add Component 메뉴에서는 `CommonUX > Samples > Typed Navigation Sample`에서도 찾을 수 있습니다.
3. Project Settings의 **Active Input Handling**에서 Input System을 활성화합니다. 설정 변경으로 Unity가 재시작을 요구하면 재시작합니다.
4. Play 모드에서 Game 뷰에 포커스를 줍니다.

샘플이 `UIDocument`, `PanelSettings`, 기본 런타임 테마를 직접 구성합니다. 씬용 UXML, PanelSettings 자산이나 EventSystem을 따로 만들 필요가 없습니다. 다른 CommonUxHost가 없는 빈 씬에서 실행하세요.

**Open settings**를 누르면 Settings 모달이 열립니다. 슬라이더를 조절하고 **Save and close** 또는 Escape / 컨트롤러 Cancel로 닫으면 메뉴에 포커스가 돌아갑니다. 다시 열면 같은 값이 남아 있습니다. **Play demo**는 실행 횟수를 표시하는 명령입니다. 샘플의 음량 값은 실행 중 메모리에만 보관하며 실제 오디오 설정이나 저장 파일을 바꾸지 않습니다.

UI Toolkit 기본 내비게이션을 사용하므로 키보드의 Tab / Enter와 게임패드 Navigate / Submit도 사용할 수 있습니다. 프로젝트에 UI 액션 맵이 있다면 `UI` 맵과 기본 액션의 이름·타입을 유지하세요. 순수 UI Toolkit 환경인 Unity 6000.5에서는 `InputSystemUIInputModule`이 필수 구성요소가 아닙니다.

## UXML과 코드 연결

연결 방법은 두 가지이며 같은 런타임을 사용합니다.

| 작성 방식 | 용도 |
| --- | --- |
| 일반 `VisualElement`를 호스트에 등록 | 기존 UXML과 컨트롤러를 유지하면서 도입 |
| `ScreenStackView`, `ScreenView`를 UXML에서 사용 | 화면 역할과 기본 옵션을 UI Builder에서 명시 |

기존 요소는 `CommonUxHost.RegisterStack(container)`와 `RegisterScreen(root)`으로 연결합니다. 등록은 기존 요소를 사용하는 방식이며, 화면 내용의 데이터 모델은 프로젝트가 계속 소유합니다. `RegisterScreen`에 `ScreenView`를 전달하고 옵션을 생략하면 요소의 `modal`, `blocks-gameplay-input`, `default-focus` 설정을 읽습니다.

```csharp
using CommonUX.Core;
using CommonUX.UIToolkit;
using UnityEngine.UIElements;

// host의 Ready 이후 실행합니다. 같은 트리에 중복 등록하지 않습니다.
VisualElement root = host.Document.rootVisualElement;
ScreenStack menu = host.RegisterStack(root.Q("menu-stack"), allowEmpty: false);
ScreenHandle home = host.RegisterScreen(root.Q("home-screen"));
menu.Push(home);

// 반환된 IDisposable은 문서 연결을 해제할 때 Dispose합니다.
// 이 메서드가 명령 등록과 Button.clicked 연결을 함께 관리합니다.
var binding = host.BindButton(root.Q<Button>("save-button"), home,
    new UiCommand("Save", SaveProjectSettings));
```

위 코드는 연결 API를 보여 주는 발췌입니다. `host`와 `SaveProjectSettings`는 프로젝트가 제공해야 합니다. 컴포넌트 생성과 수명 정리까지 포함한 전체 예제는 `Samples~/TypedNavigation/Runtime/TypedNavigationSample.cs`에 있습니다.

프로젝트에서 enum ID가 필요하면 샘플처럼 기본 요소를 상속하고 `[UxmlAttribute]`를 추가할 수 있습니다. `SampleStackView.StackId`와 `SampleScreenView.ScreenId`는 **샘플 소유 enum**이며 패키지의 필수 식별자 체계가 아닙니다. UI Builder에는 컴파일된 확장 요소의 enum 속성이 표시됩니다.

```csharp
// 프로젝트 또는 별도 확장 패키지에 작성합니다.
public enum AppLayer { Menu, Modal }

/// <summary>이 프로젝트에서 사용하는 레이어 식별자를 UI Builder에 노출합니다.</summary>
[UxmlElement]
public partial class AppStackView : ScreenStackView
{
    [UxmlAttribute("stack-id")]
    public AppLayer StackId { get; set; }
}
```

외부 바인더는 `AppLayer`를 키로 한 사전에 `RegisterStack`의 반환값을 저장합니다. 화면도 같은 방식으로 외부 ID를 `ScreenHandle`에 연결합니다. 핸들은 현재 Context 안의 등록을 가리키므로 저장 파일의 영구 식별자로 사용하지 않습니다. 사용자 정의 struct를 UXML 속성으로 직접 편집하려면 해당 타입의 Unity 직렬화·UXML 변환기를 외부 구현에서 함께 제공해야 합니다.

## 도입할 때 지킬 계약

**화면 순서의 소유자는 하나로 둡니다.** CommonUX 스택을 사용하면 기존 UIManager는 프로젝트 이벤트를 전환 요청으로 바꾸는 역할을 맡습니다. 기존 UIManager가 방문 이력을 계속 관리해야 한다면 아래의 외부 상태 연결 API를 사용합니다.

**화면을 가리는 것과 폐기는 다릅니다.** 모달을 열 때 아래 화면의 게임 데이터나 표시 모델을 초기화하지 않습니다. 화면 비활성화는 입력과 포커스 권한의 변경이고, 문서 해제·폐기는 이벤트 구독 및 바인딩 정리를 포함합니다.

**등록된 루트의 표시와 입력 허용 상태는 호스트가 관리합니다.** 활성화 시 루트에 `display: flex`를 적용하고, 가려진 루트는 `SetEnabled(false)`로 제한합니다. 자식 컨트롤의 개별 `enabledSelf`는 유지합니다. 연결 해제 시 루트의 원래 inline 표시·활성 값을 복원합니다. 레이아웃·크기·배경·전환 연출은 USS와 프로젝트 코드에서 정합니다.

**Back과 명시적 Pop은 구분합니다.** `allowEmpty: false`는 Back이 스택의 마지막 화면을 닫지 않게 합니다. 프로젝트 코드의 명시적인 `Pop()`과 `Clear()`는 마지막 화면도 제거할 수 있습니다.

**문서가 재생성되면 다시 연결합니다.** 호스트는 문서 비활성화·루트 교체를 감지해 이전 Context를 해제합니다. 문서가 다시 준비되면 새 Context를 만들고 `Ready`를 호출하므로, 외부 바인더도 이전 참조와 구독을 정리한 뒤 새 트리에 등록해야 합니다. 호스트의 `Shutdown()`을 직접 호출한 경우에는 명시적인 `Initialize()` 또는 컴포넌트 재활성화까지 다시 시작하지 않습니다.

**입력 실행 경로를 중복 연결하지 않습니다.** `BindButton`은 기본 `Button.clicked` 경로를 사용합니다. 동일 기능에 `ClickEvent`와 Input System Submit 콜백을 추가하면 중복 실행이나 장치별 동작 차이가 생길 수 있습니다.

**게임 입력은 별도 연동 대상입니다.** UI 이벤트 전파 중지나 모달 표시는 프로젝트의 모든 `InputAction` 콜백을 자동 중지하지 않습니다. `host.GameplayInputBlockedChanged`와 `host.BlocksGameplayInput`을 프로젝트가 받아 게임 액션 맵 또는 게임 명령 실행 조건에 반영합니다. CommonUX는 게임 맵을 직접 Enable / Disable하지 않습니다. 게임 시간 정지, 네트워크, 오디오 등도 프로젝트 정책으로 처리합니다.

## 기존 화면 관리자의 이력을 유지하는 경우

스택은 선택 기능입니다. 기존 UIManager나 다른 내비게이션 구현에서 순서를 관리한다면, 화면을 호스트에 등록한 뒤 **지금 보일 화면들과 입력을 받을 화면**만 전달합니다.

```csharp
// home과 settings는 host.RegisterScreen으로 얻은 핸들입니다.
host.Context.SetPresentation(home, new[] { home });

// 프로젝트의 화면 관리자가 모달을 열었을 때:
host.Context.SetPresentation(settings, new[] { home, settings });

// 모달을 닫았을 때:
host.Context.SetPresentation(home, new[] { home });
```

이 경로에서는 CommonUX가 방문 이력을 만들지 않습니다. Back의 이동 대상도 외부 관리자가 정하고, UI Toolkit의 `NavigationCancelEvent` 버블 콜백에서 처리한 뒤 `SetPresentation`을 갱신합니다. `settings`를 등록할 때 모달 옵션을 지정하면 외부 영역 포인터·포커스 제한도 적용됩니다.

한 Context에서 외부 상태 연결과 비어 있지 않은 CommonUX 스택을 동시에 사용할 수 없습니다. `ClearPresentation()`으로 외부 상태 연결을 끝낸 뒤 스택을 사용할 수 있습니다. 입력 소유자를 `default`로 전달하면 지정한 화면들은 보이지만 입력은 받지 않습니다.

## 액션바와 Input System 연동

UXML의 `ActionBar`에 `Bind(host.Context)`를 호출하면 현재 입력 소유 화면의 명령을 표시합니다. 실행 조건이 외부 게임 상태에 따라 바뀌면 `host.Refresh()`를 호출해 버튼과 액션바의 상태를 다시 계산하세요. 샘플은 홈과 모달 안에 액션바를 각각 두고 같은 명령 등록을 사용합니다.

`CommonUX.InputSystem`에는 다음 어댑터가 있습니다.

| API | 역할 |
| --- | --- |
| `InputCommandBinding` | 프로젝트의 추가 Button 액션을 이미 등록한 `UiCommand`에 연결 |
| `InputDeviceTracker` | 실제 사용자 입력을 관찰해 키보드/마우스, 게임패드, 터치 전환 감지 |
| `ActionHintResolver` | 실제 액션 바인딩에서 표시용 입력 이름 또는 글리프 정보 계산 |
| `GlyphSet` | 프로젝트가 제공한 장치 레이아웃·컨트롤 경로와 텍스처의 대응 자산 |

`InputCommandBinding`은 액션을 자동 활성화하거나 해제하지 않습니다. 액션의 Enable / Disable 및 소유권은 프로젝트에 있습니다. 기본 Submit / Cancel / Navigate 등을 이 어댑터에 다시 연결하는 것은 허용하지 않으며, 다른 액션 이름을 사용해 같은 키 입력을 중복 처리하는 구성도 피해야 합니다.

입력 힌트는 `resolver.Bind(command, action)`으로 대응 관계를 지정하고, `actionBar.SetHintProvider(resolver.GetHint)`와 `resolver.Changed += actionBar.Refresh`로 연결합니다. 글리프를 제공하지 않으면 바인딩의 표시 문자열을 사용합니다. 패키지에 콘솔 버튼 아트가 포함되지는 않습니다. 해제할 때 Changed 구독을 먼저 제거하고 입력 바인딩, resolver, tracker를 각각 Dispose합니다.

내장 ActionBar는 텍스트 힌트를 표시합니다. 이미지가 필요하면 `resolver.Resolve(command).Texture`를 프로젝트의 `Image` 요소에 연결할 수 있습니다. 같은 Context와 InputAction에 여러 명령을 연결해도 현재 화면에서 우선순위가 가장 높은 연결 하나만 선택하며, 선택된 명령이 실행 불가능하면 그 입력은 종료합니다.

런타임 상태는 **Window > CommonUX > Runtime Debugger**에서 활성 호스트를 선택해 확인할 수 있습니다.

## 0.1 범위와 제한

- 한 번에 하나의 로컬 입력 컨텍스트를 사용하는 화면 UI를 대상으로 합니다. 플레이어별 독립 입력·포커스를 제공하는 로컬 멀티플레이 UI는 지원 범위에 포함하지 않습니다.
- 모달 입력 차단은 호스트에 연결된 한 UIDocument 내부에 적용됩니다. 다른 문서, uGUI, 게임 입력과의 우선순위는 프로젝트에서 조정합니다.
- 화면 등록만으로 프로젝트 이벤트를 해석하거나 게임 상태를 복구하지 않습니다. 필요하면 화면 생성 전에 표시 모델을 연결하거나 현재 상태를 전달하세요.
- Lyra식 게임 프레임워크, XR 포인터, 아날로그 가상 커서, 화면 자동 비동기 로딩, Addressables 수명 관리를 제공하지 않습니다.
- 기본 화면 전환과 재사용 가능한 연결 지점을 제공합니다. 특정 게임의 레이아웃·테마·전환 연출·저장 방식은 프로젝트에서 확장합니다.

구조와 수명 계약은 [설계 문서](Documentation~/architecture.md), 샘플 구성은 [샘플 안내](Samples~/TypedNavigation/README.md)를 참고하세요.

## 제작 시 검증

Unity **6000.5.6f1**와 Input System **1.20.0**의 독립 프로젝트에서 컴파일하고, EditMode 자동 검증 **58개**와 실제 UIDocument를 사용하는 PlayMode 검증 **4개**를 통과했습니다. 화면 상태·외부 ID 연결·명령 수명·입력 장치 전환·모달·포커스 복귀·문서 재활성화를 확인했습니다.

배포 패키지에는 요청에 따라 검증용 Test/Tests 폴더와 테스트 C# 파일을 포함하지 않습니다. 다른 Unity 버전, 실제 콘솔 기기, XR 및 로컬 멀티플레이 환경은 검증 범위에 포함하지 않습니다.
