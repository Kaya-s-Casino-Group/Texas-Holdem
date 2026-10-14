Shader "Hidden/RetroRendering/LowResolutionSSAO"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        float4 _AOParameters; // world radius, world bias, intensity, sample count
        float4 _AOTexelSize;
        float _AOSharpness;

        bool IsBackground(float d)
        {
            #if UNITY_REVERSED_Z
                return d <= 0.000001;
            #else
                return d >= 0.999999;
            #endif
        }
        float3 PositionWS(float2 uv, float depth)
        {
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
            return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
        }
        float3 PositionWS(float2 uv) { return PositionWS(uv, SampleSceneDepth(uv)); }
        float EyeDepth(float3 p) { return -TransformWorldToView(p).z; }
        float3 SurfaceNormal(float2 uv, float3 p)
        {
            float3 l = PositionWS(saturate(uv - float2(_AOTexelSize.x, 0)));
            float3 r = PositionWS(saturate(uv + float2(_AOTexelSize.x, 0)));
            float3 d = PositionWS(saturate(uv - float2(0, _AOTexelSize.y)));
            float3 u = PositionWS(saturate(uv + float2(0, _AOTexelSize.y)));
            float z = EyeDepth(p);
            float3 dx = abs(EyeDepth(l)-z) < abs(EyeDepth(r)-z) ? p-l : r-p;
            float3 dy = abs(EyeDepth(d)-z) < abs(EyeDepth(u)-z) ? p-d : u-p;
            float3 n = cross(dx,dy);
            float3 v = GetWorldSpaceNormalizeViewDir(p);
            n = dot(n,n) > 1e-16 ? normalize(n) : v;
            return dot(n,v) < 0 ? -n : n;
        }
        half4 EvaluateAO(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float depth = SampleSceneDepth(uv);
            if (IsBackground(depth)) return 1;
            float3 p = PositionWS(uv,depth);
            float3 n = SurfaceNormal(uv,p);
            float3 t = normalize(cross(abs(n.z) < 0.999 ? float3(0,0,1) : float3(0,1,0),n));
            float3 b = cross(n,t);
            // Fixed screen-space rotation: no temporal noise or history at pixel-art resolutions.
            float2 pixel = floor(input.positionCS.xy);
            float angle = frac(52.9829189 * frac(dot(pixel,float2(0.06711056,0.00583715)))) * 6.2831853;
            float occlusion = 0;
            int count = (int)_AOParameters.w;
            [loop] for (int i = 0; i < count; ++i)
            {
                float f = (i + 0.5) / count;
                float phi = angle + i * 2.39996323;
                float z = 0.15 + 0.85 * f;
                float xy = sqrt(1-z*z);
                float3 direction = t*(cos(phi)*xy) + b*(sin(phi)*xy) + n*z;
                float scale = lerp(0.15,1,frac(i*0.61803399 + 0.5));
                float3 samplePosition = p + direction * (_AOParameters.x * scale);
                float4 clip = TransformWorldToHClip(samplePosition);
                if (clip.w <= 0 || EyeDepth(samplePosition) <= _ProjectionParams.y) continue;
                float2 sampleUV = clip.xy / clip.w * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    sampleUV.y = 1-sampleUV.y;
                #endif
                if (any(sampleUV < 0) || any(sampleUV > 1)) continue;
                float sampleDepth = SampleSceneDepth(sampleUV);
                if (IsBackground(sampleDepth)) continue;
                float3 surface = PositionWS(sampleUV,sampleDepth);
                float rangeWeight = saturate(1-distance(p,surface)/_AOParameters.x);
                occlusion += (EyeDepth(surface) < EyeDepth(samplePosition)-_AOParameters.y ? 1 : 0) * rangeWeight;
            }
            return saturate(1-occlusion/count);
        }
        float FilterAO(float2 uv, float2 axis)
        {
            float depth = SampleSceneDepth(uv);
            if (IsBackground(depth)) return 1;
            float z = EyeDepth(PositionWS(uv,depth));
            float sum = 0, weights = 0;
            [unroll] for (int i = -2; i <= 2; ++i)
            {
                float2 offsetUV = saturate(uv + axis * i * _AOTexelSize.xy);
                float d = SampleSceneDepth(offsetUV);
                float dz = abs(EyeDepth(PositionWS(offsetUV,d))-z);
                float weight = exp2(-i*i*0.5 - dz * _AOSharpness / _AOParameters.x);
                if (IsBackground(d)) weight = 0;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,offsetUV).r * weight;
                weights += weight;
            }
            return sum/max(weights,0.0001);
        }
        half4 BlurX(Varyings input) : SV_Target { return FilterAO(input.texcoord,float2(1,0)); }
        half4 Composite(Varyings input) : SV_Target
        {
            float visibility = saturate(1-(1-FilterAO(input.texcoord,float2(0,1)))*_AOParameters.z);
            return half4(visibility,visibility,visibility,1);
        }
        ENDHLSL
        Pass
        {
            Name "AO"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment EvaluateAO
            ENDHLSL
        }
        Pass
        {
            Name "Bilateral Horizontal"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment BlurX
            ENDHLSL
        }
        Pass
        {
            Name "Bilateral Vertical and Composite"
            Blend DstColor Zero
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
