using NUnit.Framework;
using UnityEngine;
using UnityToFigma.Editor;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaScreenPrefabPathSelectorTests
    {
        static FigmaImportPathResolver MakeResolver()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            return new FigmaImportPathResolver(s);
        }

        static Node MakeScreenNode(string id, string name)
        {
            return new Node
            {
                id = id,
                name = name,
                type = NodeType.FRAME
            };
        }

        [Test]
        public void ResolveSavePath_NoManifest_UsesResolverPath()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:10", "Home");
            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = ScriptableObject.CreateInstance<FigmaImportManifest>()
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;

            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 0, out var usedManifest);

            Assert.That(usedManifest, Is.False);
            Assert.That(path, Is.EqualTo(resolver.GetPathForScreenPrefab(node, 0)));
        }

        [Test]
        public void ResolveSavePath_KeepExisting_WithManifestEntry_ReusesAssetPath()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:20", "Home");
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:20",
                AssetPath = "Assets/Figma/Screens/Home.prefab"
            });

            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;

            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 0, out var usedManifest);

            Assert.That(usedManifest, Is.True);
            Assert.That(path, Is.EqualTo("Assets/Figma/Screens/Home.prefab"));
        }

        [Test]
        public void ResolveSavePath_MoveToLatest_IgnoresManifest_UsesResolverPath()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:30", "Home");
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:30",
                AssetPath = "Assets/Figma/Screens/OldHome.prefab"
            });

            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.MoveToLatestResolvedPath;

            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 0, out var usedManifest);

            Assert.That(usedManifest, Is.False);
            Assert.That(path, Is.EqualTo(resolver.GetPathForScreenPrefab(node, 0)));
        }

        [Test]
        public void ResolveSavePath_KeepExisting_EmptyManifestAssetPath_UsesResolver()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:40", "Home");
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:40",
                AssetPath = ""
            });

            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;

            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 0, out var usedManifest);

            Assert.That(usedManifest, Is.False);
            Assert.That(path, Is.EqualTo(resolver.GetPathForScreenPrefab(node, 0)));
        }

        [Test]
        public void ResolveSavePath_KeepExisting_NonPrefabManifestAssetPath_UsesResolver()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:50", "Home");
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:50",
                AssetPath = "Assets/Figma/Screens/Home.png"
            });

            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;

            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 0, out var usedManifest);

            Assert.That(usedManifest, Is.False);
            Assert.That(path, Is.EqualTo(resolver.GetPathForScreenPrefab(node, 0)));
        }

        [Test]
        public void ResolveSavePath_UsesManifestWhenDuplicateIndexWouldOtherwiseCreateNewSuffix()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:60", "Home");
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:60",
                AssetPath = "Assets/Figma/Screens/Home.prefab"
            });

            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;

            // Same manifest path even if name-based index would be 1 (e.g. another "Home" consumed 0 first)
            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 1, out var usedManifest);

            Assert.That(usedManifest, Is.True);
            Assert.That(path, Is.EqualTo("Assets/Figma/Screens/Home.prefab"));
            Assert.That(path, Is.Not.EqualTo(resolver.GetPathForScreenPrefab(node, 1)));
        }

        [Test]
        public void ResolveSavePath_ManifestPathAlreadyClaimedByOtherNode_FallsBackToUniqueResolverPath()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:70", "Home");
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:70",
                AssetPath = "Assets/Figma/Screens/Home.prefab"
            });

            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;
            data.ClaimedScreenPrefabPaths["Assets/Figma/Screens/Home.prefab"] = "other-node";

            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 0, out var usedManifest);

            Assert.That(usedManifest, Is.False);
            Assert.That(path, Is.EqualTo("Assets/Figma/Screens/Home_1.prefab"));
        }

        [Test]
        public void ResolveSavePath_ResolverFallbackSkipsAlreadyClaimedPaths()
        {
            var resolver = MakeResolver();
            var node = MakeScreenNode("1:80", "Home");
            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = ScriptableObject.CreateInstance<FigmaImportManifest>()
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.MoveToLatestResolvedPath;
            data.ClaimedScreenPrefabPaths["Assets/Figma/Screens/Home.prefab"] = "other-node";
            data.ClaimedScreenPrefabPaths["Assets/Figma/Screens/Home_1.prefab"] = "another-node";

            var path = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, node, 0, out var usedManifest);

            Assert.That(usedManifest, Is.False);
            Assert.That(path, Is.EqualTo("Assets/Figma/Screens/Home_2.prefab"));
        }

        [Test]
        public void ResolveSavePath_ReservedManifestOwnerKeepsPath_WhenOtherScreenResolvesFirst()
        {
            var resolver = MakeResolver();
            var ownerNode = MakeScreenNode("1:90", "Home");
            var otherNode = MakeScreenNode("1:91", "Home");
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:90",
                AssetPath = "Assets/Figma/Screens/Home.prefab"
            });

            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.KeepExistingAssetPath;
            data.ReservedScreenManifestPaths["Assets/Figma/Screens/Home.prefab"] = "1:90";

            var otherPath = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, otherNode, 0, out var otherUsedManifest);
            var ownerPath = FigmaScreenPrefabPathSelector.ResolveSavePath(data, resolver, ownerNode, 0, out var ownerUsedManifest);

            Assert.That(otherUsedManifest, Is.False);
            Assert.That(otherPath, Is.EqualTo("Assets/Figma/Screens/Home_1.prefab"));
            Assert.That(ownerUsedManifest, Is.True);
            Assert.That(ownerPath, Is.EqualTo("Assets/Figma/Screens/Home.prefab"));
        }

        [Test]
        public void ReserveManifestPathsForCurrentImport_MoveLatest_DoesNotReserveOldPaths()
        {
            var page = new Node { id = "0:1", name = "Page", type = NodeType.CANVAS };
            var screen = MakeScreenNode("1:100", "Home");
            page.children = new[] { screen };
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
            var manifest = ScriptableObject.CreateInstance<FigmaImportManifest>();
            manifest.Entries.Add(new FigmaImportManifestEntry
            {
                NodeId = "1:100",
                NodeType = NodeType.FRAME.ToString(),
                AssetPath = "Assets/Figma/Screens/OldHome.prefab"
            });
            var data = new FigmaImportProcessData
            {
                Settings = ScriptableObject.CreateInstance<UnityToFigmaSettings>(),
                ImportManifest = manifest,
                SourceFile = file,
                SelectedPagesForImport = new System.Collections.Generic.List<Node> { page },
                NodeLookupDictionary = new System.Collections.Generic.Dictionary<string, Node> { ["1:100"] = screen }
            };
            data.Settings.PathUpdatePolicy = PathUpdatePolicy.MoveToLatestResolvedPath;

            FigmaScreenPrefabPathSelector.ReserveManifestPathsForCurrentImport(data);

            Assert.That(data.ReservedScreenManifestPaths, Is.Empty);
        }
    }
}
