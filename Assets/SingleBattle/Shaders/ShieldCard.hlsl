#ifndef SHIELD_CARD_INCLUDED
#define SHIELD_CARD_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#include "../../CardArt/Shaders/CardSurfaceCommon.hlsl"
TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_TexelSize;
float4 _BackUVRect;
half4 _EdgeColor;
float _NoiseScale, _NoiseStrength, _EdgeWidth, _Cutoff;
CBUFFER_END
UNITY_INSTANCING_BUFFER_START(ShieldInstances)
UNITY_DEFINE_INSTANCED_PROP(float4, _FrontUVRect)
UNITY_DEFINE_INSTANCED_PROP(float4, _NoiseOffset)
UNITY_DEFINE_INSTANCED_PROP(float, _Dissolve)
UNITY_DEFINE_INSTANCED_PROP(float, _LandingFlash)
UNITY_INSTANCING_BUFFER_END(ShieldInstances)
struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct Varyings {
float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3;
UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
};
Varyings ShieldVertex(Attributes input)
{
Varyings output=(Varyings)0; UNITY_SETUP_INSTANCE_ID(input); UNITY_TRANSFER_INSTANCE_ID(input,output); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz);
output.positionCS=p.positionCS; output.positionWS=p.positionWS; output.normalWS=TransformObjectToWorldNormal(input.normalOS);
output.uv=input.uv; output.fog=ComputeFogFactor(p.positionCS.z); return output;
}
half4 SampleShield(Varyings input, bool front, out half edge)
{
float progress=saturate(UNITY_ACCESS_INSTANCED_PROP(ShieldInstances,_Dissolve));
float2 noiseUV=input.uv*_NoiseScale+UNITY_ACCESS_INSTANCED_PROP(ShieldInstances,_NoiseOffset).xy;
float noise=SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,noiseUV).r;
float field=1-saturate(input.uv.y)+(noise*2-1)*_NoiseStrength;
float threshold=lerp(-_NoiseStrength-.001,1+_NoiseStrength+.001,progress);
float keep=step(threshold,field);
clip(keep-.5);
edge=(keep-step(threshold+_EdgeWidth,field))*step(.00001,progress)*(1-step(1,progress));
float4 rect=front?UNITY_ACCESS_INSTANCED_PROP(ShieldInstances,_FrontUVRect):_BackUVRect;
float2 uv=front?input.uv:float2(1-input.uv.x,input.uv.y);
half4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,CardAtlasSampleUV(uv,rect,_BaseMap_TexelSize.xy));
clip(color.a-_Cutoff); return color;
}
void ShieldLighting(Varyings input,bool front,out SurfaceData surface,out InputData lighting)
{
half edge; half4 color=SampleShield(input,front,edge);
surface=(SurfaceData)0; surface.albedo=color.rgb; surface.alpha=color.a; surface.normalTS=half3(0,0,1); surface.specular=.02; surface.smoothness=.08; surface.occlusion=1;
surface.emission=_EdgeColor.rgb*(edge+.3*saturate(UNITY_ACCESS_INSTANCED_PROP(ShieldInstances,_LandingFlash)));
lighting=(InputData)0; lighting.positionWS=input.positionWS; lighting.positionCS=input.positionCS;
lighting.normalWS=NormalizeNormalPerPixel(front?input.normalWS:-input.normalWS);
lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS); lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
lighting.shadowMask=unity_ProbesOcclusion; lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
lighting.fogCoord=input.fog; lighting.bakedGI=SampleSH(lighting.normalWS); lighting.vertexLighting=VertexLighting(input.positionWS,lighting.normalWS);
}
FragmentOutput ShieldGBuffer(Varyings input, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC)
{
UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
SurfaceData surface; InputData lighting; ShieldLighting(input,IS_FRONT_VFACE(face,true,false),surface,lighting);
Light light=GetMainLight(lighting.shadowCoord,lighting.positionWS,lighting.shadowMask);
MixRealtimeAndBakedGI(light,lighting.normalWS,lighting.bakedGI,lighting.shadowMask);
return SurfaceDataToGbuffer(surface,lighting,lighting.bakedGI*surface.albedo+surface.emission,kLightingSimpleLit);
}
half4 ShieldForward(Varyings input, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
SurfaceData surface; InputData lighting; ShieldLighting(input,IS_FRONT_VFACE(face,true,false),surface,lighting);
half4 color=UniversalFragmentBlinnPhong(lighting,surface); color.rgb=MixFog(color.rgb,lighting.fogCoord); return color;
}
half4 ShieldDepth(Varyings input, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); half edge; SampleShield(input,IS_FRONT_VFACE(face,true,false),edge); return 0;
}
void ShieldNormals(Varyings input, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC, out half4 normal:SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
, out float4 renderingLayers:SV_Target1
#endif
)
{
UNITY_SETUP_INSTANCE_ID(input); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); half edge; bool front=IS_FRONT_VFACE(face,true,false); SampleShield(input,front,edge);
normal=half4(PackNormal(NormalizeNormalPerPixel(front?input.normalWS:-input.normalWS)),0);
#ifdef _WRITE_RENDERING_LAYERS
renderingLayers=float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()),0,0,0);
#endif
}
float3 _LightDirection, _LightPosition;
Varyings ShieldShadowVertex(Attributes input)
{
Varyings output=ShieldVertex(input);
#ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
float3 direction=normalize(_LightPosition-output.positionWS);
#else
float3 direction=_LightDirection;
#endif
float4 position=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
#if UNITY_REVERSED_Z
position.z=min(position.z,UNITY_NEAR_CLIP_VALUE);
#else
position.z=max(position.z,UNITY_NEAR_CLIP_VALUE);
#endif
output.positionCS=position; return output;
}
#endif
