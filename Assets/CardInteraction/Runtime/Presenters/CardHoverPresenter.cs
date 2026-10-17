using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UIFoundation;

namespace CardInteraction
{
    /// <summary>One observer's shared tooltip, adapted to scene or Canvas cards. Owns its instance, not interaction/scoring.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class CardHoverPresenter : MonoBehaviour
    {
        [SerializeField] private CardTooltipView tooltipPrefab;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private List<MonoBehaviour> sources = new List<MonoBehaviour>();
        [SerializeField, Min(0.01f)] private float sizeMultiplier = 0.55f;
        [SerializeField, Min(0)] private float gap = 0.008f;
        [SerializeField] private Vector2 cameraPlaneOffset;
        [SerializeField, Min(0)] private float towardCamera = 0.002f;
        [SerializeField] private bool clampToViewport = true;
        [SerializeField, Min(0)] private float viewportPadding = 12;
        [SerializeField] private Camera renderingCamera;
        [Header("Canvas 卡牌左侧提示（Canvas 单位）")]
        [SerializeField, Min(0.01f)] private float canvasSizeMultiplier = 0.4f;
        [SerializeField, Min(0)] private float canvasGap = 8;
        private CardTooltipView instance;
        private Canvas canvas;
        [Header("提示惯性显隐")]
        [SerializeField, Min(.01f)] private float showSeconds = .16f;
        [SerializeField, Min(.01f)] private float hideSeconds = .12f;
        private Tween visibilityTween;
        private Vector3 presentationScale = Vector3.one;
        private float visibility;
        private bool wantsVisible;
        public float VisibilityScale => visibility;
        private readonly Vector3[] cardCorners = new Vector3[4];

        public CardTooltipView TooltipPrefab => tooltipPrefab;
        public Camera SceneCamera => sceneCamera;
        public Camera RenderingCamera => renderingCamera != null ? renderingCamera : sceneCamera;
        public void SetRenderingCamera(Camera camera) => renderingCamera = camera;
        public CardTooltipView Instance => instance;
        public IReadOnlyList<MonoBehaviour> Sources => sources.AsReadOnly();
        public string HoveredViewId { get; private set; }
        public bool IsVisible => wantsVisible && instance != null && instance.gameObject.activeInHierarchy;

        public void Configure(CardTooltipView prefab, Camera camera, IEnumerable<MonoBehaviour> hoverSources)
        {
            ValidatePrefab(prefab);
            if (hoverSources == null) throw new ArgumentNullException(nameof(hoverSources));
            var copy = hoverSources.ToList();
            foreach (var source in copy) ValidateSource(source);
            copy = copy.Distinct().ToList();
            if (tooltipPrefab != prefab) ReleaseInstance();
            tooltipPrefab = prefab; sceneCamera = camera; sources = copy;
            Hide(false);
        }

        public void RegisterSource(MonoBehaviour source)
        {
            ValidateSource(source);
            if (!sources.Contains(source)) sources.Add(source);
        }

        public void RemoveSource(MonoBehaviour source) { sources.Remove(source); Hide(); }

        /// <summary>Also exposed for deterministic host/test refreshes; automatic updates occur only during Play.</summary>
        public void RefreshPresentation()
        {
            var camera = sceneCamera != null ? sceneCamera : Camera.main;
            var renderCamera = renderingCamera != null ? renderingCamera : camera;
            if (!isActiveAndEnabled || tooltipPrefab == null || camera == null || !camera.isActiveAndEnabled ||
                renderCamera == null || !renderCamera.isActiveAndEnabled ||
                (renderCamera.cullingMask & (1 << tooltipPrefab.gameObject.layer)) == 0)
            { Hide(false); return; }
            var scope = sources.FirstOrDefault(source => source != null && source.isActiveAndEnabled &&
                source is ICardHoverScope layer && layer.CapturesHover);
            foreach (var source in sources)
            {
                if (scope != null && source != scope) continue;
                if (source == null || !source.isActiveAndEnabled || !(source is ICardHoverSource hover) ||
                    !hover.TryGetHoveredCard(out var target)) continue;
                if (target.UIView != null)
                {
                    if (PresentCanvasCard(target,renderCamera)) return;
                    continue;
                }
                if (!CanDisplay(target.View)) continue;
                var filter = target.View.Surface.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var surface = target.View.Surface.transform;
                var bounds = filter.sharedMesh.bounds;
                var center = surface.TransformPoint(bounds.center);
                // Follow authored card size and the zone's world scale, independently of cosmetic sway/hover.
                float cardScale = target.View.PresentationScale * target.View.transform.TransformVector(Vector3.forward).magnitude;
                float depthOffset = towardCamera * cardScale;
                float depth = camera.WorldToViewportPoint(center).z - depthOffset;
                if (depth <= camera.nearClipPlane || depth >= camera.farClipPlane) continue;
                if (!EnsureInstance()) { Hide(); return; }
                if (instance.transform.parent != transform) instance.transform.SetParent(transform,false);
                canvas.overrideSorting = tooltipPrefab.GetComponent<Canvas>().overrideSorting;
                canvas.sortingLayerID = tooltipPrefab.GetComponent<Canvas>().sortingLayerID;
                canvas.sortingOrder = tooltipPrefab.GetComponent<Canvas>().sortingOrder;
                instance.SetContent(target.Suit, target.Rank, target.ChipPoints);
                canvas.worldCamera = renderCamera;
                // Keep camera-facing UI independent of the card rotation while matching its size.
                instance.transform.rotation = camera.transform.rotation;
                SetWorldPresentationScale(tooltipPrefab.transform.localScale * (sizeMultiplier * cardScale));
                instance.transform.localScale = presentationScale;
                var rect = (RectTransform)instance.transform;
                float halfHeight = Mathf.Abs(Vector3.Dot(camera.transform.up,
                    rect.TransformVector(Vector3.up * rect.rect.height * 0.5f)));
                float halfWidth = Mathf.Abs(Vector3.Dot(camera.transform.right,
                    rect.TransformVector(Vector3.right * rect.rect.width * 0.5f)));
                var extent = bounds.extents;
                float cardTop = Mathf.Abs(Vector3.Dot(camera.transform.up, surface.TransformVector(Vector3.right * extent.x))) +
                    Mathf.Abs(Vector3.Dot(camera.transform.up, surface.TransformVector(Vector3.up * extent.y))) +
                    Mathf.Abs(Vector3.Dot(camera.transform.up, surface.TransformVector(Vector3.forward * extent.z)));
                var position = center + camera.transform.up * (cardTop + halfHeight + (gap + cameraPlaneOffset.y) * cardScale) +
                    camera.transform.right * (cameraPlaneOffset.x * cardScale) - camera.transform.forward * depthOffset;
                if (clampToViewport) position = ClampPosition(camera, position, halfWidth, halfHeight);
                instance.transform.position = position;
                Show(target.View.ViewId);
                return;
            }
            Hide();
        }

        private void SetWorldPresentationScale(Vector3 worldScale)
        {
            // The presenter may share a scaled ancestor with the card. Cancel inherited scale to avoid counting it twice.
            var root = instance.transform;
            root.localScale = Vector3.one;
            presentationScale = new Vector3(
                worldScale.x / Mathf.Max(.000001f, root.TransformVector(Vector3.right).magnitude),
                worldScale.y / Mathf.Max(.000001f, root.TransformVector(Vector3.up).magnitude),
                worldScale.z / Mathf.Max(.000001f, root.TransformVector(Vector3.forward).magnitude));
        }

        private bool PresentCanvasCard(CardHoverTarget target, Camera renderCamera)
        {
            var item = target.UIView;
            if (!item.isActiveAndEnabled || item.Card == null || !item.Card.FaceUp || !item.Interaction.IsHovered ||
                item.Graphic == null || !item.Graphic.isActiveAndEnabled) return false;
            var owner = item.Graphic.canvas?.rootCanvas;
            if (owner == null || owner.renderMode == RenderMode.WorldSpace ||
                (owner.renderMode == RenderMode.ScreenSpaceCamera && owner.worldCamera != renderCamera)) return false;
            if (!EnsureInstance()) return false;
            instance.SetContent(target.Suit,target.Rank,target.ChipPoints);
            // Reuse the same owned instance in this Canvas so scaling, camera and page ordering remain consistent.
            var rect = (RectTransform)instance.transform;
            if (rect.parent != owner.transform) rect.SetParent(owner.transform,false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.localRotation = Quaternion.identity; presentationScale = Vector3.one * canvasSizeMultiplier;
            rect.localScale = presentationScale;
            instance.gameObject.SetActive(true);
            canvas.worldCamera = owner.worldCamera; canvas.overrideSorting = true;
            canvas.sortingLayerID = owner.sortingLayerID; canvas.sortingOrder = owner.sortingOrder + 1;
            item.Graphic.rectTransform.GetWorldCorners(cardCorners);
            var min = new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            foreach (var corner in cardCorners)
            {
                var point = (Vector2)owner.transform.InverseTransformPoint(corner);
                min = Vector2.Min(min,point); max = Vector2.Max(max,point);
            }
            var half = rect.rect.size * canvasSizeMultiplier * .5f;
            var position = new Vector2(min.x-canvasGap-half.x,(min.y+max.y)*.5f);
            if (clampToViewport)
            {
                var ownerRect = (RectTransform)owner.transform;
                var safe = ScreenSafeArea.ScreenRect(owner);
                var eventCamera = owner.renderMode == RenderMode.ScreenSpaceOverlay ? null : owner.worldCamera;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(ownerRect, safe.min, eventCamera, out var safeMin);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(ownerRect, safe.max, eventCamera, out var safeMax);
                var bounds = Rect.MinMaxRect(safeMin.x, safeMin.y, safeMax.x, safeMax.y);
                float padding = viewportPadding / Mathf.Max(.01f,owner.scaleFactor);
                position.x = half.x+padding >= bounds.width*.5f ? bounds.center.x :
                    Mathf.Clamp(position.x,bounds.xMin+half.x+padding,bounds.xMax-half.x-padding);
                position.y = half.y+padding >= bounds.height*.5f ? bounds.center.y :
                    Mathf.Clamp(position.y,bounds.yMin+half.y+padding,bounds.yMax-half.y-padding);
            }
            rect.localPosition = new Vector3(position.x,position.y,-towardCamera);
            Show(item.Card.ViewId);
            return true;
        }

        private Vector3 ClampPosition(Camera camera, Vector3 position, float halfWidth, float halfHeight)
        {
            var viewport = camera.WorldToViewportPoint(position);
            float xExtent = Mathf.Abs(camera.WorldToViewportPoint(position + camera.transform.right * halfWidth).x - viewport.x);
            float yExtent = Mathf.Abs(camera.WorldToViewportPoint(position + camera.transform.up * halfHeight).y - viewport.y);
            float xMargin = xExtent + viewportPadding / Mathf.Max(1, camera.pixelWidth);
            float yMargin = yExtent + viewportPadding / Mathf.Max(1, camera.pixelHeight);
            var safe = ScreenSafeArea.CalculateSafeRect(camera.pixelRect, Screen.safeArea, Application.isMobilePlatform ? 16 : 0);
            float left = (safe.xMin-camera.pixelRect.xMin)/camera.pixelWidth + xMargin;
            float right = (safe.xMax-camera.pixelRect.xMin)/camera.pixelWidth - xMargin;
            float bottom = (safe.yMin-camera.pixelRect.yMin)/camera.pixelHeight + yMargin;
            float top = (safe.yMax-camera.pixelRect.yMin)/camera.pixelHeight - yMargin;
            viewport.x = left >= right ? (left+right)*.5f : Mathf.Clamp(viewport.x, left, right);
            viewport.y = bottom >= top ? (bottom+top)*.5f : Mathf.Clamp(viewport.y, bottom, top);
            return camera.ViewportToWorldPoint(viewport);
        }

        private static bool CanDisplay(CardInstanceView card)
        {
            if (card == null || !card.isActiveAndEnabled || card.Data == null || !card.Data.FaceUp ||
                !card.Data.Interaction.IsHovered || card.IsEntering || card.IsExiting || card.Surface == null) return false;
            var renderer = card.Surface.GetComponent<Renderer>();
            return renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
        }

        private bool EnsureInstance()
        {
            if (instance != null) return true;
            if (tooltipPrefab.GetComponent<Canvas>() == null || !tooltipPrefab.IsConfigured)
                return false;
            instance = Instantiate(tooltipPrefab, transform, false);
            instance.name = "Card Hover Tooltip (Runtime)";
            instance.gameObject.hideFlags = HideFlags.DontSave;
            canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var raycaster in instance.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
            var group = instance.GetComponent<CanvasGroup>();
            if (group != null) { group.interactable = false; group.blocksRaycasts = false; }
            instance.gameObject.SetActive(false);
            return true;
        }

        private void ApplyVisibility() { if (instance != null) instance.transform.localScale = presentationScale * visibility; }
        private void Show(string id)
        {
            instance.gameObject.SetActive(true); HoveredViewId = id;
            if (!wantsVisible)
            {
                wantsVisible = true; visibilityTween?.Kill();
                visibilityTween = DOTween.To(() => visibility,value => { visibility = value; ApplyVisibility(); },1,showSeconds)
                    .SetEase(Ease.OutBack,1.25f).SetUpdate(true);
            }
            ApplyVisibility();
        }
        public void Hide() => Hide(true);
        public void Hide(bool animate)
        {
            HoveredViewId = null;
            if (!animate)
            {
                visibilityTween?.Kill(); visibilityTween = null; wantsVisible = false; visibility = 0;
                if (instance != null) instance.gameObject.SetActive(false); return;
            }
            if (!wantsVisible) return;
            wantsVisible = false; visibilityTween?.Kill(); visibilityTween = null;
            if (instance == null) return;
            visibilityTween = DOTween.To(() => visibility,value => { visibility = value; ApplyVisibility(); },0,hideSeconds)
                .SetEase(Ease.InBack,1.1f).SetUpdate(true).OnComplete(() => { if (!wantsVisible && instance != null) instance.gameObject.SetActive(false); });
        }
        public void AdvanceAnimation(float seconds)
        {
            if ((float.IsNaN(seconds) || float.IsInfinity(seconds)) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (visibilityTween != null && visibilityTween.IsActive()) visibilityTween.Goto(visibilityTween.Elapsed()+seconds,false);
        }

        private void ReleaseInstance()
        {
            Hide(false);
            if (instance != null)
            {
                if (Application.isPlaying) Destroy(instance.gameObject);
                else DestroyImmediate(instance.gameObject);
            }
            instance = null; canvas = null;
        }

        private static void ValidatePrefab(CardTooltipView prefab)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));
            var canvas = prefab.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || !prefab.IsConfigured)
                throw new ArgumentException("Use a configured World Space CardTooltipView prefab.", nameof(prefab));
        }

        private static void ValidateSource(MonoBehaviour source)
        {
            if (source == null || !(source is ICardHoverSource))
                throw new ArgumentException("Hover sources must implement ICardHoverSource.", nameof(source));
        }

        private void LateUpdate() { if (Application.isPlaying) RefreshPresentation(); }
        private void OnDisable() => ReleaseInstance();
        private void OnDestroy() => ReleaseInstance();
    }
}
