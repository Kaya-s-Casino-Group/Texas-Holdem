Shader "Harbor/Environment Deferred"
{
 Properties
 {
  [MainTexture] _BaseMap("Base Color / Alpha",2D)="white"{}
  [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
  [Normal] _BumpMap("Normal",2D)="bump"{}
  _BumpScale("Normal Strength",Range(0,2))=.18
  _Smoothness("Smoothness",Range(0,1))=.08
  _Cutoff("Alpha Cutoff (0 = Opaque)",Range(0,1))=0
  [Toggle] _VertexTone("Use Leaf Vertex Tone",Float)=0
  _PaintFill("Hand Painted Fill",Range(0,1))=0
  _BakeEmission("Physical Emission for Baking",Range(0,8))=0
  _WindStrength("Root Pinned Sway",Range(0,.1))=0
  _WindSpeed("Wind Speed",Range(0,3))=1
  _DeformMode("Vertex Deform Mode",Float)=0
  _FlagDirection("Flag World Normal",Vector)=(0,0,1,0)
  _FlagSide("Flag World Side",Vector)=(1,0,0,0)
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" "DisableBatching"="True"}
  Cull [_Cull]
  ZWrite On
  HLSLINCLUDE
  #include "HarborInput.hlsl"
  ENDHLSL
  Pass
  {
   Name "GBuffer"
   Tags {"LightMode"="UniversalGBuffer"}
   
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex FRVertex
   #pragma fragment FRGBuffer
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
   #include "HarborPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "Forward"
   Tags {"LightMode"="UniversalForward"}
   
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex FRVertex
   #pragma fragment FRForward
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
   #include "HarborPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags {"LightMode"="ShadowCaster"}
   ColorMask 0
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex FRShadowVertex
   #pragma fragment FRDepth
   #pragma multi_compile_instancing
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   #include "HarborPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags {"LightMode"="DepthOnly"}
   ColorMask R
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex FRVertex
   #pragma fragment FRDepth
   #pragma multi_compile_instancing
   #include "HarborPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "DepthNormals"
   Tags {"LightMode"="DepthNormals"}
   
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex FRVertex
   #pragma fragment FRDepthNormals
   #pragma multi_compile_instancing
   #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
   #include "HarborPasses.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "Meta"
   Tags {"LightMode"="Meta"}
   Cull Off
   HLSLPROGRAM
   #pragma target 4.5
   #pragma vertex FRMetaVertex
   #pragma fragment FRMeta
   #pragma multi_compile_instancing
   #pragma shader_feature EDITOR_VISUALIZATION
   #include "HarborPasses.hlsl"
   ENDHLSL
  }
 }

 FallBack Off
}

