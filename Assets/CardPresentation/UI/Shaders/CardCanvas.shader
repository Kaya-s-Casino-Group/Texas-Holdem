Shader "CardPresentation/Card Canvas"
{
    Properties
    {
        [PerRendererData] _MainTex ("Card Atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [NoScaleOffset] _FoilMap ("Foil Pattern", 2D) = "gray" {}
        _FoilSpeed ("Foil Speed", Range(0,3)) = 0.25
        [NoScaleOffset] _SealMap ("Seal", 2D) = "black" {}
        [NoScaleOffset] _SealNormalMap ("Seal Normal", 2D) = "bump" {}
        _SealRect ("Seal Position / Size", Vector) = (0.30,0.62,0.45254834,0.3016989)
        _SealRotation ("Seal Rotation", Float) = -12
        _SealNormalScale ("Seal Normal Strength", Range(0,2)) = 1
        [NoScaleOffset] _RedCrossMap ("Red Cross", 2D) = "black" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            Name "CardCanvas"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "../../../CardArt/Shaders/CardSurfaceCommon.hlsl"
            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0; // atlas origin xy, local xy
                float4 foil : TEXCOORD1; // HDR RGB, strength
                float4 seal : TEXCOORD2; // RGB, emission
                float4 state : TEXCOORD3; // flags*2 + desaturation, atlas width/height, tint exposure
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
                float4 foil : TEXCOORD1;
                float4 seal : TEXCOORD2;
                float4 state : TEXCOORD3;
                float4 mask : TEXCOORD4;
                float3 normal : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex, _FoilMap, _SealMap, _SealNormalMap, _RedCrossMap;
            float4 _Color, _ClipRect, _SealRect;
            float4 _MainTex_TexelSize;
            float _FoilSpeed, _SealRotation, _SealNormalScale;
            float _UIMaskSoftnessX, _UIMaskSoftnessY;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color; o.uv = v.uv; o.foil = v.foil; o.seal = v.seal; o.state = v.state;
                o.normal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 pixelSize = o.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = float4(v.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * float2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float2 atlasUV = CardAtlasSampleUV(i.uv.zw,float4(i.uv.xy,i.state.yz),_MainTex_TexelSize.xy);
                float4 card = tex2D(_MainTex, atlasUV) * i.color;
                card.rgb *= max(1, i.state.w);
                float3 emission = 0;
                float flags = floor(i.state.x / 2);
                float desaturation = i.state.x - flags * 2;
                float foilEnabled = fmod(flags, 2);
                float sealEnabled = fmod(floor(flags / 2), 2);
                float crossEnabled = fmod(floor(flags / 4), 2);
                if (foilEnabled > 0.5 && i.foil.w > 0)
                {
                    float pattern = tex2D(_FoilMap, i.uv.zw * float2(2,3)).r;
                    float3 foil; float amount;
                    CardFoilSpectrum(i.uv.zw, i.normal.xy, abs(i.normal.z), pattern, _Time.y,
                        _FoilSpeed, i.foil.w, card.rgb, i.foil.rgb, foil, amount);
                    card.rgb *= lerp(1.0.xxx, 0.2 + foil * 1.1, saturate(amount * 0.65));
                    emission = foil * amount * 0.3;
                }
                if (sealEnabled > 0.5)
                {
                    float2 uv = CardSealSampleUV(i.uv.zw, _SealRect, _SealRotation);
                    float4 seal = tex2D(_SealMap, saturate(uv));
                    seal.a *= step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
                    float3 normal = UnpackNormal(tex2D(_SealNormalMap, saturate(uv)));
                    normal.xy *= _SealNormalScale;
                    float light = 0.75 + 0.25 * saturate(dot(normalize(normal), normalize(float3(-0.3,0.4,1))));
                    seal.rgb *= i.seal.rgb * light;
                    card.rgb = lerp(card.rgb, seal.rgb, seal.a);
                    emission = emission * (1 - seal.a) + seal.rgb * seal.a * i.seal.w;
                }
                if (crossEnabled > 0.5)
                {
                    float4 cross = tex2D(_RedCrossMap, i.uv.zw);
                    card.rgb = lerp(card.rgb, cross.rgb, cross.a); emission *= 1 - cross.a;
                }
                card.rgb += emission;
                card.rgb = lerp(card.rgb, dot(card.rgb, float3(0.2126,0.7152,0.0722)).xxx, saturate(desaturation));
                #ifdef UNITY_UI_CLIP_RECT
                float2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                card.a *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(card.a - 0.001);
                #endif
                return card;
            }
            ENDCG
        }
    }
}
