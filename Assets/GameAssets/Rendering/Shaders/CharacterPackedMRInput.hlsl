#ifndef CHARACTER_PACKED_MR_INPUT_INCLUDED
#define CHARACTER_PACKED_MR_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// One identical material buffer across GBuffer, depth and shadows.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _BumpScale;
    half _Metallic;
    half _Roughness;
    half _Cull;
    half _OcclusionStrength;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_PackedMRMap); SAMPLER(sampler_PackedMRMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
#endif
