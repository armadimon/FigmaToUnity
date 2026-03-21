using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Creates and loads <see cref="FigmaImportManifest"/> under the resolved import layout.
    /// </summary>
    public static class FigmaImportManifestStore
    {
        /// <summary>
        /// Unity asset path for the manifest under <see cref="FigmaImportPathResolver.ManifestDirectory"/>.
        /// </summary>
        /// <param name="fileId">Figma file key from the document URL; drives the manifest asset file name.</param>
        public static string GetManifestAssetPath(FigmaImportPathResolver resolver, string fileId)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            var fileName = FigmaImportAssetPath.GetManifestFileNameForFileId(fileId);
            return FigmaImportPathResolver.Combine(resolver.ManifestDirectory, fileName);
        }

        /// <summary>
        /// Loads an existing manifest asset or creates a new one at the configured path.
        /// </summary>
        public static FigmaImportManifest LoadOrCreate(FigmaImportPathResolver resolver, string fileId)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            var path = GetManifestAssetPath(resolver, fileId);
            var existing = AssetDatabase.LoadAssetAtPath<FigmaImportManifest>(path);
            if (existing != null)
            {
                if (string.IsNullOrEmpty(existing.FileId) && !string.IsNullOrEmpty(fileId))
                {
                    existing.FileId = fileId;
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }

                return existing;
            }

            if (!Directory.Exists(resolver.ManifestDirectory))
                Directory.CreateDirectory(resolver.ManifestDirectory);

            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.FileId = fileId ?? "";
            manifest.Entries ??= new System.Collections.Generic.List<FigmaImportManifestEntry>();
            AssetDatabase.CreateAsset(manifest, path);
            AssetDatabase.SaveAssets();
            return manifest;
        }

        /// <summary>
        /// Convenience: resolve paths from settings and load or create the manifest.
        /// </summary>
        public static FigmaImportManifest LoadOrCreate(UnityToFigmaSettings settings, string fileId)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            return LoadOrCreate(new FigmaImportPathResolver(settings), fileId);
        }

        /// <summary>
        /// Persists manifest changes after import generation (paths were already chosen earlier).
        /// </summary>
        public static void MarkDirtyAndSave(FigmaImportManifest manifest)
        {
            if (manifest == null)
                return;

            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);
        }
    }
}
