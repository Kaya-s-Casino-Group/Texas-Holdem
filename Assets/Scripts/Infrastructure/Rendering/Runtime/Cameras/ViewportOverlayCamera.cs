using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>Independent perspective or orthographic presentation space, fitted to a rectangle of the output viewport.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(-100)]
    public sealed class ViewportOverlayCamera : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        [SerializeField, Tooltip("Insert this overlay before this camera, normally the final UI camera.")]
        private Camera beforeCamera;
        [SerializeField] private LayerMask contentLayers;
        [SerializeField, Tooltip("Layout container only. Animate its children, not this transform.")]
        private Transform contentRoot;
        [SerializeField, Tooltip("Normalized output rectangle. This positions content; it does not crop animations.")]
        private Rect contentViewport = new Rect(.56f, .06f, .4f, .24f);
        [SerializeField, Tooltip("Unscaled content width and height in camera-facing space.")]
        private Vector2 contentSize = new Vector2(.95f, .38f);
        [SerializeField, Min(.01f)] private float contentDistance = 5;
        private Camera overlayCamera;
        private Camera Overlay => overlayCamera != null ? overlayCamera : (overlayCamera = GetComponent<Camera>());

        private void Attach()
        {
            if (sceneCamera == null || sceneCamera == Overlay) return;
            var source = sceneCamera.GetUniversalAdditionalCameraData();
            if (source.renderType != CameraRenderType.Base) return;
            sceneCamera.cullingMask &= ~contentLayers.value;
            var camera = Overlay;
            camera.cullingMask = camera.eventMask = contentLayers;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = camera.useOcclusionCulling = false;
            camera.targetTexture = null;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            // URP exposes clearDepth as read-only; configure Clear Depth on the Overlay camera asset/scene.
            data.renderShadows = data.renderPostProcessing = false;
            data.requiresDepthOption = data.requiresColorOption = CameraOverrideOption.Off;
            data.antialiasing = AntialiasingMode.None;
            data.stopNaN = data.dithering = data.allowXRRendering = data.allowHDROutput = false;
            data.volumeLayerMask = 0;
            camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
            var stack = source.cameraStack;
            if (stack == null) return;
            stack.Remove(camera);
            int next = beforeCamera != null ? stack.IndexOf(beforeCamera) : -1;
            if (next < 0) stack.Add(camera); else stack.Insert(next, camera);
        }

        public void Synchronize()
        {
            if (sceneCamera == null || sceneCamera == Overlay) return;
            var camera = Overlay;
            // Overlay projections and pointer rays must use the same output viewport/aspect as the Base.
            camera.rect = sceneCamera.rect;
            camera.aspect = sceneCamera.aspect;
            camera.targetDisplay = sceneCamera.targetDisplay;
            if (contentRoot == null || contentSize.x <= 0 || contentSize.y <= 0 ||
                contentViewport.width <= 0 || contentViewport.height <= 0) return;
            float distance = Mathf.Clamp(contentDistance, camera.nearClipPlane + .001f, camera.farClipPlane - .001f);
            // Measure the actual projection at the layout plane; supports both projections and lens shift.
            var origin = camera.ViewportToWorldPoint(new Vector3(0, 0, distance));
            float width = Vector3.Distance(origin, camera.ViewportToWorldPoint(new Vector3(1, 0, distance)));
            float height = Vector3.Distance(origin, camera.ViewportToWorldPoint(new Vector3(0, 1, distance)));
            float scale = Mathf.Min(width * contentViewport.width / contentSize.x,
                height * contentViewport.height / contentSize.y);
            var center = contentViewport.center;
            contentRoot.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(center.x, center.y, distance)),
                camera.transform.rotation);
            // The layout root is a scene root; card animation transforms live below it.
            contentRoot.localScale = Vector3.one * scale;
        }

        private void OnEnable()
        {
            Attach(); Synchronize();
            RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }
        private void LateUpdate() => Synchronize();
        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        { if (camera == sceneCamera) Synchronize(); }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
            if (sceneCamera == null || sceneCamera == Overlay) return;
            var data = sceneCamera.GetUniversalAdditionalCameraData();
            if (data.renderType == CameraRenderType.Base) data.cameraStack?.Remove(Overlay);
        }
    }
}
