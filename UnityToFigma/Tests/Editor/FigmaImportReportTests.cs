using NUnit.Framework;
using UnityToFigma.Editor.Import;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaImportReportTests
    {
        [Test]
        public void Reset_ClearsCountsAndMessages()
        {
            var r = new FigmaImportReport();
            r.RecordCreated(2);
            r.RecordUpdated(1);
            r.RecordSkipped(3);
            r.RecordFailed(1);
            r.RecordOrphaned(4);
            r.RecordManifestRemoved(2);
            r.AddMessage("a");
            r.AddMessage("b");

            r.Reset();

            Assert.That(r.CreatedCount, Is.Zero);
            Assert.That(r.UpdatedCount, Is.Zero);
            Assert.That(r.SkippedCount, Is.Zero);
            Assert.That(r.FailedCount, Is.Zero);
            Assert.That(r.OrphanedCount, Is.Zero);
            Assert.That(r.ManifestRemovedCount, Is.Zero);
            Assert.That(r.Messages, Is.Empty);
        }

        [Test]
        public void FormatSummaryLine_IncludesAllCounters()
        {
            var r = new FigmaImportReport();
            r.RecordCreated(1);
            r.RecordUpdated(2);
            r.RecordSkipped(3);
            r.RecordFailed(4);
            r.RecordOrphaned(5);
            r.RecordManifestRemoved(6);

            Assert.That(r.FormatSummaryLine(),
                Is.EqualTo(
                    "UnityToFigma import: created=1, updated=2, skipped=3, failed=4, orphaned=5, manifestRemoved=6"));
        }

        [Test]
        public void AddMessage_IgnoresNullOrEmpty()
        {
            var r = new FigmaImportReport();
            r.AddMessage(null);
            r.AddMessage("");
            r.AddMessage("ok");

            Assert.That(r.Messages, Is.EqualTo(new[] { "ok" }));
        }
    }
}
