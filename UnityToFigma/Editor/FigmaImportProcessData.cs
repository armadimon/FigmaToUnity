using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Fonts;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;
using UnityToFigma.Runtime.UI;

namespace UnityToFigma.Editor
{
    /// <summary>
    /// Wrapper for all data regarding current import process
    /// </summary>
    public class FigmaImportProcessData
    {
        /// <summary>
        /// The current importer settings
        /// </summary>
        public UnityToFigmaSettings Settings;

        /// <summary>
        /// Resolved import paths for this run (same instance as used for manifest and generators when set).
        /// </summary>
        public FigmaImportPathResolver PathResolver;

        /// <summary>
        /// Node id → asset path manifest for reimport (Task 4+).
        /// </summary>
        public FigmaImportManifest ImportManifest;

        /// <summary>
        /// Per-import counters and messages (Task 4+).
        /// </summary>
        public FigmaImportReport ImportReport;

        /// <summary>
        /// Node ids that received a manifest <c>Upsert</c> during this run (screens/pages/components prefab saves).
        /// </summary>
        public HashSet<string> TouchedManifestNodeIds = new();
        /// <summary>
        /// The source FIGMA file
        /// </summary>
        public FigmaFile SourceFile;

        /// <summary>
        /// Details of components used and created for this file
        /// </summary>
        public FigmaComponentData ComponentData;
        
        /// <summary>
        /// Mapping of document fonts to TextMeshPro fonts and material variants
        /// </summary>
        public FigmaFontMap FontMap;
        
        /// <summary>
        /// Nodes that should be used for server-side rendering substitution
        /// </summary>
        public List<ServerRenderNodeData> ServerRenderNodes = new List<ServerRenderNodeData>();
        
        /// <summary>
        /// this is set when the figma unity UI document is generated
        /// </summary>
        public PrototypeFlowController PrototypeFlowController;

        /// <summary>
        /// Generated page prefabs
        /// </summary>
        public List<GameObject> PagePrefabs = new();
        
        /// <summary>
        /// Generated screens
        /// </summary>
        public List<GameObject> ScreenPrefabs = new List<GameObject>();
        
        /// <summary>
        /// Count of flowScreen prefabs created with a specific name (to prevent name collision)
        /// </summary>
        public Dictionary<string, int> ScreenPrefabNameCounter = new();

        /// <summary>
        /// Tracks screen save paths claimed during the current import run to avoid collisions across reorder/rename cases.
        /// Key: asset path, Value: Figma node id.
        /// </summary>
        public Dictionary<string, string> ClaimedScreenPrefabPaths = new();

        /// <summary>
        /// Existing manifest-owned screen prefab paths for nodes still present in the current import selection.
        /// These paths should remain reserved for their original node ids during path selection.
        /// </summary>
        public Dictionary<string, string> ReservedScreenManifestPaths = new();
        
        /// <summary>
        /// Count of page prefab created with a specific name (to prevent name collision)
        /// </summary>
        public Dictionary<string, int> PagePrefabNameCounter = new();

        /// <summary>
        /// List of all prototype flow starting points
        /// </summary>
        public List<string> PrototypeFlowStartPoints = new();

        /// <summary>
        /// List of all page nodes to import
        /// </summary>
        public List<Node> SelectedPagesForImport = new();
        
        /// <summary>
        /// Allow faster lookup of nodes by ID
        /// </summary>
        public Dictionary<string,Node> NodeLookupDictionary = new();

        /// <summary>
        /// Resolves the Unity asset path to save a screen prefab (manifest + <see cref="UnityToFigmaSettings.PathUpdatePolicy"/>,
        /// with resolver + name duplicate index as fallback).
        /// </summary>
        public string ResolveScreenPrefabSavePath(
            FigmaImportPathResolver resolver,
            Node screenNode,
            int nameDuplicateIndex,
            out bool usedManifestAssetPath)
        {
            return FigmaScreenPrefabPathSelector.ResolveSavePath(this, resolver, screenNode, nameDuplicateIndex,
                out usedManifestAssetPath);
        }
    }

    /// <summary>
    /// Contains data of components and matching prefabs (in addition to missing prefab definitions)
    /// </summary>
    public class FigmaComponentData
    {
        /// <summary>
        /// Count of component names
        /// </summary>
        private Dictionary<string, int> ComponentNameCount = new();
        
        /// <summary>
        /// List of all missing component definitions on the file
        /// </summary>
        public List<string> MissingComponentDefinitionsList=new();
        
        /// <summary>
        /// Mapping of NodeIDs to components
        /// </summary>
        public Dictionary<string, ComponentMappingEntry> ComponentInstances = new();
        
        /// <summary>
        /// Node definitions for externally referenced components 
        /// </summary>
        public FigmaFileNodes ExternalComponentDefinitions;
        
        /// <summary>
        /// Get count of prefabs created with a specific component name (to prevent name collision)
        /// </summary>
        /// <param name="componentName"></param>
        /// <returns></returns>
        public int GetComponentNameCount(string componentName)
        {
            // Check for existing use of name
            return ComponentNameCount.ContainsKey(componentName)
                ? ComponentNameCount[componentName]
                : 0;
        }

        /// <summary>
        /// Increment count of components of a specific name found
        /// </summary>
        /// <param name="componentName"></param>
        /// <param name="amount"></param>
        public void IncrementComponentNameCount(string componentName,int amount)
        {
            ComponentNameCount[componentName] = GetComponentNameCount(componentName) + amount;
        }

        /// <summary>
        /// Register a component prefab
        /// </summary>
        /// <param name="nodeId"></param>
        /// <param name="componentPrefab"></param>
        public void RegisterComponentPrefab(string nodeId, GameObject componentPrefab)
        {
            if (!ComponentInstances.ContainsKey(nodeId))
                ComponentInstances[nodeId] = new ComponentMappingEntry
                {
                    ComponentId = nodeId,
                    ComponentPrefab = componentPrefab
                };
        }
        
        public List<GameObject> AllComponentPrefabs => (from prefabPair in ComponentInstances where prefabPair.Value.ComponentPrefab != null select prefabPair.Value.ComponentPrefab).ToList();

        /// <summary>
        /// Returns the created component prefab for a given component node id
        /// </summary>
        /// <param name="componentId"></param>
        /// <returns></returns>
        public GameObject GetComponentPrefab(string componentId)
        {
            return ComponentInstances.ContainsKey(componentId) ? ComponentInstances[componentId].ComponentPrefab : null;
        }
    }

   
    /// <summary>
    /// Individual entry for component mapping
    /// </summary>
    public class ComponentMappingEntry
    {
        public string ComponentId;
        public GameObject ComponentPrefab;
    }
}