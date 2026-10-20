using UnityEngine;
using UIFoundation;

namespace Poker.SingleBattle
{
    /// <summary>Sizes a logical actor's plane to a reference viewport; its visual child remains free to animate.</summary>
    [ExecuteAlways, DefaultExecutionOrder(-150)]
    public sealed class BattleBillboardLayout : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Rect viewportArea;
        [SerializeField] private float distance = 5;
        [SerializeField] private float artworkAspect = 1;
        private Rect lastViewport, lastSafe;
        private float lastAspect;
        private Vector3 cameraPosition;
        private Quaternion cameraRotation;
        private bool initialized;

        public void Configure(Camera camera, Rect area, float depth, float aspect)
        {
            viewCamera = camera; viewportArea = area; distance = depth; artworkAspect = aspect;
            initialized = false; Refresh();
        }

        private void OnEnable() { initialized = false; Refresh(); }
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (viewCamera == null) return;
            var viewport = viewCamera.pixelRect;
            if (viewport.width <= 0 || viewport.height <= 0) return;
            var safe = Application.isPlaying ? ScreenSafeArea.CameraRect(viewCamera) : viewport;
            if (initialized && Application.isPlaying && viewport == lastViewport && safe == lastSafe &&
                Mathf.Approximately(viewCamera.aspect, lastAspect)) return;
            if (!initialized || !Application.isPlaying)
            {
                cameraPosition = viewCamera.transform.position;
                cameraRotation = viewCamera.transform.rotation;
            }
            initialized = true; lastViewport = viewport; lastSafe = safe; lastAspect = viewCamera.aspect;
            float height = viewCamera.orthographic ? 2 * viewCamera.orthographicSize :
                2 * distance * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            float width = height * viewCamera.aspect;
            var normalized = new Rect((safe.xMin - viewport.xMin) / viewport.width,
                (safe.yMin - viewport.yMin) / viewport.height, safe.width / viewport.width, safe.height / viewport.height);
            var center = normalized.position + Vector2.Scale(normalized.size, viewportArea.center);
            float planeHeight = Mathf.Min(height * normalized.height * viewportArea.height,
                width * normalized.width * viewportArea.width / Mathf.Max(.01f, artworkAspect));
            transform.SetPositionAndRotation(cameraPosition + cameraRotation *
                new Vector3((center.x - .5f) * width, (center.y - .5f) * height, distance), cameraRotation);
            transform.localScale = new Vector3(planeHeight * artworkAspect, planeHeight, 1);
        }
    }
}
