using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>Independent retro color and lens effects at the renderer-provided resolution.</summary>
    public sealed class RetroPostProcessFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public bool applyToRenderTextures = true;
            public bool applyToSceneView;
            [Range(-2, 2)] public float exposure = 0;
            [Range(0, 2)] public float contrast = 1.08f;
            [Range(0, 2)] public float saturation = 0.88f;
            [ColorUsage(false)] public Color tint = new Color(1, 0.97f, 0.9f);
            [Range(0, 1)] public float tintStrength = 0.16f;
            [Range(0, 1)] public float vignette = 0.22f;
            [Range(0.05f, 1)] public float vignetteSoftness = 0.6f;
            [Range(2, 256), Tooltip("Display-space levels per channel, not a fixed palette.")]
            public int colorLevels = 64;
            [Range(0, 1)] public float posterization = 0.2f;
            [Range(0, 1)] public float dither = 0.25f;
            [Range(0, 5), Tooltip("Red/blue separation measured in internal pixels. Default off.")]
            public float chromaticOffset;
        }

        public Settings settings = new Settings();
        [SerializeField] RenderPassEvent injectionPoint = RenderPassEvent.BeforeUpscaling;
        public RenderPassEvent InjectionPoint { get => injectionPoint; set => injectionPoint = value; }
        public Vector2Int LastSourceSize { get; private set; }
        public RenderPassEvent LastExecutionEvent { get; private set; }
        [SerializeField] Shader postShader;
        public Shader PostShader { get => postShader; set => postShader = value; }
        Material material;
        RetroPass pass;

        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = postShader != null ? CoreUtils.CreateEngineMaterial(postShader) : null;
            pass = new RetroPass(this);
        }

        bool Applies(in CameraData data)
        {
            // XR has its own eye texture sizing/projection. This feature targets ordinary platform displays.
            if (material == null || data.camera == null || data.camera.stereoEnabled) return false;
            if (data.cameraType != CameraType.Game && !(settings.applyToSceneView && data.isSceneViewCamera)) return false;
            if (data.targetTexture != null && !settings.applyToRenderTextures) return false;
            return data.cameraTargetDescriptor.dimension == TextureDimension.Tex2D &&
                   data.cameraTargetDescriptor.colorFormat != RenderTextureFormat.Depth &&
                   data.cameraTargetDescriptor.colorFormat != RenderTextureFormat.Shadowmap;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Grade the scene before a UI-only overlay. A Base camera need not resolve the final stack target.
            if (!Applies(in renderingData.cameraData) || renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
            pass.renderPassEvent = injectionPoint;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class RetroPass : ScriptableRenderPass
        {
            readonly RetroPostProcessFeature owner;
            RTHandle temporary;
            public RetroPass(RetroPostProcessFeature owner)
            {
                this.owner = owner;
                requiresIntermediateTexture = true;
                renderPassEvent = owner.injectionPoint;
                profilingSampler = new ProfilingSampler("Retro post effects");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                desc.bindMS = false;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                RenderingUtils.ReAllocateIfNeeded(ref temporary, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_RetroPostColor");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var data = renderingData.cameraData;
                var source = data.renderer.cameraColorTargetHandle;
                if (source.rt == null) return;
                owner.LastSourceSize = new Vector2Int(source.rt.width, source.rt.height);
                owner.LastExecutionEvent = renderPassEvent;
                var s = owner.settings;
                var mat = owner.material;
                mat.SetVector("_SourceTexelSize", new Vector4(1f / source.rt.width, 1f / source.rt.height, source.rt.width, source.rt.height));
                mat.SetVector("_Grade", new Vector4(s.exposure, s.contrast, s.saturation, s.tintStrength));
                mat.SetColor("_Tint", s.tint);
                mat.SetVector("_Style", new Vector4(Mathf.Clamp(s.colorLevels, 2, 256), s.posterization, s.dither, 0));
                mat.SetVector("_Lens", new Vector4(s.vignette, s.vignetteSoftness, Mathf.Clamp(s.chromaticOffset, 0, 5), 0));
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Blitter.BlitCameraTexture(cmd, source, temporary, mat, 0);
                    Blitter.BlitCameraTexture(cmd, temporary, source, 0, false);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose() => temporary?.Release();
        }
    }
}
