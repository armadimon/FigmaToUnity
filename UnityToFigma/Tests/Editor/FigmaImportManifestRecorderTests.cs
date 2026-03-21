using NUnit.Framework;
using UnityEngine;
using UnityToFigma.Editor;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaImportManifestRecorderTests
    {
        [Test]
        public void RecordGeneratedPrefab_IncrementsCreatedThenUpdated()
        {
            var frame = new Node { id = "1:10", name = "Screen", type = NodeType.FRAME };
            var page = new Node
            {
                id = "0:1",
                name = "Page A",
                type = NodeType.CANVAS,
                children = new[] { frame }
            };
            var file = new FigmaFile
            {
                document = new Node
                {
                    id = "0:0",
                    name = "Document",
                    type = NodeType.DOCUMENT,
                    children = new[] { page }
                }
            };

            var settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            var report = new FigmaImportReport();

            var data = new FigmaImportProcessData
            {
                Settings = settings,
                SourceFile = file,
                ImportManifest = manifest,
                ImportReport = report
            };

            FigmaImportManifestRecorder.RecordGeneratedPrefab(data, "Assets/Figma/Screens/Screen.prefab", frame);
            Assert.That(report.CreatedCount, Is.EqualTo(1));
            Assert.That(report.UpdatedCount, Is.Zero);
            Assert.That(data.TouchedManifestNodeIds, Does.Contain("1:10"));

            FigmaImportManifestRecorder.RecordGeneratedPrefab(data, "Assets/Figma/Screens/Screen.prefab", frame);
            Assert.That(report.CreatedCount, Is.EqualTo(1));
            Assert.That(report.UpdatedCount, Is.EqualTo(1));
        }

        [Test]
        public void RecordGeneratedPrefab_NoOp_WhenManifestOrReportMissing()
        {
            var node = new Node { id = "1:1", name = "N", type = NodeType.FRAME };
            var data = new FigmaImportProcessData { ImportManifest = null, ImportReport = new FigmaImportReport() };
            FigmaImportManifestRecorder.RecordGeneratedPrefab(data, "Assets/x.prefab", node);
            Assert.That(data.ImportReport.CreatedCount, Is.Zero);
        }

        [Test]
        public void RecordAfterPrefabSave_NullPrefab_RecordsFailed_DoesNotTouchManifest()
        {
            var frame = new Node { id = "1:10", name = "Screen", type = NodeType.FRAME };
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            var report = new FigmaImportReport();
            var data = new FigmaImportProcessData { ImportManifest = manifest, ImportReport = report };

            FigmaImportManifestRecorder.RecordAfterPrefabSave(data, null, "Assets/Figma/Screens/X.prefab", frame,
                "screen");

            Assert.That(report.FailedCount, Is.EqualTo(1));
            Assert.That(report.CreatedCount, Is.Zero);
            Assert.That(report.UpdatedCount, Is.Zero);
            Assert.That(report.Messages.Count, Is.EqualTo(1));
            Assert.That(manifest.Entries, Is.Empty);
        }

        [Test]
        public void RecordAfterPrefabSave_EmptyPath_RecordsFailed()
        {
            var frame = new Node { id = "1:10", name = "Screen", type = NodeType.FRAME };
            var go = new GameObject("tmp");
            var report = new FigmaImportReport();
            var data = new FigmaImportProcessData
            {
                ImportManifest = ScriptableObject.CreateInstance<FigmaImportManifest>(),
                ImportReport = report
            };

            try
            {
                FigmaImportManifestRecorder.RecordAfterPrefabSave(data, go, "", frame, "page");
                Assert.That(report.FailedCount, Is.EqualTo(1));
                Assert.That(report.Messages.Count, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
