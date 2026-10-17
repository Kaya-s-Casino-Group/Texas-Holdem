Shader "Hidden/RetroRendering/Post"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "RetroPost"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            float4 _Grade, _Style, _Lens;
            float4 _Tint, _SourceTexelSize;
            float Bayer4(float2 pixel)
            {
                // Ordered 4x4 Bayer threshold in [-0.5, 0.5), fixed to the internal pixel grid.
                uint2 p = (uint2)pixel & 3u;
                uint a = ((p.x & 1u) ^ (p.y & 1u)) * 2u + (p.y & 1u);
                uint b = ((p.x >> 1u) ^ (p.y >> 1u)) * 2u + (p.y >> 1u);
                return (a * 4u + b + 0.5) / 16.0 - 0.5;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _SourceTexelSize.xy;
                float2 lensUV = uv / max(_BlitScaleBias.xy, 0.0001);
                float2 delta = (lensUV - 0.5) * texel * _Lens.z * 2;
                half4 sample = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float3 color = sample.rgb;
                if (_Lens.z > 0)
                {
                    color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv + delta).r;
                    color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv - delta).b;
                }
                color *= exp2(_Grade.x);
                // Grade/quantize in display space, then return linear color for URP's final encoding.
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = LinearToSRGB(max(color, 0));
                #endif
                color = (color - 0.5) * _Grade.y + 0.5;
                color = lerp(dot(color, float3(0.2126, 0.7152, 0.0722)).xxx, color, _Grade.z);
                color *= lerp(1.0.xxx, _Tint.rgb, _Grade.w);
                float2 pixel = floor(input.positionCS.xy);
                float steps = max(1, _Style.x - 1);
                float3 quantized = floor(saturate(color + Bayer4(pixel) * _Style.z / steps) * steps + 0.5) / steps;
                color = lerp(color, quantized, _Style.y);
                // Normalize each axis so the vignette adapts to ultrawide and portrait displays.
                float2 v = (lensUV - 0.5) * 2;
                float edge = smoothstep(1 - _Lens.y, 1.25, length(v));
                color *= 1 - edge * _Lens.x;
                color = saturate(color);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = SRGBToLinear(color);
                #endif
                return half4(color, sample.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
