using System;
using System.IO;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Fixed layout segments and file names for import output that are not exposed as
    /// <see cref="Settings.UnityToFigmaSettings"/> fields (Task 2).
    /// </summary>
    public static class FigmaImportAssetPath
    {
        /// <summary>Subfolder under <see cref="Settings.UnityToFigmaSettings.ImportRoot"/> for TMP material presets.</summary>
        public const string FontMaterialPresetsFolderName = "FontMaterialPresets";

        /// <summary>Subfolder for optional debug outputs captured during import.</summary>
        public const string DebugDirectoryName = "Debug";

        /// <summary>Generated C# constants file for screen names.</summary>
        public const string ScreenNamesFileName = "ScreenNames.cs";

        public const string FigmaDocumentDebugFileName = "FigmaOutput.json";
        public const string ComponentNodesDebugFileName = "ComponentNodes.json";

        /// <summary>
        /// Manifest asset file name for a Figma file id (one manifest per document under the import root).
        /// Empty or invalid ids use <c>Default</c> stem (legacy / misconfigured settings).
        /// </summary>
        public static string GetManifestFileNameForFileId(string fileId) =>
            $"FigmaImportManifest_{SanitizeFileIdForManifestStem(fileId)}.asset";

        /// <summary>
        /// Safe segment used inside <see cref="GetManifestFileNameForFileId"/> (no path separators / invalid file name chars).
        /// </summary>
        public static string SanitizeFileIdForManifestStem(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                return "Default";

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(fileId.Length);
            foreach (var c in fileId.Trim())
            {
                if (c == '/' || c == '\\' || c == ':' || Array.IndexOf(invalid, c) >= 0)
                    sb.Append('_');
                else
                    sb.Append(c);
            }

            var s = sb.ToString();
            return string.IsNullOrEmpty(s) ? "Default" : s;
        }
    }
}
