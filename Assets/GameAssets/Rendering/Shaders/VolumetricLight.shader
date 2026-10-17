// Adapted from LifeSugar/RayMarchingGodRay (MIT). See RayMarchingGodRay-LICENSE.txt.
Shader "Hidden/RetroRendering/VolumetricLight"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "HeightFogCommon.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _FogSampling; // steps, jitter, bilateral depth tolerance, unused
        float4 _FogTexelSize;

        float4 FilterScattering(float2 uv, float2 axis)
        {
            float centerDepth = HeightFogEyeDepth(uv);
            float3 sum = 0;
            float weights = 0;
            [unroll] for (int i = -2; i <= 2; ++i)
            {
                float2 sampleUV = saturate(uv + axis * i * _FogTexelSize.xy);
                float dz = abs(HeightFogEyeDepth(sampleUV) - centerDepth);
                float weight = exp2(-i * i * 0.5 - dz / _FogSampling.z);
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, sampleUV).rgb * weight;
                weights += weight;
            }
            // Keep per-pixel extinction intact. Blurring T here would change fog when lighting is toggled.
            return float4(sum / max(weights, 1e-5), SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).a);
        }
        ENDHLSL
        Pass
        {
            Name "Ray March Scattering"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Scatter
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
            float4 _FogMainLight;
            float4 _FogLights[64]; // URP packed index, scattering, anisotropy, squared radius
            int _FogLightCount;
            float4 _FogLightTint;

            float FogMainShadow(float3 positionWS)
            {
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    // Sample the shadow atlas at the volume point, never the screen-space surface shadow.
                    // The screen-space keyword alone does not tell us whether the atlas has cascades.
                    int cascade = _FogMainLight.z > 1 ? (int)ComputeCascadeIndex(positionWS) : 0;
                    float4 coord = float4(mul(_MainLightWorldToShadow[cascade], float4(positionWS, 1)).xyz, 0);
                    return SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
                        coord, GetMainLightShadowSamplingData(), GetMainLightShadowParams(), false);
                #else
                    return 1;
                #endif
            }

            float3 FogLighting(float3 positionWS, float3 rayDirection)
            {
                float3 radiance = 0;
                if (_FogMainLight.x > 0)
                {
                    Light light = GetMainLight();
                    #if defined(_LIGHT_COOKIES)
                        light.color *= SampleMainLightCookie(positionWS);
                    #endif
                    radiance += light.color * FogMainShadow(positionWS) * _FogMainLight.x *
                        CornetteShanksPhaseFunction(_FogMainLight.y, dot(rayDirection, light.direction));
                }
                [loop] for (int i = 0; i < _FogLightCount; ++i)
                {
                    float4 parameters = _FogLights[i];
                    int index = (int)parameters.x;
                    Light light = GetAdditionalPerObjectLight(index, positionWS);
                    if (light.distanceAttenuation <= 0) continue;
                    float shadow = AdditionalLightRealtimeShadow(index, positionWS, light.direction);
                    #if defined(_LIGHT_COOKIES)
                        light.color *= SampleAdditionalLightCookie(index, positionWS);
                    #endif
                    #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                        float4 lightPosition = _AdditionalLightsBuffer[index].position;
                    #else
                        float4 lightPosition = _AdditionalLightsPosition[index];
                    #endif
                    float3 offset = lightPosition.xyz - positionWS;
                    float radiusFade = lightPosition.w == 0 ? 1 : smoothstep(0, parameters.w, dot(offset, offset));
                    radiance += light.color * (light.distanceAttenuation * shadow * parameters.y * radiusFade * radiusFade *
                        CornetteShanksPhaseFunction(parameters.z, dot(rayDirection, light.direction)));
                }
                return radiance * _FogLightTint.rgb;
            }

            float4 Scatter(Varyings input) : SV_Target
            {
                float3 origin, direction;
                float rayLength;
                HeightFogRay(input.texcoord, origin, direction, rayLength);
                if (_HeightFogNoise.x > 0)
                {
                    int noiseSteps = (int)_HeightFogNoise.y;
                    float segmentLength = rayLength / noiseSteps;
                    float transmission = 1;
                    float3 radiance = 0;
                    [loop] for (int n = 0; n < noiseSteps; ++n)
                    {
                        float integral = HeightFogNoisySegment(origin, direction, n * segmentLength, segmentLength);
                        if (integral <= 0) continue;
                        float tau = integral * _HeightFogMedium.y;
                        float segmentT = exp(-tau);
                        // Analytic attenuation within this segment; Taylor form avoids cancellation in thin fog.
                        float weight = integral * (tau > .001 ? (1 - segmentT) / tau : 1 - tau * .5 + tau * tau / 6);
                        float3 positionWS = origin + direction * ((n + .5) * segmentLength);
                        radiance += FogLighting(positionWS, direction) * (transmission * weight);
                        transmission *= segmentT;
                    }
                    return float4(radiance, transmission);
                }
                int steps = (int)_FogSampling.x;
                float stepLength = rayLength / steps;
                float noise = frac(52.9829189 * frac(dot(floor(input.positionCS.xy), float2(0.06711056, 0.00583715))));
                float jitter = lerp(0.5, noise, _FogSampling.y);
                float3 scattering = 0;
                [loop] for (int i = 0; i < steps; ++i)
                {
                    float distance = (i + jitter) * stepLength;
                    float3 positionWS = origin + direction * distance;
                    float density = HeightFogDensity(positionWS.y);
                    if (density <= 0) continue;
                    float transmission = exp(-_HeightFogMedium.y * HeightFogIntegral(origin.y, direction.y, distance));
                    scattering += FogLighting(positionWS, direction) * (density * transmission * stepLength);
                }
                return float4(scattering, 0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Bilateral Horizontal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { return FilterScattering(input.texcoord, float2(1, 0)); }
            ENDHLSL
        }
        Pass
        {
            Name "Bilateral Vertical"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target { return FilterScattering(input.texcoord, float2(0, 1)); }
            ENDHLSL
        }
        Pass
        {
            Name "Add Scattering"
            Blend One One
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                return float4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord).rgb, 0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Composite Textured Fog and Scattering"
            Blend One SrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            float4 Frag(Varyings input) : SV_Target
            {
                float4 volume = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
                return float4(volume.rgb + _HeightFogColor.rgb * (1 - volume.a), volume.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
