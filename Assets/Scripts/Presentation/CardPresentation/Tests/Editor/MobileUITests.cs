using System.Linq;
using CardPresentation.Editor;
using NUnit.Framework;
using UIFoundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    public sealed class MobileUITests
    {
        [Test]
        public void SafeAreaUsesRenderDisplayPixelsWhenScreenMetricsComeFromADifferentOrientation()
        {
            var viewport = new Rect(0, 0, 2532, 1170);
            var portrait = new Vector2(541, 2548);
            Assert.AreEqual(viewport, ScreenSafeArea.CalculateDisplaySafeRect(viewport, new Rect(Vector2.zero, portrait), portrait, viewport.size, 0));
            // Preserve proportional device cutouts, then intersect the right half camera viewport.
            var splitViewport = new Rect(1000, 0, 1000, 1000);
            var safe = new Rect(50, 100, 900, 1800);
            Assert.AreEqual(new Rect(1000, 50, 900, 900), ScreenSafeArea.CalculateDisplaySafeRect(
                splitViewport, safe, new Vector2(1000, 2000), new Vector2(2000, 1000), 0));
        }

        [Test]
        public void OffscreenCameraDoesNotApplyDeviceScreenInsets()
        {
            var root = new GameObject("Offscreen safe area");
            var target = new RenderTexture(640, 360, 0);
            try
            {
                var camera = root.AddComponent<Camera>(); camera.targetTexture = target;
                camera.rect = new Rect(.25f, .25f, .5f, .5f);
                Assert.AreEqual(camera.pixelRect, ScreenSafeArea.CameraRect(camera));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
        }

        [Test] public void UIAlwaysUsesInputSystemAndPreservesBindingsWhenDevicesChange()
        {
            var root = new GameObject("Input provider test", typeof(EventSystem), typeof(StandaloneInputModule));
            Mouse mouse = null;
            try
            {
                var router = root.AddComponent<CardUIInput>();
                router.RefreshModules();
                Assert.IsFalse(root.GetComponent<StandaloneInputModule>().enabled);
                Assert.IsTrue(root.GetComponent<InputSystemUIInputModule>().enabled);
                mouse = InputSystem.AddDevice<Mouse>();
                router.RefreshModules();
                Assert.IsFalse(root.GetComponent<StandaloneInputModule>().enabled);
                var module = root.GetComponent<InputSystemUIInputModule>();
                Assert.IsTrue(module.enabled);
                Assert.NotNull(module.point);
                Assert.NotNull(module.leftClick);
                var actions = module.actionsAsset;
                router.RefreshModules();
                Assert.AreSame(actions, module.actionsAsset, "Keep existing action bindings.");
                InputSystem.RemoveDevice(mouse); mouse = null;
                router.RefreshModules();
                Assert.IsFalse(root.GetComponent<StandaloneInputModule>().enabled);
                Assert.IsTrue(module.enabled);
            }
            finally
            {
                if (mouse != null) InputSystem.RemoveDevice(mouse);
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(59, 0, 726, 369)]
        [TestCase(0, 34, 844, 356)]
        public void SafeRectKeepsNotchAndHomeIndicatorInsetsWithExtraCornerPadding(float x, float y, float w, float h)
        {
            var viewport = new Rect(0, 0, 844, 390);
            var safe = new Rect(x, y, w, h);
            var result = ScreenSafeArea.CalculateSafeRect(viewport, safe, 16);
            Assert.AreEqual(safe.xMin+16, result.xMin);
            Assert.AreEqual(safe.xMax-16, result.xMax);
            Assert.AreEqual(safe.yMin+16, result.yMin);
            Assert.AreEqual(safe.yMax-16, result.yMax);
        }

        [Test] public void SafeRectIntersectsSplitViewportAndRepeatedSetupPreservesFullBleedBackdrop()
        {
            Assert.AreEqual(new Rect(216, 16, 168, 268), ScreenSafeArea.CalculateSafeRect(
                new Rect(200, 0, 200, 300), new Rect(30, 0, 570, 300), 16));
            var root = new GameObject("Safe test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var backdrop = new GameObject("Backdrop", typeof(RectTransform)).transform;
                backdrop.SetParent(root.transform, false);
                var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
                panel.SetParent(root.transform, false); panel.sizeDelta = new Vector2(5000, 5000);
                var layout = ScreenSafeArea.Ensure(canvas, panel, backdrop);
                Canvas.ForceUpdateCanvases(); layout.Refresh();
                Assert.AreSame(root.transform, backdrop.parent);
                Assert.AreSame(layout.Content, panel.parent);
                Assert.LessOrEqual(panel.rect.width*panel.localScale.x, layout.Content.rect.width+.01f);
                Assert.LessOrEqual(panel.rect.height*panel.localScale.y, layout.Content.rect.height+.01f);
                Assert.AreSame(layout, ScreenSafeArea.Ensure(canvas, panel, backdrop));
                Assert.AreEqual(2, root.transform.childCount);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void OpeningDeckUsesOverlayAboveHudAndTouchInspectionLastsOnlyWhileHeld()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DeckOverviewSetup.PrefabPath));
            var hud = new GameObject("Overlay HUD", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hudCanvas = hud.GetComponent<Canvas>(); hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay; hudCanvas.sortingOrder = 900;
                var view = root.GetComponent<DeckOverviewView>();
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
                view.Close(false); view.Show(false); Canvas.ForceUpdateCanvases();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.Greater(canvas.sortingOrder, hudCanvas.sortingOrder);
                Assert.IsTrue(root.GetComponent<CanvasGroup>().blocksRaycasts);
                Assert.IsTrue(view.ContainsPointer(RectTransformUtility.WorldToScreenPoint(null, view.Frame.position)));
                var data = new DeckOverviewData(new[] {
                    new DeckOverviewCard(new CardVisualData("first", "spades_a"), PokerEngine.StandardDeck.Cards().First(c => c.Suit == PokerEngine.Suit.Spades && c.Rank == PokerEngine.Rank.Ace), true),
                    new DeckOverviewCard(new CardVisualData("second", "hearts_q"), PokerEngine.StandardDeck.Cards().First(c => c.Suit == PokerEngine.Suit.Hearts && c.Rank == PokerEngine.Rank.Queen), true) });
                view.Present(data);
                var first = view.Cards[0]; var second = view.Cards[1];
                var touch = new ExtendedPointerEventData(EventSystem.current) { pointerType = UIPointerType.Touch };
                first.OnPointerEnter(touch); first.OnPointerDown(touch); Assert.IsTrue(first.Interaction.IsHovered);
                first.OnPointerUp(touch); Assert.IsFalse(first.Interaction.IsHovered);
                second.OnPointerEnter(touch); second.OnPointerDown(touch);
                Assert.IsFalse(first.Interaction.IsHovered); Assert.IsTrue(second.Interaction.IsHovered);
                second.OnPointerExit(touch); Assert.IsFalse(second.Interaction.IsHovered);
                var mouse = new ExtendedPointerEventData(EventSystem.current) { pointerType = UIPointerType.MouseOrPen };
                first.OnPointerEnter(mouse); first.OnPointerUp(mouse); Assert.IsTrue(first.Interaction.IsHovered);
                first.OnPointerExit(mouse); Assert.IsFalse(first.Interaction.IsHovered);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(hud); }
        }

        [Test] public void TouchHoldPreviewsDeckAndOnlyReleaseClickOpensOnce()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DrawPileSetup.PrefabPath));
            try
            {
                var view = root.GetComponent<DrawPileView>(); int clicks = 0; view.Clicked += () => clicks++;
                var touch = new ExtendedPointerEventData(EventSystem.current) { pointerType = UIPointerType.Touch };
                view.OnPointerEnter(touch); view.OnPointerDown(touch); Assert.IsTrue(view.IsHovered); Assert.AreEqual(0, clicks);
                view.OnPointerUp(touch); Assert.IsFalse(view.IsHovered); Assert.AreEqual(0, clicks);
                view.OnPointerClick(touch); Assert.AreEqual(1, clicks);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
