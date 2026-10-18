using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    public sealed class DepthNormalOutlineFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public bool applyToSceneView;
            public bool applyToRenderTextures = true;
            [Tooltip("Exclude these object layers from depth/normal outlines (not from normal rendering).")]
            public LayerMask excludedLayers;
            [Tooltip("Outline width in output/screen pixels, independent of internal resolution.")]
            [Range(0, 4)] public float width = 1;
            public Color color = new Color(.025f, .018f, .015f, 1);
            [Range(.001f, .2f)] public float depthThreshold = .02f;
            [Range(.01f, 1)] public float normalThreshold = .3f;
        }
        public Settings settings = new Settings();
        [SerializeField] Shader outlineShader;
        public Shader OutlineShader { get => outlineShader; set => outlineShader = value; }
        public Vector2Int LastMaskSize { get; private set; }
        Material material;
        OutlinePass pass;
        CompositePass compositePass;
        public override void Create()
        {
            pass?.Dispose(); compositePass?.Dispose(); CoreUtils.Destroy(material);
            material = outlineShader ? CoreUtils.CreateEngineMaterial(outlineShader) : null;
            pass = new OutlinePass(this);
            compositePass = new CompositePass(this);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var data = renderingData.cameraData;
            if (!material || settings.width <= 0 || settings.color.a <= 0 || data.renderType == CameraRenderType.Overlay) return;
            if (data.cameraType != CameraType.Game && !(settings.applyToSceneView && data.isSceneViewCamera)) return;
            if (data.targetTexture && !settings.applyToRenderTextures) return;
            if (data.cameraTargetDescriptor.dimension != TextureDimension.Tex2D || data.camera.stereoEnabled) return;
            renderer.EnqueuePass(pass);
            if (data.resolveFinalTarget) renderer.EnqueuePass(compositePass);
        }
        protected override void Dispose(bool disposing) { pass?.Dispose(); compositePass?.Dispose(); CoreUtils.Destroy(material); }

        sealed class OutlinePass : ScriptableRenderPass
        {
            readonly DepthNormalOutlineFeature owner;
            readonly ShaderTagId[] maskTags = { new ShaderTagId("UniversalGBuffer"), new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
            internal RTHandle mask, edges;
            public OutlinePass(DepthNormalOutlineFeature owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.BeforeUpscaling;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                profilingSampler = new ProfilingSampler("Depth Normal Outline");
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.bindMS = false;
                desc.useMipMap = desc.autoGenerateMips = desc.useDynamicScale = false;
                desc.graphicsFormat = GraphicsFormat.R8_UNorm;
                RenderingUtils.ReAllocateIfNeeded(ref mask, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_CharacterOutlineMask");
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                RenderingUtils.ReAllocateIfNeeded(ref edges, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_OutlineDirectionalEdges");
                ConfigureTarget(mask, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                ConfigureClear(ClearFlag.Color, Color.clear);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var color = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (color.rt == null || mask.rt == null) return;
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd); cmd.Clear();
                    var drawing = CreateDrawingSettings(maskTags[0], ref renderingData, SortingCriteria.CommonOpaque);
                    for (int i=1;i<maskTags.Length;i++) drawing.SetShaderPassName(i,maskTags[i]);
                    drawing.overrideMaterial = owner.material;
                    drawing.overrideMaterialPassIndex = 1;
                    var filtering = new FilteringSettings(RenderQueueRange.opaque, ~owner.settings.excludedLayers.value);
                    // Original mesh positions; the camera depth rejects occluded character fragments.
                    context.DrawRenderers(renderingData.cullResults, ref drawing, ref filtering);
                    var s = owner.settings;
                    owner.material.SetVector("_OutlineTexelSize", new Vector4(1f / mask.rt.width, 1f / mask.rt.height, mask.rt.width, mask.rt.height));
                    owner.material.SetVector("_OutlineParameters", new Vector4(s.width, s.depthThreshold, s.normalThreshold, 0));
                    owner.material.SetColor("_OutlineColor", s.color.linear);
                    owner.LastMaskSize = new Vector2Int(mask.rt.width, mask.rt.height);
                    // Store directional boundaries, leaving scene color untouched until presentation.
                    Blitter.BlitCameraTexture(cmd, mask, edges, owner.material, 0);
                }
                context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
            }
            public void Dispose() { mask?.Release(); edges?.Release(); mask = edges = null; }
        }
        sealed class CompositePass : ScriptableRenderPass
        {
            readonly DepthNormalOutlineFeature owner;
            RTHandle output;
            public CompositePass(DepthNormalOutlineFeature owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.AfterUpscaling;
                profilingSampler = new ProfilingSampler("Screen Pixel Outline Composite");
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) => ResetTarget();
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var edges = owner.pass.edges;
                if (edges?.rt == null) return;
                var data = renderingData.cameraData;
                var outputTexture = data.stackOutputTexture != null ? data.stackOutputTexture : data.targetTexture;
                RenderTargetIdentifier target = outputTexture != null
                    ? new RenderTargetIdentifier(outputTexture) : BuiltinRenderTextureType.CameraTarget;
                if (output == null || output.nameID != target)
                {
                    output?.Release();
                    output = RTHandles.Alloc(target);
                }
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    owner.material.SetVector("_OutlineOutputSize", new Vector4(data.camera.pixelRect.width, data.camera.pixelRect.height, 0, 0));
                    owner.material.SetTexture("_OutlineInclusionMask", owner.pass.mask.rt);
                    // Match FinalBlit's RT-to-backbuffer orientation and preserve the presented color.
                    bool flip = !data.isSceneViewCamera && data.targetTexture == null && data.stackOutputTexture == null && SystemInfo.graphicsUVStartsAtTop;
                    CoreUtils.SetRenderTarget(cmd, output, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store, ClearFlag.None, Color.clear);
                    cmd.SetViewport(data.camera.pixelRect);
                    Blitter.BlitTexture(cmd, edges, flip ? new Vector4(1,-1,0,1) : new Vector4(1,1,0,0), owner.material, 2);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
            public void Dispose() { output?.Release(); output = null; }
        }
    }
}
