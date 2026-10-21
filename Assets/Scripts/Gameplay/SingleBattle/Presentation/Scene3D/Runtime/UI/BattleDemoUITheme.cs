using System.Linq;
using CardPresentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>BattleDemo-only skin. Never changes card art, actor renderers or rule data.</summary>
    public sealed class BattleDemoUITheme : MonoBehaviour
    {
        public static readonly Color Ivory = new Color32(242, 230, 203, 255);
        public static readonly Color Ink = new Color32(20, 17, 23, 250);

        public void StylePanel(Image image)
        {
            if (image == null) return;
            RitualSkin.Panel(image);
            image.raycastTarget = image.GetComponent<Button>() != null;
        }

        public void StyleButton(Button target, bool primary = false)
        {
            if (target == null) return;
            var graphic = target.GetComponent<Image>();
            if (graphic == null) return;
            StylePanel(graphic);
            RitualSkin.Button(target, primary);
            graphic.raycastTarget = true; target.targetGraphic = graphic;
            var colors = target.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.1f, .95f, 1);
            colors.pressedColor = new Color(.75f, .63f, .5f, 1);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.48f, .44f, .43f, .75f);
            colors.fadeDuration = .12f; target.colors = colors;
            foreach (var label in target.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = Ivory; label.alignment = TextAlignmentOptions.Center;
                label.margin = new Vector4(6, 0, 6, 0);
            }
        }

        public void Apply(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.color = text.text == "♥" || text.text == "♦" ? new Color32(223, 65, 76, 255) : Ivory;
                text.raycastTarget = false;
            }
            // Replace the old pixel panels locally; the shared prefab remains usable by other scenes.
            foreach (var pixel in root.GetComponentsInChildren<PixelPanelGraphic>(true))
            {
                pixel.enabled = false;
                var existing = pixel.transform.Find("Ritual Surface");
                var surface = existing != null ? existing.GetComponent<Image>() :
                    new GameObject("Ritual Surface", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                surface.transform.SetParent(pixel.transform, false); surface.transform.SetAsFirstSibling();
                var rect = surface.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                surface.gameObject.layer = pixel.gameObject.layer;
                if (pixel.name == "Shadow") surface.color = Color.clear;
                else if (pixel.name == "Frame") StylePanel(surface);
                else
                {
                    surface.sprite = null;
                    surface.color = pixel.name.StartsWith("Suit Row") ? new Color32(43, 29, 34, 255) : new Color32(30, 25, 29, 255);
                    surface.raycastTarget = false;
                }
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                string name = image.name;
                if (name == "Sun Emblem") { image.gameObject.SetActive(false); continue; }
                if (name == "Ritual Surface" || name.StartsWith("Icon") || name == "Sun Emblem" || name == "Fill") continue;
                if (image.GetComponent<Button>() != null) continue;
                if (name == "Backdrop")
                { image.color = new Color(.025f, .015f, .025f, .86f); continue; }
                if (name == "Panel")
                { StylePanel(image); continue; }
                if (name == "Frame" && image.transform.Find("Panel") != null)
                { image.color = Color.clear; continue; }
                if (name.StartsWith("Rank Token") || name.StartsWith("Count Row") || name == "Deck Header")
                { image.sprite = null; image.color = new Color32(42, 29, 34, 255); continue; }
                if (new[] { "Turn", "Player", "Enemy", "Enemy Intent", "Frame", "StatisticsPanel", "Actions",
                    "Chips Box", "Multiplier Box", "Turn Flow", "Rules Feedback", "Result" }.Contains(name))
                    StylePanel(image);
            }
            foreach (var target in root.GetComponentsInChildren<Button>(true))
                StyleButton(target, target.name.Contains("Play") || target.name.Contains("Discard") ||
                    target.name == "Return" || target.name.Contains("Result"));
        }
    }
}
