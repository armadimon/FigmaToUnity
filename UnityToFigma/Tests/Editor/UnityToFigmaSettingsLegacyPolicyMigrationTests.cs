using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.Tests
{
    /// <summary>
    /// Proves <see cref="UnityEngine.Serialization.FormerlySerializedAs"/> on policy backing fields migrates
    /// legacy YAML keys PathUpdatePolicySetting / MissingNodePolicySetting into the current properties.
    /// </summary>
    /// <remarks>
    /// Brittleness: depends on Unity YAML layout for ScriptableObject assets (MonoBehaviour block), script GUID
    /// resolution at runtime, and Unity applying migration on import. Different Unity versions may rewrite or
    /// normalize YAML; the assertions are on deserialized <see cref="UnityToFigmaSettings"/> values, not raw text.
    /// </remarks>
    public class UnityToFigmaSettingsLegacyPolicyMigrationTests
    {
        private const string TestAssetPath = "Assets/__UnityToFigma_Task2_LegacyPolicyMigration.asset";

        [TearDown]
        public void TearDown()
        {
            DeleteTestAssetIfExists();
        }

        [Test]
        public void LegacyYaml_PolicyFields_MigrateIntoPathUpdatePolicy_And_MissingNodePolicy()
        {
            DeleteTestAssetIfExists();

            try
            {
                var scriptGuid = FindUnityToFigmaSettingsScriptGuid();
                var yaml = BuildLegacyYaml(scriptGuid);
                File.WriteAllText(GetAbsoluteAssetPath(TestAssetPath), yaml, Encoding.UTF8);
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(
                    TestAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var loaded = AssetDatabase.LoadAssetAtPath<UnityToFigmaSettings>(TestAssetPath);
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.PathUpdatePolicy, Is.EqualTo(PathUpdatePolicy.MoveToLatestResolvedPath));
                Assert.That(loaded.MissingNodePolicy, Is.EqualTo(MissingNodePolicy.DeleteOnImport));
            }
            finally
            {
                DeleteTestAssetIfExists();
            }
        }

        private static string FindUnityToFigmaSettingsScriptGuid()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (ms != null && ms.GetClass() == typeof(UnityToFigmaSettings))
                    return guid;
            }

            Assert.Fail("Could not locate MonoScript for UnityToFigmaSettings (expected in project).");
            throw new InvalidOperationException("Unreachable");
        }

        private static string BuildLegacyYaml(string scriptGuid)
        {
            var sb = new StringBuilder();
            sb.AppendLine("%YAML 1.1");
            sb.AppendLine("%TAG !u! tag:unity3d.com,2011:");
            sb.AppendLine("--- !u!114 &11400000");
            sb.AppendLine("MonoBehaviour:");
            sb.AppendLine("  m_ObjectHideFlags: 0");
            sb.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
            sb.AppendLine("  m_PrefabInstance: {fileID: 0}");
            sb.AppendLine("  m_PrefabAsset: {fileID: 0}");
            sb.AppendLine("  m_GameObject: {fileID: 0}");
            sb.AppendLine("  m_Enabled: 1");
            sb.AppendLine("  m_EditorHideFlags: 0");
            sb.AppendLine($"  m_Script: {{fileID: 11500000, guid: {scriptGuid}, type: 3}}");
            sb.AppendLine("  m_Name: __LegacyPolicyMigration");
            sb.AppendLine("  m_EditorClassIdentifier: ");
            sb.AppendLine("  PathUpdatePolicySetting: 1");
            sb.AppendLine("  MissingNodePolicySetting: 1");
            return sb.ToString();
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
