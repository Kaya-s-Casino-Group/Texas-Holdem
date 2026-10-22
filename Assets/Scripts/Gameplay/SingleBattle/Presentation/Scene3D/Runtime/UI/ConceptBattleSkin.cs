using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Concept sprites are contained at their native aspect; none use sliced geometry.</summary>
    public static class ConceptBattleSkin
    {
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static readonly Color Paper = new Color32(237, 228, 209, 255);
        public static readonly Color Gold = new Color32(207, 183, 130, 255);
        public static Sprite Get(string key)
        {
            if (!sprites.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = Resources.Load<Sprite>("BattleConcept/" + key);
                if (sprite == null)
                {
                    var imported = Resources.LoadAll<Sprite>("BattleConcept/" + key);
                    if (imported.Length > 0) sprite = imported[0];
                }
                sprites[key] = sprite;
            }
            return sprite;
        }
        public static RectTransform Node(Transform parent, string name)
        {
            var rect = parent.Find(name) as RectTransform;
            if (rect == null) { rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.gameObject.layer = 5; }
            return rect;
        }
        public static void Place(Transform target, Rect topLeft)
            => DeskUISkin.Place(target, new Rect(topLeft.x, 1 - topLeft.yMax, topLeft.width, topLeft.height));
        public static void Fit(RectTransform rect, Sprite sprite)
        {
            if (rect == null || sprite == null) return;
            rect.ForceUpdateRectTransforms(); var size = rect.rect.size;
            if (size.x <= 0 || size.y <= 0 || float.IsInfinity(size.x) || float.IsInfinity(size.y)) return;
            float scale = Mathf.Min(size.x / sprite.rect.width, size.y / sprite.rect.height);
            var inset = (size - sprite.rect.size * scale) * .5f;
            rect.offsetMin += inset; rect.offsetMax -= inset;
        }
        public static void HideLegacy(Transform target)
        {
            if (target == null) return;
            foreach (var name in new[] { "Painted Surface", "Gold Fillet", "Ritual Surface", "Round Frame", "Action Frame", "Desk Deck Frame", "Desk Health Frame" })
            {
                var child = target.Find(name); if (child == null) continue;
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else child.gameObject.SetActive(false);
            }
            foreach (var outline in target.GetComponents<Outline>()) outline.enabled = false;
        }
        public static Image Surface(Transform target, string key, bool fit = true, bool raycast = false)
        {
            HideLegacy(target); var image = target.GetComponent<Image>() ?? target.gameObject.AddComponent<Image>();
            image.enabled = true; image.sprite = Get(key); image.color = Color.white;
            image.type = Image.Type.Simple; image.preserveAspect = true; image.raycastTarget = raycast;
            if (fit) Fit(image.rectTransform, image.sprite); return image;
        }
        public static Image Art(Transform parent, string name, string key, Rect area, bool fit = true)
        { var rect = Node(parent, name); Place(rect, area); return Surface(rect, key, fit); }
        public static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, Rect area, string value, int size, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rect = Node(parent, name); Place(rect, area);
            var label = rect.GetComponent<TMP_Text>() ?? rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.gameObject.SetActive(true); label.font = font; label.text = value; Format(label, size, alignment); return label;
        }
        public static void Format(TMP_Text label, int size, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            label.fontSize = label.fontSizeMax = size; label.fontSizeMin = Mathf.Max(10, size * .7f);
            label.enableAutoSizing = true; label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Overflow;
            label.richText = true; label.margin = Vector4.zero; label.alignment = alignment; label.color = Paper; label.raycastTarget = false;
        }
        public static void Button(Button button, Rect area, string key, int fontSize)
        {
            if (button == null) return;
            Place(button.transform, area); var image = Surface(button.transform, key, true, true); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.10f, 1.04f); colors.pressedColor = new Color(.75f, .72f, .68f);
            colors.disabledColor = new Color(.5f, .49f, .47f, .82f); colors.fadeDuration = .12f; button.colors = colors;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null) { label.gameObject.SetActive(true); Place(label.transform, new Rect(.12f, .12f, .76f, .76f)); Format(label, fontSize); }
        }
    }
}
