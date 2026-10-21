using UIFoundation;
using UnityEngine;

namespace Poker.Presentation.LoginPage
{
    /// <summary>Fits the original landscape artboard inside device safe areas without stretching its artwork.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Canvas))]
    public sealed class LoginPageLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform artboard;
        [SerializeField] private RectTransform background;
        private Canvas owner;
        private static readonly Vector2 ReferenceSize = new Vector2(1920, 1080);

        public void Configure(RectTransform content, RectTransform backdrop)
        {
            artboard = content;
            background = backdrop;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (owner == null) owner = GetComponent<Canvas>();
            if (owner == null) return;
            ApplyViewport(owner.pixelRect, ScreenSafeArea.ScreenRect(owner));
        }

        // Explicit pixel bounds also allow editor previews of phone cutouts without changing global Screen state.
        public void ApplyViewport(Rect viewport, Rect safeArea)
        {
            if (artboard == null || background == null || viewport.width <= 0 || viewport.height <= 0) return;
            var canvasRect = (RectTransform)transform;
            var unitsPerPixel = new Vector2(canvasRect.rect.width / viewport.width, canvasRect.rect.height / viewport.height);
            var safe = ScreenSafeArea.CalculateSafeRect(viewport, safeArea, 0);
            var available = Vector2.Scale(safe.size, unitsPerPixel);
            var center = Vector2.Scale(safe.center - viewport.center, unitsPerPixel);
            float scale = Mathf.Min(1, available.x / ReferenceSize.x, available.y / ReferenceSize.y);
            if (artboard.anchoredPosition != center) artboard.anchoredPosition = center;
            var contentScale = new Vector3(scale, scale, 1);
            if (artboard.localScale != contentScale) artboard.localScale = contentScale;

            // Full-bleed backdrop: crop the background at wide/tall aspect ratios, never the menu content.
            float cover = Mathf.Max(canvasRect.rect.width / ReferenceSize.x, canvasRect.rect.height / ReferenceSize.y);
            var size = ReferenceSize * cover;
            if (background.sizeDelta != size) background.sizeDelta = size;
            if (background.anchoredPosition != Vector2.zero) background.anchoredPosition = Vector2.zero;
        }
    }
}
