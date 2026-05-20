using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Fonts;
using UnityToFigma.Editor.Nodes;
using UnityToFigma.Editor.PrototypeFlow;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;
using UnityToFigma.Editor.Utils;
using UnityToFigma.Runtime.UI;
using Object = UnityEngine.Object;

namespace UnityToFigma.Editor
{
    /// <summary>
    ///  Manages Figma importing and document creation
    /// </summary>
    public static class UnityToFigmaImporter
    {
        
        /// <summary>
        /// The settings asset, containing preferences for importing
        /// </summary>
        private static UnityToFigmaSettings s_UnityToFigmaSettings;
        
        /// <summary>
        /// We'll cache the access token in editor Player prefs
        /// </summary>
        private const string FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY = "FIGMA_PERSONAL_ACCESS_TOKEN";

        /// <summary>
        /// Root path of this package under Packages/. Used to load bundled assets via AssetDatabase.
        /// Keep in sync with package.json "name".
        /// </summary>
        public const string PackageRoot = "Packages/com.armadimon.figmatounity";

        public const string PROGRESS_BOX_TITLE = "Importing Figma Document";

        /// <summary>
        /// Figma imposes a limit on the number of images in a single batch. This is batch size
        /// (This is a bit of a guess - 650 is rejected)
        /// </summary>
        // figma's /v1/images endpoint has a server-side render time budget per request. Empirically batches
        // of ~100 vector nodes at scale=3 already trip "Render timeout"; 300 (original value) was optimistic.
        // We start conservative and the batched helper splits further on failure.
        private const int MAX_SERVER_RENDER_IMAGE_BATCH_SIZE = 25;

        /// <summary>
        /// Cached personal access token, retrieved from PlayerPrefs
        /// </summary>
        private static string s_PersonalAccessToken;

        /// <summary>
        /// Returns the cached PAT, or refetches from PlayerPrefs when unset. For helpers that need to call
        /// figma API without going through the full Sync requirements check (e.g. page-list refresh).
        /// </summary>
        public static string GetPersonalAccessToken()
        {
            if (string.IsNullOrEmpty(s_PersonalAccessToken))
                s_PersonalAccessToken = PlayerPrefs.GetString(FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY);
            return s_PersonalAccessToken;
        }
        
        /// <summary>
        /// Active canvas used for construction
        /// </summary>
        private static Canvas s_SceneCanvas;

        /// <summary>
        /// The flowScreen controller to mange prototype functionality
        /// </summary>
        private static PrototypeFlowController s_PrototypeFlowController;

        [MenuItem("UnityToFigma/Sync Document")]
        static void Sync()
        {
            SyncAsync();
        }
        
        private static async void SyncAsync()
        {
            var requirementsMet = CheckRequirements();
            if (!requirementsMet) return;

            FigmaFile figmaFile;
            List<Node> pageNodeList;

            if (s_UnityToFigmaSettings.OnlyImportSelectedPages)
            {
                var enabledPageIdList = s_UnityToFigmaSettings.PageDataList
                    .Where(p => p.Selected).Select(p => p.NodeId).ToList();
                if (enabledPageIdList.Count <= 0)
                {
                    ReportError("'Import Selected Pages' is selected, but no pages are selected for import.", "");
                    SelectSettings();
                    return;
                }

                // Restrict the saved node-level selection to currently enabled pages so we don't ask figma
                // for nodes whose parent page the user has now unchecked.
                var pageAllow = new HashSet<string>(enabledPageIdList);
                var nodeSelections = s_UnityToFigmaSettings.NodeSelections
                    .Where(s => s != null && pageAllow.Contains(s.PageNodeId)
                                && s.SelectedNodeIds != null && s.SelectedNodeIds.Count > 0)
                    .ToList();

                if (nodeSelections.Count > 0)
                {
                    // Node-level fetch — figma returns only the picked subtrees.
                    figmaFile = await DownloadFigmaDocumentBySelectedNodes(
                        s_UnityToFigmaSettings.FileId, nodeSelections);
                }
                else
                {
                    // Page-level fetch — entire pages, but at least we skip unrelated pages.
                    figmaFile = await DownloadFigmaDocumentBySelectedPages(
                        s_UnityToFigmaSettings.FileId, enabledPageIdList);
                }
                if (figmaFile == null) return;

                pageNodeList = FigmaDataUtils.GetPageNodes(figmaFile);
            }
            else
            {
                figmaFile = await DownloadFigmaDocument(s_UnityToFigmaSettings.FileId);
                if (figmaFile == null) return;
                pageNodeList = FigmaDataUtils.GetPageNodes(figmaFile);
            }

            await ImportDocument(s_UnityToFigmaSettings.FileId, figmaFile, pageNodeList);
        }

