# CommonUX

CommonUX는 Unity UI Toolkit 화면의 **스택 순서와 포커스 수명**만 관리하는 작은 UPM 패키지입니다.

패키지 이름은 `com.commonux.ui`, 버전은 `0.2.0`이며 Unity 6000.4 이상을 대상으로 합니다.

## 책임 범위

```text
CommonUX
├── ScreenStackElement
│   ├── Push / Pop
│   ├── Active / Covered 전환
│   └── 포커스 저장 / 복구
└── StackableScreenElement
    ├── DefaultFocus
    ├── OnActivated
    ├── OnCovered
    └── OnPopped
```

CommonUX가 소유하지 않는 기능은 프로젝트에서 구성합니다.

- 화면 ID와 ID-to-`VisualTreeAsset` 카탈로그
- Addressables 로드, 캐시, 해제
- Back 입력과 어떤 스택을 Pop할지 정하는 정책
- 모달, 게임 입력 차단, Input System 액션 맵
- MVP Presenter, 명령, 액션바, 입력 힌트
- 애니메이션, 화면 전환 연출, 씬 수명

전역 컨텍스트나 스택 레지스트리는 없습니다. 여러 UI 레이어가 필요하면 각 레이어에 `ScreenStackElement`를 하나씩 두며, 각 요소는 서로 독립적인 스택을 소유합니다.

## 설치

Unity Package Manager의 **Install package from git URL**에서 다음 주소를 사용합니다.

```text
https://github.com/ldsldy/CommonUX.git#v0.2.0
```

또는 `Packages/manifest.json`의 `dependencies`에 추가합니다.

```json
"com.commonux.ui": "https://github.com/ldsldy/CommonUX.git#v0.2.0"
```

로컬 개발 중에는 이 저장소의 `package.json`을 **Install package from disk**로 선택할 수 있습니다.

## 기본 사용법

레이어 UXML에는 스택 요소를 배치합니다.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:commonux="CommonUX">
    <commonux:ScreenStackElement name="main-stack" />
</ui:UXML>
```

프로젝트 화면은 `StackableScreenElement`를 상속합니다. UXML 콘텐츠는 프로젝트 카탈로그나 팩토리가 화면 인스턴스에 복제합니다.

```csharp
using CommonUX;
using UnityEngine.UIElements;

[UxmlElement]
public partial class SettingsScreen : StackableScreenElement
{
    public override VisualElement DefaultFocus => this.Q<Button>("save");

    protected override void OnActivated()
    {
        // Presenter 연결 또는 화면 갱신은 프로젝트 책임입니다.
    }

    protected override void OnCovered()
    {
    }

    protected override void OnPopped()
    {
    }
}
```

런타임에서는 프로젝트가 ID와 에셋을 해석한 다음 생성된 화면을 Push합니다.

```csharp
ScreenStackElement stack = document.rootVisualElement.Q<ScreenStackElement>("main-stack");

var settings = new SettingsScreen();
settingsVisualTreeAsset.CloneTree(settings);
stack.Push(settings);

StackableScreenElement popped = stack.Pop();
```

`Pop()`은 스택이 비어 있으면 `null`을 반환합니다. 제거된 화면을 파괴하거나 캐시에 보관하는 일은 호출자가 결정합니다.

## 동작 계약

- Push된 화면만 Active이며 스택 요소의 자식으로 렌더링됩니다.
- 이전 Top은 Covered가 되어 시각 트리에서 분리되지만 스택에는 유지됩니다.
- Push 시 이전 화면의 포커스를 저장하고 새 화면의 `DefaultFocus`에 포커스를 둡니다.
- Pop 시 제거된 Top을 반환하고 이전 화면의 마지막 유효 포커스를 복구합니다. 대상이 유효하지 않으면 `DefaultFocus`를 사용합니다.
- 같은 화면은 한 번에 한 스택만 소유할 수 있습니다. 다른 시각 트리에 붙어 있는 화면도 Push할 수 없습니다.
- 수명 콜백에서 같은 스택을 다시 Push/Pop하면 `InvalidOperationException`이 발생합니다.
- `OnPopped` 이후 화면은 분리되고 소유권이 해제되므로 다시 Push할 수 있습니다.

세부 상태 순서와 확장 경계는 [아키텍처 문서](Documentation~/architecture.md)를 참고하세요.

## 검증

패키지의 EditMode 테스트는 Push/Pop 전이, 콜백, 소유권, 재진입 차단, 실제 UI Toolkit 패널의 기본 포커스와 복구를 검증합니다.
