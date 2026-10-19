#ifndef GOTHIC_CHAPEL_FLAME_INCLUDED
#define GOTHIC_CHAPEL_FLAME_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _OuterColor;
half4 _CoreColor;
float _Intensity;
float _FlickerAmount;
float _FlickerSpeed;
float _Sway;
float _SwaySpeed;
float _BakeIntensity;
float _AnimationTime;
float _FlickerOverride;
CBUFFER_END
struct FlameAttributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 uv1:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct FlameVaryings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 clock:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
float FlameHash(float n){return frac(sin(n*127.1)*43758.5453);}
float FlameNoise(float n){float f=frac(n);f=f*f*(3-2*f);return lerp(FlameHash(floor(n)),FlameHash(floor(n)+1),f);}
float FlameTime(){return _AnimationTime>=0?_AnimationTime:_Time.y;}
float FlameSeed(){return dot(TransformObjectToWorld(float3(0,0,0)),float3(13.13,7.71,19.19));}
float FlameFlicker(float t,float seed)
{
    if (_FlickerOverride>=0) return _FlickerOverride;
    float n=.64*FlameNoise(t*_FlickerSpeed+seed)+.36*FlameNoise(t*_FlickerSpeed*2.31+seed*3.7);
    return max(.15,1+(n-.5)*2*_FlickerAmount);
}
FlameVaryings FlameVertex(FlameAttributes v)
{
    FlameVaryings o=(FlameVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float t=FlameTime(),seed=FlameSeed(),h=v.uv.y;
    float3 origin=TransformObjectToWorld(float3(0,0,0));
    float3 right=UNITY_MATRIX_I_V._m00_m10_m20;right.y=0;right=normalize(right+float3(1e-5,0,0));
    float sway=_Sway*h*h*(.65*sin(t*_SwaySpeed+seed)+.35*sin(t*_SwaySpeed*2.13+seed*2-h*3));
    float stretch=1+.08*(FlameNoise(t*3.1+seed)-.5)*2;
    float2 objectScale=float2(length(GetObjectToWorldMatrix()._m00_m10_m20),length(GetObjectToWorldMatrix()._m01_m11_m21));
    float3 world=origin+right*(v.positionOS.x+sway)*objectScale.x+float3(0,v.positionOS.y*stretch*objectScale.y,0);
    o.positionCS=TransformWorldToHClip(world);o.uv=v.uv;o.clock=float2(t,seed);return o;
}
// Procedural teardrop: smooth edge, warm shell and a smaller bright core.
half4 FlameColor(float2 uv,float t,float seed,float intensity)
{
    float h=saturate(uv.y);
    float curve=(FlameNoise(t*4+seed+h*3)-.5)*.12*h*h;
    float x=abs(uv.x-.5+curve);
    float width=.46*pow(saturate(sin(h*PI)),.65)*(1-.53*h);
    float edge=max(fwidth(x-width)*1.5,.012);
    float alpha=(1-smoothstep(width-edge,width+edge,x))*smoothstep(0,.07,h)*(1-smoothstep(.94,1,h));
    float core=(1-smoothstep(width*.18,width*.7,x))*pow(1-h,1.2);
    half3 color=lerp(_OuterColor.rgb,_CoreColor.rgb,saturate(core*1.5));
    return half4(color*intensity*alpha,alpha);
}
half4 FlameFragment(FlameVaryings i):SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    return FlameColor(i.uv,i.clock.x,i.clock.y,_Intensity*FlameFlicker(i.clock.x,i.clock.y));
}
FlameVaryings FlameMetaVertex(FlameAttributes v)
{
    FlameVaryings o=(FlameVaryings)0;UNITY_SETUP_INSTANCE_ID(v);
    o.positionCS=UnityMetaVertexPosition(v.positionOS.xyz,v.uv1,v.uv1);o.uv=v.uv;return o;
}
half4 FlameMeta(FlameVaryings i):SV_Target
{
    half4 color=FlameColor(i.uv,0,0,_BakeIntensity);
    MetaInput m=(MetaInput)0;m.Albedo=0;m.Emission=color.rgb;
    return UnityMetaFragment(m);
}
#endif
