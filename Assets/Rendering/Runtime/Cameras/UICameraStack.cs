using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>One UI-only overlay follows the scene projection, supporting screen and world-space canvases.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(10000)]
    public sealed class UICameraStack : MonoBehaviour
    {
        [SerializeField] private Camera sceneCamera;
        private Camera uiCamera;
        public Camera SceneCamera => sceneCamera;
        public Camera UICamera => uiCamera != null ? uiCamera : (uiCamera = GetComponent<Camera>());

        public void Configure(Camera source)
        {
            if (source == null || source == UICamera) throw new ArgumentException("A separate scene Base camera is required.", nameof(source));
            if (source.GetUniversalAdditionalCameraData().renderType != CameraRenderType.Base)
                throw new ArgumentException("UI must stack on a Base camera.", nameof(source));
            if (sceneCamera != source) RemoveFromStack();
            sceneCamera = source; Attach(); Synchronize();
        }
        private void Attach()
        {
            if (sceneCamera == null) return;
            int uiMask = 1 << LayerMask.NameToLayer("UI");
            sceneCamera.cullingMask &= ~uiMask;
            var camera = UICamera;
            camera.cullingMask = uiMask; camera.eventMask = uiMask;
            camera.clearFlags = CameraClearFlags.Depth; camera.allowHDR = false; camera.allowMSAA = false;
            camera.allowDynamicResolution = false; camera.useOcclusionCulling = false; camera.targetTexture = null;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay; data.renderShadows = false;
            data.renderOverlayAtNativeResolution = true;
            data.requiresDepthOption = data.requiresColorOption = CameraOverrideOption.Off;
            data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
            data.stopNaN = false; data.dithering = false; data.allowXRRendering = false; data.allowHDROutput = false;
            data.volumeLayerMask = 0;
            camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.ViaScripting);
            var stack = sceneCamera.GetUniversalAdditionalCameraData().cameraStack;
            if (stack != null && (!stack.Contains(camera) || stack.IndexOf(camera) != stack.Count-1))
            { stack.Remove(camera); stack.Add(camera); }
        }
        public void Synchronize()
        {
            if (sceneCamera == null) return;
            var camera = UICamera;
            camera.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
            camera.orthographic = sceneCamera.orthographic; camera.orthographicSize = sceneCamera.orthographicSize;
            camera.fieldOfView = sceneCamera.fieldOfView; camera.nearClipPlane = sceneCamera.nearClipPlane;
            camera.farClipPlane = sceneCamera.farClipPlane; camera.rect = sceneCamera.rect;
            camera.aspect = sceneCamera.aspect; camera.targetDisplay = sceneCamera.targetDisplay;
            camera.projectionMatrix = sceneCamera.projectionMatrix;
        }
        private void RemoveFromStack()
        {
            if (sceneCamera == null) return;
            var data = sceneCamera.GetUniversalAdditionalCameraData();
            if (data.renderType == CameraRenderType.Base) data.cameraStack?.Remove(UICamera);
        }
        private void OnEnable()
        {
            Attach(); Synchronize(); RenderPipelineManager.beginCameraRendering += BeforeCameraRendering;
        }
        private void BeforeCameraRendering(ScriptableRenderContext context, Camera camera)
        { if (camera == sceneCamera) Synchronize(); }
        private void LateUpdate() => Synchronize();
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering;
        private void OnDestroy() { RenderPipelineManager.beginCameraRendering -= BeforeCameraRendering; RemoveFromStack(); }
    }
}
