using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    public class UnityToFigmaSettingsPolicySerializationTests
    {
        private const string TestAssetPath = "Assets/__UnityToFigma_Task2_PolicyRoundTripTest.asset";

        [TearDown]
        public void TearDown()
        {
            DeleteTestAssetIfExists();
        }

        [Test]
        public void PathUpdatePolicy_And_MissingNodePolicy_RoundTrip_ThroughAssetDatabase()
        {
            DeleteTestAssetIfExists();

            try
            {
                var created = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
                created.PathUpdatePolicy = PathUpdatePolicy.MoveToLatestResolvedPath;
                created.MissingNodePolicy = MissingNodePolicy.DeleteOnImport;

                AssetDatabase.CreateAsset(created, TestAssetPath);
                EditorUtility.SetDirty(created);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(
                    TestAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var yamlOnDisk = File.ReadAllText(GetAbsoluteAssetPath(TestAssetPath));
                AssertDiskYamlContainsSerializedEnumValues(yamlOnDisk);

                var reloaded = AssetDatabase.LoadAssetAtPath<UnityToFigmaSettings>(TestAssetPath);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(reloaded.PathUpdatePolicy, Is.EqualTo(PathUpdatePolicy.MoveToLatestResolvedPath));
                Assert.That(reloaded.MissingNodePolicy, Is.EqualTo(MissingNodePolicy.DeleteOnImport));
            }
            finally
            {
                DeleteTestAssetIfExists();
            }
        }

        private static void AssertDiskYamlContainsSerializedEnumValues(string yamlOnDisk)
        {
            // Unity stores enum fields as integer indices; non-default for both policies is 1.
            Assert.That(
                Regex.IsMatch(yamlOnDisk, @"(PathUpdatePolicy|<PathUpdatePolicy>k__BackingField):\s*1"),
                Is.True,
                "Expected PathUpdatePolicy serialized as 1 (MoveToLatestResolvedPath) in asset YAML.");
            Assert.That(
                Regex.IsMatch(yamlOnDisk, @"(MissingNodePolicy|<MissingNodePolicy>k__BackingField):\s*1"),
                Is.True,
                "Expected MissingNodePolicy serialized as 1 (DeleteOnImport) in asset YAML.");
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Test asset path must be under Assets/.", nameof(assetPath));

            var relative = assetPath.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, relative);
        }

        private static void DeleteTestAssetIfExists()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TestAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(TestAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }

            var abs = GetAbsoluteAssetPath(TestAssetPath);
            if (File.Exists(abs))
            {
                File.Delete(abs);
                var meta = abs + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
                AssetDatabase.Refresh();
            }
        }
    }
}
