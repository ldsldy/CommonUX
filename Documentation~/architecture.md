# CommonUX 아키텍처

## 설계 원칙

CommonUX의 공용 추상화는 `ScreenStackElement`와 `StackableScreenElement` 두 개뿐입니다. 스택 상태와 실제 UI Toolkit 트리를 별도의 Core/View/Host 계층으로 복제하지 않습니다.

```text
프로젝트
  ID · Catalog · Addressables · Presenter · Back/Input 정책
                         │
                         ▼
ScreenStackElement ── owns ──> StackableScreenElement[]
        │                          │
        └── active child 1개 ──────┘
                         │
                         ▼
               Unity UI Toolkit panel
```

한 `ScreenStackElement`는 한 스택만 소유합니다. 여러 레이어의 순서와 우선순위는 여러 스택 요소를 배치한 프로젝트 UXML과 프로젝트 코드가 결정합니다.

## 상태 모델

별도 상태 enum은 노출하지 않습니다. 화면의 상태는 스택 소유권과 현재 Top 여부로 결정됩니다.

| 상태 | 스택이 소유 | 시각 트리에 연결 | 포커스 가능 |
| --- | --- | --- | --- |
| Active | 예 | 예, 스택의 유일한 화면 자식 | 예 |
| Covered | 예 | 아니요 | 아니요 |
| Popped | 아니요 | 아니요 | 아니요 |

`Hidden`은 없습니다. 스택 안에서 Top이 아닌 화면은 모두 Covered이며, Pop된 화면의 수명은 프로젝트가 소유합니다.

## 전이 순서

### Push

1. 입력 화면이 `null`, 이미 소유됨, 다른 시각 트리에 연결됨인지 검사합니다.
2. 기존 Top 내부의 현재 포커스를 저장합니다.
3. 기존 Top을 시각 트리에서 분리하고 새 화면을 Top으로 연결합니다.
4. 기존 Top의 `OnCovered`, 새 Top의 `OnActivated`를 호출합니다.
5. 새 Top의 `DefaultFocus`가 유효하면 포커스를 이동합니다.

### Pop

1. 빈 스택이면 `null`을 반환합니다.
2. Top을 시각 트리와 스택에서 제거하고 소유권을 해제합니다.
3. 이전 화면이 있으면 다시 스택의 자식으로 연결합니다.
4. 제거된 화면의 `OnPopped`, 이전 화면의 `OnActivated`를 호출합니다.
5. 이전 화면의 마지막 포커스가 유효하면 복구하고, 아니면 `DefaultFocus`를 사용합니다.
6. 제거된 화면을 호출자에게 반환합니다.

콜백이 실행될 때 시각 트리와 스택 상태는 이미 해당 전이의 최종 상태입니다. 같은 스택에 대한 콜백 중첩 전이는 거부합니다.

## 포커스 유효성

복구 대상은 다음 조건을 모두 만족해야 합니다.

- 현재 패널에 연결되어 있음
- 해당 화면 자신이거나 그 자손임
- `focusable`이고 `enabledInHierarchy`임
- 자신부터 화면 루트까지 어떤 요소도 `display: none` 또는 `visibility: hidden`이 아님

`StackableScreenElement` 자신은 기본적으로 포커스 가능한 fallback입니다. 프로젝트가 `DefaultFocus`를 재정의할 때는 화면 내부의 포커스 가능한 요소를 반환해야 합니다.

## 소유권 경계

CommonUX는 화면 인스턴스를 생성, 로드, 캐시, 파괴하지 않습니다. `Pop()`이 반환한 화면의 이후 수명은 프로젝트가 결정합니다.

ID와 `VisualTreeAsset`의 대응도 프로젝트 책임입니다. 이 경계 덕분에 패키지는 Addressables, enum, 문자열 ID, DI 컨테이너 중 어느 것도 강제하지 않습니다. 프로젝트 카탈로그는 다음 일만 조합하면 됩니다.

1. ID로 화면 타입과 `VisualTreeAsset`을 선택합니다.
2. `StackableScreenElement` 파생 인스턴스를 만듭니다.
3. UXML을 인스턴스에 복제합니다.
4. 대상 `ScreenStackElement`에 Push합니다.

Back, 모달, gameplay input 차단은 둘 이상의 스택과 게임 상태를 함께 알아야 하는 정책이므로 CommonUX가 이벤트나 플래그로 다시 추상화하지 않습니다. 프로젝트가 입력을 받아 적절한 스택의 `Pop()`을 직접 호출합니다.
