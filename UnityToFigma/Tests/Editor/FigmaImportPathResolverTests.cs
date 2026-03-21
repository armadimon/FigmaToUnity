using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaImportPathResolverTests
    {
        [TestCase("  Assets/Figma/  ", ExpectedResult = "Assets/Figma")]
        [TestCase("Assets\\Mixed\\Slashes", ExpectedResult = "Assets/Mixed/Slashes")]
        [TestCase("", ExpectedResult = "Assets/Figma")]
        [TestCase("Packages/com.example", ExpectedResult = "Assets/Figma")]
        [TestCase("../OutsideProject", ExpectedResult = "Assets/Figma")]
        [TestCase("Generated/Figma", ExpectedResult = "Assets/Figma")]
        [TestCase("AssetsGenerated/Figma", ExpectedResult = "Assets/Figma")]
        public string NormalizeImportRoot_NormalizesSlashesAndFallsBackToDefault(string input)
        {
            return FigmaImportPathResolver.NormalizeImportRoot(input);
        }

        [Test]
        public void Combine_AppendsSingleSegment()
        {
            Assert.That(FigmaImportPathResolver.Combine("Assets/Root", "Screens"),
                Is.EqualTo("Assets/Root/Screens"));
        }

        [Test]
        public void Combine_EmptySegment_ReturnsRoot()
        {
            Assert.That(FigmaImportPathResolver.Combine("Assets/Root", ""), Is.EqualTo("Assets/Root"));
            Assert.That(FigmaImportPathResolver.Combine("Assets/Root", "   "), Is.EqualTo("Assets/Root"));
        }

        [Test]
        public void DefaultSettings_MatchCentralLayoutUnderImportRoot()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);

            Assert.That(r.ImportRoot, Is.EqualTo("Assets/Figma"));
            Assert.That(r.PagesDirectory, Is.EqualTo("Assets/Figma/Pages"));
            Assert.That(r.ScreensDirectory, Is.EqualTo("Assets/Figma/Screens"));
            Assert.That(r.ComponentsDirectory, Is.EqualTo("Assets/Figma/Components"));
            Assert.That(r.TexturesDirectory, Is.EqualTo("Assets/Figma/Textures"));
            Assert.That(r.FontsDirectory, Is.EqualTo("Assets/Figma/Fonts"));
            Assert.That(r.ServerRenderedImagesDirectory, Is.EqualTo("Assets/Figma/ServerRenderedImages"));
            Assert.That(r.FontMaterialPresetsDirectory, Is.EqualTo("Assets/Figma/FontMaterialPresets"));
            Assert.That(r.DebugDirectory, Is.EqualTo("Assets/Figma/Debug"));
            Assert.That(r.ManifestDirectory, Is.EqualTo("Assets/Figma/Manifest"));
        }

        [Test]
        public void CustomImportRootAndFolderNames_AreReflectedInDerivedPaths()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ImportRoot = "Assets/MyImport";
            s.ScreensFolderName = "Scr";
            s.PagesFolderName = "Pg";
            s.ComponentsFolderName = "Comp";
            s.TexturesFolderName = "Tex";
            s.FontsFolderName = "Fnt";
            s.ServerRenderedImagesFolderName = "SrvImg";

            var r = new FigmaImportPathResolver(s);

            Assert.That(r.ScreensDirectory, Is.EqualTo("Assets/MyImport/Scr"));
            Assert.That(r.GetPathForImageFill("img1"), Is.EqualTo("Assets/MyImport/Tex/img1.png"));
            Assert.That(r.GetScreenNamesCodeFilePath(), Is.EqualTo("Assets/MyImport/ScreenNames.cs"));
            Assert.That(r.GetDocumentDebugJsonFilePath(), Is.EqualTo("Assets/MyImport/Debug/FigmaOutput.json"));
            Assert.That(r.GetComponentNodesDebugJsonFilePath(), Is.EqualTo("Assets/MyImport/Debug/ComponentNodes.json"));
            Assert.That(r.ManifestDirectory, Is.EqualTo("Assets/MyImport/Manifest"));
        }

        [Test]
        public void InvalidFolderSegments_FallBackToDefaults()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ImportRoot = "Assets/Custom";
            s.ScreensFolderName = "Nested/Screens";
            s.ComponentsFolderName = "../Components";

            var r = new FigmaImportPathResolver(s);

            Assert.That(r.ScreensDirectory, Is.EqualTo("Assets/Custom/Screens"));
            Assert.That(r.ComponentsDirectory, Is.EqualTo("Assets/Custom/Components"));
        }

        [Test]
        public void GetPathForScreenPrefab_UsesScreensDirectoryAndFileNameHelper()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);
            var node = new Node { id = "1:2", name = "Home" };

            Assert.That(r.GetPathForScreenPrefab(node, 0), Is.EqualTo("Assets/Figma/Screens/Home.prefab"));
        }

        [Test]
        public void GetPathForComponentPrefab_UsesComponentsDirectory()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);

            Assert.That(r.GetPathForComponentPrefab("Button", 0),
                Is.EqualTo("Assets/Figma/Components/Button.prefab"));
        }

        [Test]
        public void GetPathForServerRenderedImage_Substitution_UsesServerFolderAndSafeNodeId()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);
            var nodeId = "1:2";
            var list = new List<ServerRenderNodeData>
            {
                new ServerRenderNodeData
                {
                    RenderType = ServerRenderType.Substitution,
                    SourceNode = new Node { id = nodeId, name = "Any" }
                }
            };

            var path = r.GetPathForServerRenderedImage(nodeId, list);
            Assert.That(path, Is.EqualTo("Assets/Figma/ServerRenderedImages/1_2.png"));
        }

        [Test]
        public void GetPathForServerRenderedImage_Export_UsesConfiguredServerFolder()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);
            var nodeId = "1:2";
            var list = new List<ServerRenderNodeData>
            {
                new ServerRenderNodeData
                {
                    RenderType = ServerRenderType.Export,
                    SourceNode = new Node { id = nodeId, name = "Export Me" }
                }
            };

            var path = r.GetPathForServerRenderedImage(nodeId, list);
            Assert.That(path, Is.EqualTo("Assets/Figma/ServerRenderedImages/Export Me_1_2.png"));
        }

        [Test]
        public void GetPathForServerRenderedImage_NoMatchingEntry_FallsBackToSafeNodeId()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            var r = new FigmaImportPathResolver(s);
            Assert.That(r.GetPathForServerRenderedImage("9:9", new List<ServerRenderNodeData>()),
                Is.EqualTo("Assets/Figma/ServerRenderedImages/9_9.png"));
        }
    }
}
