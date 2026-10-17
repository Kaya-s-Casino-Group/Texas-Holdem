#ifndef CARD_DEFERRED_PASSES_INCLUDED
#define CARD_DEFERRED_PASSES_INCLUDED
#include "CardDeferredInput.hlsl"

CardVaryings CardVertex(CardAttributes input)
{
    CardVaryings output = (CardVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.localUV = input.localUV;
    output.surface = input.surface;
    output.fog = ComputeFogFactor(position.positionCS.z);
    return output;
}

FragmentOutput CardGBuffer(CardVaryings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData;
    InputData inputData;
    InitializeCardLighting(input, surfaceData, inputData);
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
    return SurfaceDataToGbuffer(surfaceData, inputData, inputData.bakedGI * surfaceData.albedo + surfaceData.emission, kLightingSimpleLit);
}

half4 CardForward(CardVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SurfaceData surfaceData;
    InputData inputData;
    InitializeCardLighting(input, surfaceData, inputData);
    half4 color = UniversalFragmentBlinnPhong(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

half4 CardDepth(CardVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SampleCard(input);
    return 0;
}

void CardDepthNormals(CardVaryings input, out half4 normal : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 renderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    SampleCard(input);
    normal = half4(PackNormal(CardSealNormal(input, SampleCardSeal(input).a)), 0);
#ifdef _WRITE_RENDERING_LAYERS
    renderingLayers = float4(EncodeMeshRenderingLayer(GetMeshRenderingLayer()), 0, 0, 0);
#endif
}

float3 _LightDirection;
float3 _LightPosition;
CardVaryings CardShadowVertex(CardAttributes input)
{
    CardVaryings output = CardVertex(input);
#ifdef _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 direction = normalize(_LightPosition - output.positionWS);
#else
    float3 direction = _LightDirection;
#endif
    float4 position = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, direction));
#if UNITY_REVERSED_Z
    position.z = min(position.z, UNITY_NEAR_CLIP_VALUE);
#else
    position.z = max(position.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS = position;
    return output;
}
#endif
