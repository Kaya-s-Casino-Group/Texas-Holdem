Shader "Hidden/RetroRendering/Bloom"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _BloomParameters; // linear threshold, knee, HDR clamp, scatter
        float4 _BloomSourceTexelSize;
        float4 _BloomLowTexelSize;
        float4 _BloomTint;
        float _BloomIntensity;
        TEXTURE2D_X(_BloomLowMip);

        float3 BoxFilter(float2 uv)
        {
            float2 t = _BloomSourceTexelSize.xy * 0.5;
            return (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-t.x, -t.y)).rgb +
                SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(t.x, -t.y)).rgb +
                SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-t.x, t.y)).rgb +
                SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(t.x, t.y)).rgb) * 0.25;
        }

        float4 Prefilter(Varyings input) : SV_Target
        {
            float3 color = clamp(BoxFilter(input.texcoord), 0, _BloomParameters.z);
            float brightness = max(color.r, max(color.g, color.b));
            float soft = clamp(brightness - _BloomParameters.x + _BloomParameters.y, 0, 2 * _BloomParameters.y);
            soft = soft * soft / max(4 * _BloomParameters.y, 1e-5);
            float contribution = max(brightness - _BloomParameters.x, soft) / max(brightness, 1e-5);
            return float4(color * contribution, 0);
        }

        float4 Downsample(Varyings input) : SV_Target { return float4(BoxFilter(input.texcoord), 0); }

        float4 Upsample(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float3 low = 0;
            // Normalized 3x3 tent; layer count changes the radius, not a uniform field's energy.
            [unroll] for (int y = -1; y <= 1; ++y)
            [unroll] for (int x = -1; x <= 1; ++x)
            {
                float weight = (x == 0 ? 2 : 1) * (y == 0 ? 2 : 1);
                low += SAMPLE_TEXTURE2D_X(_BloomLowMip, sampler_LinearClamp,
                    uv + float2(x, y) * _BloomLowTexelSize.xy).rgb * weight;
            }
            float3 high = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
            return float4(lerp(high, low / 16, _BloomParameters.w), 0);
        }

        float4 Composite(Varyings input) : SV_Target
        {
            return float4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb *
                _BloomTint.rgb * _BloomIntensity, 0);
        }
        ENDHLSL
        Pass
        {
            Name "Soft Threshold"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Prefilter
            ENDHLSL
        }
        Pass
        {
            Name "Downsample"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Downsample
            ENDHLSL
        }
        Pass
        {
            Name "Tent Upsample"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Upsample
            ENDHLSL
        }
        Pass
        {
            Name "Add Bloom"
            Blend One One
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
    }
    Fallback Off
}
