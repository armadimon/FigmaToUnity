using UnityEditor;
using UnityEngine;

namespace UnityToFigma.Editor.Postprocess
{
    // Sync 직후 자동으로 뜨는 옵션 창.
    // RT anchor 보정 / 한글 fallback / 다해상도 검증은 ugui-from-screenshot 스킬(unity-mcp 채널)이 담당하므로
    // 이 창은 AI 컨벤션 후처리 컨텍스트 준비와 자동 띄움 옵션만 관리한다.
    internal sealed class FigmaPostSyncWindow : EditorWindow
    {
        private string _lastReport;

        [MenuItem("UnityToFigma/Postprocess/Open Sync Options Window")]
        public static void Open()
        {
            var window = GetWindow<FigmaPostSyncWindow>("Figma Sync Options");
            window.minSize = new Vector2(520, 320);
            window.Show();
            window.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Figma Sync 후 후처리 옵션", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "이 창은 UnityToFigma > Sync Document 완료 후 자동으로 뜹니다.\n" +
                "AI 컨벤션 후처리(Btn_/Txt_/Img_ 명명, MonoBehaviour 매핑, 9-slice border 등)는 여기서 컨텍스트만 준비합니다.\n" +
                "RectTransform anchor 보정, 한글 TMP fallback, 다해상도 검증은 ugui-from-screenshot 스킬로 위임됩니다.",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawTaskToggles();

            EditorGUILayout.Space();
            DrawDelegatedSection();

            EditorGUILayout.Space();
            DrawApplyBar();

            if (!string.IsNullOrEmpty(_lastReport))
                EditorGUILayout.HelpBox(_lastReport, MessageType.None);
        }

        private void DrawTaskToggles()
        {
            EditorGUILayout.LabelField("실행할 후처리", EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                FigmaSyncOptions.RunAiContextGen = EditorGUILayout.ToggleLeft(
                    "AI 컨벤션 후처리 컨텍스트 생성 + 클립보드 복사", FigmaSyncOptions.RunAiContextGen);
            }

            EditorGUILayout.Space();
            FigmaSyncOptions.AutoOpenAfterSync = EditorGUILayout.ToggleLeft(
                "다음부터 Sync 완료 시 이 창을 자동으로 띄우기",
                FigmaSyncOptions.AutoOpenAfterSync);
        }

        private void DrawDelegatedSection()
        {
            EditorGUILayout.LabelField("누락 폰트 / 추가 보정", EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                var missing = FigmaMissingFontTracker.Count;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        missing > 0
                            ? $"다운로드 실패 / 미보유 폰트 {missing}개 발견"
                            : "다운로드 실패 폰트 없음",
                        GUILayout.Width(320));
                    using (new EditorGUI.DisabledScope(missing == 0))
                    {
                        if (GUILayout.Button("Open Missing Font Window", GUILayout.Width(220)))
                            FigmaMissingFontWindow.Open();
                    }
                }

                EditorGUILayout.HelpBox(
                    "다음 보정은 Claude 에서 ugui-from-screenshot 스킬을 invoke 한 뒤 unity-mcp Unity_RunCommand 로 처리합니다:\n" +
                    "  - RectTransform anchor 디자인 의도 추론 (Tools/UnityToFigma Bootstrap/Auto Anchor)\n" +
                    "  - 좌측 쏠림 진단 (Tools/UnityToFigma Bootstrap/Diagnose Screen Layout)\n" +
                    "  - 다해상도 반응형 보정 (Tools/UnityToFigma Bootstrap/Apply Responsive Layout)\n" +
                    "  - 한글 TMP fallback Dynamic SDF (Tools/UnityToFigma Bootstrap/Setup TMP Korean Fallback)\n" +
                    "  - 디자인 사이즈 정확 캡처 (Tools/UnityToFigma Bootstrap/Capture Default Screen)\n" +
                    "  - TMP overflow/wrap 일괄 보정 (Tools/Fix TMP Overflow Settings)\n\n" +
                    "Claude 호출 프롬프트는 'AI 컨벤션 후처리 컨텍스트 생성' 토글을 켜고 Apply 누르면 클립보드에 복사됩니다.",
                    MessageType.None);
            }
        }

        private void DrawApplyBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(28))) Close();
                if (GUILayout.Button("Apply", GUILayout.Height(28))) RunSelectedPostprocess();
            }
        }

        private void RunSelectedPostprocess()
        {
            SessionState.SetBool(FigmaSyncWatcher.PostprocessRunningKey, true);
            try
            {
                var report = new System.Text.StringBuilder();

                if (FigmaSyncOptions.RunAiContextGen)
                {
                    FigmaPostprocessMenu.PrepareAiContext();
                    report.AppendLine("✔ AI 컨텍스트 생성 + 클립보드 복사");
                }

                _lastReport = report.Length == 0 ? "(아무 옵션도 선택되지 않았습니다)" : report.ToString();
                Debug.Log("[FigmaPostSync] " + _lastReport);
            }
            finally
            {
                SessionState.SetBool(FigmaSyncWatcher.PostprocessRunningKey, false);
            }
        }
    }
}
