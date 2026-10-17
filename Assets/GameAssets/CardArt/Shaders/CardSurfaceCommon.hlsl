#ifndef CARD_SURFACE_COMMON_INCLUDED
#define CARD_SURFACE_COMMON_INCLUDED
// Shared artwork math; callers supply lighting/render-pipeline inputs.
float2 CardAtlasSampleUV(float2 localUV, float4 rect, float2 texelSize)
{
    float2 inset = min(texelSize * 0.5, rect.zw * 0.5);
    return clamp(rect.xy + saturate(localUV) * rect.zw, rect.xy + inset, rect.xy + rect.zw - inset);
}
float2 CardSealSampleUV(float2 localUV, float4 rect, float rotation)
{
    float2 centered = (localUV - rect.xy) / max(rect.zw, 0.0001);
    float sine, cosine; sincos(radians(rotation), sine, cosine);
    return float2(cosine * centered.x + sine * centered.y, -sine * centered.x + cosine * centered.y) + 0.5;
}
void CardFoilSpectrum(float2 localUV, float2 view, float facing, float pattern, float time,
    float speed, float strength, float3 albedo, float3 tint, out float3 foil, out float amount)
{
    float phase = dot(localUV, float2(1.25, 0.7)) + dot(view, float2(0.8, -0.6)) +
        (1 - facing) * 0.35 + pattern * 0.65 + time * speed;
    float3 rainbow = 0.5 + 0.5 * cos(6.2831853 * (phase + float3(0, 0.333333, 0.666667)));
    float band = pow(saturate(0.5 + 0.5 * cos(6.2831853 * (phase * 0.7))), 3);
    float inkMask = smoothstep(0.015, 0.3, dot(albedo, float3(0.2126, 0.7152, 0.0722)));
    amount = strength * inkMask * (0.2 + 0.8 * band) * lerp(0.4, 1, pattern);
    foil = rainbow * max(0, tint);
}
#endif
