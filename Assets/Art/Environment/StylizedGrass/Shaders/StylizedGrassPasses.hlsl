#ifndef STYLIZED_GRASS_PASSES
#define STYLIZED_GRASS_PASSES
#include "StylizedGrassInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
struct SGAttributes
{
 float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT;
 float2 uv:TEXCOORD0; float2 staticLightmapUV:TEXCOORD1; float2 dynamicLightmapUV:TEXCOORD2;
 half4 color:COLOR;
 UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct SGVaryings
{
 float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1;
 float2 uv:TEXCOORD2; half4 color:TEXCOORD3; half variation:TEXCOORD4;
 DECLARE_LIGHTMAP_OR_SH(staticLightmapUV,vertexSH,5);
 #ifdef DYNAMICLIGHTMAP_ON
 float2 dynamicLightmapUV:TEXCOORD6;
 #endif
 half fogFactor:TEXCOORD7;
 UNITY_VERTEX_INPUT_INSTANCE_ID
 UNITY_VERTEX_OUTPUT_STEREO
};
SGVaryings SGVertex(SGAttributes v)
{
 SGVaryings o=(SGVaryings)0;
 UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 float3 pivot=TransformObjectToWorld(float3(0,0,0));
 o.normalWS=TransformObjectToWorldNormal(v.normalOS);
 o.positionWS=SGDeform(TransformObjectToWorld(v.positionOS.xyz),v.color.r,pivot,o.normalWS);
 o.positionCS=TransformWorldToHClip(o.positionWS);
 o.uv=v.uv; o.color=v.color; o.variation=SGNoise(pivot.xz*2.3+17);
 o.fogFactor=ComputeFogFactor(o.positionCS.z);
 OUTPUT_LIGHTMAP_UV(v.staticLightmapUV,unity_LightmapST,o.staticLightmapUV);
 OUTPUT_SH(o.normalWS,o.vertexSH);
 #ifdef DYNAMICLIGHTMAP_ON
 o.dynamicLightmapUV=v.dynamicLightmapUV*unity_DynamicLightmapST.xy+unity_DynamicLightmapST.zw;
 #endif
 return o;
}
void SGSurface(SGVaryings i, half face, out SurfaceData s, out half3 normalWS)
{
 s=(SurfaceData)0; s.alpha=1; s.normalTS=half3(0,0,1); s.occlusion=1; s.metallic=0;
 s.smoothness=_Smoothness;
 #if defined(SG_GRASS)
 half h=saturate(i.color.r);
 s.albedo=lerp(_RootColor.rgb,_TipColor.rgb,smoothstep(0,1,h));
 s.albedo=lerp(s.albedo,_DryColor.rgb,smoothstep(.66,1,i.variation)*_ColorVariation);
 s.albedo*=lerp(.83h,1.16h,i.variation);
 s.occlusion=lerp(1-_RootOcclusion,1,h);
 normalWS=NormalizeNormalPerPixel(lerp(normalize(i.normalWS)*face,half3(0,1,0),_NormalUp));
 #else
 float2 uv=i.positionWS.xz*_GrassTiling;
 half3 tex=SAMPLE_TEXTURE2D(_GrassMap,sampler_GrassMap,uv).rgb;
 half height=SAMPLE_TEXTURE2D(_GrassHeight,sampler_GrassHeight,uv).r;
 half mask=saturate((i.color.r-.5)*_BlendSharpness+.5+(height-.5)*.3);
 half detail=dot(tex,half3(.22,.7,.08));
 half soil=SGNoise(i.positionWS.xz*_SoilTiling);
 half grain=SGNoise(i.positionWS.xz*_SoilTiling*5.7);
 half macro=SGNoise(i.positionWS.xz*.27);
 half3 grass=_GrassColor.rgb*(.62+detail*1.6);
 half3 dirt=_DirtColor.rgb*(.72+soil*.4+grain*.15);
 s.albedo=lerp(dirt,grass,mask)*lerp(.8h,1.15h,macro);
 half3 tn=UnpackNormalScale(SAMPLE_TEXTURE2D(_GrassNormal,sampler_GrassNormal,uv),_NormalStrength);
 const float e=.025;
 float dx=(SGNoise((i.positionWS.xz+float2(e,0))*_SoilTiling)-soil)/e;
 float dz=(SGNoise((i.positionWS.xz+float2(0,e))*_SoilTiling)-soil)/e;
 half3 detailN=lerp(half3(-dx*.055,1,-dz*.055),half3(tn.x,tn.z,tn.y),mask);
 normalWS=NormalizeNormalPerPixel(normalize(i.normalWS)+half3(detailN.x,0,detailN.z));
 s.smoothness=lerp(_Smoothness*.8,_Smoothness,mask);
 s.occlusion=lerp(.9h,1.0h,height);
 #endif
 // Small art-directed fill keeps dark red readable after the project's posterization.
 s.emission=s.albedo*_Fill;
}
InputData SGInput(SGVaryings i,half3 normalWS)
{
 InputData d=(InputData)0; d.positionWS=i.positionWS; d.positionCS=i.positionCS; d.normalWS=normalWS;
 d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS); d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
 #ifdef DYNAMICLIGHTMAP_ON
 d.bakedGI=SAMPLE_GI(i.staticLightmapUV,i.dynamicLightmapUV,i.vertexSH,normalWS);
 #else
 d.bakedGI=SAMPLE_GI(i.staticLightmapUV,i.vertexSH,normalWS);
 #endif
 d.shadowMask=SAMPLE_SHADOWMASK(i.staticLightmapUV); d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
 return d;
}
FragmentOutput SGGBuffer(SGVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC)
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 SurfaceData s; half3 n; SGSurface(i,IS_FRONT_VFACE(face,1.0h,-1.0h),s,n);
 InputData d=SGInput(i,n); BRDFData b; InitializeBRDFData(s,b);
 Light mainLight=GetMainLight(d.shadowCoord,d.positionWS,d.shadowMask);
 MixRealtimeAndBakedGI(mainLight,n,d.bakedGI,d.shadowMask);
 half3 gi=GlobalIllumination(b,d.bakedGI,s.occlusion,d.positionWS,n,d.viewDirectionWS);
 return BRDFDataToGbuffer(b,d,s.smoothness,s.emission+gi,s.occlusion);
}
half4 SGForward(SGVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 SurfaceData s; half3 n; SGSurface(i,IS_FRONT_VFACE(face,1.0h,-1.0h),s,n);
 half4 c=UniversalFragmentPBR(SGInput(i,n),s); c.rgb=MixFog(c.rgb,i.fogFactor);return c;
}
half4 SGDepth(SGVaryings i):SV_Target { UNITY_SETUP_INSTANCE_ID(i);return 0; }
half4 SGDepthNormals(SGVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 SurfaceData s;half3 n;SGSurface(i,IS_FRONT_VFACE(face,1.0h,-1.0h),s,n);return half4(PackNormal(n),0);
}
float3 _LightDirection, _LightPosition;
SGVaryings SGShadowVertex(SGAttributes v)
{
 SGVaryings o=SGVertex(v);
 #if _CASTING_PUNCTUAL_LIGHT_SHADOW
 float3 dir=normalize(_LightPosition-o.positionWS);
 #else
 float3 dir=_LightDirection;
 #endif
 o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,dir));
 #if UNITY_REVERSED_Z
 o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
 #else
 o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 return o;
}
SGVaryings SGMetaVertex(SGAttributes v)
{
 SGVaryings o=SGVertex(v);
 // Baking samples the undeformed rest surface.
 o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
 o.normalWS=TransformObjectToWorldNormal(v.normalOS);
 o.positionCS=UnityMetaVertexPosition(v.positionOS.xyz,v.staticLightmapUV,v.dynamicLightmapUV);
 return o;
}
half4 SGMeta(SGVaryings i):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i);
 SurfaceData s; half3 n; SGSurface(i,1,s,n); MetaInput m=(MetaInput)0;m.Albedo=s.albedo;m.Emission=0;
 return UnityMetaFragment(m);
}
#endif
