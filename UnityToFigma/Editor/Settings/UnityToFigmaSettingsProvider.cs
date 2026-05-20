using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityToFigma.Editor.Settings
{

    public class UnityToFigmaSettingsProvider : SettingsProvider
    {
        private GUIStyle m_RedStyle;
        private GUIStyle m_GreenStyle;

        public UnityToFigmaSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
            // EditorStyles is not yet initialized when [SettingsProvider] runs at editor boot; styles are built lazily in OnGUI.
        }

        private void EnsureStyles()
        {
            if (m_RedStyle != null) return;
            m_RedStyle = new GUIStyle(EditorStyles.label);
            m_RedStyle.normal.textColor = UnityEngine.Color.red;
            m_GreenStyle = new GUIStyle(EditorStyles.label);
            m_GreenStyle.normal.textColor = UnityEngine.Color.green;
        }


        public static bool IsSettingsAvailable()
        {
            return true;
        }

        private UnityToFigmaSettings unityToFigmaSettingsAsset;
        private Vector2 m_PageScrollPos;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {

            unityToFigmaSettingsAsset = FindSettingsAsset();
        }

        /// <summary>
        /// Finds the first (and should be only) matching asset
        /// </summary>
        /// <returns></returns>
        public static UnityToFigmaSettings FindSettingsAsset()
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(UnityToFigmaSettings).Name}");
            if (assets == null || assets.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<UnityToFigmaSettings>(AssetDatabase.GUIDToAssetPath(assets[0]));
        }

        public override void OnGUI(string searchContext)
        {
            EnsureStyles();
            if (unityToFigmaSettingsAsset == null)
            {
                GUILayout.Label("Create UnityToFigma Settings Asset");
                if (GUILayout.Button("Create..."))
                {
                    unityToFigmaSettingsAsset = GenerateUnityToFigmaSettingsAsset();
                }

                return;
            }

            var preDocumentUrl = unityToFigmaSettingsAsset.DocumentUrl;
            var preOnlyImportSelectedPages = unityToFigmaSettingsAsset.OnlyImportSelectedPages;

            var serializedObject = new SerializedObject(unityToFigmaSettingsAsset);
            serializedObject.Update();
            var prop = serializedObject.GetIterator();
            var enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.name == "m_Script")
                    continue;
                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();

            UnityToFigmaSettingsPageSelectionHelper.ApplyAfterSettingsFieldsEdited(
                unityToFigmaSettingsAsset, preDocumentUrl, preOnlyImportSelectedPages);

            if (unityToFigmaSettingsAsset.OnlyImportSelectedPages)
            {
                GUILayout.Space(20);
                var pageListChanged = UnityToFigmaSettingsPageSelectionHelper.DrawPageSelectionList(
                    "Select Pages to import", unityToFigmaSettingsAsset.PageDataList, ref m_PageScrollPos);
                if (pageListChanged)
                {
                    EditorUtility.SetDirty(unityToFigmaSettingsAsset);
                    AssetDatabase.SaveAssetIfDirty(unityToFigmaSettingsAsset);
                }
            }

            GUILayout.Space(10);
            var (isValid, fileId) = FigmaApi.FigmaApiUtils.GetFigmaDocumentIdFromUrl(unityToFigmaSettingsAsset.DocumentUrl);
            if (!isValid)
            {
                GUILayout.Label($"Invalid Figma Document URL",m_RedStyle);
                return;
            }
            GUILayout.Label($"Valid Figma Document URL - FileID: {fileId}",m_GreenStyle);
        }


        // Register the SettingsProvider
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            if (IsSettingsAvailable())
            {
                var provider =
                    new UnityToFigmaSettingsProvider("Project/UnityToFigma", SettingsScope.Project);
                return provider;
            }

            return null;
        }

        public static UnityToFigmaSettings GenerateUnityToFigmaSettingsAsset()
        {
            // try create a new version asset.
            var newSettingsAsset = UnityToFigmaSettings.CreateInstance<UnityToFigmaSettings>();

            // Save to the project
            AssetDatabase.CreateAsset(newSettingsAsset, "Assets/UnityToFigmaSettings.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("Generating UnityToFigmaSettings asset", newSettingsAsset);

            return newSettingsAsset;
        }
    }
}