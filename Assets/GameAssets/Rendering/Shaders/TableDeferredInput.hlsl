#ifndef TABLE_DEFERRED_INPUT_INCLUDED
#define TABLE_DEFERRED_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// Identical layout in every pass keeps the material compatible with the SRP Batcher.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _BumpScale;
    half _Metallic;
    half _Smoothness;
CBUFFER_END

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);

half3 TableNormalTS(float2 uv)
{
    return UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
}

#endif
