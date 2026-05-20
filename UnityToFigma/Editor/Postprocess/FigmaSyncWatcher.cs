using UnityEditor;
using UnityEngine;

namespace UnityToFigma.Editor.Postprocess
{
    // UnityToFigma 의 Sync 종료 시점을 AssetPostprocessor 로 감지해 옵션 GUI(FigmaPostSyncWindow)를 자동으로 띄운다.
    //
    // 우리 도구가 프리팹을 저장할 때도 OnPostprocessAllAssets 가 호출되므로,
    // "후처리 실행 중" 가드(SessionState)와 "Screens/Components/Pages 변경 여부" 조건을 함께 사용한다.
    internal sealed class FigmaSyncWatcher : AssetPostprocessor
    {
        // SessionState 키 (도메인 리로드 동안만 유효 — Unity 재시작 시 리셋)
        public const string PostprocessRunningKey = "UnityToFigma.Postprocess.Running";
        private const string WindowScheduledKey = "UnityToFigma.Postprocess.WindowScheduled";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (SessionState.GetBool(PostprocessRunningKey, false))
                return;
            if (!FigmaSyncOptions.AutoOpenAfterSync)
                return;
            if (!IsFigmaScreenOrComponentImported(importedAssets) && !IsFigmaScreenOrComponentImported(movedAssets))
                return;
            if (SessionState.GetBool(WindowScheduledKey, false))
                return;

            SessionState.SetBool(WindowScheduledKey, true);
            EditorApplication.delayCall += OpenPostSyncWindowDeferred;
        }

        private static void OpenPostSyncWindowDeferred()
        {
            SessionState.SetBool(WindowScheduledKey, false);
            if (SessionState.GetBool(PostprocessRunningKey, false)) return;
            if (!FigmaSyncOptions.AutoOpenAfterSync) return;

            FigmaPostSyncWindow.Open();

            // 다운로드 실패 / 미보유 폰트가 있으면 누락 폰트 선택 창도 함께 띄움.
            if (FigmaMissingFontTracker.Count > 0)
                FigmaMissingFontWindow.Open();
        }

        private static bool IsFigmaScreenOrComponentImported(string[] paths)
        {
            if (paths == null) return false;
            var screens = FigmaPostprocessPaths.Screens + "/";
            var components = FigmaPostprocessPaths.Components + "/";
            var pages = FigmaPostprocessPaths.Pages + "/";

            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if ((p.StartsWith(screens, System.StringComparison.OrdinalIgnoreCase) ||
                     p.StartsWith(components, System.StringComparison.OrdinalIgnoreCase) ||
                     p.StartsWith(pages, System.StringComparison.OrdinalIgnoreCase))
                    && p.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
