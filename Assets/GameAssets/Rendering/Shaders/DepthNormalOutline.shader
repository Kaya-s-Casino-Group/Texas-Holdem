Shader "Hidden/RetroRendering/DepthNormalOutline"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Single Sided Directional Edges"
            Blend Off
            ColorMask RGBA
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Outline
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _OutlineTexelSize, _OutlineParameters;
            half4 _OutlineColor;
            bool Background(float d)
            {
                #if UNITY_REVERSED_Z
                    return d <= .000001;
                #else
                    return d >= .999999;
                #endif
            }
            float EyeDepth(float d)
            {
                if (unity_OrthoParams.w > .5)
                {
                    #if UNITY_REVERSED_Z
                        d = 1-d;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, d);
                }
                return LinearEyeDepth(d, _ZBufferParams);
            }
            float DetectEdge(float2 uv, float2 direction, bool ownsNormal, float z, float3 n)
            {
                float2 q = uv + direction * _OutlineTexelSize.xy;
                if (any(q <= 0) || any(q >= 1)) return 0;
                float od = SampleSceneDepth(q);
                if (Background(od)) return 1;
                if (SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, q).r < .5) return 0;
                float oz = EyeDepth(od);
                float dz = abs(z-oz) / max(min(z,oz), .001);
                if (dz >= _OutlineParameters.y)
                    return z < oz ? smoothstep(_OutlineParameters.y, _OutlineParameters.y*1.5, dz) : 0;
                float3 on = SampleSceneNormals(q);
                float dn = 1-saturate(dot(n,on) * rsqrt(max(dot(n,n)*dot(on,on),1e-8)));
                return ownsNormal ? smoothstep(_OutlineParameters.z, _OutlineParameters.z+.1, dn) : 0;
            }
            half4 Outline(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float d = SampleSceneDepth(uv);
                if (Background(d) || SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).r < .5) return 0;
                float z = EyeDepth(d);
                float3 n = SampleSceneNormals(uv);
                // RGBA = left, right, down, up. Depth edges belong to the nearer surface.
                // Normal-only creases use right/up ownership to avoid drawing both sides.
                return float4(DetectEdge(uv,float2(-1,0),false,z,n),
                              DetectEdge(uv,float2(1,0),true,z,n),
                              DetectEdge(uv,float2(0,-1),false,z,n),
                              DetectEdge(uv,float2(0,1),true,z,n));
            }
            ENDHLSL
        }
        Pass
        {
            Name "Layer Mask"
            ZTest Equal
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MaskVertex
            #pragma fragment MaskFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings MaskVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 MaskFragment(Varyings input) : SV_Target { return 1; }
            ENDHLSL
        }
        Pass
        {
            Name "Screen Pixel Outline Composite"
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Composite
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_OutlineInclusionMask);
            float4 _OutlineTexelSize, _OutlineParameters, _OutlineOutputSize;
            half4 _OutlineColor;
            half4 Composite(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                if (SAMPLE_TEXTURE2D_X(_OutlineInclusionMask, sampler_PointClamp, uv).r < .5) return 0;
                float2 cell = floor(uv * _OutlineTexelSize.zw);
                float2 within = frac(uv * _OutlineTexelSize.zw);
                float2 pitch = _OutlineOutputSize.xy * _OutlineTexelSize.xy;
                float4 distanceToEdge = float4(within.x, 1-within.x, within.y, 1-within.y) * pitch.xxyy;
                const float2 directions[4] = { float2(-1,0),float2(1,0),float2(0,-1),float2(0,1) };
                float alpha = 0;
                [unroll] for (int i=0;i<4;i++)
                {
                    // Up to four output pixels; search further cells when output equals internal size.
                    [unroll] for (int k=0;k<4;k++)
                    {
                        float2 q = (cell + .5 + directions[i]*k) * _OutlineTexelSize.xy;
                        if (any(q <= 0) || any(q >= 1)) break;
                        if (SAMPLE_TEXTURE2D_X(_OutlineInclusionMask, sampler_PointClamp, q).r < .5) break;
                        float edge = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, q)[i];
                        float distance = distanceToEdge[i] + k * (i < 2 ? pitch.x : pitch.y);
                        // Pixel-area coverage keeps fractional widths and noninteger upscales stable.
                        alpha = max(alpha, edge * saturate(_OutlineParameters.x + .5 - distance));
                        if (edge > 0 || distance > _OutlineParameters.x + .5) break;
                    }
                }
                return half4(_OutlineColor.rgb, alpha * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
