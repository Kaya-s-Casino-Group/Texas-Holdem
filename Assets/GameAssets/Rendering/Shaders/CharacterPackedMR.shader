Shader "Rendering/Character Deferred Packed MR"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Color", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        [Normal][NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _Metallic ("Metallic Multiplier", Range(0,1)) = 1
        _Roughness ("Roughness Multiplier", Range(0,1)) = 1
        [NoScaleOffset] _PackedMRMap ("Metallic (R), Roughness (G)", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [NoScaleOffset] _OcclusionMap ("Occlusion (R)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1
        [Header(Character Fill Lighting)]
        _CharacterFillColor ("Fill Color", Color) = (1, 0.95, 0.88, 1)
        _CharacterFillStrength ("Soft Camera Fill", Range(0,0.5)) = 0
        _CharacterAmbientFloor ("Shadow Readability Floor", Range(0,0.25)) = 0
        _CharacterFillWrap ("Fill Wrap", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        Cull [_Cull]
        ZWrite On
        ZTest LEqual
        HLSLINCLUDE
        #include "CharacterPackedMRInput.hlsl"
        ENDHLSL

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode"="UniversalGBuffer" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore
            #pragma vertex CharacterVertex
            #pragma fragment CharacterGBuffer
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #include "CharacterPackedMRPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CharacterVertex
            #pragma fragment CharacterDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "CharacterPackedMRPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
