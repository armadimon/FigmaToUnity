using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Chooses the Unity asset path when saving a screen prefab: manifest + <see cref="UnityToFigmaSettings.PathUpdatePolicy"/>,
    /// with resolver + name duplicate index as fallback.
    /// </summary>
    public static class FigmaScreenPrefabPathSelector
    {
        public static void ReserveManifestPathsForCurrentImport(FigmaImportProcessData data)
        {
            if (data?.ImportManifest?.Entries == null || data.SourceFile == null || data.NodeLookupDictionary == null)
                return;

            data.ReservedScreenManifestPaths.Clear();
            if (data.Settings != null && data.Settings.PathUpdatePolicy == PathUpdatePolicy.MoveToLatestResolvedPath)
                return;

            var selectedPages = data.SelectedPagesForImport;
            var currentScreenNodeIds = FigmaDataUtils.GetScreenNodeIds(data.SourceFile, selectedPages);

            foreach (var entry in data.ImportManifest.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.AssetPath) || string.IsNullOrWhiteSpace(entry.NodeId))
                    continue;
                if (entry.NodeType != NodeType.FRAME.ToString())
                    continue;
                if (!LooksLikePrefabPath(entry.AssetPath))
                    continue;
                if (!currentScreenNodeIds.Contains(entry.NodeId))
                    continue;

                data.ReservedScreenManifestPaths[entry.AssetPath.Trim()] = entry.NodeId;
            }
        }

        public static string ResolveSavePath(
            FigmaImportProcessData data,
            FigmaImportPathResolver resolver,
            Node screenNode,
            int nameDuplicateIndex,
            out bool usedManifestAssetPath)
        {
            usedManifestAssetPath = false;

            if (resolver == null || screenNode == null)
                return string.Empty;

            var resolverPath = resolver.GetPathForScreenPrefab(screenNode, nameDuplicateIndex);

            if (data?.Settings == null)
                return resolverPath;

            if (data.Settings.PathUpdatePolicy == PathUpdatePolicy.MoveToLatestResolvedPath)
                return ClaimResolverPath(data, resolver, screenNode, nameDuplicateIndex);

            var entry = data.ImportManifest?.GetEntry(screenNode.id);
            if (entry == null || string.IsNullOrWhiteSpace(entry.AssetPath))
                return ClaimResolverPath(data, resolver, screenNode, nameDuplicateIndex);

            var p = entry.AssetPath.Trim();
            if (!LooksLikePrefabPath(p))
                return ClaimResolverPath(data, resolver, screenNode, nameDuplicateIndex);

            if (!TryClaimPath(data, screenNode.id, p))
                return ClaimResolverPath(data, resolver, screenNode, nameDuplicateIndex);

            usedManifestAssetPath = true;
            return p;
        }

        static string ClaimResolverPath(
            FigmaImportProcessData data,
            FigmaImportPathResolver resolver,
            Node screenNode,
            int startDuplicateIndex)
        {
            var duplicateIndex = startDuplicateIndex;
            while (true)
            {
                var candidate = resolver.GetPathForScreenPrefab(screenNode, duplicateIndex);
                if (TryClaimPath(data, screenNode.id, candidate))
                    return candidate;

                duplicateIndex++;
            }
        }

        static bool LooksLikePrefabPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;
            var lower = assetPath.Trim().ToLowerInvariant();
            return lower.EndsWith(".prefab");
        }

        static bool TryClaimPath(FigmaImportProcessData data, string nodeId, string assetPath)
        {
            if (data?.ClaimedScreenPrefabPaths == null || string.IsNullOrEmpty(assetPath))
                return true;

            if (data.ReservedScreenManifestPaths != null &&
                data.ReservedScreenManifestPaths.TryGetValue(assetPath, out var reservedNodeId) &&
                reservedNodeId != nodeId)
            {
                return false;
            }

            if (data.ClaimedScreenPrefabPaths.TryGetValue(assetPath, out var existingNodeId) && existingNodeId != nodeId)
                return false;

            data.ClaimedScreenPrefabPaths[assetPath] = nodeId;
            return true;
        }
    }
}
