using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    // Mask the complete visible character silhouette so hulls cannot draw internal lines,
    // including where separate character meshes overlap.
    public sealed class CharacterOutlineFeature : ScriptableRendererFeature
    {
        SilhouettePass silhouettePass;
        HullPass hullPass;

        public override void Create()
        {
            silhouettePass?.Dispose();
            silhouettePass = new SilhouettePass();
            hullPass = new HullPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if (camera.renderType == CameraRenderType.Overlay) return;
            renderer.EnqueuePass(silhouettePass);
            renderer.EnqueuePass(hullPass);
        }

        protected override void Dispose(bool disposing) => silhouettePass?.Dispose();

        sealed class SilhouettePass : ScriptableRenderPass
        {
            static readonly ShaderTagId Tag = new ShaderTagId("CharacterOutlineMask");
            static readonly int SilhouetteId = Shader.PropertyToID("_CharacterHullSilhouette");
            RTHandle silhouette;

            public SilhouettePass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                requiresIntermediateTexture = true;
                profilingSampler = new ProfilingSampler("Character Outline Silhouette");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Render)
                    ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;
                descriptor.useMipMap = descriptor.autoGenerateMips = false;
                RenderingUtils.ReAllocateIfNeeded(ref silhouette, descriptor, FilterMode.Point,
                    TextureWrapMode.Clamp, name: "_CharacterHullSilhouette");
                ConfigureTarget(silhouette, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.Color, Color.clear);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var drawing = CreateDrawingSettings(Tag, ref renderingData, SortingCriteria.CommonOpaque);
                var filtering = new FilteringSettings(RenderQueueRange.opaque,
                    renderingData.cameraData.camera.cullingMask);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
                var cmd = CommandBufferPool.Get("Bind Character Silhouette");
                cmd.SetGlobalTexture(SilhouetteId, silhouette.nameID);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose() => silhouette?.Release();
        }

        sealed class HullPass : ScriptableRenderPass
        {
            static readonly ShaderTagId Tag = new ShaderTagId("CharacterOutline");

            public HullPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                profilingSampler = new ProfilingSampler("Character Outer Outline");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var renderer = renderingData.cameraData.renderer;
                ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var drawing = CreateDrawingSettings(Tag, ref renderingData, SortingCriteria.CommonOpaque);
                var filtering = new FilteringSettings(RenderQueueRange.opaque,
                    renderingData.cameraData.camera.cullingMask);
                context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
            }
        }
    }
}
