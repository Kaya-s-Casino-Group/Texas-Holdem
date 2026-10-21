using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public static class RitualSkin
    {
        public static readonly Color Ivory = new Color32(247, 234, 205, 255);
        public static readonly Color Gold = new Color32(220, 177, 97, 255);
        public static readonly Color Muted = new Color32(185, 170, 151, 255);
        public static readonly Color Ink = new Color32(24, 20, 29, 255);
        private static Texture2D panel, nodes;
        private static readonly Sprite[] symbols = new Sprite[6];
        // Tight artwork bounds in the original 1254px atlas (top-left coordinates).
        // Removing transparent cell padding makes every emblem readable without stretching it.
        private static readonly Rect[] symbolBounds = {
            new Rect(97, 85, 251, 528), new Rect(423, 151, 405, 438), new Rect(851, 242, 375, 323),
            new Rect(32, 677, 381, 477), new Rect(446, 669, 362, 495), new Rect(853, 688, 366, 456)
        };
        public static Texture2D PanelTexture => panel != null ? panel : panel = Resources.Load<Texture2D>("RitualUI/Panel");
        public static Sprite Symbol(int index)
        {
            if (symbols[index] != null) return symbols[index];
            if (nodes == null) nodes = Resources.Load<Texture2D>("RitualUI/Nodes");
            if (nodes == null) return null;
            var bounds = symbolBounds[index];
            float scale = nodes.width / 1254f;
            var uv = new Rect(bounds.x * scale, nodes.height - bounds.yMax * scale, bounds.width * scale, bounds.height * scale);
            return symbols[index] = Sprite.Create(nodes, uv, new Vector2(.5f, .5f), 100);
        }
        public static RectTransform Region(Transform parent, string name, Rect area)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = 5; Place(rect, area); return rect;
        }
        public static void Place(Transform value, Rect area)
        {
            var rect = (RectTransform)value; rect.anchorMin = area.min; rect.anchorMax = area.max;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
        public static RitualPicture Picture(Transform parent, string name, Rect area, Texture texture, bool cover = false)
        {
            var rect = Region(parent, name, area); var image = rect.gameObject.AddComponent<RitualPicture>();
            image.Configure(texture, cover); return image;
        }
        public static Image Icon(Transform parent, string name, Rect area, Sprite sprite)
        {
            var rect = Region(parent, name, area); var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false; return image;
        }
        public static void Panel(Image target, bool primary = false)
        {
            target.sprite = null; target.type = Image.Type.Simple; target.color = primary ? new Color32(89, 30, 40, 255) : Ink;
            var surface = target.transform.Find("Painted Surface");
            var picture = surface != null ? surface.GetComponent<RitualPicture>() :
                Picture(target.transform, "Painted Surface", new Rect(0, 0, 1, 1), PanelTexture, true);
            picture.transform.SetAsFirstSibling();
            picture.Configure(PanelTexture, true, new Rect(.08f, .10f, .84f, .80f));
            picture.color = primary ? new Color(1, .45f, .45f, .65f) : Color.white;
            var edge = target.GetComponent<Outline>() ?? target.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color32(133, 102, 63, 255); edge.effectDistance = new Vector2(1, -1);
            if (target.transform.Find("Gold Fillet") == null)
            {
                var line = Region(target.transform, "Gold Fillet", new Rect(.025f, .97f, .95f, .008f)).gameObject.AddComponent<Image>();
                line.color = new Color32(178, 133, 70, 255); line.raycastTarget = false;
            }
        }
        public static void Button(Button button, bool primary = false)
        {
            var image = button.GetComponent<Image>(); if (image == null) return;
            Panel(image, primary); image.raycastTarget = true; button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.15f, .85f); colors.pressedColor = new Color(.7f, .55f, .42f);
            colors.selectedColor = Color.white; colors.disabledColor = new Color(.42f, .40f, .38f); colors.fadeDuration = .12f; button.colors = colors;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.color = Ivory;
        }
    }
}
