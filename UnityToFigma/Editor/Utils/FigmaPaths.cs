using System.IO;
using UnityToFigma.Editor.FigmaApi;

namespace UnityToFigma.Editor.Utils
{
    /// <summary>
    /// File-name helpers for Figma import. Asset paths are resolved by <see cref="UnityToFigma.Editor.Import.FigmaImportPathResolver"/>.
    /// </summary>
    public static class FigmaPaths
    {
        public static string GetFileNameForNode(Node node, int duplicateCount)
        {
            var safeNodeTitle = ReplaceUnsafeCharacters(node.name);
            if (duplicateCount > 0) safeNodeTitle += $"_{duplicateCount}";
            return safeNodeTitle;
        }

        /// <summary>
        /// File name for a component prefab (suffix and sanitization order differ from <see cref="GetFileNameForNode"/>).
        /// </summary>
        public static string GetFileNameForComponentPrefab(string nodeName, int duplicateCount)
        {
            if (duplicateCount > 0) nodeName += $"_{duplicateCount}";
            nodeName = ReplaceUnsafeCharacters(nodeName);
            return nodeName;
        }

        private static string ReplaceUnsafeCharacters(string inputFilename)
        {
            var safeFilename = inputFilename.Trim();
            return MakeValidFileName(safeFilename);
        }

        // From https://www.csharp-console-examples.com/general/c-replace-invalid-filename-characters/
        public static string MakeValidFileName(string name)
        {
            string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            invalidChars += ".";
            string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

            return System.Text.RegularExpressions.Regex.Replace(name, invalidRegStr, "_");
        }
    }
}
