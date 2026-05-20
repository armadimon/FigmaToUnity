using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityToFigma.Editor.FigmaApi;

namespace UnityToFigma.Editor.Settings
{
    /// <summary>
    /// Shared page-selection rules and list UI for <see cref="UnityToFigmaSettings"/> (custom inspector and Project Settings).
    /// </summary>
    public static class UnityToFigmaSettingsPageSelectionHelper
    {
        /// <summary>
        /// Call after settings fields are edited (e.g. default inspector or SerializedObject PropertyField pass).
        /// Pass URL and <see cref="UnityToFigmaSettings.OnlyImportSelectedPages"/> from before the edit frame.
        /// </summary>
        public static void ApplyAfterSettingsFieldsEdited(
            UnityToFigmaSettings settings,
            string previousDocumentUrl,
            bool previousOnlyImportSelectedPages)
        {
            if (settings == null)
                return;

            if (settings.DocumentUrl != previousDocumentUrl)
            {
                if (settings.OnlyImportSelectedPages)
                {
                    settings.OnlyImportSelectedPages = false;
                    settings.PageDataList.Clear();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
            }
            else if (settings.OnlyImportSelectedPages != previousOnlyImportSelectedPages)
            {
                if (settings.OnlyImportSelectedPages)
                    RefreshPageListAsync(settings);
                else
                {
                    settings.PageDataList.Clear();
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
            }
        }

        public static async void RefreshPageListAsync(UnityToFigmaSettings settings)
        {
            if (!UnityToFigmaImporter.CheckDocumentDownloadRequirements(settings))
                return;

            // Use the lightweight (?depth=1) endpoint so picking pages doesn't pull the entire document
            // (which causes HTTP/2 stream aborts and 429s on large files).
            FigmaFile figmaFile;
            try
            {
                figmaFile = await FigmaApiUtils.GetFigmaDocumentPagesLite(
                    settings.FileId,
                    UnityToFigmaImporter.GetPersonalAccessToken());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[FigmaToUnity] Failed to refresh page list: " + e.Message);
                return;
            }

            if (figmaFile == null)
                return;

            settings.RefreshForUpdatedPages(figmaFile);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        public static bool DrawPageSelectionList(
            string listTitle,
            IList<FigmaPageData> dataList,
            ref Vector2 scrollPos)
        {
            var applyChanges = false;
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label(listTitle, EditorStyles.boldLabel);
                GUILayout.Space(5);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select all", GUILayout.Width(80)))
                    {
                        applyChanges = true;
                        foreach (var data in dataList)
                            data.Selected = true;
                    }

                    if (GUILayout.Button("Deselect all", GUILayout.Width(80)))
                    {
                        applyChanges = true;
                        foreach (var data in dataList)
                            data.Selected = false;
                    }
                }

                GUILayout.Space(5);

                using (var scrollViewScope = new EditorGUILayout.ScrollViewScope(scrollPos))
                {
                    foreach (var data in dataList)
                    {
                        var isChecked = data.Selected;
                        data.Selected = EditorGUILayout.ToggleLeft(data.Name, data.Selected);
                        if (isChecked != data.Selected)
                            applyChanges = true;
                    }

                    scrollPos = scrollViewScope.scrollPosition;
                }

                return applyChanges;
            }
        }
    }
}
