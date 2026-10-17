using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>HDR soft-threshold bloom before retro grading and the custom Point upscale.</summary>
    [DisallowMultipleRendererFeature("Bloom")]
    public sealed class BloomFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public bool applyToRenderTextures = true;
            public bool applyToSceneView;
            [Min(0)] public float intensity = 0.3f;
            [Min(0), Tooltip("Gamma 空间亮度阈值，1 表示主要提取 HDR 高光。")]
            public float threshold = 1;
            [Range(0, 1), Tooltip("阈值附近的柔和过渡；0 为硬阈值。")]
            public float softKnee = 0.5f;
            [Range(0, 1), Tooltip("光晕向更低分辨率层扩散的比例。")]
            public float scatter = 0.7f;
            [Range(1, 8)] public int maxIterations = 6;
            [Min(0), Tooltip("进入 Bloom 前的线性 HDR 亮度上限，抑制极端高光。")]
            public float clamp = 65472;
            [ColorUsage(false)] public Color tint = Color.white;
        }

        public Settings settings = new Settings();
        [SerializeField] Shader bloomShader;
        public Shader BloomShader { get => bloomShader; set => bloomShader = value; }
        public Vector2Int LastSourceSize { get; private set; }
        public int LastMipCount { get; private set; }
        public RenderPassEvent LastExecutionEvent { get; private set; }
        Material material;
        BloomPass pass;

        public override void Create()
        {
            pass?.Dispose(); CoreUtils.Destroy(material);
            material = bloomShader != null ? CoreUtils.CreateEngineMaterial(bloomShader) : null;
            pass = new BloomPass(this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var data = renderingData.cameraData;
            if (!isActive || material == null || settings.intensity <= 0 || data.camera == null ||
                data.camera.stereoEnabled || data.renderType == CameraRenderType.Overlay) return;
            if (data.cameraType != CameraType.Game && !(settings.applyToSceneView && data.isSceneViewCamera)) return;
            if (data.targetTexture != null && !settings.applyToRenderTextures) return;
            var desc = data.cameraTargetDescriptor;
            if (desc.dimension != TextureDimension.Tex2D || desc.colorFormat == RenderTextureFormat.Depth ||
                desc.colorFormat == RenderTextureFormat.Shadowmap) return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose(); CoreUtils.Destroy(material); material = null;
        }

        sealed class BloomPass : ScriptableRenderPass
        {
            readonly BloomFeature owner;
            readonly RTHandle[] down = new RTHandle[8], up = new RTHandle[8];
            int mipCount;
            static readonly int SourceTexelSize = Shader.PropertyToID("_BloomSourceTexelSize");
            static readonly int LowMip = Shader.PropertyToID("_BloomLowMip");
            static readonly int LowTexelSize = Shader.PropertyToID("_BloomLowTexelSize");

            public BloomPass(BloomFeature owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.BeforeUpscaling;
                requiresIntermediateTexture = true;
                profilingSampler = new ProfilingSampler("Bloom");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.bindMS = false;
                desc.useMipMap = desc.autoGenerateMips = desc.useDynamicScale = false;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                desc.width = Mathf.Max(1, (desc.width + 1) / 2);
                desc.height = Mathf.Max(1, (desc.height + 1) / 2);
                int limit = Mathf.Clamp(owner.settings.maxIterations, 1, down.Length);
                mipCount = 0;
                for (int i = 0; i < limit; ++i)
                {
                    RenderingUtils.ReAllocateIfNeeded(ref down[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_BloomDown" + i);
                    mipCount++;
                    if (desc.width == 1 && desc.height == 1) break;
                    desc.width = Mathf.Max(1, (desc.width + 1) / 2);
                    desc.height = Mathf.Max(1, (desc.height + 1) / 2);
                }
                for (int i = 0; i < mipCount - 1; ++i)
                    RenderingUtils.ReAllocateIfNeeded(ref up[i], down[i].rt.descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_BloomUp" + i);
                // Camera resize / reduced quality must release no-longer-used levels too.
                for (int i = mipCount; i < down.Length; ++i) { down[i]?.Release(); down[i] = null; }
                for (int i = mipCount - 1; i < up.Length; ++i) { up[i]?.Release(); up[i] = null; }
            }

            static Vector4 TexelSize(RTHandle texture) => new Vector4(1f / texture.rt.width, 1f / texture.rt.height,
                texture.rt.width, texture.rt.height);

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var color = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (color.rt == null) return;
                var s = owner.settings; var mat = owner.material;
                float threshold = Mathf.GammaToLinearSpace(Mathf.Max(0, s.threshold));
                mat.SetVector("_BloomParameters", new Vector4(threshold, threshold * Mathf.Clamp01(s.softKnee),
                    Mathf.Clamp(s.clamp, 0, 65472), Mathf.Lerp(0.05f, 0.95f, Mathf.Clamp01(s.scatter))));
                mat.SetColor("_BloomTint", s.tint.linear);
                mat.SetFloat("_BloomIntensity", Mathf.Max(0, s.intensity));
                owner.LastSourceSize = new Vector2Int(color.rt.width, color.rt.height);
                owner.LastMipCount = mipCount; owner.LastExecutionEvent = renderPassEvent;
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    cmd.SetGlobalVector(SourceTexelSize, TexelSize(color));
                    Blitter.BlitCameraTexture(cmd, color, down[0], mat, 0);
                    for (int i = 1; i < mipCount; ++i)
                    {
                        cmd.SetGlobalVector(SourceTexelSize, TexelSize(down[i - 1]));
                        Blitter.BlitCameraTexture(cmd, down[i - 1], down[i], mat, 1);
                    }
                    RTHandle bloom = down[mipCount - 1];
                    for (int i = mipCount - 2; i >= 0; --i)
                    {
                        cmd.SetGlobalTexture(LowMip, bloom);
                        cmd.SetGlobalVector(LowTexelSize, TexelSize(bloom));
                        Blitter.BlitCameraTexture(cmd, down[i], up[i], mat, 2);
                        bloom = up[i];
                    }
                    // No camera color feedback: the final pass only samples bloom and adds RGB.
                    Blitter.BlitCameraTexture(cmd, bloom, color, RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store, mat, 3);
                }
                context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                for (int i = 0; i < down.Length; ++i)
                {
                    down[i]?.Release(); up[i]?.Release(); down[i] = up[i] = null;
                }
            }
        }
    }
}
