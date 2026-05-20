using UnityEditor;
using UnityEngine;

namespace UnityToFigma.Editor.Postprocess
{
    // Sync 직후 자동 띄울 옵션 GUI의 설정값.
    // EditorPrefs에 저장 (프로젝트별로 분리되도록 Application.dataPath 해시 prefix 사용).
    //
    // RT anchor / 다해상도 보정은 ugui-from-screenshot 스킬(unity-mcp 채널)로 위임.
    // 폰트는 자동 다운로드 시도 후 실패한 family 를 FigmaMissingFontWindow 가 GUI 로 보여주고 사용자가 직접 선택한다.
    // (한글 fallback Dynamic SDF 만 별도로 ugui-from-screenshot 스킬의 Setup TMP Korean Fallback 메뉴로 등록.)
    internal static class FigmaSyncOptions
    {
        private static string Prefix => "UnityToFigma.Postprocess." + ProjectHash + ".";

        private static string s_projectHash;
        private static string ProjectHash
        {
            get
            {
                if (s_projectHash == null)
                    s_projectHash = Application.dataPath.GetHashCode().ToString("X");
                return s_projectHash;
            }
        }

        public static bool AutoOpenAfterSync
        {
            get => EditorPrefs.GetBool(Prefix + "AutoOpen", true);
            set => EditorPrefs.SetBool(Prefix + "AutoOpen", value);
        }

        public static bool RunAiContextGen
        {
            get => EditorPrefs.GetBool(Prefix + "RunAi", true);
            set => EditorPrefs.SetBool(Prefix + "RunAi", value);
        }
    }
}
