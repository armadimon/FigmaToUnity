using System.Collections.Generic;
using System.Linq;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// After an import run, marks manifest rows whose Figma nodes are gone, or drops rows per <see cref="MissingNodePolicy"/>.
    /// Does not delete Unity assets on disk.
    /// </summary>
    public static class FigmaImportManifestReconciler
    {
        /// <summary>
        /// Entries scoped to the current file and selected pages: if not touched this run and absent from the
        /// document node lookup, they are orphaned or removed from the manifest per policy.
        /// </summary>
        public static void ReconcileAfterImport(FigmaImportProcessData data, string fileId)
        {
            if (data?.ImportManifest?.Entries == null || data.ImportReport == null)
                return;

            var touched = data.TouchedManifestNodeIds;
            var selectedPageIds = new HashSet<string>(
                data.SelectedPagesForImport != null
                    ? data.SelectedPagesForImport.Where(p => p != null).Select(p => p.id)
                    : Enumerable.Empty<string>());
            if (selectedPageIds.Count == 0)
                return;

            var nodeLookup = data.NodeLookupDictionary ?? new Dictionary<string, Node>();
            var policy = data.Settings != null
                ? data.Settings.MissingNodePolicy
                : UnityToFigmaImportSettingsDefaults.DefaultMissingNodePolicy;

            var toRemove = new List<FigmaImportManifestEntry>();

            foreach (var entry in data.ImportManifest.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.NodeId))
                    continue;

                if (!string.IsNullOrEmpty(entry.FileId) && entry.FileId != fileId)
                    continue;

                if (string.IsNullOrEmpty(entry.PageId) || !selectedPageIds.Contains(entry.PageId))
                    continue;

                if (touched != null && touched.Contains(entry.NodeId))
                    continue;

                if (nodeLookup.ContainsKey(entry.NodeId))
                    continue;

                if (policy == MissingNodePolicy.DeleteOnImport)
                {
                    toRemove.Add(entry);
                    data.ImportReport.RecordManifestRemoved();
                    data.ImportReport.AddMessage(
                        $"Manifest row removed (source node missing, DeleteOnImport): node {entry.NodeId} — was '{entry.AssetPath}'.");
                }
                else
                {
                    entry.Status = FigmaImportManifestEntryStatus.Orphaned;
                    data.ImportReport.RecordOrphaned();
                    data.ImportReport.AddMessage(
                        $"Manifest entry orphaned (source node missing): {entry.NodeId} — '{entry.NodeName}' → '{entry.AssetPath}'.");
                }
            }

            if (toRemove.Count <= 0)
                return;

            foreach (var e in toRemove)
                data.ImportManifest.Entries.Remove(e);
        }
    }
}
