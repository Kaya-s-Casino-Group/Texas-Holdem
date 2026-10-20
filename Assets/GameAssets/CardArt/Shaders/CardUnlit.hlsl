#ifndef CARD_UNLIT_INCLUDED
#define CARD_UNLIT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "CardSurfaceCommon.hlsl"
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_FoilMap); SAMPLER(sampler_FoilMap);
TEXTURE2D(_SealMap); SAMPLER(sampler_SealMap);
TEXTURE2D(_RedCrossMap); SAMPLER(sampler_RedCrossMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_TexelSize, _BackUVRect, _SealRect;
half4 _BaseColor, _EdgeColor;
float _FoilSpeed, _SealRotation;
half _Cutoff;
CBUFFER_END
UNITY_INSTANCING_BUFFER_START(CardInstances)
UNITY_DEFINE_INSTANCED_PROP(float4, _FrontUVRect)
UNITY_DEFINE_INSTANCED_PROP(float4, _ColorTint)
UNITY_DEFINE_INSTANCED_PROP(float, _FoilEnabled)
UNITY_DEFINE_INSTANCED_PROP(float4, _FoilColor)
UNITY_DEFINE_INSTANCED_PROP(float, _FoilStrength)
UNITY_DEFINE_INSTANCED_PROP(float, _SealEnabled)
UNITY_DEFINE_INSTANCED_PROP(float4, _SealColor)
UNITY_DEFINE_INSTANCED_PROP(float, _SealEmissionStrength)
UNITY_DEFINE_INSTANCED_PROP(float, _RedCrossEnabled)
UNITY_INSTANCING_BUFFER_END(CardInstances)
struct Attributes
{
    float4 positionOS : POSITION;
    float2 localUV : TEXCOORD0;
    float2 surface : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 localUV : TEXCOORD0;
    float2 surface : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
Varyings CardVertex(Attributes input)
{
    Varyings o=(Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input,o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
    o.localUV=input.localUV;o.surface=input.surface;
    return o;
}
half4 CardBase(Varyings input)
{
    float4 rect=input.surface.x>.5 ? _BackUVRect : UNITY_ACCESS_INSTANCED_PROP(CardInstances,_FrontUVRect);
    float2 uv=CardAtlasSampleUV(input.localUV,rect,_BaseMap_TexelSize.xy);
    half4 color=input.surface.y>.5 ? _EdgeColor : SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
    color*=_BaseColor*UNITY_ACCESS_INSTANCED_PROP(CardInstances,_ColorTint);
    clip(color.a-_Cutoff);return color;
}
half4 CardColor(Varyings input):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 color=CardBase(input);
    if(input.surface.x>.5||input.surface.y>.5)return color;
    float3 emission=0;
    float strength=clamp(UNITY_ACCESS_INSTANCED_PROP(CardInstances,_FoilStrength),0,5);
    if(UNITY_ACCESS_INSTANCED_PROP(CardInstances,_FoilEnabled)>.5&&strength>0)
    {
        float pattern=SAMPLE_TEXTURE2D(_FoilMap,sampler_FoilMap,input.localUV*float2(2,3)).r;
        float3 foil;float amount;
        // UV/time-driven shimmer: no mesh normals, lighting or view-dependent shading.
        CardFoilSpectrum(input.localUV,0,1,pattern,_Time.y,_FoilSpeed,strength,color.rgb,
            UNITY_ACCESS_INSTANCED_PROP(CardInstances,_FoilColor).rgb,foil,amount);
        color.rgb*=lerp(1.0.xxx,.2+foil*1.1,saturate(amount*.65));emission=foil*amount*.3;
    }
    if(UNITY_ACCESS_INSTANCED_PROP(CardInstances,_SealEnabled)>.5)
    {
        float2 uv=CardSealSampleUV(input.localUV,_SealRect,_SealRotation);
        half4 seal=SAMPLE_TEXTURE2D(_SealMap,sampler_SealMap,saturate(uv));
        seal.a*=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
        seal.rgb*=UNITY_ACCESS_INSTANCED_PROP(CardInstances,_SealColor).rgb;
        color.rgb=lerp(color.rgb,seal.rgb,seal.a);
        emission=emission*(1-seal.a)+seal.rgb*seal.a*saturate(UNITY_ACCESS_INSTANCED_PROP(CardInstances,_SealEmissionStrength));
    }
    if(UNITY_ACCESS_INSTANCED_PROP(CardInstances,_RedCrossEnabled)>.5)
    {
        half4 paint=SAMPLE_TEXTURE2D(_RedCrossMap,sampler_RedCrossMap,saturate(input.localUV));
        color.rgb=lerp(color.rgb,paint.rgb,paint.a);emission*=1-paint.a;
    }
    color.rgb+=emission;return color;
}
half4 CardDepth(Varyings input):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    CardBase(input);return 0;
}
#endif
