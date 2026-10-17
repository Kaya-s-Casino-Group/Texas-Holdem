Shader "CardArt/Validation Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap ("卡牌图集", 2D) = "white" {}
        [PerRendererData] _FrontUVRect ("正面 UV（XY 偏移 / ZW 缩放）", Vector) = (0,0,1,1)
        [PerRendererData] _ColorTint ("单牌颜色", Color) = (1,1,1,1)
        _BackUVRect ("背面 UV（XY 偏移 / ZW 缩放）", Vector) = (0,0,1,1)
        [MainColor] _BaseColor ("整体颜色", Color) = (1,1,1,1)
        _EdgeColor ("纸边颜色", Color) = (0.76,0.65,0.44,1)
        _Cutoff ("透明裁切阈值", Range(0.001,1)) = 0.01
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline"
            "UniversalMaterialType"="Unlit"
            "CardUVRectContract"="OffsetXYScaleZW" "CardSurfaceContract"="UV1BackXEdgeY"
        }
        Cull Back
        ZWrite On

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "CardSurfaceCommon.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_TexelSize;
            float4 _BackUVRect;
            half4 _BaseColor;
            half4 _EdgeColor;
            half _Cutoff;
        CBUFFER_END
        UNITY_INSTANCING_BUFFER_START(CardInstances)
            UNITY_DEFINE_INSTANCED_PROP(float4, _FrontUVRect)
            UNITY_DEFINE_INSTANCED_PROP(float4, _ColorTint)
        UNITY_INSTANCING_BUFFER_END(CardInstances)

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 localUV : TEXCOORD0;
            float2 surface : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 localUV : TEXCOORD0;
            float2 surface : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings CardVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.localUV = input.localUV;
            output.surface = input.surface;
            return output;
        }

        half4 SampleCard(Varyings input)
        {
            UNITY_SETUP_INSTANCE_ID(input);
            float4 rect = input.surface.x > 0.5 ? _BackUVRect : UNITY_ACCESS_INSTANCED_PROP(CardInstances, _FrontUVRect);
            float2 uv = CardAtlasSampleUV(input.localUV, rect, _BaseMap_TexelSize.xy);
            half4 color = input.surface.y > 0.5 ? _EdgeColor : SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
            color *= _BaseColor * UNITY_ACCESS_INSTANCED_PROP(CardInstances, _ColorTint);
            clip(color.a - _Cutoff);
            return color;
        }

        half4 CardColor(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return SampleCard(input);
        }

        half4 CardDepth(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            SampleCard(input);
            return 0;
        }
        ENDHLSL

        Pass
        {
            Name "Unlit2D"
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment CardColor
            #pragma multi_compile_instancing
            ENDHLSL
        }
        // ForwardOnly also renders this unlit surface with the project's deferred renderer.
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment CardColor
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment CardDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
    FallBack Off
}
