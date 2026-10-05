#ifndef CARD_DEFERRED_INPUT_INCLUDED
#define CARD_DEFERRED_INPUT_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

TEXTURE2D(_RedCrossMap);
SAMPLER(sampler_RedCrossMap);
TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
TEXTURE2D(_FoilMap);
SAMPLER(sampler_FoilMap);
TEXTURE2D(_SealMap);
SAMPLER(sampler_SealMap);
TEXTURE2D(_SealNormalMap);
SAMPLER(sampler_SealNormalMap);
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_TexelSize;
    float4 _BackUVRect;
    half4 _BaseColor;
    half4 _EdgeColor;
    half _Cutoff;
    float _FoilSpeed;
    float4 _SealRect;
    float _SealNormalScale;
    float _SealRotation;
CBUFFER_END
UNITY_INSTANCING_BUFFER_START(CardInstances)
    UNITY_DEFINE_INSTANCED_PROP(float4, _FrontUVRect)
    UNITY_DEFINE_INSTANCED_PROP(float4, _ColorTint)
    UNITY_DEFINE_INSTANCED_PROP(float, _FoilEnabled)
    UNITY_DEFINE_INSTANCED_PROP(float4, _FoilColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _FoilStrength)
    UNITY_DEFINE_INSTANCED_PROP(float, _SealEmissionStrength)
    UNITY_DEFINE_INSTANCED_PROP(float, _RedCrossEnabled)
    UNITY_DEFINE_INSTANCED_PROP(float, _SealEnabled)
    UNITY_DEFINE_INSTANCED_PROP(float4, _SealColor)
UNITY_INSTANCING_BUFFER_END(CardInstances)

struct CardAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 localUV : TEXCOORD0;
    float2 surface : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct CardVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    half3 normalWS : TEXCOORD1;
    float2 localUV : TEXCOORD2;
    float2 surface : TEXCOORD3;
    half fog : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

half4 SampleCard(CardVaryings input)
{
    half4 color;
    if (input.surface.y > 0.5)
        color = _EdgeColor;
    else
    {
        float4 rect = input.surface.x > 0.5 ? _BackUVRect
            : UNITY_ACCESS_INSTANCED_PROP(CardInstances, _FrontUVRect);
        float2 uv = rect.xy + saturate(input.localUV) * rect.zw;
        // Stay within this card even when the shared atlas uses bilinear filtering.
        float2 inset = min(_BaseMap_TexelSize.xy * 0.5, rect.zw * 0.5);
        uv = clamp(uv, rect.xy + inset, rect.xy + rect.zw - inset);
        color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    }
    color *= _BaseColor * UNITY_ACCESS_INSTANCED_PROP(CardInstances, _ColorTint);
    clip(color.a - _Cutoff);
    return color;
}

void ApplyCardFoil(CardVaryings input, InputData lighting, inout SurfaceData surface)
{
    float enabled = UNITY_ACCESS_INSTANCED_PROP(CardInstances, _FoilEnabled);
    float strength = clamp(UNITY_ACCESS_INSTANCED_PROP(CardInstances, _FoilStrength), 0, 5);
    // Back and paper edge remain ordinary paper. No extra geometry or shader variant.
    if (enabled < 0.5 || strength <= 0 || input.surface.x > 0.5 || input.surface.y > 0.5) return;

    // Recover UV-aligned axes without requiring tangents in the imported mesh.
    float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
    float2 ux = ddx(input.localUV), uy = ddy(input.localUV);
    float orientation = ux.x * uy.y - ux.y * uy.x < 0 ? -1 : 1;
    float3 tangent = SafeNormalize((dx * uy.y - dy * ux.y) * orientation);
    float3 bitangent = SafeNormalize((dy * ux.x - dx * uy.x) * orientation);
    float2 view = float2(dot(lighting.viewDirectionWS, tangent), dot(lighting.viewDirectionWS, bitangent));
    float facing = saturate(dot(lighting.normalWS, lighting.viewDirectionWS));
    // Pattern stays attached to the card; time animates the illumination across its facets.
    float pattern = SAMPLE_TEXTURE2D(_FoilMap, sampler_FoilMap, input.localUV * float2(2, 3)).r;
    float phase = dot(input.localUV, float2(1.25, 0.7)) + dot(view, float2(0.8, -0.6)) +
        (1 - facing) * 0.35 + pattern * 0.65 + _Time.y * _FoilSpeed;
    float3 rainbow = 0.5 + 0.5 * cos(6.2831853 * (phase + float3(0, 0.333333, 0.666667)));
    float band = pow(saturate(0.5 + 0.5 * cos(6.2831853 * (phase * 0.7))), 3);
    // Suppress foil on dark ink so rank, suit and illustration remain readable.
    float inkMask = smoothstep(0.015, 0.3, dot(surface.albedo, float3(0.2126, 0.7152, 0.0722)));
    float amount = strength * inkMask * (0.2 + 0.8 * band) * lerp(0.4, 1, pattern);
    float3 foil = rainbow * max(0, UNITY_ACCESS_INSTANCED_PROP(CardInstances, _FoilColor).rgb);
    surface.albedo *= lerp(1.0.xxx, 0.2 + foil * 1.1, saturate(amount * 0.65));
    surface.specular = lerp(surface.specular, foil * 0.35, saturate(amount));
    surface.smoothness = lerp(surface.smoothness, 0.7, saturate(amount));
    surface.emission = foil * amount * 0.3;
}

