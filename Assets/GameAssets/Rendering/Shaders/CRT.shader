Shader "Hidden/RetroRendering/CRT"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "CRT Television"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment CRT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            float4 _CRTShape, _CRTMask, _CRTOutputSize, _CRTViewport;
            float _CRTEncodeSRGB;
            half4 CRT(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float strength = saturate(_CRTShape.z);
                float2 p = uv * 2 - 1;
                float curvature = _CRTShape.x * strength;
                float2 curved = p * (1 + p.yx*p.yx * curvature);
                float2 sampleUV = curved * .5 + .5;
                float2 border = 1 - abs(curved);
                float feather = max(_CRTShape.y, .0001);
                float screenMask = smoothstep(0,feather,min(border.x,border.y));
                float2 sourceUV = sampleUV * _CRTViewport.xy + _CRTViewport.zw;
                half4 source = SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,sourceUV);
                float3 color = source.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = LinearToSRGB(max(color,0));
                #endif
                // Scanlines and phosphor texture are generated at output resolution, after Point scaling.
                float period = max(_CRTMask.x,2);
                float phase = uv.y * _CRTOutputSize.y * (6.2831853 / period);
                float scan = .5 + .5*cos(phase);
                float scanAmount = strength * _CRTMask.y;
                color *= (1 - scanAmount * scan) / max(1 - .5*scanAmount,.1);
                float column = floor(uv.x * _CRTOutputSize.x);
                float3 phosphor = .5 + .5*cos((column - float3(0,1,2)) * 2.0943951);
                float maskAmount = strength * _CRTMask.z;
                color *= (1 - maskAmount * (1-phosphor)) / max(1 - .5*maskAmount,.1);
                color = max(color,0) * screenMask;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = SRGBToLinear(color);
                #endif
                if (_CRTEncodeSRGB > .5) color = LinearToSRGB(color);
                return half4(color,source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
