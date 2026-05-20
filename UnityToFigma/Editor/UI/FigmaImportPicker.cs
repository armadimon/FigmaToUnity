using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Settings;
using Newtonsoft.Json;

namespace UnityToFigma.Editor.UI
{
    /// <summary>
    /// Two-stage picker: choose pages from the figma document (lightweight /?depth=1 call),
    /// then drill into each page and check which child frames/components to import.
    /// Selection is persisted into UnityToFigmaSettings.PageDataList + NodeSelections
    /// so a regular Sync uses only the picked subset.
    /// </summary>
    public class FigmaImportPicker : EditorWindow
    {
        [MenuItem("UnityToFigma/Pick Pages and Nodes...")]
        public static void Open()
        {
            var win = GetWindow<FigmaImportPicker>("Figma Import Picker");
            win.minSize = new Vector2(420, 480);
            win.RefreshPagesAsync();
        }

        UnityToFigmaSettings m_Settings;
        readonly Dictionary<string, PageRow> m_PageRows = new();
        readonly List<string> m_PageOrder = new();
        Vector2 m_Scroll;
        bool m_Loading;
        string m_StatusMessage = "";

        class PageRow
        {
            public string Id;
            public string Name;
            public bool Selected;
            public bool Expanded;
            public bool ChildrenLoaded;
            public bool Loading;
            public List<NodeRow> Children = new();
        }

        class NodeRow
        {
            public string Id;
            public string Name;
            public NodeType Type;
            public bool Selected;
            public bool Expanded;
            public List<NodeRow> Children = new();
        }

        void OnEnable()
        {
            m_Settings = UnityToFigmaSettingsProvider.FindSettingsAsset();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Stage 1: pick the pages you want.\n" +
                "Stage 2: expand a page and tick only the frames/components to import.\n" +
                "Sync afterwards will fetch only your selection (uses GET /v1/files/{key}/nodes — light response).",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(m_Loading);
                if (GUILayout.Button("Refresh Pages")) RefreshPagesAsync();
                if (GUILayout.Button("Select All Pages")) ApplyToAllPages(true);
                if (GUILayout.Button("Deselect All Pages")) ApplyToAllPages(false);
                EditorGUI.EndDisabledGroup();
            }

            if (!string.IsNullOrEmpty(m_StatusMessage))
                EditorGUILayout.LabelField(m_StatusMessage, EditorStyles.miniLabel);

            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            foreach (var pageId in m_PageOrder)
            {
                if (!m_PageRows.TryGetValue(pageId, out var page)) continue;
                DrawPageRow(page);
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                EditorGUI.BeginDisabledGroup(m_Settings == null);
                if (GUILayout.Button("Apply Selection to Settings", GUILayout.Width(240)))
                    ApplySelectionToSettings();
                EditorGUI.EndDisabledGroup();
            }
        }

        void DrawPageRow(PageRow page)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                page.Expanded = EditorGUILayout.Foldout(page.Expanded, GUIContent.none, true, EditorStyles.foldout);
                page.Selected = EditorGUILayout.ToggleLeft(
                    $"{page.Name}  ({page.Id})", page.Selected, GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Load nodes", GUILayout.Width(100)))
                    LoadPageChildrenAsync(page);
            }

            if (!page.Expanded) return;

