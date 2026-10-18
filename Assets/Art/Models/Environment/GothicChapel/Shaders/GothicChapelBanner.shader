// Gothic Chapel: adapted from the project Forest Ruins deferred/lightmap shader.
Shader "Gothic Chapel/Animated Banner"
{
 Properties
 {
  [MainTexture] _BaseMap("Base Color / Alpha",2D)="white"{}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  [Normal] _BumpMap("Normal",2D)="bump"{}
  _BumpScale("Normal Strength",Range(0,2))=.18
  _Metallic("Metallic Multiplier",Range(0,1))=1
  [NoScaleOffset] _MaskMap("Mask: Metallic R / Occlusion G / Smoothness A",2D)="white"{}
  _AtmosphereColor("Backdrop Atmosphere",Color)=(.19,.26,.33,1)
  _AtmosphereBlend("Backdrop Atmosphere Blend",Range(0,1))=0
  [Toggle] _ExcludeFromGI("Exclude Albedo From GI",Float)=0
  _Smoothness("Smoothness",Range(0,1))=.08
  _Cutoff("Alpha Cutoff (0 = Opaque)",Range(0,1))=0
  [Toggle] _VertexTone("Use Leaf Vertex Tone",Float)=0
  _Unlit("Unlit Backdrop",Range(0,1))=0
  _BakeEmission("Physical Emission for Baking",Range(0,8))=0
  _WindStrength("Root Pinned Sway",Range(0,.1))=0
  _WindSpeed("Wind Speed",Range(0,3))=1
  _BannerAmplitude("Banner Sway (metres)",Range(0,.15))=.038
  _BannerSpeed("Banner Speed",Range(0,5))=1.25
  _BannerFrequency("Banner Ripple Frequency",Range(0,12))=5
  [HideInInspector] _AnimationTime("Animation Test Time (-1 = Live)",Float)=-1
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "DisableBatching"="True"}
  Cull [_Cull]
  ZWrite On
  HLSLINCLUDE
  #define GC_ANIMATED_BANNER 1
  #include "GothicChapelInput.hlsl"
  ENDHLSL
  Pass
  {
   Name "GBuffer"
   Tags {"LightMode"="UniversalGBuffer"}

   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex GCVertex
   #pragma fragment GCGBuffer
   #pragma multi_compile_instancing

   #pragma multi_compile _ LIGHTMAP_ON
   #pragma multi_compile _ DIRLIGHTMAP_COMBINED
   #pragma multi_compile _ DYNAMICLIGHTMAP_ON
   #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
   #pragma multi_compile _ SHADOWS_SHADOWMASK
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
   #include "GothicChapelPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "Forward"
   Tags {"LightMode"="UniversalForward"}

   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex GCVertex
   #pragma fragment GCForward
   #pragma multi_compile_instancing

   #pragma multi_compile _ LIGHTMAP_ON
   #pragma multi_compile _ DIRLIGHTMAP_COMBINED
   #pragma multi_compile _ DYNAMICLIGHTMAP_ON
   #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
   #pragma multi_compile _ SHADOWS_SHADOWMASK
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
   #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
   #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fog
   #include "GothicChapelPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags {"LightMode"="ShadowCaster"}
   ColorMask 0
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex GCShadowVertex
   #pragma fragment GCDepth
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "GothicChapelPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ColorMask R
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex GCVertex
   #pragma fragment GCDepth
   #pragma multi_compile_instancing
   #include "GothicChapelPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "DepthNormals"
   Tags {"LightMode"="DepthNormals"}

   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex GCVertex
   #pragma fragment GCDepthNormals
   #pragma multi_compile_instancing
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   #include "GothicChapelPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "Meta"
   Tags {"LightMode"="Meta"}
   Cull Off
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex GCMetaVertex
   #pragma fragment GCMeta
   #pragma multi_compile_instancing
   #pragma shader_feature EDITOR_VISUALIZATION
   #include "GothicChapelPasses.hlsl"
   ENDHLSL
  }
 }
 FallBack Off
}
