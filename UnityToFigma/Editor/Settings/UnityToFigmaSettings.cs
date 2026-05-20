using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityToFigma.Editor.FigmaApi;

namespace UnityToFigma.Editor.Settings
{
    public class UnityToFigmaSettings : ScriptableObject
    {
        [Header("Figma Document")]
        [Tooltip("The FIGMA Document URL to import")]
        public string DocumentUrl;
        
        [Tooltip("Generate logic and linking of screens based on FIGMA's 'Prototype' settings")]
        public bool BuildPrototypeFlow=true;
        
        [Space(10)]
        [Tooltip("Scene used for prototype assets, including canvas")]
        public string RunTimeAssetsScenePath;
        
        [Tooltip("Enable Auto layout components (Horizontal/Vertical layout) (EXPERIMENTAL)")]
        public bool EnableAutoLayout = false;
        
        [Tooltip("C# Namespace filter for binding MonoBehaviours for screens. Use this to ensure it will only bind to MonoBehaviours in that namespace (eg specify 'MyGame.UI' to only bind MyGame.UI.PlayScreen node to 'PlayScreen')")]
        public string ScreenBindingNamespace="";
        
        [Tooltip("Scale for rendering server images")]
        public int ServerRenderImageScale=3;

        [Tooltip("Tick this to enable downloading missing fonts from Google Fonts")]
        public bool EnableGoogleFontsDownloads = true;

        [Tooltip("Generate a C# file containing all found screens")]
        public bool CreateScreenNameCSharpFile = false;
        
        [Tooltip("If false, the generator will not attempt to build any nodes marked for export")]
        public bool GenerateNodesMarkedForExport = true;
        
        [Tooltip("If true, download only selected pages and screens")]
        public bool OnlyImportSelectedPages = false;

        [Header("Caching")]
        [Tooltip("Reuse cached figma file response when available. Avoids burning the per-month GET file quota on Starter plans. Disable or use 'Force Refetch' when the figma document changed.")]
        public bool UseResponseCache = true;

        [Tooltip("Ignore cache and force a fresh download on the next Sync.")]
        public bool ForceRefetch = false;

        [Tooltip("How long (in hours) a cached file response stays fresh before being refetched automatically. 0 = no expiry (cache lives until manually refreshed).")]
        public int CacheTtlHours = 0;

        [HideInInspector]
        public List<FigmaNodeSelection> NodeSelections = new ();

        [Header("Import Paths & Policies")]
        [Tooltip("Root folder under Assets for generated import output (no trailing slash).")]
        public string ImportRoot = UnityToFigmaImportSettingsDefaults.ImportRoot;

        [Tooltip("Subfolder name for screen assets under the import layout.")]
        public string ScreensFolderName = UnityToFigmaImportSettingsDefaults.ScreensFolderName;

        [Tooltip("Subfolder name for component assets under the import layout.")]
        public string ComponentsFolderName = UnityToFigmaImportSettingsDefaults.ComponentsFolderName;

        [Tooltip("Subfolder name for textures under the import layout.")]
        public string TexturesFolderName = UnityToFigmaImportSettingsDefaults.TexturesFolderName;

        [Tooltip("Subfolder name for fonts under the import layout.")]
        public string FontsFolderName = UnityToFigmaImportSettingsDefaults.FontsFolderName;

        [Tooltip("Subfolder name for page-related assets under the import layout.")]
        public string PagesFolderName = UnityToFigmaImportSettingsDefaults.PagesFolderName;

        [Tooltip("Subfolder name for server-rendered images under the import layout.")]
        public string ServerRenderedImagesFolderName =
            UnityToFigmaImportSettingsDefaults.ServerRenderedImagesFolderName;

        [Tooltip("Subfolder name for manifest data under the import layout.")]
        public string ManifestFolderName = UnityToFigmaImportSettingsDefaults.ManifestFolderName;

        [Tooltip("Name of the parent transform under which imported screens are parented (when import wiring is enabled).")]
        public string ScreenParentTransformName =
            UnityToFigmaImportSettingsDefaults.ScreenParentTransformName;

        [Tooltip("If true, create a Canvas when missing during import (when import wiring is enabled).")]
        public bool CreateMissingCanvas = UnityToFigmaImportSettingsDefaults.CreateMissingCanvas;

        [field: SerializeField]
        [field: FormerlySerializedAs("PathUpdatePolicySetting")]
        [field: Tooltip("How to reconcile on-disk asset paths when the resolved import layout changes.")]
        public PathUpdatePolicy PathUpdatePolicy { get; set; } =
            UnityToFigmaImportSettingsDefaults.DefaultPathUpdatePolicy;

        [field: SerializeField]
        [field: FormerlySerializedAs("MissingNodePolicySetting")]
        [field: Tooltip("What to do when a previously imported node no longer exists in the Figma document.")]
        public MissingNodePolicy MissingNodePolicy { get; set; } =
            UnityToFigmaImportSettingsDefaults.DefaultMissingNodePolicy;

        [HideInInspector]
        public List<FigmaPageData> PageDataList = new ();

        public string FileId {
            get
            {
                var (isValid, fileId) = FigmaApiUtils.GetFigmaDocumentIdFromUrl(DocumentUrl);
                return isValid ? fileId : "";
            }
        }
        
        public void RefreshForUpdatedPages(FigmaFile file)
        {
            // Get all pages from Figma Doc
            var pageNodeList = FigmaDataUtils.GetPageNodes(file);
            var downloadPageNodeIdList = pageNodeList.Select(p => p.id).ToList();

            // Get a list of all pages in the settings file
            var settingsPageDataIdList = PageDataList.Select(p => p.NodeId).ToList();

            // Build a list of all new pages to add
            var addPageIdList = downloadPageNodeIdList.Except(settingsPageDataIdList);
            foreach (var addPageId in addPageIdList)
            {
                var addNode = pageNodeList.FirstOrDefault(p => p.id == addPageId);
                PageDataList.Add(new FigmaPageData(addNode.name, addNode.id));
            }
            
            // Build a list of removed pages to remove from list
            var deletePageIdList = settingsPageDataIdList.Except(downloadPageNodeIdList);
            foreach (var deletePageId in deletePageIdList)
            {
                var index = PageDataList.FindIndex(p => p.NodeId == deletePageId);
                PageDataList.RemoveAt(index);
            }
            PageDataList.OrderBy(p => p.NodeId);
        }
    }

    [Serializable]
    public class FigmaPageData
    {
        public string Name;
        public string NodeId;
        public bool Selected;

        public FigmaPageData(){}

        public FigmaPageData(string name, string nodeId)
        {
            Name = name;
            NodeId = nodeId;
            Selected = true; // default is true
        }
    }

    /// <summary>
    /// Per-page node selection persisted from the FigmaImportPicker GUI.
    /// </summary>
    [Serializable]
    public class FigmaNodeSelection
    {
        public string PageNodeId;
        public List<string> SelectedNodeIds = new();
    }
}