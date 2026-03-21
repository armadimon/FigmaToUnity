namespace UnityToFigma.Editor.Settings
{
    public enum PathUpdatePolicy
    {
        /// <summary>Keep .meta GUID paths; do not move assets when the resolved folder layout changes.</summary>
        KeepExistingAssetPath,

        /// <summary>Move assets to match the latest resolved import paths (when wired by import logic).</summary>
        MoveToLatestResolvedPath
    }

    public enum MissingNodePolicy
    {
        /// <summary>Preserve existing generated objects and mark them when the source node is gone.</summary>
        MarkAsOrphaned,

        /// <summary>Remove manifest rows for nodes that no longer exist in the document (does not delete Unity assets).</summary>
        DeleteOnImport
    }

    /// <summary>
    /// Central defaults for import path and policy fields on <see cref="UnityToFigmaSettings"/>.
    /// Later import tasks should read resolved paths from settings while keeping these as the single source of default strings.
    /// </summary>
    public static class UnityToFigmaImportSettingsDefaults
    {
        public const string ImportRoot = "Assets/Figma";

        public const string ScreensFolderName = "Screens";
        public const string ComponentsFolderName = "Components";
        public const string TexturesFolderName = "Textures";
        public const string FontsFolderName = "Fonts";
        public const string PagesFolderName = "Pages";
        public const string ServerRenderedImagesFolderName = "ServerRenderedImages";
        public const string ManifestFolderName = "Manifest";

        public const string ScreenParentTransformName = "ScreenParentTransform";

        public const bool CreateMissingCanvas = true;

        public const PathUpdatePolicy DefaultPathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;
        public const MissingNodePolicy DefaultMissingNodePolicy = MissingNodePolicy.MarkAsOrphaned;
    }
}
