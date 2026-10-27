#ifndef HARBOR_PASSES_INCLUDED
#define HARBOR_PASSES_INCLUDED
#include "HarborInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
struct FRAttributes
{
 float4 positionOS:POSITION;
 float3 normalOS:NORMAL;
 float4 tangentOS:TANGENT;
 float2 uv:TEXCOORD0;
 float2 staticLightmapUV:TEXCOORD1;
 float2 dynamicLightmapUV:TEXCOORD2;
 half4 color:COLOR;
 UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct FRVaryings
{
 float4 positionCS:SV_POSITION;
 float2 uv:TEXCOORD0;
 float3 positionWS:TEXCOORD1;
 half3 normalWS:TEXCOORD2;
 half4 tangentWS:TEXCOORD3;
 half tone:TEXCOORD4;
 half fogFactor:TEXCOORD7;
 DECLARE_LIGHTMAP_OR_SH(staticLightmapUV,vertexSH,5);
 #ifdef DYNAMICLIGHTMAP_ON
 float2 dynamicLightmapUV:TEXCOORD6;
 #endif
 UNITY_VERTEX_INPUT_INSTANCE_ID
 UNITY_VERTEX_OUTPUT_STEREO
};
FRVaryings FRVertex(FRAttributes v)
{
 FRVaryings o=(FRVaryings)0;
 UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 float3 deformedWS=TransformObjectToWorld(v.positionOS.xyz);
 float3 deformedNormal=TransformObjectToWorldNormal(v.normalOS);
 HarborDeform(deformedWS,deformedNormal,v.color);
 VertexPositionInputs p=GetVertexPositionInputs(TransformWorldToObject(deformedWS));
 VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);
 o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.fogFactor=ComputeFogFactor(p.positionCS.z);
 o.normalWS=deformedNormal; o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());
 o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.tone=v.color.r;
 OUTPUT_LIGHTMAP_UV(v.staticLightmapUV,unity_LightmapST,o.staticLightmapUV);
 OUTPUT_SH(o.normalWS,o.vertexSH);
 #ifdef DYNAMICLIGHTMAP_ON
 o.dynamicLightmapUV=v.dynamicLightmapUV*unity_DynamicLightmapST.xy+unity_DynamicLightmapST.zw;
 #endif
 return o;
}
half3 FRNormal(FRVaryings i,half faceSign)
{
 half3 b=i.tangentWS.w*cross(i.normalWS,i.tangentWS.xyz);
 half3 ts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
 return NormalizeNormalPerPixel(TransformTangentToWorld(ts,half3x3(i.tangentWS.xyz,b,i.normalWS)))*faceSign;
}
InputData FRInput(FRVaryings i,half faceSign)
{
 InputData d=(InputData)0; d.positionWS=i.positionWS; d.positionCS=i.positionCS;
 d.normalWS=FRNormal(i,faceSign); d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
 d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
 #ifdef DYNAMICLIGHTMAP_ON
 d.bakedGI=SAMPLE_GI(i.staticLightmapUV,i.dynamicLightmapUV,i.vertexSH,d.normalWS);
 #else
 d.bakedGI=SAMPLE_GI(i.staticLightmapUV,i.vertexSH,d.normalWS);
 #endif
 d.shadowMask=SAMPLE_SHADOWMASK(i.staticLightmapUV);
 d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
 return d;
}
SurfaceData FRSurface(half4 c)
{
 SurfaceData s=(SurfaceData)0; s.albedo=c.rgb*(1-_PaintFill);
 s.specular=0.04h*(1-_PaintFill); s.metallic=0; s.smoothness=_Smoothness;
 s.normalTS=half3(0,0,1); s.occlusion=1; s.alpha=1; s.emission=c.rgb*_PaintFill;
 return s;
}
FragmentOutput FRGBuffer(FRVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC)
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 SurfaceData s=FRSurface(FRSample(i.uv,i.tone));
 InputData d=FRInput(i,IS_FRONT_VFACE(face,1.0h,-1.0h));
 BRDFData b; InitializeBRDFData(s,b);
 Light mainLight=GetMainLight(d.shadowCoord,d.positionWS,d.shadowMask);
 MixRealtimeAndBakedGI(mainLight,d.normalWS,d.bakedGI,d.shadowMask);
 half3 gi=GlobalIllumination(b,d.bakedGI,1,d.positionWS,d.normalWS,d.viewDirectionWS);
 return BRDFDataToGbuffer(b,d,_Smoothness,s.emission+gi,1);
}
half4 FRForward(FRVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 SurfaceData s=FRSurface(FRSample(i.uv,i.tone));
 InputData d=FRInput(i,IS_FRONT_VFACE(face,1.0h,-1.0h));
 half4 c=UniversalFragmentPBR(d,s);
 c.rgb=MixFog(c.rgb,i.fogFactor);
 return c;
}
half4 FRDepth(FRVaryings i):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); FRSample(i.uv,i.tone); return 0;
}
half4 FRDepthNormals(FRVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 FRSample(i.uv,i.tone);
 return half4(PackNormal(FRNormal(i,IS_FRONT_VFACE(face,1.0h,-1.0h))),0);
}
float3 _LightDirection;
float3 _LightPosition;
FRVaryings FRShadowVertex(FRAttributes v)
{
 FRVaryings o=FRVertex(v);
 #if _CASTING_PUNCTUAL_LIGHT_SHADOW
 float3 lightDirection=normalize(_LightPosition-o.positionWS);
 #else
 float3 lightDirection=_LightDirection;
 #endif
 o.positionCS=TransformWorldToHClip(ApplyShadowBias(o.positionWS,o.normalWS,lightDirection));
 #if UNITY_REVERSED_Z
 o.positionCS.z=min(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
 #else
 o.positionCS.z=max(o.positionCS.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 return o;
}
struct FRMetaVaryings
{
 float4 positionCS:SV_POSITION;
 float2 uv:TEXCOORD0;
 half tone:TEXCOORD1;
 #ifdef EDITOR_VISUALIZATION
 float2 VizUV:TEXCOORD2;
 float4 LightCoord:TEXCOORD3;
 #endif
};
FRMetaVaryings FRMetaVertex(FRAttributes v)
{
 FRMetaVaryings o=(FRMetaVaryings)0;
 UNITY_SETUP_INSTANCE_ID(v);
 // Baking deliberately uses the rest mesh, independent of time/wind.
 o.positionCS=UnityMetaVertexPosition(v.positionOS.xyz,v.staticLightmapUV,v.dynamicLightmapUV);
 o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.tone=v.color.r;
 #ifdef EDITOR_VISUALIZATION
 UnityEditorVizData(v.positionOS.xyz,v.uv,v.staticLightmapUV,v.dynamicLightmapUV,o.VizUV,o.LightCoord);
 #endif
 return o;
}
half4 FRMeta(FRMetaVaryings i):SV_Target
{
 half4 c=FRSample(i.uv,i.tone);
 half alpha=1; BRDFData b;
 InitializeBRDFData(c.rgb,0,half3(.04h,.04h,.04h),_Smoothness,alpha,b);
 MetaInput m=(MetaInput)0;
 m.Albedo=b.diffuse+b.specular*b.roughness*.5h;
 // PaintFill is a display/art-direction control, not a light source.
 m.Emission=c.rgb*_BakeEmission;
 #ifdef EDITOR_VISUALIZATION
 m.VizUV=i.VizUV; m.LightCoord=i.LightCoord;
 #endif
 return UnityMetaFragment(m);
}
#endif

