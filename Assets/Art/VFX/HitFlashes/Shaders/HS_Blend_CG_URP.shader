Shader "VFX/URP/Blend CG"
{
    Properties
    {
        [MainTexture] _MainTex("Main Texture", 2D) = "white" {}
        _Noise("Noise", 2D) = "white" {}
        _Flow("Flow", 2D) = "white" {}
        _Mask("Mask", 2D) = "white" {}
        [MainColor] _Color("Color", Color) = (1,1,1,1)
        _Emission("Emission", Float) = 1
        _Opacity("Opacity", Float) = 1
        _SpeedMainTexUVNoiseZW("Main Speed XY / Noise Speed ZW", Vector) = (0,0,0,0)
        _DistortionSpeedXYPowerZ("Flow Speed XY / Strength Z", Vector) = (0,0,0,0)
        [ToggleUI] _Usecenterglow("Use Center Glow", Float) = 0
        [ToggleUI] _Usedepth("Use Depth Fade", Float) = 0
        _Depthpower("Depth Fade Distance", Float) = 1
        [Header(Render State)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
        [ToggleUI] _ZWrite("Depth Write", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZTest [_ZTest]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VfxVertex
            #pragma fragment VfxBlendFragment
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ALPHAMODULATE_ON
            #include "VfxParticleCommon.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Noise); SAMPLER(sampler_Noise);
            TEXTURE2D(_Flow); SAMPLER(sampler_Flow);
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Noise_ST, _Flow_ST, _Mask_ST;
                float4 _Color, _SpeedMainTexUVNoiseZW, _DistortionSpeedXYPowerZ;
                float _Emission, _Opacity, _Usecenterglow, _Usedepth, _Depthpower;
                float _SrcBlend, _DstBlend, _Cull, _ZTest, _ZWrite;
            CBUFFER_END

            half4 VfxBlendFragment(VfxVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv0.xy;
                float2 flowUV = uv*_Flow_ST.xy + _Flow_ST.zw + _TimeParameters.x*_DistortionSpeedXYPowerZ.xy;
                float4 flow = SAMPLE_TEXTURE2D(_Flow,sampler_Flow,flowUV);
                float4 mask = SAMPLE_TEXTURE2D(_Mask,sampler_Mask,uv*_Mask_ST.xy+_Mask_ST.zw);
                float2 distortion = (flow*mask).xy*_DistortionSpeedXYPowerZ.z;
                float2 mainUV = uv*_MainTex_ST.xy+_MainTex_ST.zw + _TimeParameters.x*_SpeedMainTexUVNoiseZW.xy-distortion;
                float4 main = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,mainUV);

                // UV0.w randomizes both noise UV axes; UV1.x is an emission multiplier.
                float randomOffset = fmod(input.uv0.w,0.9999);
                float2 noiseUV = (uv+randomOffset)*_Noise_ST.xy+_Noise_ST.zw + _TimeParameters.x*_SpeedMainTexUVNoiseZW.zw;
                float4 noise = SAMPLE_TEXTURE2D(_Noise,sampler_Noise,noiseUV);
                float3 rgb = main.rgb*noise.rgb*_Color.rgb*input.color.rgb;
                if (_Usecenterglow > 0.5)
                    rgb *= saturate(mask.rgb*saturate(mask.rgb-(1.0-input.uv0.z)));
                rgb *= _Emission*clamp(input.uv1.x,1.0,1000000.0);
                float alpha = main.a*noise.a*_Color.a*input.color.a;
                if (_Usedepth > 0.5) alpha *= VfxDepthFade(input.screenPosition,_Depthpower);
                alpha = saturate(alpha*_Opacity);

                #if defined(_ALPHAMODULATE_ON)
                    rgb = lerp(1.0.xxx,rgb,alpha);
                #endif
                #if defined(_ALPHAPREMULTIPLY_ON)
                    rgb *= alpha;
                #endif
                return half4(rgb,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
