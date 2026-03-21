using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Settings;
using UnityToFigma.Editor.Utils;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Resolves Unity asset paths for Figma import output from <see cref="UnityToFigmaSettings"/>.
    /// </summary>
    public sealed class FigmaImportPathResolver
    {
        readonly UnityToFigmaSettings _settings;

        public FigmaImportPathResolver(UnityToFigmaSettings settings)
        {
            _settings = settings ?? throw new System.ArgumentNullException(nameof(settings));
        }

        public string ImportRoot => NormalizeImportRoot(
            string.IsNullOrWhiteSpace(_settings.ImportRoot)
                ? UnityToFigmaImportSettingsDefaults.ImportRoot
                : _settings.ImportRoot);

        public string PagesDirectory =>
            Combine(ImportRoot, OrDefaultFolder(_settings.PagesFolderName, UnityToFigmaImportSettingsDefaults.PagesFolderName));

        public string ScreensDirectory =>
            Combine(ImportRoot, OrDefaultFolder(_settings.ScreensFolderName, UnityToFigmaImportSettingsDefaults.ScreensFolderName));

        public string ComponentsDirectory =>
            Combine(ImportRoot,
                OrDefaultFolder(_settings.ComponentsFolderName, UnityToFigmaImportSettingsDefaults.ComponentsFolderName));

        public string TexturesDirectory =>
            Combine(ImportRoot,
                OrDefaultFolder(_settings.TexturesFolderName, UnityToFigmaImportSettingsDefaults.TexturesFolderName));

        public string FontsDirectory =>
            Combine(ImportRoot, OrDefaultFolder(_settings.FontsFolderName, UnityToFigmaImportSettingsDefaults.FontsFolderName));

        public string ServerRenderedImagesDirectory =>
            Combine(ImportRoot,
                OrDefaultFolder(_settings.ServerRenderedImagesFolderName,
                    UnityToFigmaImportSettingsDefaults.ServerRenderedImagesFolderName));

        public string FontMaterialPresetsDirectory =>
            Combine(ImportRoot, FigmaImportAssetPath.FontMaterialPresetsFolderName);

        public string DebugDirectory =>
            Combine(ImportRoot, FigmaImportAssetPath.DebugDirectoryName);

        public string ManifestDirectory =>
            Combine(ImportRoot,
                OrDefaultFolder(_settings.ManifestFolderName, UnityToFigmaImportSettingsDefaults.ManifestFolderName));

        public string GetPathForImageFill(string imageId) => $"{TexturesDirectory}/{imageId}.png";

        public string GetPathForServerRenderedImage(string nodeId, List<ServerRenderNodeData> serverRenderNodeData)
        {
            var matchingEntry = serverRenderNodeData.FirstOrDefault(node => node.SourceNode.id == nodeId);
            var safeNodeId = FigmaDataUtils.ReplaceUnsafeFileCharactersForNodeId(nodeId);
            if (matchingEntry == null)
                return $"{ServerRenderedImagesDirectory}/{safeNodeId}.png";

            switch (matchingEntry.RenderType)
            {
                case ServerRenderType.Export:
                    var safeExportName = FigmaPaths.MakeValidFileName(matchingEntry.SourceNode.name);
                    return $"{ServerRenderedImagesDirectory}/{safeExportName}_{safeNodeId}.png";
                default:
                    return $"{ServerRenderedImagesDirectory}/{safeNodeId}.png";
            }
        }

        public string GetPathForScreenPrefab(Node node, int duplicateCount) =>
            $"{ScreensDirectory}/{FigmaPaths.GetFileNameForNode(node, duplicateCount)}.prefab";

        public string GetPathForPagePrefab(Node node, int duplicateCount) =>
            $"{PagesDirectory}/{FigmaPaths.GetFileNameForNode(node, duplicateCount)}.prefab";

        public string GetPathForComponentPrefab(string nodeName, int duplicateCount) =>
            $"{ComponentsDirectory}/{FigmaPaths.GetFileNameForComponentPrefab(nodeName, duplicateCount)}.prefab";

        public string GetScreenNamesCodeFilePath() =>
            Combine(ImportRoot, FigmaImportAssetPath.ScreenNamesFileName);

        public string GetDocumentDebugJsonFilePath() =>
            Combine(DebugDirectory, FigmaImportAssetPath.FigmaDocumentDebugFileName);

        public string GetComponentNodesDebugJsonFilePath() =>
            Combine(DebugDirectory, FigmaImportAssetPath.ComponentNodesDebugFileName);

        /// <summary>
        /// Ensures import layout folders exist. Does not delete existing assets.
        /// </summary>
        public void EnsureImportDirectoriesExist()
        {
            foreach (var dir in new[]
                     {
                         PagesDirectory, ScreensDirectory, ComponentsDirectory, TexturesDirectory,
                         ServerRenderedImagesDirectory, FontsDirectory, FontMaterialPresetsDirectory,
                         DebugDirectory, ManifestDirectory
                     })
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
        }

        public static string NormalizeImportRoot(string importRoot)
        {
            if (string.IsNullOrWhiteSpace(importRoot))
                return UnityToFigmaImportSettingsDefaults.ImportRoot;

            var s = importRoot.Trim().Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(s))
                return UnityToFigmaImportSettingsDefaults.ImportRoot;

            if (!(s.Equals("Assets", System.StringComparison.Ordinal) ||
                  s.StartsWith("Assets/", System.StringComparison.Ordinal)))
                return UnityToFigmaImportSettingsDefaults.ImportRoot;

            if (s.Contains("../") || s.Contains("/..") || s == "..")
                return UnityToFigmaImportSettingsDefaults.ImportRoot;

            return s;
        }

        public static string Combine(string root, string segment)
        {
            var seg = segment.Trim().Trim('/');
            return string.IsNullOrEmpty(seg) ? root : $"{root}/{seg}";
        }

        static string OrDefaultFolder(string value, string fallback)
        {
            var v = value?.Trim();
            if (string.IsNullOrEmpty(v))
                return fallback;

            v = v.Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(v))
                return fallback;

            if (v.Contains('/') || v.Contains(".."))
                return fallback;

            return v;
        }
    }
}
