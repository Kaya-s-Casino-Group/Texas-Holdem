// Gothic Chapel: adapted from the project Forest Ruins deferred/lightmap shader.
#ifndef GOTHIC_CHAPEL_INPUT_INCLUDED
#define GOTHIC_CHAPEL_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST;
half4 _BaseColor;
half _BumpScale;
half _Smoothness;
half _Metallic;
half4 _AtmosphereColor;
half _AtmosphereBlend;
half _ExcludeFromGI;
half _Cutoff;
half _VertexTone;
half _Unlit;
half _BakeEmission;
float _WindStrength;
float _WindSpeed;
half _Cull;
float _BannerAmplitude;
float _BannerSpeed;
float _BannerFrequency;
float _AnimationTime;
CBUFFER_END
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
half4 GCSample(float2 uv, half tone)
{
    half4 c = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
    c.rgb=lerp(c.rgb,_AtmosphereColor.rgb,_AtmosphereBlend);
    c *= _BaseColor;
    clip(c.a - _Cutoff);
    c.rgb *= lerp(1.0h, tone * 2.0h, _VertexTone);
    return c;
}
float3 GCWind(float3 p)
{
    float h = max(0.0,p.y);
    float sway = h*h*_WindStrength*sin(_Time.y*_WindSpeed*0.7853981634+p.x);
    p.x += sway;
    p.z += sway*0.38;
    return p;
}
#ifdef GC_ANIMATED_BANNER
// COLOR: R=distance down the cloth, G=inverse height, BA=horizontal direction.
// Direction is constant per mesh so split front/back normals cannot separate the cloth.
void GCBanner(inout float3 p,inout float3 n,inout float4 tangent,float4 mask)
{
    float d=saturate(mask.r);
    float t=(_AnimationTime>=0 ? _AnimationTime : _Time.y)*_BannerSpeed;
    float3 origin=TransformObjectToWorld(float3(0,0,0));
    float phase=dot(origin.xz,float2(1.37,2.17));
    float width=dot(p.xz,float2(-mask.a,mask.b))*3;
    float a=t*1.6-d*_BannerFrequency+phase+width*1.3;
    float b=t*2.73-d*(_BannerFrequency*1.71)+phase*1.8-width*2.1;
    float wave=.72*sin(a)+.28*sin(b);
    float offset=_BannerAmplitude*d*d*wave;
    float derivative=_BannerAmplitude*(2*d*wave-d*d*_BannerFrequency*(.72*cos(a)+.4788*cos(b)));
    float3 direction=normalize(float3(mask.b,0,mask.a));
    // A small positive lift keeps wall-mounted cloth outside its rest surface.
    p+=direction*(offset+_BannerAmplitude*d*d*.8);
    float3 upGradient=float3(0,-mask.g,0);
    float3 gradient=upGradient*(derivative+_BannerAmplitude*d*1.6);
    n=normalize(n-gradient*dot(n,direction)/(1+dot(gradient,direction)));
    tangent.xyz=normalize(tangent.xyz+direction*dot(gradient,tangent.xyz));
}
#endif
#endif
