using UnityEditor;
using UnityEngine;

namespace UnityToFigma.Editor.Settings
{
    /// <summary>
    /// Default inspector for <see cref="UnityToFigmaSettings"/> (including import path/policy fields) plus page selection when enabled.
    /// </summary>
    [CustomEditor(typeof(UnityToFigmaSettings))]
    public sealed class UnityToFigmaSettingsEditor : UnityEditor.Editor
    {
        private static Vector2 s_PageScrollPos;
        private static Vector2 s_ScreenScrollPos;

        public override void OnInspectorGUI()
        {
            var targetSettingsObject = target as UnityToFigmaSettings;
            var onlyImportPages = targetSettingsObject.OnlyImportSelectedPages;
            var preEditUrl = targetSettingsObject.DocumentUrl;
            base.OnInspectorGUI();
            UnityToFigmaSettingsPageSelectionHelper.ApplyAfterSettingsFieldsEdited(
                targetSettingsObject, preEditUrl, onlyImportPages);

            if (targetSettingsObject.OnlyImportSelectedPages)
            {
                GUILayout.Space(20);
                var changed = UnityToFigmaSettingsPageSelectionHelper.DrawPageSelectionList(
                    "Select Pages to import", targetSettingsObject.PageDataList, ref s_PageScrollPos);
                if (changed)
                {
                    EditorUtility.SetDirty(targetSettingsObject);
                    AssetDatabase.SaveAssetIfDirty(targetSettingsObject);
                }
            }
        }
    }
}
