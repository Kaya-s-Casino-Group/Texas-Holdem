#ifndef VFX_PARTICLE_COMMON_INCLUDED
#define VFX_PARTICLE_COMMON_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Shuriken packs custom vertex streams into the unused UV components.
// Keep both TEXCOORDs at float4; narrowing them loses dissolve/hue controls.
struct VfxAttributes
{
    float3 positionOS : POSITION;
    float4 color : COLOR;
    float4 uv0 : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct VfxVaryings
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
    float4 uv0 : TEXCOORD0;
    float4 uv1 : TEXCOORD1;
    float4 screenPosition : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
VfxVaryings VfxVertex(VfxAttributes input)
{
    VfxVaryings output = (VfxVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS);
    output.screenPosition = ComputeScreenPos(output.positionCS);
    output.color = input.color;
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    return output;
}

// Preserve the source graphs' depth convention, including their fallback at
// nonpositive separation. SwordSlash's graph uses a fixed depth distance of 1.
float VfxDepthFade(float4 screenPosition, float distanceScale)
{
    float2 screenUV = screenPosition.xy / screenPosition.w;
    float depth = SampleSceneDepth(screenUV);
    float sceneEye = Linear01Depth(depth, _ZBufferParams) * _ProjectionParams.z;
    float fade = saturate((sceneEye - screenPosition.w) / max(distanceScale, 0.0001));
    return lerp(fade, 1.0, step(fade, 0.0));
}

// Normalized hue rotation, matching the source graph's HSV node.
float3 VfxHue(float3 rgb, float offset)
{
    float4 k = float4(0, -1.0/3.0, 2.0/3.0, -1);
    float4 p = lerp(float4(rgb.bg, k.wz), float4(rgb.gb, k.xy), step(rgb.b, rgb.g));
    float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
    float delta = q.x - min(q.w, q.y);
    float epsilon = 1e-10;
    float3 hsv = float3(abs(q.z + (q.w-q.y)/(6.0*delta+epsilon)),
        delta/(q.x+epsilon), delta == 0 ? q.x : q.x+epsilon);
    hsv.x += offset;
    hsv.x = hsv.x < 0 ? hsv.x+1 : (hsv.x > 1 ? hsv.x-1 : hsv.x);
    float3 wheel = abs(frac(hsv.xxx + float3(1,2.0/3.0,1.0/3.0))*6.0-3.0);
    return hsv.z * lerp(1.0.xxx, saturate(wheel-1.0), hsv.y);
}
#endif
