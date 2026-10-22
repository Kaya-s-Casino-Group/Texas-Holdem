using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Original desk_UI artwork, shared by the single-chapter battle and journey pages.</summary>
    public static class DeskUISkin
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static Sprite Get(string name)
        {
            switch (name)
            {
                case "Button": case "MenuButton": case "SortButton": name = "ContentButton"; break;
                case "Panel": case "RoundFrame": case "StatFrame": case "RelicTray":
                case "HealthFrame": case "BossHealthFrame": case "DeckFrame":
                case "ShopCard": case "ShopServiceCard": name = "ContentPanel"; break;
            }
            if (sprites.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>("DeskUI/" + name);
            if (sprite != null) sprites[name] = sprite;
            return sprite;
        }
        public static Image Decor(Transform parent, string name, Rect area, string sprite)
        {
            var child = parent.Find(name);
            var image = child != null ? child.GetComponent<Image>() : null;
            if (image == null) image = RitualSkin.Region(parent, name, area).gameObject.AddComponent<Image>();
            Place(image.transform, area); image.sprite = Get(sprite);
            ConfigureFrame(image);
            image.color = Color.white; image.raycastTarget = false;
            return image;
        }
        public static void Panel(Image image, string sprite = "Panel")
        {
            if (image == null) return;
            var art = Get(sprite); if (art == null) art = Get("Panel");
            image.sprite = art; image.color = Color.white;
            ConfigureFrame(image);
            FitRect(image.rectTransform, art);
            foreach (var name in new[] { "Painted Surface", "Gold Fillet", "Ritual Surface" })
            { var old = image.transform.Find(name); if (old != null) old.gameObject.SetActive(false); }
            foreach (var outline in image.GetComponents<Outline>()) outline.enabled = false;
        }
        public static void Button(Button button, string sprite = "Button")
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image == null) image = button.gameObject.AddComponent<Image>();
            Panel(image, sprite); image.raycastTarget = true; button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.1f, .92f);
            colors.pressedColor = new Color(.72f, .65f, .55f);
            colors.disabledColor = new Color(.45f, .42f, .40f, .72f); colors.fadeDuration = .10f;
            button.colors = colors;
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.color = RitualSkin.Ivory; label.enableAutoSizing = true;
                label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis;
            }
        }
        private static bool IsContentFrame(Sprite sprite) => sprite != null &&
            (sprite.name == "ContentButton" || sprite.name == "ContentPanel");
        private static void ConfigureFrame(Image image)
        {
            bool fitted = IsContentFrame(image.sprite);
            image.type = fitted ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !fitted;
            // Fixed-size corners; only plain straight edges and the quiet center expand.
            image.pixelsPerUnitMultiplier = fitted ? (image.sprite.name == "ContentButton" ? 300f / 18f : 180f / 20f) : 1;
        }
        /// <summary>Contain the complete authored frame; child text follows the fitted artwork bounds.</summary>
        public static void FitRect(RectTransform rect, Sprite sprite)
        {
            if (rect == null || sprite == null || IsContentFrame(sprite) || sprite.rect.height <= 0) return;
            rect.ForceUpdateRectTransforms();
            var size = rect.rect.size;
            if (!(size.x > 0) || !(size.y > 0) || float.IsInfinity(size.x) || float.IsInfinity(size.y)) return;
            float aspect = sprite.rect.width / sprite.rect.height;
            var fitted = size.x / size.y > aspect ? new Vector2(size.y * aspect, size.y) : new Vector2(size.x, size.x / aspect);
            var inset = (size - fitted) * .5f;
            rect.offsetMin += inset; rect.offsetMax -= inset;
        }
        public static void Place(Transform target, Rect area)
        {
            if (!(target is RectTransform rect)) return;
            rect.anchorMin = area.min; rect.anchorMax = area.max;
            // Assign directly: offset setters retain NaN if an inactive Canvas once had a zero-sized parent.
            rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
    }
}
