#ifndef GARGOYLE_CRACKS_PASSES_INCLUDED
#define GARGOYLE_CRACKS_PASSES_INCLUDED

#include "GargoyleCracksInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

#include "GargoyleVATVertex.hlsl"

struct GargoyleVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    float3 restPositionOS : TEXCOORD4;
    half3 restNormalOS : TEXCOORD5;
    half interior : TEXCOORD6;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

GargoyleVaryings GargoyleVertex(GargoyleAttributes input)
{
    GargoyleVaryings output = (GargoyleVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    // Preserve these before the VAT transform. Projection must follow the
    // original stone surface, not the animated position or world-space location.
    output.interior = _UseSurfaceMask * input.color.r;
    output.restPositionOS = input.positionOS.xyz;
    output.restNormalOS = input.normalOS;
    GargoyleDeform(input);
    VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.positionCS = position.positionCS;
    output.positionWS = position.positionWS;
    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.normalWS = normal.normalWS;
    output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    return output;
}

float CrackProjectionMask(float2 data)
{
    // R is a distance ramp, so width can change without another texture lookup.
    float width = _CrackWidth * lerp(0.45, 1.0, _CrackProgress);
    float threshold = 1.0 - width;
    float edgeAA = max(fwidth(data.x), 1.0 / 255.0);
    float fissure = smoothstep(threshold - edgeAA, threshold + edgeAA, data.x);
    float revealed = smoothstep(data.y - 0.06, data.y + 0.06, _CrackProgress);
    return fissure * revealed * smoothstep(0.0, 0.08, _CrackProgress);
}

float GargoyleCrackMask(GargoyleVaryings input)
{
    // Surface markers are constant per triangle; caps have no exterior cracks.
    [branch] if (_CrackProgress <= 0.0 || input.interior > 0.5h) return 0.0;
    float3 p = input.restPositionOS * _CrackScale;
    float3 w = abs(normalize(input.restNormalOS));
    w *= w;
    w *= w;
    w /= max(w.x + w.y + w.z, 1e-5);
    float x = CrackProjectionMask(SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, p.zy + float2(0.17,0.31)).rg);
    float y = CrackProjectionMask(SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, p.xz + float2(0.43,0.11)).rg);
    float z = CrackProjectionMask(SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, p.xy + float2(0.07,0.67)).rg);
    return saturate(dot(float3(x,y,z), w));
}

half3 GargoyleNormalWS(GargoyleVaryings input, float crack)
{
    // The interior has no source UV textures and uses its geometric normal.
    [branch] if (input.interior > 0.5h) return NormalizeNormalPerPixel(input.normalWS);
    half3 bitangentWS = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
    half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(GargoyleNormalTS(input.uv),
        half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));
    // Screen derivatives of the same height mask add an inset edge without extra
    // normal-map samples. Convert object-space depth to world units for scaling.
    float scale = length(mul((float3x3)GetObjectToWorldMatrix(), normalize(input.restNormalOS)));
    float height = -crack * _CrackDepth * scale;
    float3 dx = ddx(input.positionWS);
    float3 dy = ddy(input.positionWS);
    float3 rx = cross(dy, normalWS);
    float3 ry = cross(normalWS, dx);
    float determinant = dot(dx, rx);
    float3 gradient = (rx * ddx(height) + ry * ddy(height)) *
        (determinant / max(determinant * determinant, 1e-16));
    return normalize(normalWS - gradient);
}

FragmentOutput GargoyleGBuffer(GargoyleVaryings input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float crack = 0;
    half3 albedo;
    half metallic, smoothness, occlusion;
    [branch] if (input.interior > 0.5h)
    {
        albedo = _InteriorColor.rgb;
        metallic = _InteriorMetallic;
        smoothness = _InteriorSmoothness;
        occlusion = 1.0h;
    }
    else
    {
        crack = GargoyleCrackMask(input);
        albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
        albedo = lerp(albedo, _CrackColor.rgb, crack);
        half4 packed = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
        metallic = packed.r * _Metallic * (1.0 - crack);
        smoothness = lerp(packed.a * _Smoothness, 0.08h, crack);
        occlusion = lerp(1.0h, 0.3h, crack);
    }
    half alpha = 1;
    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, half3(0,0,0), smoothness, alpha, brdfData);

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = GargoyleNormalWS(input, crack);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.shadowMask = unity_ProbesOcclusion;
    half3 gi = GlobalIllumination(brdfData, inputData.bakedGI, occlusion,
        inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
    return BRDFDataToGbuffer(brdfData, inputData, smoothness, gi, occlusion);
}

half4 GargoyleDepthNormals(GargoyleVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    return half4(PackNormal(GargoyleNormalWS(input, GargoyleCrackMask(input))), 0);
}

#endif
