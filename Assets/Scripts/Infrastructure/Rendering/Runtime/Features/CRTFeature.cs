using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering
{
    /// <summary>Final camera-stack presentation: scene, outlines and UI share one CRT screen.</summary>
    public sealed class CRTFeature : ScriptableRendererFeature, ICameraStackOutputFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public bool applyToRenderTextures = true;
            public bool applyToSceneView;
            public bool crtEnabled = true;
            [Range(0,1)] public float crtIntensity = .35f;
            [Range(0,.15f)] public float crtCurvature = .025f;
            [Range(2,12), Tooltip("Output pixels per scanline period.")] public float crtScanlinePitch = 4;
            [Range(0,1)] public float crtScanlineStrength = .3f;
            [Range(0,1)] public float crtMaskStrength = .2f;
            [Range(.001f,.1f)] public float crtEdgeSoftness = .015f;
        }
        public Settings settings = new Settings();
        [SerializeField] Shader crtShader;
        public Shader CRTShader { get => crtShader; set => crtShader = value; }
        public Vector2Int LastCRTSize { get; private set; }
        public int LastPresentedCamera { get; private set; }
        Material material;
        RTHandle stackColor, output;
        PresentPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = crtShader ? CoreUtils.CreateEngineMaterial(crtShader) : null;
            pass = new PresentPass(this);
        }
        public RenderTexture PrepareStackOutput(in CameraData data)
        {
            if (!material || !settings.crtEnabled || settings.crtIntensity <= 0 || data.camera == null || data.camera.stereoEnabled || data.isHDROutputActive) return null;
            if (data.cameraType != CameraType.Game && !(settings.applyToSceneView && data.isSceneViewCamera)) return null;
            if (data.targetTexture != null && !settings.applyToRenderTextures) return null;
            var desc = data.cameraTargetDescriptor;
            if (desc.dimension != TextureDimension.Tex2D || desc.colorFormat == RenderTextureFormat.Depth || desc.colorFormat == RenderTextureFormat.Shadowmap) return null;
            var rect = data.camera.pixelRect;
            if (rect.width < 1 || rect.height < 1) return null;
            desc.width = data.targetTexture ? data.targetTexture.width : Mathf.CeilToInt(rect.xMax);
            desc.height = data.targetTexture ? data.targetTexture.height : Mathf.CeilToInt(rect.yMax);
            desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            desc.depthBufferBits = 0; desc.msaaSamples = 1; desc.bindMS = false;
            desc.useMipMap = desc.autoGenerateMips = desc.useDynamicScale = false;
            RenderingUtils.ReAllocateIfNeeded(ref stackColor,desc,FilterMode.Point,TextureWrapMode.Clamp,name:"_CRTStackColor");
            LastCRTSize = new Vector2Int(desc.width,desc.height);
            return stackColor.rt;
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var data = renderingData.cameraData;
            if (!ReferenceEquals(data.stackOutputFeature,this) || !data.isFinalStackCamera || data.stackOutputTexture == null) return;
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing)
        {
            stackColor?.Release(); stackColor = null;
            output?.Release(); output = null;
            CoreUtils.Destroy(material); material = null;
        }
        sealed class PresentPass : ScriptableRenderPass
        {
            readonly CRTFeature owner;
            public PresentPass(CRTFeature owner)
            {
                this.owner = owner;
                // Includes both camera-space UI and Screen Space Overlay canvases.
                renderPassEvent = RenderPassEvent.AfterRenderingUI;
                profilingSampler = new ProfilingSampler("CRT final camera stack presentation");
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) => ResetTarget();
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var data = renderingData.cameraData;
                RenderTargetIdentifier target = data.targetTexture ? new RenderTargetIdentifier(data.targetTexture) : BuiltinRenderTextureType.CameraTarget;
                if (owner.output == null || owner.output.nameID != target)
                {
                    owner.output?.Release(); owner.output = RTHandles.Alloc(target);
                }
                var rect = data.camera.pixelRect;
                var s = owner.settings;
                var mat = owner.material;
                mat.SetVector("_CRTShape",new Vector4(s.crtCurvature,s.crtEdgeSoftness,s.crtIntensity,0));
                mat.SetVector("_CRTMask",new Vector4(s.crtScanlinePitch,s.crtScanlineStrength,s.crtMaskStrength,0));
                mat.SetVector("_CRTOutputSize",new Vector4(rect.width,rect.height,1f/rect.width,1f/rect.height));
                mat.SetVector("_CRTViewport",new Vector4(rect.width/owner.stackColor.rt.width,rect.height/owner.stackColor.rt.height,rect.x/owner.stackColor.rt.width,rect.y/owner.stackColor.rt.height));
                mat.SetFloat("_CRTEncodeSRGB",data.targetTexture == null && !data.isSceneViewCamera && Display.main.requiresSrgbBlitToBackbuffer ? 1 : 0);
                bool flip = !data.isSceneViewCamera && data.targetTexture == null && SystemInfo.graphicsUVStartsAtTop;
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd,profilingSampler))
                {
                    CoreUtils.SetRenderTarget(cmd,owner.output,RenderBufferLoadAction.Load,RenderBufferStoreAction.Store,ClearFlag.None,Color.clear);
                    cmd.SetViewport(rect);
                    Blitter.BlitTexture(cmd,owner.stackColor,flip ? new Vector4(1,-1,0,1) : new Vector4(1,1,0,0),mat,0);
                }
                context.ExecuteCommandBuffer(cmd); CommandBufferPool.Release(cmd);
                owner.LastPresentedCamera = data.camera.GetInstanceID();
                renderingData.cameraData.CompleteStackOutput();
                data.renderer.ConfigureCameraTarget(owner.output,owner.output);
            }
        }
    }
}
