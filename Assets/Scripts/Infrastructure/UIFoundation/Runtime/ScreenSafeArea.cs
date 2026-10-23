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
        private Vector2 referenceSize;
        public RectTransform Content => content;
        public const float MobilePadding = 16;

        // Authored anchor positions and pixel offsets must share one reference rectangle.
        public void SetReferenceSize(Vector2 size)
        {
            referenceSize = size.x > 0 && size.y > 0 ? size : Vector2.zero;
            Refresh();
        }

        public static Rect FitAspect(Rect bounds, float aspect)
        {
            if (bounds.width <= 0 || bounds.height <= 0 || aspect <= 0 || float.IsNaN(aspect) || float.IsInfinity(aspect)) return bounds;
            var size = bounds.width / bounds.height > aspect
                ? new Vector2(bounds.height * aspect, bounds.height)
                : new Vector2(bounds.width, bounds.width / aspect);
            return new Rect(bounds.center - size * .5f, size);
        }

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

        /// <summary>Maps device insets to the actual display before intersecting a camera or Canvas viewport.</summary>
        public static Rect CalculateDisplaySafeRect(Rect viewport, Rect safeArea, Vector2 screenSize, Vector2 displaySize, float padding)
        {
            if (screenSize.x > 0 && screenSize.y > 0)
                safeArea = Rect.MinMaxRect(safeArea.xMin / screenSize.x * displaySize.x, safeArea.yMin / screenSize.y * displaySize.y,
                    safeArea.xMax / screenSize.x * displaySize.x, safeArea.yMax / screenSize.y * displaySize.y);
            return CalculateSafeRect(viewport, safeArea, padding);
        }

        public static Rect CameraRect(Camera camera)
        {
            var viewport = camera.pixelRect;
            // Off-screen textures are independent of device cutouts and system screen metrics.
            if (camera.targetTexture != null) return viewport;
            var rect = camera.rect;
            var display = new Vector2(viewport.width / Mathf.Max(.000001f, rect.width), viewport.height / Mathf.Max(.000001f, rect.height));
            return CalculateDisplaySafeRect(viewport, Screen.safeArea, new Vector2(Screen.width, Screen.height), display,
                Application.isMobilePlatform ? MobilePadding : 0);
        }

        public static Rect ScreenRect(Canvas owner)
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var viewport = owner != null ? owner.pixelRect : screen;
            bool inset = Application.isMobilePlatform || Screen.safeArea != screen;
            return CalculateDisplaySafeRect(viewport, Screen.safeArea, screen.size,
                owner != null ? owner.renderingDisplaySize : screen.size,
                inset ? MobilePadding * (owner != null ? owner.scaleFactor : 1) : 0);
        }

        public void Refresh()
        {
            if (canvas == null || content == null || Screen.width <= 0 || Screen.height <= 0) return;
            var viewport = canvas.pixelRect;
            if (viewport.width <= 0 || viewport.height <= 0) return;
            var safe = ScreenRect(canvas);
            if (referenceSize.x > 0 && referenceSize.y > 0)
            {
                var fitted = FitAspect(safe, referenceSize.x / referenceSize.y);
                var center = new Vector2((fitted.center.x - viewport.xMin) / viewport.width,
                    (fitted.center.y - viewport.yMin) / viewport.height);
                content.anchorMin = content.anchorMax = center;
                content.pivot = new Vector2(.5f, .5f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = referenceSize;
                // Use the current Canvas geometry: scaleFactor can lead its rebuilt rect during a viewport change.
                float unitsPerPixel = ((RectTransform)canvas.transform).rect.height / viewport.height;
                float contentScale = fitted.height * unitsPerPixel / referenceSize.y;
                content.localScale = new Vector3(contentScale, contentScale, 1);
            }
            else
            {
                var min = new Vector2((safe.xMin-viewport.xMin)/viewport.width, (safe.yMin-viewport.yMin)/viewport.height);
                var max = new Vector2((safe.xMax-viewport.xMin)/viewport.width, (safe.yMax-viewport.yMin)/viewport.height);
                if (content.anchorMin != min) content.anchorMin = min;
                if (content.anchorMax != max) content.anchorMax = max;
                content.offsetMin = content.offsetMax = Vector2.zero;
                content.localScale = Vector3.one;
            }
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
