using CommonUX.UIToolkit;
using UnityEditor;
using UnityEngine;

namespace CommonUX.Editor
{
    /// <summary>
    /// 실행 중 호스트의 입력 소유 화면과 등록 상태를 확인하는 읽기 전용 창입니다.
    /// 프로젝트의 ID나 화면 정책을 추론하거나 수정하지 않습니다.
    /// </summary>
    public sealed class CommonUxDebugger : EditorWindow
    {
        private CommonUxHost host;
        private Vector2 scroll;

        [MenuItem("Window/CommonUX/Runtime Debugger")]
        private static void Open() => GetWindow<CommonUxDebugger>("CommonUX");

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            host = (CommonUxHost)EditorGUILayout.ObjectField("Host", host, typeof(CommonUxHost), true);
            if (host == null || !host.IsInitialized)
            {
                EditorGUILayout.HelpBox("Play Mode에서 활성 CommonUxHost를 선택하세요.", MessageType.Info);
                return;
            }
            var context = host.Context;
            EditorGUILayout.LabelField("Active screen", context.ActiveScreen.ToString());
            EditorGUILayout.LabelField("Gameplay input blocked", host.BlocksGameplayInput.ToString());
            EditorGUILayout.LabelField("Snapshot version", context.Snapshot.Version.ToString());
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var view in host.ScreenViews)
                EditorGUILayout.LabelField(view.Root.name, context.GetScreenState(view.Handle).ToString());
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active commands", EditorStyles.boldLabel);
            foreach (var command in context.GetActiveCommands())
                EditorGUILayout.LabelField(command.Label, command.CanExecute ? "Enabled" : "Disabled");
            EditorGUILayout.EndScrollView();
        }
    }
}
