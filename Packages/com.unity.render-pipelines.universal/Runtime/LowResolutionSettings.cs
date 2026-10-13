using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>Renderer-owned internal resolution and nearest-neighbor presentation settings.</summary>
    [Serializable]
    public sealed class LowResolutionSettings
    {
        public bool enabled;
        [Min(1), Tooltip("Maximum internal render height. Width follows the native output aspect ratio. Small windows are not upsampled internally.")]
        public int maximumHeight = 540;
        public bool applyToRenderTextures = true;
        public bool applyToSceneView;

        public static Vector2Int CalculateRenderSize(int outputWidth, int outputHeight, int maximumHeight)
        {
            outputWidth = Mathf.Max(1, outputWidth);
            outputHeight = Mathf.Max(1, outputHeight);
            int height = Mathf.Min(outputHeight, Mathf.Max(1, maximumHeight));
            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt((float)outputWidth * height / outputHeight)), height);
        }

        /// <summary>Called by UniversalRenderer before camera buffers, culling and temporal history.</summary>
        public void ConfigureCamera(ref CameraData data)
        {
            if (data.camera == null || data.camera.stereoEnabled) return;
            if (data.nativeResolutionOverlay) return;
            // A stack is owned by its base renderer; even an overlay using another renderer inherits it.
            if (data.renderType == CameraRenderType.Overlay)
            {
                if (data.customRenderResolution)
                    data.SetPointRenderResolution(data.cameraTargetDescriptor.width, data.cameraTargetDescriptor.height);
                return;
            }
            if (!enabled || (data.cameraType != CameraType.Game && !(applyToSceneView && data.isSceneViewCamera))) return;
            if (data.targetTexture != null && !applyToRenderTextures) return;
            if (data.cameraTargetDescriptor.dimension != Rendering.TextureDimension.Tex2D ||
                data.cameraTargetDescriptor.colorFormat == RenderTextureFormat.Depth ||
                data.cameraTargetDescriptor.colorFormat == RenderTextureFormat.Shadowmap) return;
            var size = CalculateRenderSize(data.camera.pixelWidth, data.camera.pixelHeight, maximumHeight);
            data.SetPointRenderResolution(size.x, size.y);
        }
    }

    /// <summary>Last allocated camera targets, independent of any Renderer Feature.</summary>
    [Serializable]
    public struct CameraRenderTargetInfo
    {
        public string camera;
        public Vector2Int outputSize, renderSize, colorTargetSize, depthTargetSize;
        public bool lowResolution;
        public string finalFilter;
    }
}
