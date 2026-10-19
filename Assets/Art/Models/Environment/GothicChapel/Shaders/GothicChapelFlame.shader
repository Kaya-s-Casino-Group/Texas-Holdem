Shader "Gothic Chapel/Candle Flame"
{
 Properties
 {
  [HDR] _OuterColor("Outer Flame",Color)=(1,.19,.018,1)
  [HDR] _CoreColor("Flame Core",Color)=(1,.83,.35,1)
  _Intensity("Emission Intensity",Range(0,20))=6
  _FlickerAmount("Random Flicker Amount",Range(0,.9))=.32
  _FlickerSpeed("Random Flicker Speed",Range(.1,20))=7
  _Sway("Tip Sway (metres)",Range(0,.03))=.009
  _SwaySpeed("Sway Speed",Range(.1,12))=4
  _BakeIntensity("Static Bake Emission",Range(0,20))=0
  [HideInInspector] _AnimationTime("Animation Test Time (-1 = Live)",Float)=-1
  [HideInInspector] _FlickerOverride("Synchronized Light Flicker (-1 = Shader)",Float)=-1
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "DisableBatching"="True"}
  Cull Off ZWrite Off Blend One OneMinusSrcAlpha
  Pass
  {
   Name "Flame"
   Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex FlameVertex
   #pragma fragment FlameFragment
   #pragma multi_compile_instancing
   #include "GothicChapelFlame.hlsl"
   ENDHLSL
  }
  Pass
  {
   Name "Meta"
   Tags {"LightMode"="Meta"}
   Blend Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex FlameMetaVertex
   #pragma fragment FlameMeta
   #pragma multi_compile_instancing
   #include "GothicChapelFlame.hlsl"
   ENDHLSL
  }
 }
 FallBack Off
}
