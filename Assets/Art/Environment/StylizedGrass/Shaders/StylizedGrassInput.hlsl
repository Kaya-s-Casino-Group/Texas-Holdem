#ifndef STYLIZED_GRASS_INPUT
#define STYLIZED_GRASS_INPUT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _RootColor, _TipColor, _DryColor, _GrassColor, _DirtColor;
float4 _WindDirection;
float _WindStrength, _WindSpeed, _WindScale, _WindTime;
float _Lodging, _LodgingDirection, _LodgingVariation, _LodgingPatchSize;
float _GrassTiling, _SoilTiling, _BlendSharpness, _NormalStrength;
half _Smoothness, _ColorVariation, _RootOcclusion, _NormalUp, _Fill;
half _Cull;
CBUFFER_END
TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
TEXTURE2D(_GrassHeight); SAMPLER(sampler_GrassHeight);
TEXTURE2D(_GrassNormal); SAMPLER(sampler_GrassNormal);
float SGNoise(float2 p)
{
    float2 i=floor(p),f=frac(p); f=f*f*(3-2*f);
    float4 h=frac(sin(float4(dot(i,float2(127.1,311.7)),dot(i+float2(1,0),float2(127.1,311.7)),
        dot(i+float2(0,1),float2(127.1,311.7)),dot(i+1,float2(127.1,311.7))))*43758.5453);
    return lerp(lerp(h.x,h.y,f.x),lerp(h.z,h.w,f.x),f.y);
}
float3 SGRotateBend(float3 v, float3 axis, float sine, float cosine)
{
    return v*cosine+cross(axis,v)*sine+axis*dot(axis,v)*(1-cosine);
}
float3 SGDeform(float3 positionWS, half rootWeight, float3 pivotWS, inout half3 normalWS)
{
    #if defined(SG_GRASS)
    float lodging=0;
    if(_Lodging>0)
    {
        // World-space patches remain still while the wind moves; both LODs share them.
        float patch=SGNoise(pivotWS.xz/max(_LodgingPatchSize,.5)+float2(19.7,7.3));
        lodging=saturate(_Lodging)*lerp(1,smoothstep(.22,.78,patch),saturate(_LodgingVariation));
        float heading=radians(_LodgingDirection)+(patch-.5)*1.2*_LodgingVariation;
        float3 upVector=mul((float3x3)GetObjectToWorldMatrix(),float3(0,1,0));
        float height=max(length(upVector),.0001);
        float3 up=upVector/height;
        float3 direction=float3(cos(heading),0,sin(heading));
        direction=SafeNormalize(direction-up*dot(direction,up));
        float3 axis=SafeNormalize(cross(up,direction));
        // A circular arc lowers the tips without stretching the blade or moving its root.
        float curvature=lodging*2.6;
        if(curvature>.0001)
        {
            float h=saturate(rootWeight);
            float sine,cosine;sincos(curvature*h,sine,cosine);
            float3 side=positionWS-pivotWS-up*(height*h);
            float3 center=height/curvature*(up*sine+direction*(1-cosine));
            positionWS=pivotWS+center+SGRotateBend(side,axis,sine,cosine);
            normalWS=normalize(SGRotateBend(normalWS,axis,sine,cosine));
        }
    }
    float t=(_WindTime<0?_Time.y:_WindTime)*_WindSpeed;
    float2 dir=normalize(_WindDirection.xz+float2(.0001,0));
    float wave=sin(dot(pivotWS.xz,dir)*_WindScale-t*1.7);
    float gust=SGNoise(pivotWS.xz*.35+dir*t*.2);
    float flutter=sin(t*3.1+pivotWS.x*2.3+pivotWS.z*1.7+positionWS.y*3);
    float weight=rootWeight*rootWeight*lerp(1,.25,lodging);
    positionWS.xz+=dir*(wave*.65+gust*.5+flutter*.15)*_WindStrength*weight;
    positionWS.y-=abs(wave)*_WindStrength*.12*weight;
    #endif
    return positionWS;
}
#endif
