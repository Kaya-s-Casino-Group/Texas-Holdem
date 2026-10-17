#ifndef TABLE_DEFERRED_PASSES_INCLUDED
#define TABLE_DEFERRED_PASSES_INCLUDED

#include "TableDeferredInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

struct TableAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TableVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

TableVaryings TableVertex(TableAttributes input)
{
    TableVaryings output = (TableVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = normal.normalWS;
    output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    return output;
}

half3 TableNormalWS(TableVaryings input)
{
    half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
    return NormalizeNormalPerPixel(TransformTangentToWorld(TableNormalTS(input.uv),
        half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));
}

FragmentOutput TableGBuffer(TableVaryings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
    half4 metallicSmoothness = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
    half metallic = metallicSmoothness.r * _Metallic;
    half smoothness = metallicSmoothness.a * _Smoothness;
    half alpha = 1;
    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, half3(0,0,0), smoothness, alpha, brdfData);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = TableNormalWS(input);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    // This table uses realtime lights and SH/light probes, with no baked lightmap.
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.shadowMask = unity_ProbesOcclusion;
    half3 gi = GlobalIllumination(brdfData, inputData.bakedGI, 1,
        inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);

    // URP's deferred lighting pass evaluates direct lights and their shadows.
    return BRDFDataToGbuffer(brdfData, inputData, smoothness, gi, 1);
}

half4 TableDepthNormals(TableVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return half4(PackNormal(TableNormalWS(input)), 0);
}

#endif
