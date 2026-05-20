using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UnityToFigma.Editor.Postprocess
{
    // FigmaMissingFontTracker 가 모은 "다운로드 실패 / 로컬 미보유 → GetClosestFont 폴백" 폰트들을 사용자에게 보여주고,
    // 각 family 별로 TMP_FontAsset 을 ObjectField 로 직접 선택받는다. Apply 시 임포트된 프리팹의 TMP_Text 폰트를 일괄 교체.
    internal sealed class FigmaMissingFontWindow : EditorWindow
    {
        private readonly Dictionary<string, TMP_FontAsset> _selections = new Dictionary<string, TMP_FontAsset>();
        private List<FigmaMissingFontTracker.Entry> _entries = new List<FigmaMissingFontTracker.Entry>();
        private Vector2 _scroll;
        private string _lastReport;

        [MenuItem("UnityToFigma/Postprocess/Open Missing Font Window")]
        public static void Open()
        {
            var window = GetWindow<FigmaMissingFontWindow>("Figma Missing Fonts");
            window.minSize = new Vector2(560, 360);
            window.Refresh();
            window.Show();
            window.Focus();
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            _entries = FigmaMissingFontTracker.GetAll();
            // 같은 family 가 여러 weight 로 잡혀도 한 row 로 모은다.
            _entries = _entries
                .GroupBy(e => e.Family)
                .Select(g => new FigmaMissingFontTracker.Entry(g.Key, g.Max(x => x.Weight)))
                .ToList();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("다운로드 실패 / 미보유 폰트 목록", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "FigmaToUnity 가 자동 다운로드에 실패해 GetClosestFont 폴백으로 매핑한 family 목록입니다.\n" +
                "각 row 의 ObjectField 에 사용할 TMP_FontAsset 을 지정하고 Apply 를 누르면, " +
                "{ImportRoot}/Screens/Components/Pages 의 모든 프리팹에서 해당 family 의 TMP_Text 폰트를 선택된 자산으로 교체합니다.\n" +
                "비워두면 그 family 는 건드리지 않습니다.",
                MessageType.Info);

            EditorGUILayout.Space();
            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox("추적된 누락 폰트가 없습니다. (이 창은 Sync 직후 자동으로 뜨거나 메뉴에서 수동 호출됩니다.)", MessageType.None);
            }
            else
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(180));
                foreach (var entry in _entries)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(entry.ToString(), GUILayout.Width(260));
                        _selections.TryGetValue(entry.Family, out var current);
                        var picked = (TMP_FontAsset)EditorGUILayout.ObjectField(current, typeof(TMP_FontAsset), false);
                        if (picked != current)
                            _selections[entry.Family] = picked;
                    }
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rescan", GUILayout.Width(100))) Refresh();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear Tracker", GUILayout.Width(120))) { FigmaMissingFontTracker.Clear(); Refresh(); }
                if (GUILayout.Button("Cancel", GUILayout.Width(100))) Close();
                using (new EditorGUI.DisabledScope(!_selections.Values.Any(v => v != null)))
                {
                    if (GUILayout.Button("Apply", GUILayout.Width(120), GUILayout.Height(24))) ApplySelections();
                }
            }

            if (!string.IsNullOrEmpty(_lastReport))
                EditorGUILayout.HelpBox(_lastReport, MessageType.None);
        }

        private void ApplySelections()
        {
            SessionState.SetBool(FigmaSyncWatcher.PostprocessRunningKey, true);
            try
            {
                var assignments = _selections
                    .Where(kv => kv.Value != null)
                    .ToDictionary(kv => kv.Key, kv => kv.Value, System.StringComparer.OrdinalIgnoreCase);
                if (assignments.Count == 0)
                {
                    _lastReport = "(선택된 폰트가 없습니다)";
                    return;
                }

                var roots = FigmaPostprocessPaths.PrefabSearchRoots
                    .Where(AssetDatabase.IsValidFolder)
                    .ToArray();
                if (roots.Length == 0)
                {
                    _lastReport = "임포트된 프리팹 폴더가 없습니다.";
                    return;
                }

                var guids = AssetDatabase.FindAssets("t:Prefab", roots);
                var prefabChanged = 0;
                var textsChanged = 0;
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var dirty = false;
                        foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(true))
                        {
                            var target = ResolveTarget(tmp, assignments);
                            if (target == null || tmp.font == target) continue;
                            tmp.font = target;
                            dirty = true;
                            textsChanged++;
                        }
                        if (dirty)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            prefabChanged++;
                        }
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }

                _lastReport = $"✔ 폰트 교체 완료 — 프리팹 {prefabChanged}개, TMP_Text {textsChanged}개. 적용된 family: {string.Join(", ", assignments.Keys)}";
                Debug.Log("[FigmaMissingFontWindow] " + _lastReport);
                FigmaMissingFontTracker.Clear();
                _selections.Clear();
                Refresh();
            }
            finally
            {
                SessionState.SetBool(FigmaSyncWatcher.PostprocessRunningKey, false);
            }
        }

        // 임포트된 TMP_Text 의 font.name 이 어떤 누락 family 와 매칭되는지 결정한다.
        // FontManager.GetClosestFont 의 정규화 방식과 동일하게 hyphen/sdf/regular/bold/italic/space 를 제거 후
        // family 의 정규화 형태와 부분 일치하면 매칭.
        private static TMP_FontAsset ResolveTarget(TMP_Text tmp, Dictionary<string, TMP_FontAsset> assignments)
        {
            if (tmp == null || tmp.font == null) return null;
            var stripped = StripFontDetailsFromName(tmp.font.name);
            foreach (var kv in assignments)
            {
                var familyNormalized = kv.Key.ToLowerInvariant().Replace(" ", string.Empty);
                if (stripped.IndexOf(familyNormalized, System.StringComparison.Ordinal) >= 0)
                    return kv.Value;
            }
            return null;
        }

        private static string StripFontDetailsFromName(string fontName)
        {
            var lower = fontName.ToLowerInvariant();
            var hyphen = lower.IndexOf('-');
            if (hyphen > -1) lower = lower.Substring(0, hyphen);
            foreach (var w in new[] { "sdf", "regular", "bold", "italic", " " })
                lower = lower.Replace(w, string.Empty);
            return lower;
        }
    }
}
