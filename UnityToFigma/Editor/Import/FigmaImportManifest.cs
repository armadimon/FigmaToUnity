using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Editor asset listing imported nodes by Figma id for reimport and path reconciliation (Task 4 shell).
    /// </summary>
    public sealed class FigmaImportManifest : ScriptableObject
    {
        [Tooltip("Figma file id this manifest was created for.")]
        public string FileId;

        [Tooltip("Imported node rows keyed by workflow; list form for Unity serialization.")]
        public List<FigmaImportManifestEntry> Entries = new();

        public FigmaImportManifestEntry GetEntry(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return null;

            foreach (var e in Entries)
            {
                if (e != null && e.NodeId == nodeId)
                    return e;
            }

            return null;
        }

        /// <summary>
        /// Inserts or updates a row by <see cref="FigmaImportManifestEntry.NodeId"/>.
        /// Sets <see cref="FigmaImportManifestEntryStatus.Created"/> or <see cref="FigmaImportManifestEntryStatus.Updated"/>.
        /// </summary>
        /// <returns>True if a new entry was added; false if an existing entry was updated.</returns>
        public bool UpsertEntry(FigmaImportManifestEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.NodeId))
                throw new ArgumentException("NodeId is required.", nameof(entry));

            var existing = GetEntry(entry.NodeId);
            if (existing == null)
            {
                entry.Status = FigmaImportManifestEntryStatus.Created;
                Entries.Add(entry);
                return true;
            }

            existing.FileId = entry.FileId;
            existing.PageId = entry.PageId;
            existing.NodeName = entry.NodeName;
            existing.NodeType = entry.NodeType;
            existing.AssetPath = entry.AssetPath;
            existing.HierarchyPath = entry.HierarchyPath;
            existing.Status = FigmaImportManifestEntryStatus.Updated;
            return false;
        }
    }
}
