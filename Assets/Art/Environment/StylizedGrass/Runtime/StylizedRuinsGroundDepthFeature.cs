using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StylizedGrass
{
    /// <summary>Writes depth only for the opaque core of painted terrain; its soft edge remains transparent.</summary>
    public sealed class StylizedRuinsGroundDepthFeature : ScriptableRendererFeature
    {
        GroundDepthPass pass;
        public override void Create(){pass=new GroundDepthPass();}
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(data.cameraData.cameraType!=CameraType.Preview)renderer.EnqueuePass(pass);
        }
        sealed class GroundDepthPass:ScriptableRenderPass
        {
            static readonly ShaderTagId Tag=new ShaderTagId("StylizedRuinsGroundDepth");
            public GroundDepthPass(){renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents-1);profilingSampler=new ProfilingSampler("Painted Ground Solid Depth");}
            public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData data)
            {
                var renderer=data.cameraData.renderer;
                ConfigureTarget(renderer.cameraColorTargetHandle,renderer.cameraDepthTargetHandle);
            }
            public override void Execute(ScriptableRenderContext context,ref RenderingData data)
            {
                var drawing=CreateDrawingSettings(Tag,ref data,SortingCriteria.CommonOpaque);
                var filtering=new FilteringSettings(RenderQueueRange.transparent);
                context.DrawRenderers(data.cullResults,ref drawing,ref filtering);
            }
        }
    }
}
