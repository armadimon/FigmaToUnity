using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaImportManifestStoreTests
    {
        const string TestImportRoot = "Assets/__UnityToFigma_Task4_ManifestStoreTests";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestImportRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [Test]
        public void GetManifestAssetPath_UsesFileIdAndFileName()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);

            Assert.That(FigmaImportManifestStore.GetManifestAssetPath(r, "abcXYZ"),
                Is.EqualTo("Assets/Figma/Manifest/FigmaImportManifest_abcXYZ.asset"));
        }

        [Test]
        public void GetManifestAssetPath_EmptyFileId_UsesDefaultStem()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);

            Assert.That(FigmaImportManifestStore.GetManifestAssetPath(r, ""),
                Is.EqualTo("Assets/Figma/Manifest/FigmaImportManifest_Default.asset"));
            Assert.That(FigmaImportManifestStore.GetManifestAssetPath(r, null),
                Is.EqualTo("Assets/Figma/Manifest/FigmaImportManifest_Default.asset"));
        }

        [Test]
        public void GetManifestAssetPath_DifferentFileIds_DifferentPaths()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);

            Assert.That(FigmaImportManifestStore.GetManifestAssetPath(r, "file-a"),
                Is.Not.EqualTo(FigmaImportManifestStore.GetManifestAssetPath(r, "file-b")));
        }

        [Test]
        public void GetManifestAssetPath_RespectsCustomImportRootAndManifestFolderName()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ImportRoot = "Assets/MyImport";
            s.ManifestFolderName = "Meta";

            var r = new FigmaImportPathResolver(s);

            Assert.That(FigmaImportManifestStore.GetManifestAssetPath(r, "id1"),
                Is.EqualTo("Assets/MyImport/Meta/FigmaImportManifest_id1.asset"));
        }

        [Test]
        public void GetManifestAssetPath_InvalidManifestFolderSegment_FallsBackToDefaultFolderName()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ImportRoot = "Assets/Custom";
            s.ManifestFolderName = "bad/../name";

            var r = new FigmaImportPathResolver(s);

            Assert.That(FigmaImportManifestStore.GetManifestAssetPath(r, "x"),
                Is.EqualTo("Assets/Custom/Manifest/FigmaImportManifest_x.asset"));
        }

        [Test]
        public void SanitizeFileIdForManifestStem_ReplacesInvalidCharacters()
        {
            Assert.That(FigmaImportAssetPath.SanitizeFileIdForManifestStem("a:b/c"),
                Is.EqualTo("a_b_c"));
        }

        [Test]
        public void LoadOrCreate_CreatesManifestAssetAtResolvedPath()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ImportRoot = TestImportRoot;

            var manifest = FigmaImportManifestStore.LoadOrCreate(s, "file-123");
            var path = AssetDatabase.GetAssetPath(manifest);

            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.FileId, Is.EqualTo("file-123"));
            Assert.That(path, Is.EqualTo($"{TestImportRoot}/Manifest/FigmaImportManifest_file-123.asset"));
        }

        [Test]
        public void LoadOrCreate_LoadsExistingManifestAndPreservesEntries()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ImportRoot = TestImportRoot;

            var created = FigmaImportManifestStore.LoadOrCreate(s, "file-123");
            created.Entries.Add(new FigmaImportManifestEntry
            {
                FileId = "file-123",
                NodeId = "1:2",
                AssetPath = "Assets/Figma/Screens/Home.prefab"
            });
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssetIfDirty(created);
            AssetDatabase.Refresh();

            var loaded = FigmaImportManifestStore.LoadOrCreate(s, "file-123");

            Assert.That(AssetDatabase.GetAssetPath(loaded),
                Is.EqualTo(AssetDatabase.GetAssetPath(created)));
            Assert.That(loaded.Entries.Count, Is.EqualTo(1));
            Assert.That(loaded.Entries[0].NodeId, Is.EqualTo("1:2"));
        }

        [Test]
        public void LoadOrCreate_BackfillsEmptyFileIdOnExistingManifest()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ImportRoot = TestImportRoot;

            var created = FigmaImportManifestStore.LoadOrCreate(s, "file-456");
            created.FileId = "";
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssetIfDirty(created);
            AssetDatabase.Refresh();

            var loaded = FigmaImportManifestStore.LoadOrCreate(s, "file-456");

            Assert.That(loaded.FileId, Is.EqualTo("file-456"));
            Assert.That(AssetDatabase.GetAssetPath(loaded), Is.EqualTo(AssetDatabase.GetAssetPath(created)));
        }
    }
}
