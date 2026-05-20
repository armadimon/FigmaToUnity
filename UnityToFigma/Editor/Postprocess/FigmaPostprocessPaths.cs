using UnityEditor;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Postprocess
{
    // 후처리 도구들이 공통으로 사용하는 임포트 경로를 한 곳에서 풀어준다.
    // 활성 UnityToFigmaSettings.asset 이 있으면 그 ImportRoot 를 따르고, 없으면 기본값(Assets/Figma)으로 폴백.
    internal static class FigmaPostprocessPaths
    {
        private const string FallbackImportRoot = "Assets/Figma";

        public static string ImportRoot
        {
            get
            {
                var settings = FindActiveSettings();
                var root = settings != null ? settings.ImportRoot : null;
                return string.IsNullOrEmpty(root) ? FallbackImportRoot : root.TrimEnd('/');
            }
        }

        public static string Screens => ImportRoot + "/Screens";
        public static string Components => ImportRoot + "/Components";
        public static string Pages => ImportRoot + "/Pages";
        public static string Textures => ImportRoot + "/Textures";
        public static string Manifest => ImportRoot + "/Manifest";
        public static string Debug => ImportRoot + "/Debug";
        public static string FigmaOutputJson => Debug + "/FigmaOutput.json";

        public static string[] PrefabSearchRoots => new[] { Screens, Components, Pages };

        private static UnityToFigmaSettings FindActiveSettings()
        {
            var guids = AssetDatabase.FindAssets("t:UnityToFigmaSettings");
            if (guids == null || guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<UnityToFigmaSettings>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
}
