using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace RetroRendering
{
    /// <summary>
    /// Small forward renderer for unlit cards, sprites and overlay UI. Writes directly to the camera
    /// target at its native size; no GBuffer, lights, shadows, intermediate textures or post effects.
    /// Based on URP 14's DrawObjectsPass and DrawScreenSpaceUIPass in the embedded package.
    /// </summary>
    public sealed class Simple2DRenderer : ScriptableRenderer
    {
        private static readonly ShaderTagId[] PassTags =
        {
            new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("Universal2D")
        };
        private readonly DrawObjectsPass opaque;
        private readonly DrawObjectsPass transparent;
        private readonly OverlayUIPass overlayUI = new OverlayUIPass();

        public Simple2DRenderer(Simple2DRendererData data) : base(data)
        {
            supportedRenderingFeatures = new RenderingFeatures { msaa = false };
            opaque = new DrawObjectsPass("Simple 2D / Opaque", PassTags, true,
                RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, -1, StencilState.defaultValue, 0);
            transparent = new DrawObjectsPass("Simple 2D / Transparent", PassTags, false,
                RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, -1, StencilState.defaultValue, 0);
        }

        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // k_CameraTarget also resolves Camera.targetTexture; the base renderer handles clears/viewport.
            ConfigureCameraTarget(k_CameraTarget, k_CameraTarget);
            opaque.m_IsActiveTargetBackBuffer = transparent.m_IsActiveTargetBackBuffer =
                renderingData.cameraData.targetTexture == null;
            EnqueuePass(opaque);
            EnqueuePass(transparent);
            if (renderingData.cameraData.rendersOverlayUI) EnqueuePass(overlayUI);
        }

        public override void SetupCullingParameters(ref ScriptableCullingParameters parameters, ref CameraData cameraData)
        {
            parameters.cullingOptions = CullingOptions.None;
            parameters.isOrthographic = cameraData.camera.orthographic;
            parameters.shadowDistance = 0;
            parameters.maximumVisibleLights = 0;
        }

        private sealed class OverlayUIPass : ScriptableRenderPass
        {
            public OverlayUIPass()
            {
                profilingSampler = new ProfilingSampler("Simple 2D / Overlay UI");
                renderPassEvent = RenderPassEvent.AfterRendering;
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) =>
                context.DrawUIOverlay(renderingData.cameraData.camera);
        }
    }
}
