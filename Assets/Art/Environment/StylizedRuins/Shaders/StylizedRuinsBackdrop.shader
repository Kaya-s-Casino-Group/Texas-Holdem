Shader "StylizedRuins/Backdrop"
{
    Properties
    {
        [MainTexture] _BaseMap("Painted Layer (RGBA)", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Exposure("Layer Brightness", Range(0,4)) = 1
        _PaintContrast("Paint Contrast", Range(0,2)) = 1
        _ShadowLift("Paint Shadow Lift", Range(0,0.4)) = 0
        [Toggle] _ZWrite("Solid Cutout / Write Depth", Float) = 0
        _Cutoff("Solid Alpha Cutoff", Range(0.01,0.99)) = 0.5
        _TopFadeStart("Top Alpha Fade Start (UV)", Float) = 1
        _TopFadeEnd("Top Alpha Fade End (UV)", Float) = 1.1
        [Toggle] _ProjectiveUV("Baked Perspective UV", Float) = 0
        _GroundFadeHeight("Ground Contact Blend Height (0 = Off)", Float) = 0
        _GroundPlaneY("Ground Contact Plane Y", Float) = 0
        _GroundFadeBreakup("Ground Contact Edge Variation", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Backdrop"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Exposure;
                float _PaintContrast;
                float _ShadowLift;
                float _ZWrite;
                float _Cutoff;
                float _TopFadeStart;
                float _TopFadeEnd;
                float _ProjectiveUV;
                float _GroundFadeHeight;
                float _GroundPlaneY;
                float _GroundFadeBreakup;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.world=TransformObjectToWorld(v.positionOS.xyz);
                o.uv=float3(v.uv.xy,_ProjectiveUV>.5?v.uv.z:1); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=(i.uv.xy/max(i.uv.z,.00001))*_BaseMap_ST.xy+_BaseMap_ST.zw;
                half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;
                c.a*=1-smoothstep(_TopFadeStart,max(_TopFadeStart+.0001,_TopFadeEnd),uv.y);
                if(_GroundFadeHeight>.0001)
                {
                    float breakup=(sin(i.world.x*1.7+i.world.z*.61)+sin(i.world.x*3.13-i.world.z*.83))*.25*_GroundFadeBreakup;
                    c.a*=smoothstep(0,_GroundFadeHeight,i.world.y-_GroundPlaneY+breakup);
                }
                if(_ZWrite>.5)
                {
                    clip(c.a-_Cutoff);
                    c.a=1; // Solid painted surfaces occlude per pixel, including intersecting cards.
                }
                // Per-layer art calibration keeps painted darks readable under the project's existing grade.
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    c.rgb=SRGBToLinear(saturate(LinearToSRGB(max(c.rgb,0))*_PaintContrast+_ShadowLift));
                #else
                    c.rgb=saturate(c.rgb*_PaintContrast+_ShadowLift);
                #endif
                c.rgb*=c.a*_Exposure;
                return c;
            }
            ENDHLSL
        }
    }
}
