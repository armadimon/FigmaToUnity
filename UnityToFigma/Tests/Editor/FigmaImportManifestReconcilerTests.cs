using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaImportManifestReconcilerTests
    {
        [Test]
        public void Reconcile_MarkAsOrphaned_SetsStatus_WhenNodeMissingFromLookup()
        {
            var page = new Node { id = "0:1", name = "Page", type = NodeType.CANVAS };
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                FileId = "abc",
                PageId = "0:1",
                NodeId = "99:99",
                NodeName = "Gone",
                AssetPath = "Assets/Figma/Screens/Gone.prefab",
                Status = FigmaImportManifestEntryStatus.Updated
            });

            var data = new FigmaImportProcessData
            {
                ImportManifest = manifest,
                ImportReport = new FigmaImportReport(),
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                SelectedPagesForImport = new List<Node> { page },
                NodeLookupDictionary = new Dictionary<string, Node>()
            };
            data.Settings.MissingNodePolicy = MissingNodePolicy.MarkAsOrphaned;

            FigmaImportManifestReconciler.ReconcileAfterImport(data, "abc");

            Assert.That(data.ImportReport.OrphanedCount, Is.EqualTo(1));
            Assert.That(data.ImportReport.ManifestRemovedCount, Is.Zero);
            Assert.That(manifest.GetEntry("99:99")?.Status, Is.EqualTo(FigmaImportManifestEntryStatus.Orphaned));
        }

        [Test]
        public void Reconcile_Skips_WhenNodeStillInLookupButNotTouched()
        {
            var page = new Node { id = "0:1", name = "Page", type = NodeType.CANVAS };
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                FileId = "abc",
                PageId = "0:1",
                NodeId = "1:1",
                AssetPath = "Assets/Figma/Screens/Keep.prefab"
            });

            var data = new FigmaImportProcessData
            {
                ImportManifest = manifest,
                ImportReport = new FigmaImportReport(),
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                SelectedPagesForImport = new List<Node> { page },
                NodeLookupDictionary = new Dictionary<string, Node>
                {
                    ["1:1"] = new Node { id = "1:1", name = "StillHere", type = NodeType.FRAME }
                }
            };
            data.Settings.MissingNodePolicy = MissingNodePolicy.MarkAsOrphaned;

            FigmaImportManifestReconciler.ReconcileAfterImport(data, "abc");

            Assert.That(data.ImportReport.OrphanedCount, Is.Zero);
        }

        [Test]
        public void Reconcile_Skips_EntryForPageNotInCurrentSelection()
        {
            var pageA = new Node { id = "0:1", name = "A", type = NodeType.CANVAS };
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                FileId = "abc",
                PageId = "0:2",
                NodeId = "2:2",
                AssetPath = "Assets/Figma/Screens/Other.prefab"
            });

            var data = new FigmaImportProcessData
            {
                ImportManifest = manifest,
                ImportReport = new FigmaImportReport(),
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                SelectedPagesForImport = new List<Node> { pageA },
                NodeLookupDictionary = new Dictionary<string, Node>()
            };
            data.Settings.MissingNodePolicy = MissingNodePolicy.MarkAsOrphaned;

            FigmaImportManifestReconciler.ReconcileAfterImport(data, "abc");

            Assert.That(data.ImportReport.OrphanedCount, Is.Zero);
        }

        [Test]
        public void Reconcile_DeleteOnImport_RemovesEntry_WhenNodeMissing()
        {
            var page = new Node { id = "0:1", name = "Page", type = NodeType.CANVAS };
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                FileId = "abc",
                PageId = "0:1",
                NodeId = "3:3",
                AssetPath = "Assets/Figma/Screens/X.prefab"
            });

            var data = new FigmaImportProcessData
            {
                ImportManifest = manifest,
                ImportReport = new FigmaImportReport(),
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                SelectedPagesForImport = new List<Node> { page },
                NodeLookupDictionary = new Dictionary<string, Node>()
            };
            data.Settings.MissingNodePolicy = MissingNodePolicy.DeleteOnImport;

            FigmaImportManifestReconciler.ReconcileAfterImport(data, "abc");

            Assert.That(data.ImportReport.ManifestRemovedCount, Is.EqualTo(1));
            Assert.That(manifest.Entries, Is.Empty);
        }
    }
}
