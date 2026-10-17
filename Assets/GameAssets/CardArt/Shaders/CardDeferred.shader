Shader "CardArt/Card Deferred"
{
    Properties
    {
        [MainTexture] _BaseMap ("Card Atlas", 2D) = "white" {}
        [PerRendererData] _FrontUVRect ("Front UV Rect (offset XY, scale ZW)", Vector) = (0,0,1,1)
        [PerRendererData] _ColorTint ("Instance Color Tint", Color) = (1,1,1,1)
        [PerRendererData] _FoilEnabled ("Foil Enabled", Float) = 0
        [HDR][PerRendererData] _FoilColor ("Foil Color", Color) = (1,1,1,1)
        [PerRendererData] _FoilStrength ("Foil Strength", Range(0,5)) = 1
        [NoScaleOffset] _FoilMap ("镭射纹理（灰度）", 2D) = "gray" {}
        _FoilSpeed ("镭射动画速度", Range(0,3)) = 0.25
        [PerRendererData] _SealEnabled ("开启火漆", Float) = 0
        [PerRendererData] _SealEmissionStrength ("火漆自发光强度", Range(0,1)) = 0
        [PerRendererData] _SealColor ("火漆颜色", Color) = (0.65,0.08,0.04,1)
        [NoScaleOffset] _SealMap ("火漆颜色贴图", 2D) = "black" {}
        [Normal][NoScaleOffset] _SealNormalMap ("火漆法线贴图", 2D) = "bump" {}
        _SealRotation ("火漆旋转角度（逆时针）", Range(-180,180)) = -12
        _SealNormalScale ("火漆法线强度", Range(0,2)) = 1
        _SealRect ("火漆位置 XY / 尺寸 ZW", Vector) = (0.30,0.62,0.45254834,0.3016989)
        [PerRendererData] _RedCrossEnabled ("显示红叉", Float) = 0
        [NoScaleOffset] _RedCrossMap ("红叉贴图", 2D) = "black" {}
        _BackUVRect ("Back UV Rect (offset XY, scale ZW)", Vector) = (0,0,1,1)
        [MainColor] _BaseColor ("Global Tint", Color) = (1,1,1,1)
        _EdgeColor ("Paper Edge", Color) = (0.76,0.65,0.44,1)
        _Cutoff ("Alpha Cutoff", Range(0.001,1)) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="SimpleLit" "CardUVRectContract"="OffsetXYScaleZW" "CardSurfaceContract"="UV1BackXEdgeY" "CardFoilContract"="ColorStrengthV1" "CardSealContract"="ColorNormalV1" "CardRedCrossContract"="OverlayV1" }
        Cull Back
        ZWrite On
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode"="UniversalGBuffer" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex CardVertex
            #pragma fragment CardGBuffer
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "CardDeferredPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment CardForward
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #define _SPECULAR_COLOR 1
            #include "CardDeferredPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardShadowVertex
            #pragma fragment CardDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "CardDeferredPasses.hlsl"
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
            #include "CardDeferredPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment CardDepthNormals
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "CardDeferredPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
