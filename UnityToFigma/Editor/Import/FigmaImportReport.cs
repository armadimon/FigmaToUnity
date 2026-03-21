using System.Collections.Generic;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// In-memory summary of one import run (Task 4 model; generators fill in later tasks).
    /// </summary>
    public sealed class FigmaImportReport
    {
        public int CreatedCount { get; private set; }
        public int UpdatedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public int FailedCount { get; private set; }
        public int OrphanedCount { get; private set; }
        /// <summary>Manifest rows dropped when MissingNodePolicy is DeleteOnImport.</summary>
        public int ManifestRemovedCount { get; private set; }

        public readonly List<string> Messages = new();

        public void Reset()
        {
            CreatedCount = 0;
            UpdatedCount = 0;
            SkippedCount = 0;
            FailedCount = 0;
            OrphanedCount = 0;
            ManifestRemovedCount = 0;
            Messages.Clear();
        }

        public void RecordCreated(int amount = 1) => CreatedCount += amount;
        public void RecordUpdated(int amount = 1) => UpdatedCount += amount;
        public void RecordSkipped(int amount = 1) => SkippedCount += amount;
        public void RecordFailed(int amount = 1) => FailedCount += amount;
        public void RecordOrphaned(int amount = 1) => OrphanedCount += amount;
        public void RecordManifestRemoved(int amount = 1) => ManifestRemovedCount += amount;

        /// <summary>One-line summary for Console / optional dialog (import run).</summary>
        public string FormatSummaryLine()
        {
            return
                $"UnityToFigma import: created={CreatedCount}, updated={UpdatedCount}, skipped={SkippedCount}, failed={FailedCount}, orphaned={OrphanedCount}, manifestRemoved={ManifestRemovedCount}";
        }

        public void AddMessage(string message)
        {
            if (!string.IsNullOrEmpty(message))
                Messages.Add(message);
        }
    }
}