        /// <summary>
        /// Download a synthesized FigmaFile that only contains the user-selected nodes (frames/components),
        /// not entire pages. Smallest possible response payload — used when the picker captured node-level
        /// selections via FigmaImportPicker.
        /// </summary>
        public static async Task<FigmaFile> DownloadFigmaDocumentBySelectedNodes(
            string fileId, IList<FigmaNodeSelection> selections)
        {
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE,
                "Downloading selected node(s)", 0);
            try
            {
                return await FigmaApiUtils.GetFigmaDocumentBySelectedNodes(fileId, s_PersonalAccessToken, selections);
            }
            catch (Exception e)
            {
                ReportError(
                    "Error downloading Figma nodes - Check your selection and personal access key.",
                    e.ToString());
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Download a synthesized FigmaFile that only contains the user-selected pages.
        /// Calls GET /v1/files/{key}/nodes?ids=PAGE_IDS instead of the full GET file endpoint.
        /// </summary>
        public static async Task<FigmaFile> DownloadFigmaDocumentBySelectedPages(string fileId, IList<string> selectedPageIds)
        {
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE,
                $"Downloading {selectedPageIds.Count} selected page(s)", 0);
            try
            {
                return await FigmaApiUtils.GetFigmaDocumentBySelectedPages(fileId, s_PersonalAccessToken, selectedPageIds);
            }
            catch (Exception e)
            {
                ReportError(
                    "Error downloading Figma pages - Check your personal access key and selected page list.",
                    e.ToString());
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Check to make sure all requirements are met before syncing
        /// </summary>
        /// <returns></returns>
        public static bool CheckRequirements() {
            if (!CheckDocumentDownloadRequirements())
                return false;

            if (Shader.Find("TextMeshPro/Mobile/Distance Field")==null)
            {
                EditorUtility.DisplayDialog("Text Mesh Pro" ,"You need to install TestMeshPro Essentials. Use Window->Text Mesh Pro->Import TMP Essential Resources","OK");
                return false;
            }
            
            // Check all requirements for run time if required
            if (s_UnityToFigmaSettings.BuildPrototypeFlow)
            {
                if (!CheckRunTimeRequirements())
                    return false;
            }
            
            return true;
            
        }

        public static bool CheckDocumentDownloadRequirements(UnityToFigmaSettings settingsOverride = null)
        {
            if (settingsOverride != null)
                s_UnityToFigmaSettings = settingsOverride;

            // Find the settings asset if it exists
            if (s_UnityToFigmaSettings == null)
                s_UnityToFigmaSettings = UnityToFigmaSettingsProvider.FindSettingsAsset();
            
            if (s_UnityToFigmaSettings == null)
            {
                if (
                    EditorUtility.DisplayDialog("No UnityToFigma Settings File",
                        "Create a new UnityToFigma settings file? ", "Create", "Cancel"))
                {
                    s_UnityToFigmaSettings =
                        UnityToFigmaSettingsProvider.GenerateUnityToFigmaSettingsAsset();
                }
                else
                {
                    return false;
                }
            }
            
            if (s_UnityToFigmaSettings.FileId.Length == 0)
            {
                EditorUtility.DisplayDialog("Missing Figma Document" ,"Figma Document Url is not valid, please enter valid URL","OK");
                return false;
            }
            
            // Get stored personal access key
            s_PersonalAccessToken = PlayerPrefs.GetString(FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY);

            if (string.IsNullOrEmpty(s_PersonalAccessToken))
            {
                var setToken = RequestPersonalAccessToken();
                if (!setToken) return false;
            }
            
            if (Application.isPlaying)
            {
                EditorUtility.DisplayDialog("UnityToFigma Importer","Please exit play mode before importing", "OK");
                return false;
            }

            return true;
        }


        private static bool CheckRunTimeRequirements()
        {
            if (string.IsNullOrEmpty(s_UnityToFigmaSettings.RunTimeAssetsScenePath))
            {
                if (
                    EditorUtility.DisplayDialog("No UnityToFigma Scene set",
                        "Use current scene for generating prototype flow? ", "OK", "Cancel"))
                {
                    var currentScene = SceneManager.GetActiveScene();
                    s_UnityToFigmaSettings.RunTimeAssetsScenePath = currentScene.path;
                    EditorUtility.SetDirty(s_UnityToFigmaSettings);
                    AssetDatabase.SaveAssetIfDirty(s_UnityToFigmaSettings);
                }
                else
                {
                    return false;
                }
            }
            
            // If current scene doesnt match, switch
            if (SceneManager.GetActiveScene().path != s_UnityToFigmaSettings.RunTimeAssetsScenePath)
            {
                if (EditorUtility.DisplayDialog("UnityToFigma Scene",
                        "Current Scene doesnt match Runtime asset scene - switch scenes?", "OK", "Cancel"))
                {
                    EditorSceneManager.OpenScene(s_UnityToFigmaSettings.RunTimeAssetsScenePath);
                }
                else
                {
                    return false;
                }
            }
            
            var placement = FigmaRuntimePlacementResolver.Resolve(s_UnityToFigmaSettings, () => CreateCanvas(true));
            if (!placement.Success)
            {
                EditorUtility.DisplayDialog("UnityToFigma", placement.ErrorMessage, "OK");
                return false;
            }

            s_SceneCanvas = placement.Canvas;
            s_PrototypeFlowController = placement.Controller;
            return true;
        }

        [MenuItem("UnityToFigma/Select Settings File")]
        static void SelectSettings()
        {
            var settingsAsset=UnityToFigmaSettingsProvider.FindSettingsAsset();
            Selection.activeObject = settingsAsset;
        }

        [MenuItem("UnityToFigma/Set Personal Access Token")]
        static void SetPersonalAccessToken()
        {
            RequestPersonalAccessToken();
        }

        [MenuItem("UnityToFigma/Clear Response Cache")]
        static void ClearResponseCache()
        {
            var settings = UnityToFigmaSettingsProvider.FindSettingsAsset();
            if (settings == null || string.IsNullOrEmpty(settings.FileId))
            {
                EditorUtility.DisplayDialog("UnityToFigma", "No settings or file id available.", "OK");
                return;
            }
            FigmaApiUtils.InvalidateCachedFigmaDocument(settings.FileId);
            Debug.Log($"[FigmaToUnity] Cleared response cache for file {settings.FileId}.");
        }

        [MenuItem("UnityToFigma/Force Refetch Next Sync")]
        static void ForceRefetchNextSync()
        {
            var settings = UnityToFigmaSettingsProvider.FindSettingsAsset();
            if (settings == null) return;
            settings.ForceRefetch = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            Debug.Log("[FigmaToUnity] Next Sync will bypass cache and refetch from figma API.");
        }
        
        /// <summary>
        /// Launch window to request personal access token
        /// </summary>
        /// <returns></returns>
        static bool RequestPersonalAccessToken()
        {
            s_PersonalAccessToken = PlayerPrefs.GetString(FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY);
            var newAccessToken = EditorInputDialog.Show( "Personal Access Token", "Please enter your Figma Personal Access Token (you can create in the 'Developer settings' page)",s_PersonalAccessToken);
            if (!string.IsNullOrEmpty(newAccessToken))
            {
                s_PersonalAccessToken = newAccessToken;
                Debug.Log(FigmaTokenLogMessages.GetPersonalAccessTokenSavedMessage());
                PlayerPrefs.SetString(FIGMA_PERSONAL_ACCESS_TOKEN_PREF_KEY,s_PersonalAccessToken);
                PlayerPrefs.Save();
                return true;
            }

            return false;
        }


        private static Canvas CreateCanvas(bool createEventSystem)
        {
            // Canvas
            var canvasGameObject = new GameObject("Canvas");
            var canvas=canvasGameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGameObject.AddComponent<GraphicRaycaster>();

            if (!createEventSystem) return canvas;

            var existingEventSystem = Object.FindObjectOfType<EventSystem>();
            if (existingEventSystem == null)
            {
                // Create new event system
                var eventSystemGameObject = new GameObject("EventSystem");
                existingEventSystem=eventSystemGameObject.AddComponent<EventSystem>();
            }

            var pointerInputModule = Object.FindObjectOfType<PointerInputModule>();
            if (pointerInputModule == null)
            {
                // TODO - Allow for new input system?
                existingEventSystem.gameObject.AddComponent<StandaloneInputModule>();
            }

            return canvas;
        }
        

        private static void ReportError(string message,string error)
        {
            EditorUtility.DisplayDialog("UnityToFigma Error",message,"Ok");
            Debug.LogWarning($"{message}\n {error}\n");
        }

        public static async Task<FigmaFile> DownloadFigmaDocument(string fileId)
        {
            // Download figma document
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Downloading file", 0);
            try
            {
                var debugOutputPath = s_UnityToFigmaSettings == null
                    ? null
                    : new FigmaImportPathResolver(s_UnityToFigmaSettings).GetDocumentDebugJsonFilePath();

                bool useCache = false;
                int ttl = 0;
                if (s_UnityToFigmaSettings != null)
                {
                    useCache = s_UnityToFigmaSettings.UseResponseCache && !s_UnityToFigmaSettings.ForceRefetch;
                    ttl = s_UnityToFigmaSettings.CacheTtlHours;
                    if (s_UnityToFigmaSettings.ForceRefetch)
                    {
                        FigmaApiUtils.InvalidateCachedFigmaDocument(fileId);
                        // Consume the one-shot flag so subsequent Syncs go back to cache.
                        s_UnityToFigmaSettings.ForceRefetch = false;
                        EditorUtility.SetDirty(s_UnityToFigmaSettings);
                        AssetDatabase.SaveAssetIfDirty(s_UnityToFigmaSettings);
                    }
                }

                var figmaTask = FigmaApiUtils.GetFigmaDocument(fileId, s_PersonalAccessToken, debugOutputPath, useCache, ttl);
                await figmaTask;
                return figmaTask.Result;
            }
            catch (Exception e)
            {
                ReportError(
                    "Error downloading Figma document - Check your personal access key and document url are correct",
                    e.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return null;
        }

        private static async Task ImportDocument(string fileId, FigmaFile figmaFile, List<Node> downloadPageNodeList)
        {

            // Build a list of page IDs to download
            var downloadPageIdList = downloadPageNodeList.Select(p => p.id).ToList();

            var pathResolver = new FigmaImportPathResolver(s_UnityToFigmaSettings);
            // Ensure we have all required directories (does not delete existing assets)
            pathResolver.EnsureImportDirectoriesExist();

            var importManifest = FigmaImportManifestStore.LoadOrCreate(pathResolver, fileId);
            var importReport = new FigmaImportReport();
            
            // Next build a list of all externally referenced components not included in the document (eg
            // from external libraries) and download
            var externalComponentList = FigmaDataUtils.FindMissingComponentDefinitions(figmaFile);
            
            // TODO - Implement external components
            // This is currently not working as only returns a depth of 1 of returned nodes. Need to get original files too
            /*
            FigmaFileNodes activeExternalComponentsData=null;
            if (externalComponentList.Count > 0)
            {
                EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Getting external component data", 0);
                try
                {
                    var figmaTask = FigmaApiUtils.GetFigmaFileNodes(fileId, s_PersonalAccessToken,externalComponentList);
                    await figmaTask;
                    activeExternalComponentsData = figmaTask.Result;
                }
                catch (Exception e)
                {
                    EditorUtility.ClearProgressBar();
                    ReportError("Error downloading external component Data",e.ToString());
                    return;
                }
            }
            */

            // For any missing component definitions, we are going to find the first instance and switch it to be
            // The source component. This has to be done early to ensure download of server images
            //FigmaFileUtils.ReplaceMissingComponents(figmaFile,externalComponentList);
            
            // Some of the nodes, we'll want to identify to use Figma server side rendering (eg vector shapes, SVGs)
            // First up create a list of nodes we'll substitute with rendered images
            var serverRenderNodes = FigmaDataUtils.FindAllServerRenderNodesInFile(figmaFile,externalComponentList,downloadPageIdList);
            
            // Request a render of these nodes on the server if required. Dedupe ids and use the adaptive
            // batching helper so a "Render timeout" on one batch splits + retries instead of aborting.
            var serverRenderData = new List<FigmaServerRenderData>();
            if (serverRenderNodes.Count > 0)
            {
                var allNodeIds = serverRenderNodes
                    .Select(serverRenderNode => serverRenderNode.SourceNode.id)
                    .Distinct()
                    .ToList();
                try
                {
                    serverRenderData = await FigmaApiUtils.GetFigmaServerRenderDataBatched(
                        fileId,
                        s_PersonalAccessToken,
                        allNodeIds,
                        s_UnityToFigmaSettings.ServerRenderImageScale,
                        MAX_SERVER_RENDER_IMAGE_BATCH_SIZE,
                        onProgress: (done, total) =>
                        {
                            if (total <= 0) return;
                            EditorUtility.DisplayProgressBar(
                                PROGRESS_BOX_TITLE,
                                $"Downloading server-rendered image data {done}/{total}",
                                (float)done / total);
                        });
                }
                catch (Exception e)
                {
                    EditorUtility.ClearProgressBar();
                    ReportError("Error downloading Figma Server Render Image Data", e.ToString());
                    return;
                }
            }

            // Make sure that existing downloaded assets are in the correct format
            FigmaApiUtils.CheckExistingAssetProperties(s_UnityToFigmaSettings);
            
            // Track fills that are actually used. This is needed as FIGMA has a way of listing any bitmap used rather than active 
            var foundImageFills = FigmaDataUtils.GetAllImageFillIdsFromFile(figmaFile,downloadPageIdList);
            
            // Get image fill data for the document (list of urls to download any bitmap data used)
            FigmaImageFillData activeFigmaImageFillData; 
            EditorUtility.DisplayProgressBar(PROGRESS_BOX_TITLE, $"Downloading image fill data", 0);
            try
            {
                var figmaTask = FigmaApiUtils.GetDocumentImageFillData(fileId, s_PersonalAccessToken);
                await figmaTask;
                activeFigmaImageFillData = figmaTask.Result;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                ReportError("Error downloading Figma Image Fill Data",e.ToString());
                return;
            }
            
            // Generate a list of all items that need to be downloaded
            var downloadList =
                FigmaApiUtils.GenerateDownloadQueue(activeFigmaImageFillData,foundImageFills, serverRenderData, serverRenderNodes, s_UnityToFigmaSettings, importReport);

            // Download all required files
            await FigmaApiUtils.DownloadFiles(downloadList, s_UnityToFigmaSettings, importReport);
            

            // Generate font mapping data
            var figmaFontMapTask = FontManager.GenerateFontMapForDocument(figmaFile,
                s_UnityToFigmaSettings.EnableGoogleFontsDownloads, s_UnityToFigmaSettings);
            await figmaFontMapTask;
            var fontMap = figmaFontMapTask.Result;


            var componentData = new FigmaComponentData
            { 
                MissingComponentDefinitionsList = externalComponentList, 
            };
            
            // Stores necessary importer data needed for document generator.
            // ImportManifest is consulted when saving screen prefabs (node id + PathUpdatePolicy) so reimports reuse paths.
            var figmaImportProcessData = new FigmaImportProcessData
            {
                Settings=s_UnityToFigmaSettings,
                PathResolver = pathResolver,
                ImportManifest = importManifest,
                ImportReport = importReport,
                SourceFile = figmaFile,
                ComponentData = componentData,
                ServerRenderNodes = serverRenderNodes,
                PrototypeFlowController = s_PrototypeFlowController,
                FontMap = fontMap,
                PrototypeFlowStartPoints = FigmaDataUtils.GetAllPrototypeFlowStartingPoints(figmaFile),
                SelectedPagesForImport = downloadPageNodeList,
                NodeLookupDictionary = FigmaDataUtils.BuildNodeLookupDictionary(figmaFile)
            };
            FigmaScreenPrefabPathSelector.ReserveManifestPathsForCurrentImport(figmaImportProcessData);
            
            
            // Clear the existing screens on the flowScreen controller
            if (s_UnityToFigmaSettings.BuildPrototypeFlow)
            {
                if (figmaImportProcessData.PrototypeFlowController)
                    figmaImportProcessData.PrototypeFlowController.ClearFigmaScreens();
            }
            else
            {
                s_SceneCanvas = CreateCanvas(false);
            }

            try
            {
                FigmaAssetGenerator.BuildFigmaFile(s_SceneCanvas, figmaImportProcessData);
                FigmaImportManifestReconciler.ReconcileAfterImport(figmaImportProcessData, fileId);
            }
            catch (Exception e)
            {
                ReportError("Error generating Figma document. Check log for details", e.ToString());
                EditorUtility.ClearProgressBar();
                CleanUpPostGeneration();
                return;
            }
            finally
            {
                FigmaImportManifestStore.MarkDirtyAndSave(figmaImportProcessData.ImportManifest);
            }
           
            
            // Lastly, for prototype mode, instantiate the default flowScreen and set the scaler up appropriately
            if (s_UnityToFigmaSettings.BuildPrototypeFlow)
            {
                // Make sure all required default elements are present
                var screenController = figmaImportProcessData.PrototypeFlowController;
                
                // Find default flow start position
                screenController.PrototypeFlowInitialScreenId =  FigmaDataUtils.FindPrototypeFlowStartScreenId(figmaImportProcessData.SourceFile);;

                if (screenController.ScreenParentTransform == null)
                    screenController.ScreenParentTransform = FigmaRuntimePlacementResolver.FindOrCreateScreenParent(
                        screenController,
                        FigmaRuntimePlacementResolver.GetEffectiveScreenParentName(s_UnityToFigmaSettings));

                if (screenController.TransitionEffect == null)
                {
                    // Instantiate and apply the default transition effect (loaded from package assets folder)
                    var defaultTransitionAnimationEffect = AssetDatabase.LoadAssetAtPath($"{PackageRoot}/UnityToFigma/Assets/TransitionFadeToBlack.prefab", typeof(GameObject)) as GameObject;
                    var transitionObject = (GameObject) PrefabUtility.InstantiatePrefab(defaultTransitionAnimationEffect,
                        screenController.transform.transform);
                    screenController.TransitionEffect =
                        transitionObject.GetComponent<TransitionEffect>();
                    
                    UnityUiUtils.SetTransformFullStretch(transitionObject.transform as RectTransform);
                }

                // Set start flowScreen on stage by default                
                var defaultScreenData = figmaImportProcessData.PrototypeFlowController.StartFlowScreen;
                if (defaultScreenData != null)
                {
                    var defaultScreenTransform = defaultScreenData.FigmaScreenPrefab.transform as RectTransform;
                    if (defaultScreenTransform != null)
                    {
                        var defaultSize = defaultScreenTransform.sizeDelta;
                        var canvasScaler = s_SceneCanvas.GetComponent<CanvasScaler>();
                        if (canvasScaler == null) canvasScaler = s_SceneCanvas.gameObject.AddComponent<CanvasScaler>();
                        canvasScaler.referenceResolution = defaultSize;
                        // If we are a vertical template, drive by width
                        canvasScaler.matchWidthOrHeight = (defaultSize.x>defaultSize.y) ? 1f : 0f; // Use height as driver
                        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    }

                    var screenInstance=(GameObject)PrefabUtility.InstantiatePrefab(defaultScreenData.FigmaScreenPrefab, figmaImportProcessData.PrototypeFlowController.ScreenParentTransform);
                    figmaImportProcessData.PrototypeFlowController.SetCurrentScreen(screenInstance,defaultScreenData.FigmaNodeId,true);
                }
                // Write CS file with references to flowScreen name
                if (s_UnityToFigmaSettings.CreateScreenNameCSharpFile) ScreenNameCodeGenerator.WriteScreenNamesCodeFile(figmaImportProcessData.ScreenPrefabs, s_UnityToFigmaSettings);
            }
            CleanUpPostGeneration();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            LogImportSummary(figmaImportProcessData.ImportReport);
        }

        static void LogImportSummary(FigmaImportReport report)
        {
            if (report == null)
                return;

            Debug.Log(report.FormatSummaryLine());
            foreach (var m in report.Messages)
                Debug.Log($"[UnityToFigma] {m}");

            if (report.FailedCount > 0 || report.OrphanedCount > 0 || report.ManifestRemovedCount > 0)
            {
                EditorUtility.DisplayDialog("UnityToFigma import finished",
                    report.FormatSummaryLine() +
                    (report.Messages.Count > 0
                        ? "\n\nSee Console for details."
                        : ""),
                    "OK");
            }
        }

        /// <summary>
        ///  Clean up any leftover assets post-generation
        /// </summary>
        private static void CleanUpPostGeneration()
        {
            if (!s_UnityToFigmaSettings.BuildPrototypeFlow)
            {
                // Destroy temporary canvas
                Object.DestroyImmediate(s_SceneCanvas.gameObject);
            }
        }
    }
}
