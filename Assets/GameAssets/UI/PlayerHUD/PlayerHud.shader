Shader "Poker/UI/Player HUD"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Toggle] _Dissolve ("Health texture dissolve", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Alpha Clip", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 effect:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; float4 effect:TEXCOORD1; float4 mask:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _Color, _TextureSampleAdd, _ClipRect;
            float _Dissolve, _UIMaskSoftnessX, _UIMaskSoftnessY;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.effect=v.effect;o.color=v.color*_Color;
                float2 pixelSize=o.vertex.w/abs(mul((float2x2)UNITY_MATRIX_P,_ScreenParams.xy));
                float4 rect=clamp(_ClipRect,-2e10,2e10);
                o.mask=float4(v.vertex.xy*2-rect.xy-rect.zw,.25/(.25*float2(_UIMaskSoftnessX,_UIMaskSoftnessY)+abs(pixelSize)));
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float4 paint=tex2D(_MainTex,i.uv)+_TextureSampleAdd;
                float4 result=paint*i.color;
                if (_Dissolve>.5)
                {
                    // Follow the original paint's pigment and scratches, not a straight geometric cutoff.
                    float grain=saturate(paint.r*2.4-paint.g*.4);
                    float threshold=i.effect.x+(grain-.5)*i.effect.w;
                    float feather=max(fwidth(threshold)*1.25,.003);
                    float coverage=smoothstep(-feather,feather,i.effect.z-threshold);
                    if(i.effect.z<=0) coverage=0;
                    if(i.effect.z>=1) coverage=1;
                    result.a*=coverage;
                }
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask=saturate((_ClipRect.zw-_ClipRect.xy-abs(i.mask.xy))*i.mask.zw);
                result.a*=mask.x*mask.y;
                #endif
                // Also discard the transparent portrait/frame texels; the stencil mask clips the portrait silhouette.
                clip(result.a-.01);
                return result;
            }
            ENDCG
        }
    }
}
