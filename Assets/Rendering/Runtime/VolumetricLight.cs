using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace RetroRendering
{
    /// <summary>Opts an additional light into volumetric scattering. Main light uses feature settings.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Light))]
    [AddComponentMenu("Rendering/Volumetric Light")]
    [MovedFrom(true, "RetroRendering", "RetroRendering.Runtime", "HeightFogLight")]
    public sealed class VolumetricLight : MonoBehaviour
    {
        [Min(0)] public float scattering = 0.1f;
        [Range(-0.95f, 0.95f)] public float anisotropy = 0.3f;
        [Min(0.001f), Tooltip("抑制点光源或聚光灯中心的过曝。世界单位。")]
        public float radius = 0.04f;
    }
}
