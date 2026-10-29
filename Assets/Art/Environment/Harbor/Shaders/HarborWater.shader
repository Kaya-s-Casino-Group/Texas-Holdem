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
  _ReflectionStrength("Environment Sheen",Range(0,1))=.75
  _ShimmerColor("Warm Sky Shimmer",Color)=(.42,.38,.17,1)
  _ShimmerStrength("Shimmer Strength",Range(0,1))=.35
  _Smoothness("Smoothness",Range(0,1))=.85
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Opaque" "UniversalMaterialType"="Lit" "DisableBatching"="True"}
  Cull Back ZWrite On
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  CBUFFER_START(UnityPerMaterial)
  half4 _BaseColor,_ShimmerColor;
  float _ShimmerStrength;
  float _WaveStrength,_WaveSpeed,_ReflectionStrength,_Smoothness;
  float _SwellLength,_RippleStrength,_RippleSpeed,_RippleLength;
  float4 _SwellDirection,_RippleDirection;
  CBUFFER_END
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
   // Existing environment cubemap only: no extra camera, render texture or screen-color copy.
   // Animated Gerstner normals move the light highlights and environment sheen across the surface.
   half3 reflection=GlossyEnvironmentReflection(reflect(-view,normal),i.positionWS,1-_Smoothness,1,d.normalizedScreenSpaceUV);
   reflection*=lerp(_BaseColor.rgb,_ShimmerColor.rgb,.5);
   float fresnel=.02037+.97963*pow(1-saturate(dot(normal,view)),5);
   // The only step is AFTER lighting/reflection composition, never in texture sampling.
   half3 finalColor=lerp(lit,reflection,fresnel*_ReflectionStrength);
   // Broken wave-length bands give a restrained moving glint even without a bright scene light.
   float2 direction=dot(_RippleDirection.xy,_RippleDirection.xy)>.0001?normalize(_RippleDirection.xy):float2(0,1);
   float phase=6.2831853*dot(direction,i.restWS.xz)/max(_RippleLength,.1)-_Time.y*_RippleSpeed*3.77;
   float crossPhase=dot(float2(-direction.y,direction.x),i.restWS.xz)*2.1+_Time.y*.23;
   float glint=smoothstep(.72,.98,sin(phase))*smoothstep(.1,.85,sin(crossPhase));
   finalColor+=_ShimmerColor.rgb*glint*_ShimmerStrength*detailFilter*HarborShoreFade(i.restWS)*(.25+.75*fresnel);
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
