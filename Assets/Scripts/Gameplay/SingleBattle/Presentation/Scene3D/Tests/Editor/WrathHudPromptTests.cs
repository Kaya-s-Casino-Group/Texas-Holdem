using System;
using System.Collections.Generic;
using System.Reflection;
using global::BattlePresentation;
using NUnit.Framework;
using Poker.BattlePresentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.SingleBattle.Tests
{
    public sealed class WrathHudPromptTests
    {
        [TestCase(WrathIntentIcon.Attack)]
        [TestCase(WrathIntentIcon.Defense)]
        [TestCase(WrathIntentIcon.Charge)]
        public void PlaceholderIconProducesDrawableGeometryWithoutAnArtAsset(WrathIntentIcon kind)
        {
            var root = new GameObject("Icon", typeof(RectTransform), typeof(WrathIntentPlaceholderGraphic));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(40, 40);
                var graphic = root.GetComponent<WrathIntentPlaceholderGraphic>(); graphic.Show(kind);
                using (var mesh = new VertexHelper())
                {
                    typeof(WrathIntentPlaceholderGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { mesh });
                    Assert.Greater(mesh.currentVertCount, 0); Assert.Greater(mesh.currentIndexCount, 0);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void RulesRemainReadableAndAllEightIconSlotsFitThe720pHud()
        {
            var root = new GameObject("Wrath HUD", typeof(RectTransform), typeof(Canvas), typeof(SingleBattleHud));
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
                var template = new GameObject("Text Template", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                template.transform.SetParent(root.transform, false);
                var hud = root.GetComponent<SingleBattleHud>(); var serialized = new SerializedObject(hud);
                serialized.FindProperty("message").objectReferenceValue = template; serialized.ApplyModifiedPropertiesWithoutUndo();
                Invoke(hud, "EnsureWrathHud"); Invoke(hud, "EnsureWrathPromptViews");
                Canvas.ForceUpdateCanvases();
                var panel = root.transform.Find("Wrath Boss");
                for (int i = 0; i < 8; i++)
                {
                    var slot = panel.Find("Regular Intent " + i).GetComponent<RectTransform>();
                    Assert.Greater(slot.rect.height, 0); Assert.Greater(slot.rect.width, 0);
                    Assert.IsNotNull(slot.Find("Placeholder").GetComponent<WrathIntentPlaceholderGraphic>());
                }
                var warning = panel.Find("Ultimate Warning").GetComponent<RectTransform>();
                Assert.Greater(warning.rect.height, 0);
                var button = panel.Find("Wrath Rules").GetComponent<Button>(); button.onClick.Invoke();
                var opening = root.transform.Find("Wrath Opening Warning");
                Assert.IsTrue(opening.gameObject.activeSelf);
                var text = opening.GetComponentInChildren<TMP_Text>();
                Assert.AreEqual(WrathPromptText.Opening, text.text); Assert.Greater(text.color.r, text.color.g * 2);
                button.onClick.Invoke(); Assert.IsFalse(opening.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdventureWarningsFitCompleteLargeRedTextInsideAPaddedBlackBannerAt720p(bool ultimate)
        {
            using (var fixture = new AdventurePromptFixture())
            {
                fixture.Present(ultimate);
                if (!ultimate) fixture.OpeningFor(4);
                var text = fixture.Text(ultimate);
                text.ForceMeshUpdate();
                var background = fixture.Background(ultimate);
                Assert.IsNotNull(background, "Both warnings need a dark backdrop, including the ultimate warning.");
                Assert.IsTrue(text.gameObject.activeInHierarchy);
                Assert.GreaterOrEqual(text.fontSize, 30, "The warning must remain substantially larger than the former 24px text.");
                Assert.IsFalse(text.isTextOverflowing, "The complete warning must fit at 1280x720: " + text.text);
                Assert.AreEqual(text.text.Length, text.textInfo.characterCount, "No warning characters may be truncated or replaced by ellipsis.");
                Assert.IsTrue(text.textInfo.characterInfo[text.textInfo.characterCount - 1].isVisible, "The warning's final punctuation must be rendered.");
                if (ultimate) StringAssert.Contains("将于下一回合释放大招", text.text);
                Assert.Greater(text.color.r, text.color.g * 2);
                Assert.Less(Mathf.Max(background.color.r, background.color.g, background.color.b), .15f);
                Assert.Greater(background.color.a, .9f);
                Assert.GreaterOrEqual(background.rectTransform.rect.height, 90, "The larger text needs approximately 1.5x the original banner height.");
                var outer = RectInHud(fixture.Hud, background.rectTransform);
                var inner = RectInHud(fixture.Hud, text.rectTransform);
                Assert.GreaterOrEqual(inner.xMin - outer.xMin, 16);
                Assert.GreaterOrEqual(outer.xMax - inner.xMax, 16);
                Assert.GreaterOrEqual(inner.yMin - outer.yMin, 7);
                Assert.GreaterOrEqual(outer.yMax - inner.yMax, 7);
            }
        }

        [Test]
        public void AdventureUltimateUsesTheSameBannerPositionAndStyleAsTheOpening()
        {
            using (var fixture = new AdventurePromptFixture())
            {
                fixture.Present(false); fixture.OpeningFor(4);
                var opening = fixture.Background(false);
                var openingBounds = RectInHud(fixture.Hud, opening.rectTransform);
                var openingColor = opening.color;
                var openingText = fixture.Text(false);
                fixture.OpeningFor(-1); fixture.Present(true);
                var ultimate = fixture.Background(true);
                Assert.IsNotNull(ultimate, "The ultimate must use the same black-backed presentation.");
                Assert.AreEqual(openingBounds, RectInHud(fixture.Hud, ultimate.rectTransform));
                Assert.AreEqual(openingColor, ultimate.color);
                Assert.AreEqual(openingText.color, fixture.Text(true).color);
                Assert.AreEqual(openingText.fontSizeMax, fixture.Text(true).fontSizeMax);
            }
        }

        [Test]
        public void AdventureOpeningFinishesBeforePendingUltimateAppearsAndReleaseHidesItImmediately()
        {
            using (var fixture = new AdventurePromptFixture())
            {
                fixture.Present(false); fixture.OpeningFor(4); fixture.Present(true);
                Assert.IsTrue(fixture.Text(false).gameObject.activeInHierarchy);
                Assert.IsFalse(fixture.Text(true).gameObject.activeInHierarchy, "An active opening owns the shared warning position.");
                fixture.OpeningFor(.01f);
                Assert.IsFalse(fixture.Text(true).gameObject.activeInHierarchy, "The ultimate must wait through the last part of the opening.");
                fixture.OpeningFor(-1);
                Assert.IsFalse(fixture.Text(false).gameObject.activeInHierarchy);
                Assert.IsTrue(fixture.Text(true).gameObject.activeInHierarchy);
                fixture.Present(true);
                Assert.IsTrue(fixture.Text(true).gameObject.activeInHierarchy, "Refreshing an unchanged warning must keep it visible.");
                fixture.Present(false);
                Assert.IsFalse(fixture.Text(true).gameObject.activeInHierarchy, "The first snapshot after cancellation or release must hide the warning.");
                fixture.Present(true); fixture.Present(true, finished: true);
                Assert.IsFalse(fixture.Text(true).gameObject.activeInHierarchy, "Finishing the battle also clears the warning.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdventureWarningAllowsRaycastsToReachTheBattleButtonBehindIt(bool ultimate)
        {
            using (var fixture = new AdventurePromptFixture())
            {
                fixture.Present(ultimate);
                if (!ultimate) fixture.OpeningFor(4);
                var warning = fixture.Text(ultimate);
                warning.ForceMeshUpdate();
                // GraphicRaycaster excludes unrendered graphics (depth == -1). Force the
                // real world-space canvas through its camera before testing pointer hits.
                fixture.Camera.Render();
                Assert.GreaterOrEqual(fixture.BattleButton.GetComponent<Image>().depth, 0,
                    "The battle control must have been rendered before raycasting.");
                var pointer = new PointerEventData(fixture.Events)
                { position = fixture.Camera.WorldToScreenPoint(warning.rectTransform.position) };
                var hits = new List<RaycastResult>();
                fixture.Hud.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
                Assert.IsNotEmpty(hits, "The fixture must exercise an actual hit on the battle control.");
                Assert.AreSame(fixture.BattleButton.gameObject, hits[0].gameObject, "Warning text and its backdrop must not intercept battle clicks.");
            }
        }

        private sealed class AdventurePromptFixture : IDisposable
        {
            private readonly GameObject root, hostRoot, cameraRoot, eventRoot;
            private readonly TMP_FontAsset font;
            private readonly RenderTexture target;
            public readonly SingleBattleHud Hud;
            public readonly Camera Camera;
            public readonly EventSystem Events;
            public readonly Button BattleButton;

            public AdventurePromptFixture()
            {
                hostRoot = new GameObject("Prompt Host"); hostRoot.SetActive(false);
                var host = hostRoot.AddComponent<SingleBattleSceneAdapter>();
                SetField(host, "externalCommands", true);
                root = new GameObject("Adventure Prompt HUD", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(SingleBattleHud));
                Hud = root.GetComponent<SingleBattleHud>();
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1280, 720);
                cameraRoot = new GameObject("Prompt Camera", typeof(Camera)); Camera = cameraRoot.GetComponent<Camera>();
                Camera.enabled = false; Camera.orthographic = true; Camera.orthographicSize = 360;
                Camera.transform.position = new Vector3(0, 0, -10);
                target = new RenderTexture(1280, 720, 0); Camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                eventRoot = new GameObject("Prompt Events", typeof(EventSystem)); Events = eventRoot.GetComponent<EventSystem>();
                BattleButton = new GameObject("Battle Button", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
                BattleButton.transform.SetParent(root.transform, false);
                var buttonRect = BattleButton.GetComponent<RectTransform>();
                buttonRect.anchorMin = Vector2.zero; buttonRect.anchorMax = Vector2.one; buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
                var template = new GameObject("Text Template", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                template.transform.SetParent(root.transform, false);
                // A transient font keeps glyph generation out of the user's existing dynamic font asset.
                font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansCJK/NotoSansCJKsc-Regular.otf"));
                template.font = font; template.gameObject.SetActive(false);
                SetField(Hud, "host", host); SetField(Hud, "message", template);
            }

            public TMP_Text Text(bool ultimate) => (TMP_Text)typeof(SingleBattleHud)
                .GetField(ultimate ? "wrathUltimatePrompt" : "wrathOpeningText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Hud);
            public Image Background(bool ultimate) => Text(ultimate).GetComponentInParent<Image>(true);
            public void Present(bool warning, bool finished = false)
            {
                var snapshot = new BattleDisplaySnapshot("prompt-battle", "player", 1, "player", 0, finished ? "Won" : null,
                    new[] { new UnitDisplayData("body", "暴怒", "enemy", 2000, 2000, 0, 0, 0) },
                    zones: null, resources: new[] { new ResourceDisplayData("body", "wrath.warning", warning ? 1 : 0) },
                    hasStarted: true, labels: new Dictionary<string, string> { ["wrath.body"] = "body" });
                Invoke(Hud, "RefreshWrathPrompts", snapshot); Canvas.ForceUpdateCanvases();
            }
            public void OpeningFor(float remaining)
            {
                // EditMode never commits one-shot prompts; drive only their animation clock here.
                Text(false).text = WrathPromptText.Opening;
                SetField(Hud, "wrathOpeningUntil", Time.unscaledTime + remaining);
                Invoke(Hud, "UpdateWrathPromptViews"); Canvas.ForceUpdateCanvases();
            }
            public void Dispose()
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(hostRoot);
                Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(eventRoot); Object.DestroyImmediate(target);
                foreach (var atlas in font.atlasTextures) if (atlas != null) Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(font.material); Object.DestroyImmediate(font);
            }
        }

        private static Rect RectInHud(SingleBattleHud hud, RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = hud.transform.InverseTransformPoint(corners[0]);
            var max = hud.transform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Invoke(SingleBattleHud target, string method, params object[] arguments) =>
            typeof(SingleBattleHud).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }
}
