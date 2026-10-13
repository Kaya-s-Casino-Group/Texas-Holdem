#ifndef GARGOYLE_VAT_DEPTH_PASSES_INCLUDED
#define GARGOYLE_VAT_DEPTH_PASSES_INCLUDED
#include "GargoyleVATVertex.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct GargoyleDepthVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

GargoyleDepthVaryings GargoyleDepthVertex(GargoyleAttributes input)
{
    GargoyleDepthVaryings output = (GargoyleDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    GargoyleDeform(input);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

GargoyleDepthVaryings GargoyleShadowVertex(GargoyleAttributes input)
{
    GargoyleDepthVaryings output = (GargoyleDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    GargoyleDeform(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    return output;
}

half GargoyleDepthFragment(GargoyleDepthVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return input.positionCS.z;
}

half4 GargoyleShadowFragment(GargoyleDepthVaryings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return 0;
}
#endif
