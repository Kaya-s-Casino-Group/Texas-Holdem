#ifndef CHARACTER_DEFERRED_INPUT_INCLUDED
#define CHARACTER_DEFERRED_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

// One identical material buffer across GBuffer, depth, shadows and outline.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _BumpScale;
    half _Metallic;
    half _Roughness;
    half _OcclusionStrength;
    float _OutlineWidth;
    half4 _OutlineColor;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_MetallicMap); SAMPLER(sampler_MetallicMap);
TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
#endif
