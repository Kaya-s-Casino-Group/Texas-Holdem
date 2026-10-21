Shader "Poker/BattleDemo Enemy Ash"
{
    Properties
    {
        _BaseMap ("Artwork", 2D) = "white" {}
        _BaseColor ("Artwork tint", Color) = (1,1,1,1)
        _AshProgress ("Dissolve", Range(0,1)) = 0
        _AshMode ("Fragment layer", Float) = 0
        _AshColor ("Ash tint", Color) = (0.48,0.46,0.43,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _AshColor;
                float _AshProgress, _AshMode;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float2 corner : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float age : TEXCOORD1; float seed : TEXCOORD2; };
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Field(float2 uv) { return Hash(floor(uv * 96)) * .68 + uv.y * .32; }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.seed = Hash(input.uv * 133.7);
                output.age = 0;
                float3 position = input.positionOS.xyz;
                if (_AshMode > .5)
                {
                    float birth = (Field(input.uv) + .1) / 1.45;
                    output.age = (_AshProgress - birth) / (.18 + output.seed * .06);
                    float drift = max(0, output.age);
                    float spin = output.seed * 6.283 + drift * 3;
                    float2 corner = float2(input.corner.x * cos(spin) - input.corner.y * sin(spin), input.corner.x * sin(spin) + input.corner.y * cos(spin));
                    position.xy += corner * (.002 + output.seed * .003) * max(0, 1 - drift * .6);
                    position.x += drift * (.08 + output.seed * .25) + sin(drift * 5 + output.seed * 30) * drift * .025;
                    position.y += drift * (.20 + output.seed * .45);
                    position.z -= .003;
                }
                output.positionCS = TransformObjectToHClip(position);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(source.a - .03);
                if (_AshMode > .5)
                {
                    clip(input.age); clip(1 - input.age);
                    half alpha = source.a * (1 - smoothstep(.35, 1, input.age));
                    half3 tint = _AshColor.rgb * lerp(.5, 1.5, input.seed);
                    return half4(tint, alpha);
                }
                float distance = Field(input.uv) - (_AshProgress * 1.45 - .1);
                clip(distance);
                half grey = dot(source.rgb, half3(.25, .6, .15));
                half charred = smoothstep(0, .32, _AshProgress);
                half3 tint = lerp(source.rgb, grey * _AshColor.rgb, charred);
                half edge = 1 - smoothstep(0, .055, distance);
                tint = lerp(tint, _AshColor.rgb * 1.35, edge * charred);
                return half4(tint, source.a);
            }
            ENDHLSL
        }
    }
}
