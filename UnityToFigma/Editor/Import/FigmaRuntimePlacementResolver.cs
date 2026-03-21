using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityToFigma.Editor.Settings;
using UnityToFigma.Editor.Utils;
using UnityToFigma.Runtime.UI;

namespace UnityToFigma.Editor.Import
{
    /// <summary>
    /// Resolves Canvas and screen parent <see cref="RectTransform"/> for prototype import from
    /// <see cref="UnityToFigmaSettings"/> and the active scene, using stable ordering rules.
    /// </summary>
    public static class FigmaRuntimePlacementResolver
    {
        public sealed class Result
        {
            public bool Success;
            public string ErrorMessage;
            public Canvas Canvas;
            public PrototypeFlowController Controller;
            public RectTransform ScreenParent;
        }

        /// <summary>
        /// Effective name for the screen parent object (trimmed, or package default when unset).
        /// </summary>
        public static string GetEffectiveScreenParentName(UnityToFigmaSettings settings)
        {
            if (settings == null)
                return UnityToFigmaImportSettingsDefaults.ScreenParentTransformName;
            var n = settings.ScreenParentTransformName;
            if (string.IsNullOrWhiteSpace(n))
                return UnityToFigmaImportSettingsDefaults.ScreenParentTransformName;
            return n.Trim();
        }

        /// <summary>
        /// Finds or creates a Canvas, ensures <see cref="PrototypeFlowController"/> on that Canvas,
        /// then finds or creates the screen parent <see cref="RectTransform"/> under the controller.
        /// </summary>
        public static Result Resolve(UnityToFigmaSettings settings, Func<Canvas> createCanvasIfMissing)
        {
            var result = new Result();
            if (settings == null)
            {
                result.ErrorMessage = "UnityToFigma settings are missing.";
                return result;
            }

            if (createCanvasIfMissing == null)
                throw new ArgumentNullException(nameof(createCanvasIfMissing));

            var canvases = CollectCanvasesInActiveSceneSorted();
            Canvas canvas;
            if (canvases.Count == 0)
            {
                if (!settings.CreateMissingCanvas)
                {
                    result.ErrorMessage =
                        "No Canvas found in the active scene. Add a Canvas, or enable 'Create Missing Canvas' in UnityToFigma settings.";
                    return result;
                }

                canvas = createCanvasIfMissing();
            }
            else
            {
                canvas = canvases[0];
            }

            var controller = canvas.GetComponent<PrototypeFlowController>();
            if (controller == null)
                controller = canvas.gameObject.AddComponent<PrototypeFlowController>();

            var parentName = GetEffectiveScreenParentName(settings);
            var screenParent = FindOrCreateScreenParent(controller, parentName);

            controller.ScreenParentTransform = screenParent;

            result.Success = true;
            result.Canvas = canvas;
            result.Controller = controller;
            result.ScreenParent = screenParent;
            return result;
        }

        /// <summary>
        /// Collects every <see cref="Canvas"/> in the active scene, ordered by full hierarchy path (ordinal).
        /// </summary>
        internal static List<Canvas> CollectCanvasesInActiveSceneSorted()
        {
            var list = new List<Canvas>();
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                CollectCanvasesRecursive(root.transform, list);

            list.Sort((a, b) =>
                string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform)));
            return list;
        }

        static void CollectCanvasesRecursive(Transform t, List<Canvas> list)
        {
            var c = t.GetComponent<Canvas>();
            if (c != null)
                list.Add(c);
            for (var i = 0; i < t.childCount; i++)
                CollectCanvasesRecursive(t.GetChild(i), list);
        }

        internal static string GetHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }

        internal static RectTransform FindOrCreateScreenParent(PrototypeFlowController controller,
            string screenParentObjectName)
        {
            if (controller == null)
                throw new ArgumentNullException(nameof(controller));
            if (string.IsNullOrEmpty(screenParentObjectName))
                screenParentObjectName = UnityToFigmaImportSettingsDefaults.ScreenParentTransformName;

            var existing = controller.ScreenParentTransform;
            if (existing != null
                && existing.name == screenParentObjectName
                && existing.transform.IsChildOf(controller.transform))
                return existing;

            var matches = new List<RectTransform>();
            CollectNamedRectTransformsUnder(controller.transform, screenParentObjectName, matches);
            matches.Sort((a, b) =>
                string.CompareOrdinal(GetHierarchyPath(a.transform), GetHierarchyPath(b.transform)));

            if (matches.Count > 0)
                return matches[0];

            return UnityUiUtils.CreateRectTransform(screenParentObjectName,
                controller.transform as RectTransform);
        }

        static void CollectNamedRectTransformsUnder(Transform root, string objectName,
            List<RectTransform> matches)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == objectName)
                {
                    var rt = child.GetComponent<RectTransform>();
                    if (rt != null)
                        matches.Add(rt);
                }

                CollectNamedRectTransformsUnder(child, objectName, matches);
            }
        }
    }
}
