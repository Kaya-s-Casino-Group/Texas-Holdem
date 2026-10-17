using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>Owns the participating medium. Volumetric lighting adds scattering to this fog.</summary>
    [DisallowMultipleRendererFeature("Height Fog")]
    public sealed class HeightFogFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public bool applyToRenderTextures = true;
            public bool applyToSceneView;
            [Tooltip("雾保持完整密度的最高世界高度。")]
            public float baseHeight = 0;
            [Tooltip("雾密度减为零的世界高度，必须大于基础高度。")]
            public float maximumHeight = 1.5f;
            public HeightFogFalloff heightFalloff = HeightFogFalloff.Linear;
            [Tooltip("Custom 时生效。X：基础高度到最大高度（0–1），Y：密度倍率（限制 0–1）。端点固定为 (0,1) 与 (1,0)，保持高度边界连续。")]
            public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
            public bool enableGround;
            public float groundHeight = 0;
            [Min(0)] public float density = 0.15f;
            [Min(0.001f), Tooltip("完整密度为 1 时，透射率降至 1/e 的距离。")]
            public float attenuationDistance = 1;
            [Min(0)] public float distance = 4;
            [ColorUsage(false, true)] public Color color = new Color(0.12f, 0.14f, 0.17f);

            [Header("动态 3D 密度纹理（与体积光共用）")]
            public bool useNoiseTexture;
            [Tooltip("使用可平铺的线性 Texture3D，读取 R 通道；建议 32³ R8。")]
            public Texture3D noiseTexture;
            [Range(0, 1), Tooltip("密度乘以 lerp(1, 2 × R, Strength)，R=0.5 时密度不变。")]
            public float noiseStrength = 1;
            [Tooltip("纹理在世界空间重复一次的尺寸，各轴使用正数。")]
            public Vector3 noiseWorldSize = new Vector3(2, 1, 2);
            public Vector3 noiseOffset;
            [Tooltip("噪声移动速度，世界单位/秒；设为零可静止。")]
            public Vector3 noiseVelocity = new Vector3(.03f, 0, .015f);
            [Range(8, 128), Tooltip("纹理模式的共享积分步数；同时控制雾与体积光，使用固定中点采样。")]
            public int noiseSteps = 32;

            public bool NoiseEnabled => useNoiseTexture && noiseTexture != null && noiseStrength > 0;
            public Vector3 NoiseSize => new Vector3(Mathf.Max(.001f, Mathf.Abs(noiseWorldSize.x)),
                Mathf.Max(.001f, Mathf.Abs(noiseWorldSize.y)), Mathf.Max(.001f, Mathf.Abs(noiseWorldSize.z)));
            public Vector3 NoisePhase(double time)
            {
                var tile = NoiseSize;
                // Wrap in double precision on the CPU. Never quantize to a frame or regenerate the texture.
                float Phase(float offset, float velocity, float period)
                {
                    double value = -((double)offset + velocity * time) / period;
                    return (float)(value - Math.Floor(value));
                }
                return new Vector3(Phase(noiseOffset.x, noiseVelocity.x, tile.x),
                    Phase(noiseOffset.y, noiseVelocity.y, tile.y), Phase(noiseOffset.z, noiseVelocity.z, tile.z));
            }

            public bool IsValid => density > 0 && distance > 0 && attenuationDistance > 0 &&
                maximumHeight > baseHeight && (!enableGround || groundHeight < maximumHeight);
        }

        public Settings settings = new Settings();
        [SerializeField] Shader fogShader;
        public Shader FogShader { get => fogShader; set => fogShader = value; }
        public Vector2Int LastSourceSize { get; private set; }
        public RenderPassEvent LastExecutionEvent { get; private set; }
        Material material;
        readonly HeightFogCurveLut curveLut = new HeightFogCurveLut();
        FogPass pass;
        ScriptableRenderer scheduledRenderer;
        Camera scheduledCamera;
        bool sharedNoisePass;
        Vector3 noisePhase;
        public bool LastUsedSharedNoisePass { get; private set; }

        public override void Create()
        {
            curveLut.Dispose();
            CoreUtils.Destroy(material);
            material = fogShader != null ? CoreUtils.CreateEngineMaterial(fogShader) : null;
            pass = new FogPass(this);
            ClearSchedule();
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData) => ClearSchedule();

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            ClearSchedule();
            if (!isActive || material == null || !settings.IsValid || !Applies(in renderingData.cameraData)) return;
            scheduledRenderer = renderer;
            scheduledCamera = renderingData.cameraData.camera;
            double time = Time.timeAsDouble;
            #if UNITY_EDITOR
            if (!Application.isPlaying) time = UnityEditor.EditorApplication.timeSinceStartup;
            #endif
            noisePhase = settings.NoisePhase(time);
            renderer.EnqueuePass(pass);
        }

        internal void ShareNoisePass(ScriptableRenderer renderer, Camera camera)
        {
            if (settings.NoiseEnabled && ScheduledFor(renderer, camera)) sharedNoisePass = true;
        }

        internal bool ScheduledFor(ScriptableRenderer renderer, Camera camera) => isActive && material != null &&
            settings.IsValid && scheduledRenderer == renderer && scheduledCamera == camera;

        internal bool Applies(in CameraData data)
        {
            if (data.camera == null || data.camera.stereoEnabled || data.renderType == CameraRenderType.Overlay) return false;
            if (data.cameraType != CameraType.Game && !(settings.applyToSceneView && data.isSceneViewCamera)) return false;
            if (data.targetTexture != null && !settings.applyToRenderTextures) return false;
            var desc = data.cameraTargetDescriptor;
            return desc.dimension == TextureDimension.Tex2D && desc.colorFormat != RenderTextureFormat.Depth &&
                desc.colorFormat != RenderTextureFormat.Shadowmap;
        }

        internal void SetMedium(Material target)
        {
            var s = settings;
            bool useCurve = s.heightFalloff != HeightFogFalloff.Linear;
            if (useCurve) curveLut.Update(s.heightFalloff, s.heightCurve);
            target.SetVector("_HeightFogCurveInfo", new Vector4(useCurve ? 1 : 0, HeightFogCurveLut.Segments, 0, 0));
            target.SetTexture("_HeightFogCurveLut", curveLut.Texture);
            target.SetVector("_HeightFogHeights", new Vector4(s.baseHeight, s.maximumHeight, s.groundHeight, s.enableGround ? 1 : 0));
            target.SetVector("_HeightFogMedium", new Vector4(Mathf.Max(0, s.density),
                1 / Mathf.Max(0.001f, s.attenuationDistance), Mathf.Max(0, s.distance), 0));
            target.SetColor("_HeightFogColor", s.color);
            target.SetVector("_HeightFogNoise", new Vector4(s.NoiseEnabled ? Mathf.Clamp01(s.noiseStrength) : 0,
                Mathf.Clamp(s.noiseSteps, 8, 128), 0, 0));
            var tile = s.NoiseSize;
            target.SetVector("_HeightFogNoiseScale", new Vector4(1 / tile.x, 1 / tile.y, 1 / tile.z, 0));
            target.SetVector("_HeightFogNoisePhase", noisePhase);
            target.SetTexture("_HeightFogNoiseTexture", s.noiseTexture);
        }

        void ClearSchedule() { scheduledRenderer = null; scheduledCamera = null; sharedNoisePass = false; }
        protected override void Dispose(bool disposing) { curveLut.Dispose(); CoreUtils.Destroy(material); material = null; ClearSchedule(); }

        sealed class FogPass : ScriptableRenderPass
        {
            readonly HeightFogFeature owner;
            public FogPass(HeightFogFeature owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.BeforeUpscaling;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                profilingSampler = new ProfilingSampler("Height Fog");
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) => ResetTarget();

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var color = renderingData.cameraData.renderer.cameraColorTargetHandle;
                if (color.rt == null) return;
                owner.LastSourceSize = new Vector2Int(color.rt.width, color.rt.height);
                owner.LastExecutionEvent = renderPassEvent;
                owner.LastUsedSharedNoisePass = owner.sharedNoisePass;
                // The following volumetric pass integrates both T and radiance and composites once.
                if (owner.sharedNoisePass) return;
                owner.SetMedium(owner.material);
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // Shader only reads depth. Hardware blending applies extinction exactly once.
                    Blitter.BlitCameraTexture(cmd, color, color, RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store, owner.material, 0);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd) => owner.ClearSchedule();
        }
    }
}