            EditorGUI.indentLevel++;
            if (page.Loading)
                EditorGUILayout.LabelField("Loading...", EditorStyles.miniLabel);
            else if (!page.ChildrenLoaded)
                EditorGUILayout.LabelField("Click 'Load nodes' to fetch children.", EditorStyles.miniLabel);
            else
            {
                foreach (var n in page.Children) DrawNodeRow(n);
            }
            EditorGUI.indentLevel--;
        }

        void DrawNodeRow(NodeRow node)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (node.Children.Count > 0)
                    node.Expanded = EditorGUILayout.Foldout(node.Expanded, GUIContent.none, true, EditorStyles.foldout);
                else
                    GUILayout.Space(16);
                node.Selected = EditorGUILayout.ToggleLeft(
                    $"[{node.Type}] {node.Name}  ({node.Id})", node.Selected, GUILayout.ExpandWidth(true));
            }
            if (node.Expanded)
            {
                EditorGUI.indentLevel++;
                foreach (var c in node.Children) DrawNodeRow(c);
                EditorGUI.indentLevel--;
            }
        }

        void ApplyToAllPages(bool selected)
        {
            foreach (var p in m_PageRows.Values) p.Selected = selected;
        }

        async void RefreshPagesAsync()
        {
            if (m_Settings == null)
            {
                m_Settings = UnityToFigmaSettingsProvider.FindSettingsAsset();
                if (m_Settings == null)
                {
                    m_StatusMessage = "Settings asset not found. Create one from Project Settings → UnityToFigma.";
                    return;
                }
            }
            if (!UnityToFigmaImporter.CheckDocumentDownloadRequirements(m_Settings)) return;

            m_Loading = true;
            m_StatusMessage = "Fetching pages (depth=1)...";
            Repaint();
            try
            {
                var file = await FigmaApiUtils.GetFigmaDocumentPagesLite(
                    m_Settings.FileId, UnityToFigmaImporter.GetPersonalAccessToken());
                m_PageRows.Clear();
                m_PageOrder.Clear();
                if (file?.document?.children != null)
                {
                    foreach (var c in file.document.children)
                    {
                        if (c == null || c.type != NodeType.CANVAS) continue;
                        var prev = m_Settings.PageDataList.FirstOrDefault(p => p.NodeId == c.id);
                        m_PageRows[c.id] = new PageRow
                        {
                            Id = c.id,
                            Name = c.name,
                            Selected = prev?.Selected ?? true,
                        };
                        m_PageOrder.Add(c.id);
                    }
                }
                m_StatusMessage = $"Loaded {m_PageOrder.Count} page(s).";
            }
            catch (Exception e)
            {
                m_StatusMessage = "Failed: " + e.Message;
            }
            finally
            {
                m_Loading = false;
                Repaint();
            }
        }

        async void LoadPageChildrenAsync(PageRow page)
        {
            if (m_Settings == null) return;
            if (page.Loading) return;
            if (page.ChildrenLoaded)
            {
                // Toggle a refresh on demand.
                page.ChildrenLoaded = false;
                page.Children.Clear();
            }
            page.Loading = true;
            page.Expanded = true;
            Repaint();
            try
            {
                var token = UnityToFigmaImporter.GetPersonalAccessToken();
                var url = $"https://api.figma.com/v1/files/{m_Settings.FileId}/nodes?ids={Uri.EscapeDataString(page.Id)}&depth=3";
                var req = await FigmaApiUtils.SendGetWithRetryAsync(url, token);
                if (req.result != UnityWebRequest.Result.Success)
                    throw new Exception($"HTTP {req.responseCode} {req.error}");

                var settings = new JsonSerializerSettings()
                {
                    DefaultValueHandling = DefaultValueHandling.Include,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                };
                var fileNodes = JsonConvert.DeserializeObject<FigmaFileNodes>(req.downloadHandler.text, settings);
                page.Children.Clear();
                if (fileNodes?.nodes != null && fileNodes.nodes.TryGetValue(page.Id, out var miniFile))
                {
                    var pageNode = miniFile?.document;
                    if (pageNode?.children != null)
                    {
                        var preselect = m_Settings.NodeSelections.FirstOrDefault(s => s.PageNodeId == page.Id);
                        var preselectSet = preselect != null
                            ? new HashSet<string>(preselect.SelectedNodeIds)
                            : null;
                        foreach (var child in pageNode.children)
                            page.Children.Add(BuildNodeRow(child, preselectSet));
                    }
                }
                page.ChildrenLoaded = true;
            }
            catch (Exception e)
            {
                m_StatusMessage = $"Failed to load page {page.Name}: {e.Message}";
            }
            finally
            {
                page.Loading = false;
                Repaint();
            }
        }

        static NodeRow BuildNodeRow(Node n, HashSet<string> preselect)
        {
            var row = new NodeRow
            {
                Id = n.id,
                Name = n.name,
                Type = n.type,
                Selected = preselect == null || preselect.Contains(n.id),
            };
            if (n.children != null)
            {
                foreach (var c in n.children)
                {
                    if (c == null) continue;
                    row.Children.Add(BuildNodeRow(c, preselect));
                }
            }
            return row;
        }

        void ApplySelectionToSettings()
        {
            if (m_Settings == null) return;

            // Update PageDataList (preserve order from picker).
            m_Settings.PageDataList.Clear();
            foreach (var pageId in m_PageOrder)
            {
                if (!m_PageRows.TryGetValue(pageId, out var page)) continue;
                m_Settings.PageDataList.Add(new FigmaPageData(page.Name, page.Id)
                {
                    Selected = page.Selected,
                });
            }
            m_Settings.OnlyImportSelectedPages = true;

            // Update NodeSelections only for pages whose children we loaded; leave others untouched
            // so partial picking is non-destructive.
            foreach (var pageId in m_PageOrder)
            {
                if (!m_PageRows.TryGetValue(pageId, out var page) || !page.ChildrenLoaded) continue;
                var existing = m_Settings.NodeSelections.FirstOrDefault(s => s.PageNodeId == pageId);
                if (existing == null)
                {
                    existing = new FigmaNodeSelection { PageNodeId = pageId };
                    m_Settings.NodeSelections.Add(existing);
                }
                existing.SelectedNodeIds = CollectSelectedNodeIds(page.Children);
            }

            EditorUtility.SetDirty(m_Settings);
            AssetDatabase.SaveAssetIfDirty(m_Settings);
            m_StatusMessage = "Selection saved. Run UnityToFigma → Sync Document to import.";
        }

        static List<string> CollectSelectedNodeIds(List<NodeRow> rows)
        {
            var ids = new List<string>();
            void Walk(NodeRow r)
            {
                if (r.Selected) ids.Add(r.Id);
                foreach (var c in r.Children) Walk(c);
            }
            foreach (var r in rows) Walk(r);
            return ids;
        }
    }
}
