Shader "Rendering/Gargoyle Cracks Deferred"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Color", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        [NoScaleOffset] _MetallicGlossMap ("Metallic (R) / Smoothness (A)", 2D) = "white" {}
        _Metallic ("Metallic Multiplier", Range(0,1)) = 1
        _Smoothness ("Smoothness Multiplier", Range(0,1)) = 1
        [NoScaleOffset] _CrackMap ("Cracks (R: Proximity / G: Reveal Order)", 2D) = "black" {}
        _CrackProgress ("裂痕程度", Range(0,1)) = 0
        _CrackScale ("裂痕密度（局部空间）", Range(0.25,4)) = 1.2
        _CrackWidth ("裂痕宽度", Range(0.02,0.5)) = 0.16
        _CrackDepth ("裂痕凹陷", Range(0,0.02)) = 0.004
        [ToggleUI] _UseSurfaceMask ("断面顶点标记（Color R）", Float) = 0
        _InteriorColor ("断面颜色", Color) = (0.332,0.358,0.381,1)
        _InteriorMetallic ("断面金属度", Range(0,1)) = 0
        _InteriorSmoothness ("断面光滑度", Range(0,1)) = 0.08
        [ToggleUI] _VATEnabled ("VAT 顶点动画", Float) = 0
        _VATProgress ("崩塌进度", Range(0,1)) = 0
        [NoScaleOffset] _VATPosition ("VAT Pivot Position", 2D) = "black" {}
        [NoScaleOffset] _VATRotation ("VAT Rotation XYZW", 2D) = "gray" {}
        [HideInInspector] _VATFrameCount ("VAT Frame Count", Float) = 110
        [HideInInspector] _VATTextureSize ("VAT Width/Height/Reciprocals", Vector) = (64,128,0.015625,0.0078125)
        [HideInInspector] _VATPositionMin ("VAT Position Min", Vector) = (0,0,0,0)
        [HideInInspector] _VATPositionRange ("VAT Position Range", Vector) = (1,1,1,0)
        _CrackColor ("裂痕颜色", Color) = (0.035,0.025,0.02,1)
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        // The imported statue contains double-sided geometry.
        Cull Off
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "GargoyleCracksInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode"="UniversalGBuffer" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex GargoyleVertex
            #pragma fragment GargoyleGBuffer
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #include "GargoyleCracksPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GargoyleShadowVertex
            #pragma fragment GargoyleShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "GargoyleVATDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GargoyleDepthVertex
            #pragma fragment GargoyleDepthFragment
            #pragma multi_compile_instancing
            #include "GargoyleVATDepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GargoyleVertex
            #pragma fragment GargoyleDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "GargoyleCracksPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
