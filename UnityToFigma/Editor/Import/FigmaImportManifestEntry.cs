using System;
using UnityEngine;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Lifecycle / reconciliation status for a manifest row (Task 4 model; Task 5+ populate).
    /// </summary>
    public enum FigmaImportManifestEntryStatus
    {
        Unknown = 0,
        Pending = 1,
        Created = 2,
        Updated = 3,
        Skipped = 4,
        Failed = 5,
        Orphaned = 6
    }

    /// <summary>
    /// One imported node tracked by Figma file, page, and node id (reimport preservation uses this in later tasks).
    /// </summary>
    [Serializable]
    public sealed class FigmaImportManifestEntry
    {
        [Tooltip("Figma file key from document URL.")]
        public string FileId;

        [Tooltip("Canvas/page node id containing this node.")]
        public string PageId;

        [Tooltip("Stable Figma node id.")]
        public string NodeId;

        [Tooltip("Last known node name from Figma.")]
        public string NodeName;

        [Tooltip("Figma type string (e.g. FRAME, COMPONENT).")]
        public string NodeType;

        [Tooltip("Unity asset path (.prefab, texture, etc.).")]
        public string AssetPath;

        [Tooltip("Human-readable path in the Figma tree (e.g. Page/Frame/Screen).")]
        public string HierarchyPath;

        public FigmaImportManifestEntryStatus Status = FigmaImportManifestEntryStatus.Unknown;
    }
}
