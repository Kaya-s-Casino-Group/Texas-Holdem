using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Draws the existing animated player mesh above overlay UI without changing its placement.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(2000)]
    public sealed class PlayerPortraitOverlay : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private MeshRenderer artwork;
        private Canvas overlay;
        private PlayerPortraitOverlayGraphic graphic;
        private bool previousForceRenderingOff;
        public Canvas OverlayCanvas => overlay;
        public MeshRenderer Artwork => artwork;

        public void Configure(Camera camera, MeshRenderer source)
        { viewCamera = camera; artwork = source; }

        private void OnEnable()
        {
            if (!Application.isPlaying || artwork == null || viewCamera == null) return;
            previousForceRenderingOff = artwork.forceRenderingOff;
            artwork.forceRenderingOff = true;
            var root = new GameObject("Player Portrait Overlay", typeof(RectTransform), typeof(Canvas));
            // Overlay canvases need an unscaled screen-space root, independent of the animated 3D actor.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            overlay = root.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = short.MaxValue;
            var image = new GameObject("Portrait", typeof(RectTransform), typeof(PlayerPortraitOverlayGraphic));
            image.transform.SetParent(root.transform, false);
            var rect = (RectTransform)image.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            graphic = image.GetComponent<PlayerPortraitOverlayGraphic>();
            graphic.Configure(viewCamera, artwork);
            // No GraphicRaycaster: even opaque parts of the portrait let pointer input reach the UI below.
        }

        private void LateUpdate()
        {
            if (overlay == null) return;
            overlay.enabled = artwork != null && artwork.enabled && artwork.gameObject.activeInHierarchy;
            if (overlay.enabled) graphic.Refresh();
        }

        private void OnDisable()
        {
            if (overlay == null) return;
            if (artwork != null) artwork.forceRenderingOff = previousForceRenderingOff;
            overlay.gameObject.SetActive(false); Destroy(overlay.gameObject);
            overlay = null; graphic = null;
        }
    }
}
