# Typed Navigation 샘플

프로젝트 소유 enum을 UXML 속성으로 표시하고 CommonUX의 화면 스택에 연결하는 예제입니다. 샘플을 Package Manager에서 Import한 뒤, **빈 씬의 빈 GameObject에 `TypedNavigationSample` 컴포넌트**를 추가하고 Play하면 실행됩니다. Input System이 Active Input Handling에서 활성화되어 있어야 합니다.

## 구성

- `Runtime/SampleViews.cs`: `UiLayer`와 `ScreenId`, UI Builder에서 enum을 선택할 수 있는 두 커스텀 요소
- `Runtime/TypedNavigationSample.cs`: 런타임 문서 구성, enum 기반 등록, 명령 연결과 정리
- `Resources/CommonUX/TypedNavigation/TypedNavigation.uxml`: 홈 화면과 설정 모달
- `Resources/CommonUX/TypedNavigation/TypedNavigationStyles.uss`: 샘플 스타일
- `Resources/CommonUX/TypedNavigation/TypedNavigationTheme.tss`: Unity 기본 런타임 테마 참조

샘플은 런타임에 별도 자식 GameObject와 PanelSettings를 만듭니다. 기존 씬의 UI, 입력 액션 자산이나 설정 파일을 수정하지 않습니다. v0.1의 한 입력 컨텍스트 범위에 맞게 다른 CommonUxHost가 없는 빈 씬에서 실행하세요.

## 확인할 동작

1. **Play demo**로 홈 화면의 명령이 한 번씩 실행되는지 봅니다.
2. **Open settings**로 모달을 엽니다. 아래 홈 화면의 버튼은 동작하지 않아야 합니다.
3. 슬라이더를 변경하고 **Save and close** 또는 Escape / 컨트롤러 Cancel로 닫습니다.
4. 메뉴로 포커스가 돌아오는지 확인합니다.
5. 설정을 다시 열어 변경한 값이 남아 있는지 확인합니다.
6. 화면 아래 **ACTIVE SCREEN COMMANDS**가 활성 화면의 명령으로 바뀌는지 확인합니다. 이 액션바는 별도 명령을 중복 등록하지 않습니다.

볼륨은 수명 동작을 보여 주는 예제 데이터입니다. 실제 음량을 바꾸거나 실행 간 저장하지 않습니다. 게임패드 Submit은 별도 `InputAction.performed`로 버튼을 호출하지 않고 UI Toolkit의 `Button.clicked`를 사용합니다.

## 새 프로젝트에서 바꿀 부분

`UiLayer`와 `ScreenId`의 값을 프로젝트 용어로 바꾸고 UXML에서 선택합니다. 패키지 코드를 수정할 필요가 없습니다. 일반 `VisualElement`를 `RegisterStack` / `RegisterScreen`에 직접 전달하는 연결 방식도 사용할 수 있습니다.

샘플은 모든 화면을 먼저 등록합니다. 모달을 닫을 때는 화면의 표시와 입력 상태만 바뀌며, 화면과 명령 연결을 폐기하지 않습니다. 샘플 컴포넌트나 문서가 해제될 때만 `ReleaseBindings`에서 프로젝트 구독을 정리합니다.
