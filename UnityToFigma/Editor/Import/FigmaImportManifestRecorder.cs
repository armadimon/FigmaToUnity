using UnityEngine;
using UnityToFigma.Editor;
using UnityToFigma.Editor.FigmaApi;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Records generated prefab paths into <see cref="FigmaImportManifest"/> after save (post-hoc; does not affect paths).
    /// </summary>
    public static class FigmaImportManifestRecorder
    {
        /// <summary>
        /// After <see cref="UnityEditor.PrefabUtility.SaveAsPrefabAssetAndConnect"/>: updates manifest/report only if <paramref name="savedPrefab"/> is non-null.
        /// </summary>
        public static void RecordAfterPrefabSave(FigmaImportProcessData data, GameObject savedPrefab, string assetPath,
            Node targetNode, string kind)
        {
            if (data?.ImportReport == null || targetNode == null)
                return;

            if (string.IsNullOrEmpty(assetPath) || savedPrefab == null)
            {
                RecordPrefabSaveFailed(data, assetPath ?? string.Empty, targetNode, kind);
                return;
            }

            RecordGeneratedPrefab(data, assetPath, targetNode);
        }

        public static void RecordGeneratedPrefab(FigmaImportProcessData data, string assetPath, Node targetNode)
        {
            if (data?.ImportManifest == null || data.ImportReport == null || targetNode == null)
                return;
            if (string.IsNullOrEmpty(assetPath))
                return;

            var fileId = data.Settings != null ? data.Settings.FileId : string.Empty;
            var pageId = FigmaDataUtils.GetPageCanvasIdContainingNode(data.SourceFile, targetNode);
            var hierarchy = data.SourceFile != null
                ? FigmaDataUtils.GetFullPathForNode(targetNode, data.SourceFile)
                : string.Empty;

            var entry = new FigmaImportManifestEntry
            {
                FileId = fileId,
                PageId = pageId,
                NodeId = targetNode.id,
                NodeName = targetNode.name ?? string.Empty,
                NodeType = targetNode.type.ToString(),
                AssetPath = assetPath,
                HierarchyPath = hierarchy
            };

            var created = data.ImportManifest.UpsertEntry(entry);
            if (!string.IsNullOrEmpty(targetNode.id))
                data.TouchedManifestNodeIds.Add(targetNode.id);
            if (created)
                data.ImportReport.RecordCreated();
            else
                data.ImportReport.RecordUpdated();
        }

        static void RecordPrefabSaveFailed(FigmaImportProcessData data, string assetPath, Node targetNode, string kind)
        {
            data.ImportReport.RecordFailed();
            var id = targetNode.id ?? "?";
            var name = targetNode.name ?? "";
            data.ImportReport.AddMessage(
                $"Prefab save failed ({kind}): node {id} ({name}), path '{assetPath}'.");
        }
    }
}
