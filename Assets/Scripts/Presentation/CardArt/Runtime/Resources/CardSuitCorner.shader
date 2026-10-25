Shader "CardArt/Readable Suit Corner"
{
    Properties
    {
        [PerRendererData] _MainTex ("Existing card atlas", 2D) = "white" {}
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil
        {
            Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp]
            ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]
        }
        Cull Back
        ZWrite Off
        ZTest [_ZTest]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _ClipRect;
            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 state : TEXCOORD1; fixed4 color : COLOR; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; float fade : TEXCOORD2; fixed4 color : COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex); output.local = input.vertex;
                output.uv = input.uv; output.fade = input.state.x; output.color = input.color;
                return output;
            }
            half Ink(float2 uv) { return 1 - smoothstep(.14, .23, tex2D(_MainTex, uv).g); }
            fixed4 frag(Varyings input) : SV_Target
            {
                half ink = Ink(input.uv);
                float2 stepUV = _MainTex_TexelSize.xy * .85;
                half edge = max(max(Ink(input.uv + float2(stepUV.x, 0)), Ink(input.uv - float2(stepUV.x, 0))),
                    max(Ink(input.uv + float2(0, stepUV.y)), Ink(input.uv - float2(0, stepUV.y))));
                edge = max(edge, ink);
                half3 color = lerp(half3(.96, .84, .62), tex2D(_MainTex, input.uv).rgb, ink) * input.color.rgb;
                color = lerp(color, dot(color, half3(.299, .587, .114)).xxx, input.fade);
                half alpha = edge * input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(input.local.xy, _ClipRect);
                #endif
                clip(alpha - .001);
                return half4(color, alpha);
            }
            ENDCG
        }
    }
}
