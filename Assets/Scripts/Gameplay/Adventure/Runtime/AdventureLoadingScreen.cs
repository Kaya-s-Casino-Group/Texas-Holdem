using TMPro;
using UIFoundation;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.Adventure
{
    /// <summary>Lightweight startup cover. Animation keeps running when game time is paused.</summary>
    public sealed class AdventureLoadingScreen : MonoBehaviour
    {
        private readonly RectTransform[] dots = new RectTransform[3];
        private float elapsed;

        public static AdventureLoadingScreen Create(Transform owner, TMP_FontAsset font)
        {
            var root = new GameObject("Adventure Loading", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AdventureLoadingScreen));
            root.transform.SetParent(owner, false);
            root.layer = 5;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            var cover = Node(root.transform, "Cover");
            cover.anchorMin = Vector2.zero; cover.anchorMax = Vector2.one;
            cover.offsetMin = cover.offsetMax = Vector2.zero;
            var image = cover.gameObject.AddComponent<Image>();
            image.color = new Color32(16, 14, 22, 255);
            image.raycastTarget = true;
            var safe = ScreenSafeArea.Ensure(canvas, fullBleed: cover).Content;
            var indicator = Node(safe, "Loading Indicator");
            indicator.anchorMin = indicator.anchorMax = indicator.pivot = new Vector2(1, 0);
            indicator.anchoredPosition = new Vector2(-52, 40);
            indicator.sizeDelta = new Vector2(190, 48);
            var caption = Node(indicator, "加载中");
            caption.anchorMin = Vector2.zero; caption.anchorMax = Vector2.one;
            caption.offsetMin = Vector2.zero; caption.offsetMax = new Vector2(-50, 0);
            var label = caption.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = "加载中"; label.fontSize = 28;
            label.alignment = TextAlignmentOptions.MidlineRight;
            label.color = new Color32(219, 183, 112, 255); label.raycastTarget = false;
            var screen = root.GetComponent<AdventureLoadingScreen>();
            for (int i = 0; i < screen.dots.Length; i++)
            {
                var dot = Node(indicator, "Dot " + (i + 1));
                dot.anchorMin = dot.anchorMax = new Vector2(1, 0);
                dot.sizeDelta = new Vector2(5, 5);
                var graphic = dot.gameObject.AddComponent<Image>();
                graphic.color = label.color; graphic.raycastTarget = false;
                screen.dots[i] = dot;
            }
            screen.AnimateDots();
            return screen;
        }

        private static RectTransform Node(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.gameObject.layer = 5;
            return rect;
        }

        private void Update() { elapsed += Time.unscaledDeltaTime; AnimateDots(); }
        private void AnimateDots()
        {
            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;
                float phase = Mathf.Repeat(elapsed / .9f - i * .14f, 1);
                float jump = phase < .45f ? Mathf.Sin(phase / .45f * Mathf.PI) * 8 : 0;
                dots[i].anchoredPosition = new Vector2(-36 + i * 14, 14 + jump);
            }
        }
    }
}
