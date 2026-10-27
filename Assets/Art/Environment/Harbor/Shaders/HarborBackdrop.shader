Shader "Harbor/Painted Unlit"
{
 Properties
 {
  [MainTexture] _BaseMap("Painted RGBA",2D)="white"{}
  [MainColor] _BaseColor("Tint / Brightness",Color)=(1,1,1,1)
  _Cutoff("Alpha Cutoff",Range(0,1))=.35
  _Saturation("Saturation",Range(0,2))=1
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"}
  Cull Off ZWrite On
  Pass
  {
   Name "PaintedUnlit" Tags {"LightMode"="UniversalForwardOnly"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half4 _BaseColor;half _Cutoff,_Saturation;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   V Vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);return o;}
   half4 Frag(V i):SV_Target
   {
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(c.a-max(_Cutoff,.001));
    c.rgb=lerp(dot(c.rgb,half3(.2126,.7152,.0722)).xxx,c.rgb,_Saturation)*_BaseColor.rgb;return half4(c.rgb,1);
   }
   ENDHLSL
  }
 }
}
