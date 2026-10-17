using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;

namespace RetroRendering
{
    /// <summary>Fixed URP renderer settings for the small, unlit 2D validation scene.</summary>
    [CreateAssetMenu(menuName = "渲染/简易 2D 前向 Renderer", fileName = "Battle2DRenderer")]
    [MovedFrom(true, "RetroRendering", "RetroRendering.Runtime", "Simple2DRendererData")]
    public sealed class Simple2DRendererData : ScriptableRendererData
    {
        protected override ScriptableRenderer Create()
        {
            // URP 14's base renderer constructs its editor DebugHandler from this container.
            if (debugShaders == null) debugShaders = new DebugShaderResources();
            return new Simple2DRenderer(this);
        }
    }
}
