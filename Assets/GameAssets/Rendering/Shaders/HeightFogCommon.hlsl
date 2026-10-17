#ifndef RETRO_HEIGHT_FOG_INCLUDED
#define RETRO_HEIGHT_FOG_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

float4 _HeightFogHeights; // base, maximum, ground, ground enabled
float4 _HeightFogMedium; // density, extinction, maximum view distance, unused
float4 _HeightFogColor;
float4 _HeightFogNoise; // strength (zero = analytic), steps, unused, unused
float4 _HeightFogNoiseScale;
float4 _HeightFogNoisePhase;
TEXTURE3D(_HeightFogNoiseTexture);
// Inline sampler enforces spatial linear filtering + repeat on all axes, independent of asset settings.
SAMPLER(sampler_HeightFogNoise_Linear_Repeat);
float4 _HeightFogCurveInfo; // enabled, segment count
TEXTURE2D(_HeightFogCurveLut);

// R is density at each knot, G is its cumulative trapezoidal integral.
// Integrate the line within a LUT cell exactly rather than linearly interpolating the integral.
float2 HeightFogCurve(float height01)
{
    float x = saturate(height01) * _HeightFogCurveInfo.y;
    int index = min((int)floor(x), (int)_HeightFogCurveInfo.y - 1);
    float f = x - index;
    float2 a = LOAD_TEXTURE2D(_HeightFogCurveLut, int2(index, 0)).rg;
    float2 b = LOAD_TEXTURE2D(_HeightFogCurveLut, int2(index + 1, 0)).rg;
    return float2(lerp(a.x, b.x, f), a.y + (a.x * f + .5 * (b.x - a.x) * f * f) / _HeightFogCurveInfo.y);
}

float HeightFogDensity(float y)
{
    if (_HeightFogHeights.w > 0.5 && y < _HeightFogHeights.z) return 0;
    float h = saturate((y - _HeightFogHeights.x) / max(_HeightFogHeights.y - _HeightFogHeights.x, 1e-5));
    return _HeightFogMedium.x * (_HeightFogCurveInfo.x > .5 ? HeightFogCurve(h).x : 1 - h);
}

// Exact optical path integral for a constant layer plus a linear height ramp.
// Also used by scattering, so fog extinction does not depend on the ray-march step count.
float HeightFogIntegral(float startY, float directionY, float distance)
{
    if (distance <= 0) return 0;
    if (abs(directionY) < 1e-5) return HeightFogDensity(startY + directionY * distance * 0.5) * distance;
    float endY = startY + directionY * distance;
    float lo = min(startY, endY), hi = min(max(startY, endY), _HeightFogHeights.y);
    if (_HeightFogHeights.w > 0.5) lo = max(lo, _HeightFogHeights.z);
    if (hi <= lo) return 0;
    float base = _HeightFogHeights.x;
    float constantArea = max(0, min(hi, base) - lo);
    float rampLo = max(lo, base);
    float rampLength = max(0, hi - rampLo);
    if (_HeightFogCurveInfo.x > .5)
    {
        float height = max(_HeightFogHeights.y - base, 1e-5);
        float a = saturate((rampLo - base) / height), b = saturate((hi - base) / height);
        // Tiny intervals use the local midpoint to avoid cancellation between nearly equal prefix sums.
        float area = b - a < 1e-3 ? HeightFogCurve((a + b) * .5).x * max(0, b - a) :
            max(0, HeightFogCurve(b).y - HeightFogCurve(a).y);
        return _HeightFogMedium.x * (constantArea + height * area) / abs(directionY);
    }
    float rampDensity = saturate(1 - ((rampLo + hi) * 0.5 - base) / max(_HeightFogHeights.y - base, 1e-5));
    return _HeightFogMedium.x * (constantArea + rampLength * rampDensity) / abs(directionY);
}

float HeightFogNoisySegment(float3 origin, float3 direction, float start, float stepLength)
{
    // Integrate height/ground boundaries exactly within each segment, then modulate by one 3D sample.
    float integral = HeightFogIntegral(origin.y + direction.y * start, direction.y, stepLength);
    if (integral <= 0) return 0;
    float3 positionWS = origin + direction * (start + .5 * stepLength);
    float3 uvw = positionWS * _HeightFogNoiseScale.xyz + _HeightFogNoisePhase.xyz;
    float noise = SAMPLE_TEXTURE3D_LOD(_HeightFogNoiseTexture, sampler_HeightFogNoise_Linear_Repeat, uvw, 0).r;
    return integral * lerp(1, 2 * saturate(noise), _HeightFogNoise.x);
}

float HeightFogTransmission(float3 origin, float3 direction, float rayLength)
{
    if (_HeightFogNoise.x <= 0)
        return exp(-_HeightFogMedium.y * HeightFogIntegral(origin.y, direction.y, rayLength));
    int steps = (int)_HeightFogNoise.y;
    float stepLength = rayLength / steps;
    float transmission = 1;
    [loop] for (int i = 0; i < steps; ++i)
        transmission *= exp(-_HeightFogMedium.y * HeightFogNoisySegment(origin, direction, i * stepLength, stepLength));
    return transmission;
}

float3 HeightFogWorldPosition(float2 uv, float depth)
{
    #if !UNITY_REVERSED_Z
        depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
    #endif
    return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
}

void HeightFogRay(float2 uv, out float3 origin, out float3 direction, out float rayLength)
{
    #if UNITY_REVERSED_Z
        const float nearDepth = 1;
    #else
        const float nearDepth = 0;
    #endif
    // Reconstruct both endpoints. Works for perspective, orthographic and off-center projections.
    origin = HeightFogWorldPosition(uv, nearDepth);
    float3 surface = HeightFogWorldPosition(uv, SampleSceneDepth(uv));
    float3 delta = surface - origin;
    float lengthToSurface = length(delta);
    direction = delta / max(lengthToSurface, 1e-6);
    rayLength = min(lengthToSurface, _HeightFogMedium.z);
}

float HeightFogEyeDepth(float2 uv)
{
    return -TransformWorldToView(HeightFogWorldPosition(uv, SampleSceneDepth(uv))).z;
}
#endif
