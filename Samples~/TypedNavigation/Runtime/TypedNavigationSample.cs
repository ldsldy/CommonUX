using System;
using System.Collections.Generic;
using CommonUX.Core;
using CommonUX.UIToolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace CommonUX.Samples.TypedNavigation
{
    /// <summary>
    /// 빈 GameObject에 추가해서 실행할 수 있는 예제 진입점입니다.
    /// 프로젝트 enum과 UXML 요소를 연결하며, 전환 상태 자체는 CommonUxHost에 맡깁니다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("CommonUX/Samples/Typed Navigation Sample")]
    public sealed class TypedNavigationSample : MonoBehaviour
    {
        private const string ResourceRoot = "CommonUX/TypedNavigation/";

        private readonly Dictionary<UiLayer, ScreenStack> m_stacks = new();
        private readonly Dictionary<ScreenId, ScreenHandle> m_screens = new();
        private readonly List<IDisposable> m_bindings = new();
        private readonly List<ActionBar> m_actionBars = new();

        private GameObject m_uiObject;
        private PanelSettings m_panelSettings;
        private StyleSheet m_styleSheet;
        private CommonUxHost m_host;
        private UiContext m_boundContext;
        private Slider m_volumeSlider;
        private Label m_volumeValue;
        private Label m_demoStatus;
        private float m_volume = 50f;
        private int m_demoCount;

        private void OnEnable()
        {
            if (!EnsureRuntimeUi())
                return;

            // 먼저 Ready를 구독합니다. UIDocument를 활성화할 때 호스트가 즉시 초기화될 수 있습니다.
            m_host.Ready += BindViews;
            m_uiObject.SetActive(true);
            m_host.Initialize();
        }

        private void OnDisable()
        {
            if (m_host != null)
                m_host.Ready -= BindViews;

            ReleaseBindings();
            if (m_uiObject != null)
                m_uiObject.SetActive(false);
        }

        private void OnDestroy()
        {
            // 샘플이 직접 만든 런타임 자산만 정리합니다. Resources에서 읽은 원본 자산은 소유하지 않습니다.
            if (m_uiObject != null)
                Destroy(m_uiObject);
            if (m_panelSettings != null)
                Destroy(m_panelSettings);
        }

        private bool EnsureRuntimeUi()
        {
            if (m_uiObject != null)
                return true;

            VisualTreeAsset tree = Resources.Load<VisualTreeAsset>(ResourceRoot + "TypedNavigation");
            m_styleSheet = Resources.Load<StyleSheet>(ResourceRoot + "TypedNavigationStyles");
            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>(ResourceRoot + "TypedNavigationTheme");
            if (tree == null || m_styleSheet == null || theme == null)
            {
                Debug.LogError("CommonUX 샘플 리소스를 찾을 수 없습니다. Package Manager에서 Typed Navigation 샘플 전체를 Import하세요.", this);
                enabled = false;
                return false;
            }

            m_panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            m_panelSettings.name = "CommonUX Sample Runtime Panel";
            m_panelSettings.themeStyleSheet = theme;
            m_panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            m_panelSettings.referenceResolution = new Vector2Int(960, 720);

            // 비활성 자식에서 문서와 호스트를 구성한 뒤 한 번에 활성화합니다.
            // 프로젝트의 기존 UIDocument나 InputActionAsset을 변경하지 않습니다.
            m_uiObject = new GameObject("CommonUX Sample UI");
            m_uiObject.SetActive(false);
            m_uiObject.transform.SetParent(transform, false);
            UIDocument document = m_uiObject.AddComponent<UIDocument>();
            document.panelSettings = m_panelSettings;
            document.visualTreeAsset = tree;
            m_host = m_uiObject.AddComponent<CommonUxHost>();
            m_host.Document = document;
            return true;
        }

        private void BindViews()
        {
            if (ReferenceEquals(m_boundContext, m_host.Context))
                return;

            ReleaseBindings();
            m_boundContext = m_host.Context;
            VisualElement root = m_host.Document.rootVisualElement;
            if (!root.styleSheets.Contains(m_styleSheet))
                root.styleSheets.Add(m_styleSheet);

            // 모든 UXML 화면을 먼저 연결합니다. 모달을 닫아도 이 요소와 표시 데이터는 유지됩니다.
            root.Query<SampleStackView>().ForEach(view =>
            {
                int priority = view.StackId == UiLayer.Modal ? 100 : 0;
                bool allowEmpty = view.StackId == UiLayer.Modal;
                m_stacks.Add(view.StackId, m_host.RegisterStack(view, priority, allowEmpty));
            });
            root.Query<SampleScreenView>().ForEach(view =>
                m_screens.Add(view.ScreenId, m_host.RegisterScreen(view)));

            m_demoStatus = Require<Label>(root, "demo-status");
            m_volumeSlider = Require<Slider>(root, "volume-slider");
            m_volumeValue = Require<Label>(root, "volume-value");
            m_volumeSlider.SetValueWithoutNotify(m_volume);
            UpdateVolumeLabel();
            m_volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);

            ScreenHandle home = m_screens[ScreenId.Home];
            ScreenHandle settings = m_screens[ScreenId.Settings];

            // BindButton이 명령 등록과 Button.clicked 연결을 함께 소유합니다.
            // 같은 버튼에 ClickEvent나 InputAction Submit 콜백을 추가하지 않습니다.
            m_bindings.Add(m_host.BindButton(Require<Button>(root, "play-demo"), home,
                new UiCommand("Play demo", PlayDemo)));
            m_bindings.Add(m_host.BindButton(Require<Button>(root, "open-settings"), home,
                new UiCommand("Open settings", () => m_stacks[UiLayer.Modal].Push(settings))));
            m_bindings.Add(m_host.BindButton(Require<Button>(root, "close-settings"), settings,
                new UiCommand("Save and close", () => m_boundContext.TryGoBack())));

            // 액션바도 같은 명령 객체를 표시합니다. 임의의 단축키나 입력 글리프를 추측해서 넣지 않습니다.
            root.Query<ActionBar>().ForEach(bar =>
            {
                bar.Bind(m_boundContext);
                m_actionBars.Add(bar);
            });

            m_stacks[UiLayer.Menu].Push(home);
        }

        private void PlayDemo()
        {
            m_demoCount++;
            m_demoStatus.text = $"Demo action executed {m_demoCount} time(s). Volume: {Mathf.RoundToInt(m_volume)}%.";
        }

        private void OnVolumeChanged(ChangeEvent<float> evt)
        {
            // 실제 음량에 반영하려면 이 부분을 게임의 설정 서비스에 연결합니다.
            m_volume = evt.newValue;
            UpdateVolumeLabel();
        }

        private void UpdateVolumeLabel()
        {
            m_volumeValue.text = $"{Mathf.RoundToInt(m_volume)}%";
        }

        private void ReleaseBindings()
        {
            foreach (ActionBar bar in m_actionBars)
                bar.Unbind();
            m_actionBars.Clear();

            if (m_volumeSlider != null)
                m_volumeSlider.UnregisterValueChangedCallback(OnVolumeChanged);

            for (int i = m_bindings.Count - 1; i >= 0; i--)
                m_bindings[i].Dispose();

            m_bindings.Clear();
            m_screens.Clear();
            m_stacks.Clear();
            m_boundContext = null;
            m_volumeSlider = null;
            m_volumeValue = null;
            m_demoStatus = null;
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            return root.Q<T>(name) ?? throw new InvalidOperationException($"CommonUX 샘플의 필수 요소가 없습니다: {name}");
        }
    }
}