// The seal is a material overlay, not a hole or transparent geometry.
float2 CardSealUV(CardVaryings input)
{
    // Rotate in seal space to preserve the circular footprint on a non-square card.
    float2 centered = (input.localUV - _SealRect.xy) / max(_SealRect.zw, 0.0001);
    float sine, cosine;
    sincos(radians(_SealRotation), sine, cosine);
    return float2(cosine * centered.x + sine * centered.y,
        -sine * centered.x + cosine * centered.y) + 0.5;
}

half4 SampleCardSeal(CardVaryings input)
{
    if (UNITY_ACCESS_INSTANCED_PROP(CardInstances, _SealEnabled) < 0.5 ||
        input.surface.x > 0.5 || input.surface.y > 0.5) return 0;
    float2 uv = CardSealUV(input);
    half4 seal = SAMPLE_TEXTURE2D(_SealMap, sampler_SealMap, saturate(uv));
    seal.rgb *= UNITY_ACCESS_INSTANCED_PROP(CardInstances, _SealColor).rgb;
    seal.a *= step(0, uv.x) * step(uv.x, 1) * step(0, uv.y) * step(uv.y, 1);
    return seal;
}

half3 CardSealNormal(CardVaryings input, half mask)
{
    half3 n = NormalizeNormalPerPixel(input.normalWS);
    if (UNITY_ACCESS_INSTANCED_PROP(CardInstances, _SealEnabled) < 0.5 ||
        input.surface.x > 0.5 || input.surface.y > 0.5) return n;
    // Derivatives must be evaluated before the per-pixel alpha branch.
    float3 dx = ddx(input.positionWS), dy = ddy(input.positionWS);
    float2 sealUV = CardSealUV(input);
    float2 ux = ddx(sealUV), uy = ddy(sealUV);
    float orientation = ux.x * uy.y - ux.y * uy.x < 0 ? -1 : 1;
    float3 t = (dx * uy.y - dy * ux.y) * orientation;
    float3 b = (dy * ux.x - dx * uy.x) * orientation;
    t = SafeNormalize(t - n * dot(n, t));
    float handedness = dot(cross(n, t), b) < 0 ? -1 : 1;
    b = cross(n, t) * handedness;
    if (mask <= 0 || _SealNormalScale <= 0) return n;
    half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_SealNormalMap, sampler_SealNormalMap,
        saturate(CardSealUV(input))), _SealNormalScale);
    half3 mapped = NormalizeNormalPerPixel(t * normalTS.x + b * normalTS.y + n * normalTS.z);
    return NormalizeNormalPerPixel(lerp(n, mapped, mask));
}

void ApplyCardSeal(CardVaryings input, inout InputData lighting, inout SurfaceData surface)
{
    half4 seal = SampleCardSeal(input);
    lighting.normalWS = CardSealNormal(input, seal.a);
    surface.albedo = lerp(surface.albedo, seal.rgb, seal.a);
    surface.specular = lerp(surface.specular, half3(0.08, 0.08, 0.08), seal.a);
    surface.smoothness = lerp(surface.smoothness, 0.32, seal.a);
    // Wax covers the foil; no rainbow emission shines through an opaque seal.
    half emissionStrength = saturate(UNITY_ACCESS_INSTANCED_PROP(CardInstances, _SealEmissionStrength));
    surface.emission = surface.emission * (1 - seal.a) + seal.rgb * seal.a * emissionStrength;
}

// Painted on top of the front surface; alpha does not change card geometry.
void ApplyCardRedCross(CardVaryings input, inout SurfaceData surface)
{
    if (UNITY_ACCESS_INSTANCED_PROP(CardInstances, _RedCrossEnabled) < 0.5 ||
        input.surface.x > 0.5 || input.surface.y > 0.5) return;
    half4 paint = SAMPLE_TEXTURE2D(_RedCrossMap, sampler_RedCrossMap, saturate(input.localUV));
    surface.albedo = lerp(surface.albedo, paint.rgb, paint.a);
    surface.specular = lerp(surface.specular, half3(0.02, 0.02, 0.02), paint.a);
    surface.smoothness = lerp(surface.smoothness, 0.08, paint.a);
    surface.emission *= 1 - paint.a;
}

void InitializeCardLighting(CardVaryings input, out SurfaceData surfaceData, out InputData inputData)
{
    half4 color = SampleCard(input);
    surfaceData = (SurfaceData)0;
    surfaceData.albedo = color.rgb;
    surfaceData.alpha = color.a;
    surfaceData.specular = half3(0.02, 0.02, 0.02);
    surfaceData.smoothness = 0.08;
    surfaceData.normalTS = half3(0, 0, 1);
    surfaceData.occlusion = 1;
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    // UV1 is face metadata, never a lightmap UV. Cards use probe/ambient lighting.
    inputData.shadowMask = unity_ProbesOcclusion;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.fogCoord = input.fog;
    ApplyCardFoil(input, inputData, surfaceData);
    ApplyCardSeal(input, inputData, surfaceData);
    ApplyCardRedCross(input, surfaceData);
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.vertexLighting = VertexLighting(input.positionWS, inputData.normalWS);
}
#endif
