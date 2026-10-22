using System.Reflection;
using NUnit.Framework;
using Poker.BattlePresentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

        private static void Invoke(SingleBattleHud target, string method) =>
            typeof(SingleBattleHud).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
