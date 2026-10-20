Shader "StylizedGrass/Opaque Grass"
{
    Properties
    {
        _RootColor("Grass Root",Color)=(.12,.045,.075,1)
        _TipColor("Grass Tip",Color)=(.42,.09,.15,1)
        _DryColor("Dry Tips",Color)=(.3,.15,.16,1)
        _GrassColor("Turf Color",Color)=(.22,.065,.09,1)
        _DirtColor("Bare Soil Color",Color)=(.38,.09,.12,1)
        _GrassMap("Turf Detail",2D)="white"{}
        _GrassHeight("Turf Height",2D)="gray"{}
        [Normal] _GrassNormal("Turf Normal",2D)="bump"{}
        _GrassTiling("Turf Tiling / Meter",Float)=1.4
        _SoilTiling("Soil Detail / Meter",Float)=5
        _BlendSharpness("Grass Soil Blend",Range(.5,5))=1.7
        _NormalStrength("Turf Normal Strength",Range(0,2))=.55
        _WindDirection("Wind Direction XZ",Vector)=(1,0,.35,0)
        _WindStrength("Wind Amplitude / Meter",Range(0,.5))=.08
        _WindSpeed("Wind Speed",Range(0,4))=.7
        _WindScale("Wind Wave Scale",Float)=1.1
        [HideInInspector] _WindTime("Wind Time Override (-1 = Auto)",Float)=-1
        [Header(Lodging)]
        _Lodging("Lodging Amount",Range(0,1))=0
        _LodgingDirection("Lodging Direction (0=X 90=Z)",Range(0,360))=25
        _LodgingVariation("Lodging Patch Variation",Range(0,1))=.85
        _LodgingPatchSize("Lodging Patch Size",Range(.5,12))=3.5
        _Smoothness("Smoothness",Range(0,1))=.18
        _ColorVariation("Dry Color Variation",Range(0,1))=.35
        _RootOcclusion("Root Occlusion",Range(0,1))=.65
        _NormalUp("Soft Upward Normals",Range(0,1))=.45
        _Fill("Painted Ambient Fill",Range(0,1))=.12
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }
        Cull [_Cull]
        ZWrite On
        ZTest LEqual
        HLSLINCLUDE
        #define SG_GRASS 1
        #include "StylizedGrassInput.hlsl"
        ENDHLSL
        Pass
        {
            Name "GBuffer"
            Tags { "LightMode"="UniversalGBuffer" }
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SGVertex
            #pragma fragment SGGBuffer
            #pragma multi_compile_instancing
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #include "StylizedGrassPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SGVertex
            #pragma fragment SGForward
            #pragma multi_compile_instancing
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fog
            #include "StylizedGrassPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SGShadowVertex
            #pragma fragment SGDepth
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "StylizedGrassPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SGVertex
            #pragma fragment SGDepth
            #pragma multi_compile_instancing
            #include "StylizedGrassPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SGVertex
            #pragma fragment SGDepthNormals
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "StylizedGrassPasses.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex SGMetaVertex
            #pragma fragment SGMeta
            #pragma multi_compile_instancing
            #include "StylizedGrassPasses.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
