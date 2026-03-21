using NUnit.Framework;
using UnityEngine;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    public class UnityToFigmaImportSettingsDefaultsTests
    {
        [Test]
        public void Defaults_StringConstants_AreStable()
        {
            Assert.That(UnityToFigmaImportSettingsDefaults.ImportRoot, Is.EqualTo("Assets/Figma"));
            Assert.That(UnityToFigmaImportSettingsDefaults.ScreensFolderName, Is.EqualTo("Screens"));
            Assert.That(UnityToFigmaImportSettingsDefaults.ComponentsFolderName, Is.EqualTo("Components"));
            Assert.That(UnityToFigmaImportSettingsDefaults.TexturesFolderName, Is.EqualTo("Textures"));
            Assert.That(UnityToFigmaImportSettingsDefaults.FontsFolderName, Is.EqualTo("Fonts"));
            Assert.That(UnityToFigmaImportSettingsDefaults.PagesFolderName, Is.EqualTo("Pages"));
            Assert.That(UnityToFigmaImportSettingsDefaults.ServerRenderedImagesFolderName,
                Is.EqualTo("ServerRenderedImages"));
            Assert.That(UnityToFigmaImportSettingsDefaults.ManifestFolderName, Is.EqualTo("Manifest"));
            Assert.That(UnityToFigmaImportSettingsDefaults.ScreenParentTransformName, Is.EqualTo("ScreenParentTransform"));
        }

        [Test]
        public void PathUpdatePolicy_EnumOrder_IsStableForSerialization()
        {
            Assert.That((int)PathUpdatePolicy.KeepExistingAssetPath, Is.EqualTo(0));
            Assert.That((int)PathUpdatePolicy.MoveToLatestResolvedPath, Is.EqualTo(1));
        }

        [Test]
        public void MissingNodePolicy_EnumOrder_IsStableForSerialization()
        {
            Assert.That((int)MissingNodePolicy.MarkAsOrphaned, Is.EqualTo(0));
            Assert.That((int)MissingNodePolicy.DeleteOnImport, Is.EqualTo(1));
        }

        [Test]
        public void NewSettings_Instance_FieldDefaults_MatchCentralDefaults()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            Assert.That(s.ImportRoot, Is.EqualTo(UnityToFigmaImportSettingsDefaults.ImportRoot));
            Assert.That(s.ScreensFolderName, Is.EqualTo(UnityToFigmaImportSettingsDefaults.ScreensFolderName));
            Assert.That(s.ComponentsFolderName, Is.EqualTo(UnityToFigmaImportSettingsDefaults.ComponentsFolderName));
            Assert.That(s.TexturesFolderName, Is.EqualTo(UnityToFigmaImportSettingsDefaults.TexturesFolderName));
            Assert.That(s.FontsFolderName, Is.EqualTo(UnityToFigmaImportSettingsDefaults.FontsFolderName));
            Assert.That(s.PagesFolderName, Is.EqualTo(UnityToFigmaImportSettingsDefaults.PagesFolderName));
            Assert.That(s.ServerRenderedImagesFolderName,
                Is.EqualTo(UnityToFigmaImportSettingsDefaults.ServerRenderedImagesFolderName));
            Assert.That(s.ManifestFolderName, Is.EqualTo(UnityToFigmaImportSettingsDefaults.ManifestFolderName));
            Assert.That(s.ScreenParentTransformName,
                Is.EqualTo(UnityToFigmaImportSettingsDefaults.ScreenParentTransformName));
            Assert.That(s.CreateMissingCanvas, Is.EqualTo(UnityToFigmaImportSettingsDefaults.CreateMissingCanvas));
            Assert.That(s.PathUpdatePolicy,
                Is.EqualTo(UnityToFigmaImportSettingsDefaults.DefaultPathUpdatePolicy));
            Assert.That(s.MissingNodePolicy,
                Is.EqualTo(UnityToFigmaImportSettingsDefaults.DefaultMissingNodePolicy));
        }
    }
}
