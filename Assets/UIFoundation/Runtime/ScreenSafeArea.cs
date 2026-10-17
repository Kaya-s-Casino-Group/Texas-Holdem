using UnityEngine;

namespace UIFoundation
{
    /// <summary>Insets screen UI while leaving full-bleed backgrounds on the original canvas.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Canvas)), DefaultExecutionOrder(50)]
    public sealed class ScreenSafeArea : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform content, fittedPanel;
        private Vector3 panelScale;
        public RectTransform Content => content;
        public const float MobilePadding = 16;

        public static ScreenSafeArea Ensure(Canvas owner, RectTransform fitPanel = null, Transform fullBleed = null)
        {
            if (owner == null || owner.renderMode == RenderMode.WorldSpace) return null;
            var layout = owner.GetComponent<ScreenSafeArea>();
            if (layout != null && layout.content != null) return layout;
            if (layout == null) layout = owner.gameObject.AddComponent<ScreenSafeArea>();
            layout.canvas = owner; layout.fittedPanel = fitPanel;
            if (fitPanel != null) layout.panelScale = fitPanel.localScale;
            var children = new Transform[owner.transform.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = owner.transform.GetChild(i);
            layout.content = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            layout.content.gameObject.layer = owner.gameObject.layer;
            layout.content.SetParent(owner.transform, false);
            layout.content.anchorMin = Vector2.zero; layout.content.anchorMax = Vector2.one;
            layout.content.offsetMin = layout.content.offsetMax = Vector2.zero;
            foreach (var child in children)
                if (child != fullBleed) child.SetParent(layout.content, false);
            layout.Refresh();
            return layout;
        }

        // Screen.safeArea is in physical screen pixels; intersect with the canvas camera's viewport.
        public static Rect CalculateSafeRect(Rect viewport, Rect safeArea, float padding)
        {
            float left = Mathf.Max(viewport.xMin, safeArea.xMin), right = Mathf.Min(viewport.xMax, safeArea.xMax);
            float bottom = Mathf.Max(viewport.yMin, safeArea.yMin), top = Mathf.Min(viewport.yMax, safeArea.yMax);
            if (right <= left || top <= bottom) return viewport;
            float xPad = Mathf.Min(Mathf.Max(0, padding), (right-left)*.25f);
            float yPad = Mathf.Min(Mathf.Max(0, padding), (top-bottom)*.25f);
            return Rect.MinMaxRect(left+xPad, bottom+yPad, right-xPad, top-yPad);
        }

        public static Rect ScreenRect(Canvas owner)
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var viewport = owner != null ? owner.pixelRect : screen;
            var safe = Screen.safeArea;
            // Device Simulator/editor windows may report a different pixel size from the canvas display.
            // Normalize the system insets before converting to the actual rendering display's pixels.
            if (owner != null && screen.width > 0 && screen.height > 0)
            {
                var display = owner.renderingDisplaySize;
                safe = Rect.MinMaxRect(safe.xMin/screen.width*display.x, safe.yMin/screen.height*display.y,
                    safe.xMax/screen.width*display.x, safe.yMax/screen.height*display.y);
            }
            bool inset = Application.isMobilePlatform || Screen.safeArea != screen;
            return CalculateSafeRect(viewport, safe, inset ? MobilePadding * (owner != null ? owner.scaleFactor : 1) : 0);
        }

        public void Refresh()
        {
            if (canvas == null || content == null || Screen.width <= 0 || Screen.height <= 0) return;
            var viewport = canvas.pixelRect;
            if (viewport.width <= 0 || viewport.height <= 0) return;
            var safe = ScreenRect(canvas);
            var min = new Vector2((safe.xMin-viewport.xMin)/viewport.width, (safe.yMin-viewport.yMin)/viewport.height);
            var max = new Vector2((safe.xMax-viewport.xMin)/viewport.width, (safe.yMax-viewport.yMin)/viewport.height);
            if (content.anchorMin != min) content.anchorMin = min;
            if (content.anchorMax != max) content.anchorMax = max;
            if (fittedPanel == null) return;
            var size = Vector2.Scale(fittedPanel.rect.size, new Vector2(panelScale.x, panelScale.y));
            if (size.x <= 0 || size.y <= 0) return;
            float scale = Mathf.Min(1, content.rect.width / size.x, content.rect.height / size.y);
            fittedPanel.localScale = panelScale * scale;
        }
        // Read device screen metrics in the player loop, not editor-driven canvas rebuild callbacks.
        private void LateUpdate() => Refresh();
    }
}
