using NUnit.Framework;
using UnityEngine;
using UnityToFigma.Editor.Import;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaImportManifestModelTests
    {
        [Test]
        public void GetEntry_ReturnsNull_ForUnknownOrEmptyId()
        {
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            Assert.That(manifest.GetEntry(null), Is.Null);
            Assert.That(manifest.GetEntry(""), Is.Null);
            Assert.That(manifest.GetEntry("9:9"), Is.Null);
        }

        [Test]
        public void UpsertEntry_FirstWrite_IsCreated_SecondWrite_IsUpdated()
        {
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();

            var first = new FigmaImportManifestEntry
            {
                FileId = "f1",
                PageId = "p1",
                NodeId = "1:2",
                NodeName = "A",
                NodeType = "FRAME",
                AssetPath = "Assets/a.prefab",
                HierarchyPath = "Doc/P/A"
            };

            Assert.That(manifest.UpsertEntry(first), Is.True);
            Assert.That(first.Status, Is.EqualTo(FigmaImportManifestEntryStatus.Created));
            Assert.That(manifest.Entries.Count, Is.EqualTo(1));

            var second = new FigmaImportManifestEntry
            {
                FileId = "f1",
                PageId = "p1",
                NodeId = "1:2",
                NodeName = "A",
                NodeType = "FRAME",
                AssetPath = "Assets/b.prefab",
                HierarchyPath = "Doc/P/A"
            };

            Assert.That(manifest.UpsertEntry(second), Is.False);
            Assert.That(manifest.Entries.Count, Is.EqualTo(1));
            Assert.That(manifest.GetEntry("1:2").AssetPath, Is.EqualTo("Assets/b.prefab"));
            Assert.That(manifest.GetEntry("1:2").Status, Is.EqualTo(FigmaImportManifestEntryStatus.Updated));
        }

        [Test]
        public void Manifest_CanAddAndReplaceEntryByNodeId_InMemory()
        {
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.FileId = "abc123";
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                FileId = "abc123",
                PageId = "0:1",
                NodeId = "1:2",
                NodeName = "Screen",
                NodeType = "FRAME",
                AssetPath = "Assets/Figma/Screens/Screen.prefab",
                HierarchyPath = "Page/Screen",
                Status = FigmaImportManifestEntryStatus.Created
            });

            Assert.That(manifest.Entries.Count, Is.EqualTo(1));
            Assert.That(manifest.Entries[0].NodeId, Is.EqualTo("1:2"));

            // Simulate update: same node id, new path and status
            var idx = manifest.Entries.FindIndex(e => e.NodeId == "1:2");
            Assert.That(idx, Is.GreaterThanOrEqualTo(0));
            manifest.Entries[idx].AssetPath = "Assets/Figma/Screens/Screen_v2.prefab";
            manifest.Entries[idx].Status = FigmaImportManifestEntryStatus.Updated;

            Assert.That(manifest.Entries[0].AssetPath, Does.EndWith("Screen_v2.prefab"));
            Assert.That(manifest.Entries[0].Status, Is.EqualTo(FigmaImportManifestEntryStatus.Updated));
        }
    }
}
