using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>Depth-based AO at the renderer's internal resolution, before presentation and native UI.</summary>
    public sealed class LowResolutionSSAOFeature : ScriptableRendererFeature
    {
        public enum SampleCount { Low = 8, Medium = 16, High = 32 }
        [Serializable]
        public sealed class Settings
        {
            public bool applyToRenderTextures = true;
            public bool applyToSceneView;
            [Range(0, 3)] public float intensity = 1.2f;
            [Range(0.001f, 1)] public float radius = 0.06f;
            [Range(0, 0.05f)] public float bias = 0.001f;
            public SampleCount samples = SampleCount.Medium;
            [Range(0.1f, 16)] public float bilateralSharpness = 4;
        }

        public Settings settings = new Settings();
        [SerializeField] Shader aoShader;
        public Shader AOShader { get => aoShader; set => aoShader = value; }
        public Vector2Int LastAOSize { get; private set; }
        public RenderPassEvent LastExecutionEvent { get; private set; }
        Material material;
        AOPass pass;

        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = aoShader != null ? CoreUtils.CreateEngineMaterial(aoShader) : null;
            pass = new AOPass(this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var data = renderingData.cameraData;
            if (material == null || settings.intensity <= 0 || data.camera == null || data.camera.stereoEnabled ||
                data.renderType == CameraRenderType.Overlay) return;
            if (data.cameraType != CameraType.Game && !(settings.applyToSceneView && data.isSceneViewCamera)) return;
            if (data.targetTexture != null && !settings.applyToRenderTextures) return;
            var desc = data.cameraTargetDescriptor;
            if (desc.dimension != TextureDimension.Tex2D || desc.colorFormat == RenderTextureFormat.Depth ||
                desc.colorFormat == RenderTextureFormat.Shadowmap) return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class AOPass : ScriptableRenderPass
        {
            readonly LowResolutionSSAOFeature owner;
            RTHandle ao, blurred;
            static readonly int Parameters = Shader.PropertyToID("_AOParameters");
            static readonly int TexelSize = Shader.PropertyToID("_AOTexelSize");
            static readonly int Sharpness = Shader.PropertyToID("_AOSharpness");

            public AOPass(LowResolutionSSAOFeature owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.BeforeUpscaling;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                profilingSampler = new ProfilingSampler("Low Resolution SSAO");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.bindMS = false;
                desc.useMipMap = desc.autoGenerateMips = desc.useDynamicScale = false;
                desc.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Render)
                    ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;
                RenderingUtils.ReAllocateIfNeeded(ref ao, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_LowResolutionAO");
                RenderingUtils.ReAllocateIfNeeded(ref blurred, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_LowResolutionAOBlur");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var color = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (color.rt == null) return;
                var s = owner.settings;
                var mat = owner.material;
                mat.SetVector(Parameters, new Vector4(Mathf.Max(0.001f, s.radius), Mathf.Max(0, s.bias),
                    Mathf.Clamp(s.intensity, 0, 3), Mathf.Clamp((int)s.samples, 8, 32)));
                mat.SetVector(TexelSize, new Vector4(1f / ao.rt.width, 1f / ao.rt.height, ao.rt.width, ao.rt.height));
                mat.SetFloat(Sharpness, Mathf.Max(0.1f, s.bilateralSharpness));
                owner.LastAOSize = new Vector2Int(ao.rt.width, ao.rt.height);
                owner.LastExecutionEvent = renderPassEvent;
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // AO reads only camera depth. Composite blends into the loaded color target,
                    // so no color feedback or extra scene-color copy is required.
                    Blitter.BlitCameraTexture(cmd, color, ao, mat, 0);
                    Blitter.BlitCameraTexture(cmd, ao, blurred, mat, 1);
                    Blitter.BlitCameraTexture(cmd, blurred, color, RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store, mat, 2);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose() { ao?.Release(); blurred?.Release(); ao = blurred = null; }
        }
    }
}
