Shader "CardArt/BreathingOutline"
{
    Properties
    {
        [HDR] _Color ("Outline Color", Color) = (0.3,0.85,1,1)
        _HalfSize ("Card Half Size / Padding", Vector) = (0.5,0.75,0.15,0)
        _Width ("Outer Border Width", Float) = 0.025
        _Radius ("Corner Radius", Float) = 0.04
        _Softness ("Glow Falloff", Float) = 0.04
        _Pulse ("Synchronized Pulse", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _HalfSize;
                float _Width, _Radius, _Softness, _Pulse;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.p = (input.uv * 2 - 1) * (_HalfSize.xy + _HalfSize.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float radius = min(_Radius, min(_HalfSize.x, _HalfSize.y));
                float2 q = abs(input.p) - _HalfSize.xy + radius;
                float distance = length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
                float aa = max(fwidth(distance), 0.0001);
                float outside = smoothstep(-aa, aa, distance);
                float border = 1 - smoothstep(_Width - aa, _Width + aa, distance);
                float glow = exp2(-max(0, distance - _Width) / max(_Softness, 0.0001) * 5) * 0.35;
                half alpha = saturate(max(border, glow) * outside * _Pulse * _Color.a);
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
