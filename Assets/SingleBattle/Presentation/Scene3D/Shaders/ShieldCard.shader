Shader "CardArt/Shield Deferred"
{
Properties
{
[MainTexture] _BaseMap("Card Atlas",2D)="white"{}
[NoScaleOffset] _NoiseMap("Perlin Noise",2D)="gray"{}
[PerRendererData] _FrontUVRect("Front UV Rect",Vector)=(0,0,1,1)
_BackUVRect("Back UV Rect",Vector)=(0,0,1,1)
[PerRendererData] _Dissolve("Dissolve",Range(0,1))=0
[PerRendererData] _NoiseOffset("Noise Offset",Vector)=(0,0,0,0)
[PerRendererData] _LandingFlash("Landing Flash",Range(0,1))=0
_NoiseScale("Noise Scale",Range(.1,12))=2
_NoiseStrength("Noise Strength",Range(0,.5))=.12
_EdgeWidth("Edge Width",Range(.001,.2))=.035
[HDR] _EdgeColor("Emission Edge",Color)=(2.8,1.1,.2,1)
_Cutoff("Alpha Cutoff",Range(.001,1))=.01
}
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="SimpleLit" }
        Cull Off
        ZWrite On
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode"="UniversalGBuffer" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex ShieldVertex
            #pragma fragment ShieldGBuffer
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "ShieldCard.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShieldVertex
            #pragma fragment ShieldForward
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
            #include "ShieldCard.hlsl"
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
            #pragma vertex ShieldShadowVertex
            #pragma fragment ShieldDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "ShieldCard.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShieldVertex
            #pragma fragment ShieldDepth
            #pragma multi_compile_instancing
            #include "ShieldCard.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShieldVertex
            #pragma fragment ShieldNormals
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "ShieldCard.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
