Shader "Hidden/RetroRendering/HeightFog"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        Pass
        {
            Name "Height Fog Extinction"
            Blend One SrcAlpha
            ColorMask RGB
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "HeightFogCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 Frag(Varyings input) : SV_Target
            {
                float3 origin, direction;
                float rayLength;
                HeightFogRay(input.texcoord, origin, direction, rayLength);
                float transmission = HeightFogTransmission(origin, direction, rayLength);
                return float4(_HeightFogColor.rgb * (1 - transmission), transmission);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
