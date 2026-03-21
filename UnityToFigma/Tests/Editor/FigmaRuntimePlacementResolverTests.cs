using System;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;
using UnityToFigma.Editor.Utils;
using UnityToFigma.Runtime.UI;

namespace UnityToFigma.Editor.Tests
{
    public class FigmaRuntimePlacementResolverTests
    {
        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void GetEffectiveScreenParentName_TrimsAndFallsBackToDefault()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ScreenParentTransformName = "  CustomParent  ";
            Assert.That(FigmaRuntimePlacementResolver.GetEffectiveScreenParentName(s), Is.EqualTo("CustomParent"));

            s.ScreenParentTransformName = "   ";
            Assert.That(FigmaRuntimePlacementResolver.GetEffectiveScreenParentName(s),
                Is.EqualTo(UnityToFigmaImportSettingsDefaults.ScreenParentTransformName));
        }

        [Test]
        public void Resolve_NoCanvas_CreateMissingCanvasFalse_Fails()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.CreateMissingCanvas = false;

            var r = FigmaRuntimePlacementResolver.Resolve(s,
                () => throw new InvalidOperationException("Should not create canvas when none exists and flag is false."));

            Assert.That(r.Success, Is.False);
            Assert.That(string.IsNullOrEmpty(r.ErrorMessage), Is.False);
        }

        [Test]
        public void Resolve_NoCanvas_CreateMissingCanvasTrue_CreatesViaCallback()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.CreateMissingCanvas = true;

            var r = FigmaRuntimePlacementResolver.Resolve(s, () =>
            {
                var go = new GameObject("Canvas");
                var c = go.AddComponent<Canvas>();
                go.AddComponent<GraphicRaycaster>();
                return c;
            });

            Assert.That(r.Success, Is.True);
            Assert.That(r.Canvas, Is.Not.Null);
            Assert.That(r.Controller, Is.Not.Null);
            Assert.That(r.ScreenParent, Is.Not.Null);
            Assert.That(r.ScreenParent.name, Is.EqualTo(FigmaRuntimePlacementResolver.GetEffectiveScreenParentName(s)));
            Assert.That(r.Controller.ScreenParentTransform, Is.EqualTo(r.ScreenParent));
        }

        [Test]
        public void Resolve_ExistingCanvas_DoesNotInvokeCreateCallback()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.CreateMissingCanvas = true;

            var existing = NewCanvasHierarchy("MainCanvas");

            var r = FigmaRuntimePlacementResolver.Resolve(s,
                () => throw new InvalidOperationException("Canvas already exists; create callback must not run."));

            Assert.That(r.Success, Is.True);
            Assert.That(r.Canvas, Is.EqualTo(existing));
        }

        [Test]
        public void Resolve_MultipleCanvases_PicksDeterministicHierarchyPath()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.CreateMissingCanvas = true;

            NewCanvasHierarchy("ZRoot");
            var expected = NewCanvasHierarchy("ARoot");

            var r = FigmaRuntimePlacementResolver.Resolve(s,
                () => throw new InvalidOperationException("Should not create when canvases exist."));

            Assert.That(r.Success, Is.True);
            Assert.That(r.Canvas, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_ExistingNamedParentUnderController_ReusesIt()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ScreenParentTransformName = "Screens";
            s.CreateMissingCanvas = true;

            var canvasGo = new GameObject("Canvas");
            canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var controller = canvasGo.AddComponent<PrototypeFlowController>();
            var expected = UnityUiUtils.CreateRectTransform("Screens", controller.transform as RectTransform);

            var r = FigmaRuntimePlacementResolver.Resolve(s,
                () => throw new InvalidOperationException("Should not create canvas."));

            Assert.That(r.Success, Is.True);
            Assert.That(r.ScreenParent, Is.EqualTo(expected));
            Assert.That(controller.ScreenParentTransform, Is.EqualTo(expected));
        }

        [Test]
        public void Resolve_MultipleSameNamedParents_PicksLexicographicallyFirstPath()
        {
            var s = ScriptableObject.CreateInstance<UnityToFigmaSettings>();
            s.ScreenParentTransformName = "Screens";
            s.CreateMissingCanvas = true;

            var canvasGo = new GameObject("Canvas");
            canvasGo.AddComponent<Canvas>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var controller = canvasGo.AddComponent<PrototypeFlowController>();

            UnityUiUtils.CreateRectTransform("Screens", controller.transform as RectTransform);
            var a = new GameObject("A");
            a.transform.SetParent(controller.transform, false);
            var expected = UnityUiUtils.CreateRectTransform("Screens", a.transform as RectTransform);
            var b = new GameObject("B");
            b.transform.SetParent(controller.transform, false);
            UnityUiUtils.CreateRectTransform("Screens", b.transform as RectTransform);

            var r = FigmaRuntimePlacementResolver.Resolve(s,
                () => throw new InvalidOperationException("Should not create canvas."));

            Assert.That(r.Success, Is.True);
            Assert.That(r.ScreenParent, Is.EqualTo(expected));
        }

        static Canvas NewCanvasHierarchy(string rootName)
        {
            var go = new GameObject(rootName);
            var c = go.AddComponent<Canvas>();
            go.AddComponent<GraphicRaycaster>();
            return c;
        }
    }
}
