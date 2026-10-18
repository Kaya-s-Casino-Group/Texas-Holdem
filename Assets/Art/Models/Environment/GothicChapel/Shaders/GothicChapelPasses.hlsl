// Gothic Chapel: adapted from the project Forest Ruins deferred/lightmap shader.
#ifndef GOTHIC_CHAPEL_PASSES_INCLUDED
#define GOTHIC_CHAPEL_PASSES_INCLUDED
#include "GothicChapelInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
struct GCAttributes
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
struct GCVaryings
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
GCVaryings GCVertex(GCAttributes v)
{
 GCVaryings o=(GCVaryings)0;
 UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 #ifdef GC_ANIMATED_BANNER
 GCBanner(v.positionOS.xyz,v.normalOS,v.tangentOS,v.color);
 VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
 #else
 VertexPositionInputs p=GetVertexPositionInputs(GCWind(v.positionOS.xyz));
 #endif
 VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);
 o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.fogFactor=ComputeFogFactor(p.positionCS.z);
 o.normalWS=n.normalWS; o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());
 o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.tone=v.color.r;
 OUTPUT_LIGHTMAP_UV(v.staticLightmapUV,unity_LightmapST,o.staticLightmapUV);
 OUTPUT_SH(o.normalWS,o.vertexSH);
 #ifdef DYNAMICLIGHTMAP_ON
 o.dynamicLightmapUV=v.dynamicLightmapUV*unity_DynamicLightmapST.xy+unity_DynamicLightmapST.zw;
 #endif
 return o;
}
half3 GCNormal(GCVaryings i,half faceSign)
{
 half3 b=i.tangentWS.w*cross(i.normalWS,i.tangentWS.xyz);
 half3 ts=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
 return NormalizeNormalPerPixel(TransformTangentToWorld(ts,half3x3(i.tangentWS.xyz,b,i.normalWS)))*faceSign;
}
InputData GCInput(GCVaryings i,half faceSign)
{
 InputData d=(InputData)0; d.positionWS=i.positionWS; d.positionCS=i.positionCS;
 d.normalWS=GCNormal(i,faceSign); d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
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
SurfaceData GCSurface(half4 c,float2 uv)
{
 half4 mask=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,uv);
 SurfaceData s=(SurfaceData)0; s.albedo=c.rgb*(1-_Unlit);
 s.specular=0.04h*(1-_Unlit); s.metallic=mask.r*_Metallic; s.smoothness=mask.a*_Smoothness;
 s.normalTS=half3(0,0,1); s.occlusion=mask.g; s.alpha=1; s.emission=c.rgb*_Unlit;
 return s;
}
FragmentOutput GCGBuffer(GCVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC)
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 SurfaceData s=GCSurface(GCSample(i.uv,i.tone),i.uv);
 InputData d=GCInput(i,IS_FRONT_VFACE(face,1.0h,-1.0h));
 BRDFData b; InitializeBRDFData(s,b);
 Light mainLight=GetMainLight(d.shadowCoord,d.positionWS,d.shadowMask);
 MixRealtimeAndBakedGI(mainLight,d.normalWS,d.bakedGI,d.shadowMask);
 half3 gi=GlobalIllumination(b,d.bakedGI,s.occlusion,d.positionWS,d.normalWS,d.viewDirectionWS);
 return BRDFDataToGbuffer(b,d,s.smoothness,s.emission+gi,s.occlusion);
}
half4 GCForward(GCVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 SurfaceData s=GCSurface(GCSample(i.uv,i.tone),i.uv);
 InputData d=GCInput(i,IS_FRONT_VFACE(face,1.0h,-1.0h));
 half4 c=UniversalFragmentPBR(d,s);
 c.rgb=MixFog(c.rgb,i.fogFactor);
 return c;
}
half4 GCDepth(GCVaryings i):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); GCSample(i.uv,i.tone); return 0;
}
half4 GCDepthNormals(GCVaryings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
{
 UNITY_SETUP_INSTANCE_ID(i); UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 GCSample(i.uv,i.tone);
 return half4(PackNormal(GCNormal(i,IS_FRONT_VFACE(face,1.0h,-1.0h))),0);
}
float3 _LightDirection;
float3 _LightPosition;
GCVaryings GCShadowVertex(GCAttributes v)
{
 GCVaryings o=GCVertex(v);
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
struct GCMetaVaryings
{
 float4 positionCS:SV_POSITION;
 float2 uv:TEXCOORD0;
 half tone:TEXCOORD1;
 #ifdef EDITOR_VISUALIZATION
 float2 VizUV:TEXCOORD2;
 float4 LightCoord:TEXCOORD3;
 #endif
};
GCMetaVaryings GCMetaVertex(GCAttributes v)
{
 GCMetaVaryings o=(GCMetaVaryings)0;
 UNITY_SETUP_INSTANCE_ID(v);
 // Baking deliberately uses the rest mesh, independent of time/wind.
 o.positionCS=UnityMetaVertexPosition(v.positionOS.xyz,v.staticLightmapUV,v.dynamicLightmapUV);
 o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.tone=v.color.r;
 #ifdef EDITOR_VISUALIZATION
 UnityEditorVizData(v.positionOS.xyz,v.uv,v.staticLightmapUV,v.dynamicLightmapUV,o.VizUV,o.LightCoord);
 #endif
 return o;
}
half4 GCMeta(GCMetaVaryings i):SV_Target
{
 half4 c=GCSample(i.uv,i.tone);
 half4 mask=SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv);
 half alpha=1; BRDFData b;
 InitializeBRDFData(c.rgb,mask.r*_Metallic,half3(.04h,.04h,.04h),mask.a*_Smoothness,alpha,b);
 MetaInput m=(MetaInput)0;
 m.Albedo=(b.diffuse+b.specular*b.roughness*.5h)*(1-_ExcludeFromGI);
 // PaintFill is a display/art-direction control, not a light source.
 m.Emission=c.rgb*_BakeEmission;
 #ifdef EDITOR_VISUALIZATION
 m.VizUV=i.VizUV; m.LightCoord=i.LightCoord;
 #endif
 return UnityMetaFragment(m);
}
#endif
