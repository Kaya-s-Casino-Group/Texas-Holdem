Shader "Harbor/Gerstner Reflective Step Water"
{
 Properties
 {
  _BaseColor("Deep Teal",Color)=(.025,.08,.065,1)
  [Header(Layer 1 Swell)]
  _WaveStrength("Swell Strength",Range(0,2))=1
  _WaveSpeed("Swell Speed",Range(0,2))=.8
  _SwellLength("Swell Wavelength",Range(8,24))=12
  _SwellDirection("Swell Direction XZ",Vector)=(-.28,-.96,0,0)
  [Header(Layer 2 Ripples)]
  _RippleStrength("Ripple Strength",Range(0,2))=1
  _RippleSpeed("Ripple Speed",Range(0,2))=1.15
  _RippleLength("Ripple Wavelength",Range(.5,6))=2.4
  _RippleDirection("Ripple Direction XZ",Vector)=(.76,-.65,0,0)
  _ReflectionStrength("Reflection",Range(0,1))=.75
  _Smoothness("Smoothness",Range(0,1))=.85
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" "UniversalMaterialType"="Lit" "DisableBatching"="True"}
  Cull Back ZWrite On
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
  CBUFFER_START(UnityPerMaterial)
  half4 _BaseColor;
  float _WaveStrength,_WaveSpeed,_ReflectionStrength,_Smoothness;
  float _SwellLength,_RippleStrength,_RippleSpeed,_RippleLength;
  float4 _SwellDirection,_RippleDirection;
  CBUFFER_END
  TEXTURE2D(_HarborPlanarReflection);SAMPLER(sampler_HarborPlanarReflection);
  float4x4 _HarborReflectionVP;
  float _HarborHasReflection;
  #include "HarborGerstner.hlsl"
  struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
  struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 restWS:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
  V WaterVert(A a)
  {
   V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
   o.positionWS=TransformObjectToWorld(a.positionOS.xyz);
   o.restWS=o.positionWS;
   HarborWaterVertex(o.positionWS);
   o.positionCS=TransformWorldToHClip(o.positionWS);return o;
  }
  half4 WaterFrag(V i):SV_Target
  {
   // Fade unresolved ripples before they alias at the horizon.
   float footprint=max(length(ddx(i.restWS.xz)),length(ddy(i.restWS.xz)));
   float detailFilter=1-smoothstep(.15,.5,footprint/max(_RippleLength,.1));
   float3 normal=HarborWaterNormal(i.restWS,detailFilter),view=GetWorldSpaceNormalizeViewDir(i.positionWS);
   InputData d=(InputData)0;d.positionWS=i.positionWS;d.normalWS=normal;d.viewDirectionWS=view;
   d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);d.bakedGI=SampleSH(normal);d.shadowMask=1;
   d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
   SurfaceData surf=(SurfaceData)0;surf.albedo=_BaseColor.rgb;surf.specular=.02;surf.smoothness=_Smoothness;
   surf.occlusion=1;surf.alpha=1;surf.normalTS=float3(0,0,1);
   half3 lit=UniversalFragmentPBR(d,surf).rgb;
   float4 rp=mul(_HarborReflectionVP,float4(i.positionWS,1));float2 uv=rp.xy/max(rp.w,.0001)*.5+.5;
   half3 reflection=SAMPLE_TEXTURE2D(_HarborPlanarReflection,sampler_HarborPlanarReflection,saturate(uv+normal.xz*.025)).rgb;
   half3 fallback=GlossyEnvironmentReflection(reflect(-view,normal),i.positionWS,1-_Smoothness,1,d.normalizedScreenSpaceUV);
   reflection=lerp(fallback,reflection,_HarborHasReflection);
   float fresnel=.02037+.97963*pow(1-saturate(dot(normal,view)),5);
   float2 refractUV=saturate(d.normalizedScreenSpaceUV+normal.xz*.008);
   half3 body=lerp(lit,SampleSceneColor(refractUV)*_BaseColor.rgb,.07);
   // The only step is AFTER lighting/reflection composition, never in texture sampling.
   half3 finalColor=lerp(body,reflection,fresnel*_ReflectionStrength);
   return half4(HarborHardStep(finalColor),1);
  }
  half4 WaterDepth(V i):SV_Target{return 0;}
  ENDHLSL
  Pass
  {
   Name "WaterForwardOnly" Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex WaterVert
   #pragma fragment WaterFrag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask R
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex WaterVert
   #pragma fragment WaterDepth
   #pragma multi_compile_instancing
   ENDHLSL
  }
 }
}
