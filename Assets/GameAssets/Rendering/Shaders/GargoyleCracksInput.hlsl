#ifndef GARGOYLE_CRACKS_INPUT_INCLUDED
#define GARGOYLE_CRACKS_INPUT_INCLUDED

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
    half4 _CrackColor;
    float _CrackProgress;
    float _CrackScale;
    float _CrackWidth;
    float _CrackDepth;
    float _UseSurfaceMask;
    half4 _InteriorColor;
    half _InteriorMetallic;
    half _InteriorSmoothness;
    float _VATEnabled;
    float _VATProgress;
    float _VATFrameCount;
    float4 _VATTextureSize;
    float4 _VATPositionMin;
    float4 _VATPositionRange;
CBUFFER_END

TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);

TEXTURE2D(_CrackMap); SAMPLER(sampler_CrackMap);

half3 GargoyleNormalTS(float2 uv)
{
    return UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
}

#endif
