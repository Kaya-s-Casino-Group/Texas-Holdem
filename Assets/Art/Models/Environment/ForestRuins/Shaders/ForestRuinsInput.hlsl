#ifndef FOREST_RUINS_INPUT_INCLUDED
#define FOREST_RUINS_INPUT_INCLUDED
#define _SPECULAR_SETUP 1
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half _BumpScale;
half _Smoothness;
half _Cutoff;
half _VertexTone;
half _PaintFill;
half _BakeEmission;
float _WindStrength;
float _WindSpeed;
half _Cull;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
half4 FRSample(float2 uv, half tone)
{
    half4 c = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv) * _BaseColor;
    clip(c.a - _Cutoff);
    c.rgb *= lerp(1.0h, tone * 2.0h, _VertexTone);
    return c;
}
float3 FRWind(float3 p)
{
    float h = max(0.0,p.y);
    float sway = h*h*_WindStrength*sin(_Time.y*_WindSpeed*0.7853981634+p.x);
    p.x += sway;
    p.z += sway*0.38;
    return p;
}
#endif
