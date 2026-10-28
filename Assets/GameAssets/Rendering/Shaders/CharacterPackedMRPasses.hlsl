#ifndef CHARACTER_PACKED_MR_PASSES_INCLUDED
#define CHARACTER_PACKED_MR_PASSES_INCLUDED
#include "CharacterPackedMRInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

struct CharacterAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct CharacterVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
CharacterVaryings CharacterVertex(CharacterAttributes input)
{
    CharacterVaryings output = (CharacterVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = p.positionCS;
    output.positionWS = p.positionWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = n.normalWS;
    output.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    return output;
}
half3 CharacterNormalWS(CharacterVaryings input)
{
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
    half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
    return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
        half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));
}
FragmentOutput CharacterGBuffer(CharacterVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
    // Packed data is linear: R = metallic, G = roughness.
    half2 mr = SAMPLE_TEXTURE2D(_PackedMRMap, sampler_PackedMRMap, input.uv).rg;
    half metallic = saturate(mr.r * _Metallic);
    half smoothness = 1 - saturate(mr.g * _Roughness);
    half occlusion = lerp(1, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r, _OcclusionStrength);
    half alpha = 1;
    BRDFData brdf;
    InitializeBRDFData(albedo, metallic, half3(0,0,0), smoothness, alpha, brdf);
    InputData data = (InputData)0;
    data.positionWS = input.positionWS;
    data.positionCS = input.positionCS;
    data.normalWS = CharacterNormalWS(input) * IS_FRONT_VFACE(face, 1, -1);
    data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    // Animated character: light probes / SH, not static lightmaps.
    data.bakedGI = SampleSH(data.normalWS);
    data.shadowMask = unity_ProbesOcclusion;
    half3 gi = GlobalIllumination(brdf, data.bakedGI, occlusion,
        data.positionWS, data.normalWS, data.viewDirectionWS);
    // Art-directed diffuse readability floor, not a specular light or rim outline.
    // Camera-side, slightly elevated fill gives broad form shading as the camera moves.
    // Use the geometric normal so painted normal detail cannot turn into glitter.
    half3 fillNormal = NormalizeNormalPerPixel(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
    half3 fillDirection = SafeNormalize(data.viewDirectionWS + half3(0, 0.45h, 0));
    half wrap = saturate(_CharacterFillWrap);
    half fillShape = saturate((dot(fillNormal, fillDirection) + wrap) / (1 + wrap));
    half fillAmount = max(0, _CharacterAmbientFloor) + max(0, _CharacterFillStrength) * fillShape;
    half3 readability = albedo * max(0, _CharacterFillColor.rgb) * fillAmount * occlusion;
    // Only supply missing indirect brightness; existing GI and direct scene lights stay intact.
    // Both strengths default to zero, preserving every existing material's appearance.
    gi = max(gi, readability);
    // Direct lighting, shadows and screen-space AO remain the deferred renderer's responsibility.
    return BRDFDataToGbuffer(brdf, data, smoothness, gi, occlusion);
}
half4 CharacterDepthNormals(CharacterVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return half4(PackNormal(CharacterNormalWS(input) * IS_FRONT_VFACE(face, 1, -1)), 0);
}

#endif
