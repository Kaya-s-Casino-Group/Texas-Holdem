Shader "Poker/BattleDemo Target Outline"
{
    Properties
    {
        _MainTex ("Portrait alpha", 2D) = "white" {}
        _OutlineColor ("Selection", Color) = (1, 0.08, 0.06, 1)
        _OutlinePixels ("Width in screen pixels", Range(1, 8)) = 3
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _OutlineColor;
                float _OutlinePixels;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 dx = ddx(input.uv) * _OutlinePixels;
                float2 dy = ddy(input.uv) * _OutlinePixels;
                half center = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half surrounding = center;
                [unroll] for (int index = 0; index < 16; index++)
                {
                    float angle = index * 0.3926990817;
                    float2 offset = dx * cos(angle) + dy * sin(angle);
                    surrounding = max(surrounding, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + offset).a);
                }
                half alpha = saturate((surrounding - center) * 2);
                clip(alpha - 0.02);
                return half4(_OutlineColor.rgb, alpha * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
