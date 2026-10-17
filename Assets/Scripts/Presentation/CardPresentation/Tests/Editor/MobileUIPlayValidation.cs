#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using CardInteraction;
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
    public static class MobileUIPlayValidation
    {
        [MenuItem("工具/卡牌表现/输入诊断/原生输入")]
        public static void DiagnoseNativeInput()
        {
            Directory.CreateDirectory("Library/MobileUIValidation");
            File.WriteAllText("Library/MobileUIValidation/native-input.txt", "Unity=" + Application.unityVersion +
                "\neditorStarted=" + System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime().ToString("O") +
                "\nlog=" + Application.consoleLogPath +
                "\nbatch=" + Application.isBatchMode +
                "\ndevices=" + string.Join(";", InputSystem.devices.Select(d => d + " native=" + d.native + " enabled=" + d.enabled)) +
                "\nunsupported=" + string.Join(";", InputSystem.GetUnsupportedDevices()) +
                "\nsupported=" + string.Join(";", InputSystem.settings.supportedDevices) +
                "\nupdateMode=" + InputSystem.settings.updateMode + "\nmetrics=" + InputSystem.metrics.totalEventCount);

        }

        [MenuItem("工具/卡牌表现/输入诊断/验证运行时移动端界面")]
        public static void Validate()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
            new GameObject("Mobile UI Validation (Temporary)").AddComponent<MobileUIValidationTick>().Run = ValidateInPlayerLoop();
        }
        private static IEnumerator ValidateInPlayerLoop()
        {
            var pile = Object.FindObjectsOfType<DrawPilePresenter>().First(item => item.Overview != null);
            var view = pile.Overview.View;
            var system = EventSystem.current;
            Assert.IsTrue(system.GetComponent<InputSystemUIInputModule>()?.isActiveAndEnabled ?? false);
            Assert.IsFalse(system.GetComponents<StandaloneInputModule>().Any(item => item.isActiveAndEnabled));
            Assert.IsTrue(InputSystem.devices.Any(device => device.native && device is Mouse && device.enabled),
                "Desktop validation requires a real native mouse. Restart Unity after changing Active Input Handling.");
            foreach (var panel in Object.FindObjectsOfType<HandActionPanelView>()) Assert.NotNull(panel.GetComponent<ScreenSafeArea>());
            Assert.NotNull(pile.View.GetComponent<ScreenSafeArea>());
            DeckOverviewPlayValidation.ValidateMotion();
            pile.SetPinned(true); view.AdvanceAnimation(1);
            // GraphicRaycaster needs the rendered canvas depth after reactivating the modal.
            yield return null;
            Canvas.ForceUpdateCanvases();
            var canvas = view.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.NotNull(view.GetComponent<ScreenSafeArea>());
            var safe = ScreenSafeArea.ScreenRect(canvas);
            var corners = new Vector3[4]; view.Frame.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(null, corner);
                Assert.That(point.x, Is.InRange(safe.xMin-.1f, safe.xMax+.1f));
                Assert.That(point.y, Is.InRange(safe.yMin-.1f, safe.yMax+.1f));
            }
            var touch = new ExtendedPointerEventData(system) { pointerType = UIPointerType.Touch };
            var card = view.Cards.First(item => item.Card.FaceUp);
            card.OnPointerEnter(touch); card.OnPointerDown(touch);
            Assert.IsTrue(card.Interaction.IsHovered);
            var tooltip = Object.FindObjectsOfType<CardHoverPresenter>().First(item => item.Sources.Contains(pile.Overview));
            tooltip.RefreshPresentation(); tooltip.AdvanceAnimation(1);
            Assert.IsTrue(tooltip.IsVisible);
            Assert.Greater(tooltip.Instance.GetComponent<Canvas>().sortingOrder, canvas.sortingOrder);
            card.OnPointerUp(touch); Assert.IsFalse(card.Interaction.IsHovered);
            card.OnPointerEnter(touch); card.OnPointerDown(touch); // Capture the held preview.
            var hits = new System.Collections.Generic.List<RaycastResult>();
            system.RaycastAll(new PointerEventData(system) { position = new Vector2(canvas.pixelRect.xMax-2, canvas.pixelRect.yMax-2) }, hits);
            Directory.CreateDirectory("Library/MobileUIValidation");
            Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(view.transform),
                "Backdrop must intercept the full screen. Screen=" + Screen.width + "x" + Screen.height +
                "; Canvas=" + canvas.pixelRect + "; Hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name)));
            File.WriteAllText("Library/MobileUIValidation/result.json", "{\"status\":\"Passed\",\"inputSystem\":true,\"safeArea\":true,\"modal\":true,\"touchInspection\":true}");
            ScreenCapture.CaptureScreenshot("Library/MobileUIValidation/overview.png");
            Debug.Log("Mobile UI validation passed: native Input System mouse, safe area, overlay input shield, touch inspection and tooltip.");
        }
    }
    public sealed class MobileUIValidationTick : MonoBehaviour
    {
        public IEnumerator Run;
        private IEnumerator Start()
        {
            try { yield return Run; }
            finally { Destroy(gameObject); }
        }
    }
}
#endif
