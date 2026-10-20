Shader "CardArt/Card Unlit Forward"
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
        _SealRotation ("火漆旋转角度（逆时针）", Range(-180,180)) = -12
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
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Unlit" "CardUVRectContract"="OffsetXYScaleZW" "CardSurfaceContract"="UV1BackXEdgeY" "CardFoilContract"="ColorStrengthV1" "CardSealContract"="ColorOverlayV1" "CardRedCrossContract"="OverlayV1" }
        Cull Back
        ZWrite On
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex CardVertex
            #pragma fragment CardColor
            #pragma multi_compile_instancing
            #include "CardUnlit.hlsl"
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
            #include "CardUnlit.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
