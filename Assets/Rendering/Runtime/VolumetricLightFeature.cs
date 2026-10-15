using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>Ray-marched single scattering in the medium owned by HeightFogFeature.</summary>
    [DisallowMultipleRendererFeature("Volumetric Light")]
    [MovedFrom(true, "RetroRendering", "RetroRendering.Runtime", "HeightFogVolumetricLightFeature")]
    public sealed class VolumetricLightFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public bool mainLight = true;
            [Min(0)] public float mainScattering = 0.2f;
            [Range(-0.95f, 0.95f)] public float mainAnisotropy = 0.3f;
            public bool additionalLights = true;
            [Range(1, 64)] public int maxAdditionalLights = 16;
            [Range(8, 128)] public int steps = 48;
            [Range(0, 3)] public int blurIterations = 1;
            [Min(0.001f), Tooltip("滤波允许的深度差，世界单位。")]
            public float depthTolerance = 0.03f;
            [Range(0, 1), Tooltip("固定屏幕噪声，无时间闪烁。0 为中点采样。")]
            public float jitter = 0.8f;
            [ColorUsage(false, true)] public Color tint = Color.white;
        }

        [Tooltip("必须引用同一 Renderer 中排在本 Feature 前面的高度雾。")]
        public HeightFogFeature heightFog;
        public Settings settings = new Settings();
        [SerializeField] Shader scatteringShader;
        public Shader ScatteringShader { get => scatteringShader; set => scatteringShader = value; }
        public Vector2Int LastSourceSize { get; private set; }
        public RenderPassEvent LastExecutionEvent { get; private set; }
        Material material;
        ScatteringPass pass;
        readonly Vector4[] lightParameters = new Vector4[64];

        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = scatteringShader != null ? CoreUtils.CreateEngineMaterial(scatteringShader) : null;
            pass = new ScatteringPass(this);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!isActive || material == null || heightFog == null ||
                !heightFog.ScheduledFor(renderer, renderingData.cameraData.camera)) return;
            if (PrepareLights(ref renderingData) == 0) return;
            heightFog.ShareNoisePass(renderer, renderingData.cameraData.camera);
            renderer.EnqueuePass(pass);
        }

        // Match URP's packed global light buffer, not unity_LightIndices (a mesh's per-object list).
        int PrepareLights(ref RenderingData data)
        {
            var s = settings;
            bool main = s.mainLight && s.mainScattering > 0 && data.lightData.mainLightIndex >= 0;
            int count = 0, slot = 0;
            int limit = Mathf.Clamp(s.maxAdditionalLights, 1, lightParameters.Length);
            int available = Mathf.Min(data.lightData.additionalLightsCount, UniversalRenderPipeline.maxVisibleAdditionalLights);
            if (s.additionalLights)
                for (int i = 0; i < data.lightData.visibleLights.Length && slot < available && count < limit; ++i)
                {
                    if (i == data.lightData.mainLightIndex) continue;
                    int lightIndex = slot++;
                    var light = data.lightData.visibleLights[i].light;
                    if (light == null) continue;
#if UNITY_EDITOR
                    if (light.lightmapBakeType == LightmapBakeType.Baked) continue;
#else
                    if (light.bakingOutput.lightmapBakeType == LightmapBakeType.Baked) continue;
#endif
                    if (!light.TryGetComponent<VolumetricLight>(out var custom) ||
                        !custom.isActiveAndEnabled || custom.scattering <= 0) continue;
                    lightParameters[count++] = new Vector4(lightIndex, custom.scattering,
                        Mathf.Clamp(custom.anisotropy, -0.95f, 0.95f), Mathf.Max(0.000001f, custom.radius * custom.radius));
                }
            material.SetVector("_FogMainLight", new Vector4(main ? s.mainScattering : 0,
                Mathf.Clamp(s.mainAnisotropy, -0.95f, 0.95f), data.shadowData.mainLightShadowCascadesCount, 0));
            material.SetInt("_FogLightCount", count);
            material.SetVectorArray("_FogLights", lightParameters);
            return count + (main ? 1 : 0);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose(); CoreUtils.Destroy(material); material = null;
        }

        sealed class ScatteringPass : ScriptableRenderPass
        {
            readonly VolumetricLightFeature owner;
            RTHandle scattering, temporary;
            public ScatteringPass(VolumetricLightFeature owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.BeforeUpscaling;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                profilingSampler = new ProfilingSampler("Volumetric Light");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.bindMS = false;
                desc.useMipMap = desc.autoGenerateMips = desc.useDynamicScale = false;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                RenderingUtils.ReAllocateIfNeeded(ref scattering, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_VolumetricScattering");
                RenderingUtils.ReAllocateIfNeeded(ref temporary, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_VolumetricScatteringBlur");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var color = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (color.rt == null) return;
                var s = owner.settings;
                var mat = owner.material;
                owner.heightFog.SetMedium(mat);
                // Refresh here too: separate cameras must never reuse a previous camera's culled lights.
                owner.PrepareLights(ref renderingData);
                mat.SetVector("_FogSampling", new Vector4(Mathf.Clamp(s.steps, 8, 128),
                    Mathf.Clamp01(s.jitter), Mathf.Max(0.001f, s.depthTolerance), 0));
                mat.SetColor("_FogLightTint", s.tint);
                mat.SetVector("_FogTexelSize", new Vector4(1f / scattering.rt.width, 1f / scattering.rt.height,
                    scattering.rt.width, scattering.rt.height));
                owner.LastSourceSize = new Vector2Int(scattering.rt.width, scattering.rt.height);
                owner.LastExecutionEvent = renderPassEvent;
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Blitter.BlitCameraTexture(cmd, color, scattering, mat, 0);
                    for (int i = 0; i < Mathf.Clamp(s.blurIterations, 0, 3); ++i)
                    {
                        Blitter.BlitCameraTexture(cmd, scattering, temporary, mat, 1);
                        Blitter.BlitCameraTexture(cmd, temporary, scattering, mat, 2);
                    }
                    // Noise mode carries T in alpha and composites fog + scattering together.
                    // Analytic mode retains the separate exact-extinction pass and additive scattering.
                    Blitter.BlitCameraTexture(cmd, scattering, color, RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store, mat, owner.heightFog.settings.NoiseEnabled ? 4 : 3);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose() { scattering?.Release(); temporary?.Release(); scattering = temporary = null; }
        }
    }
}
