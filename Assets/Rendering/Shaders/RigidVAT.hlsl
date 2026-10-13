#ifndef RIGID_VAT_INCLUDED
#define RIGID_VAT_INCLUDED

// statue-rigid-vat-v1 (Tools/Blender/rigid_vat.py).
// UV0 remains the surface UV. UV1 is the first-frame texel center; UV2.xy and
// UV3.x store the Blender-space rest pivot. Point/clamp/linear RGBA EXRs, no mips.
// The Unity mesh has the FBX node transform baked: Blender XYZ -> Unity XZY.
// Material uniforms belong in the caller's shared UnityPerMaterial cbuffer.
TEXTURE2D(_VATPosition); SAMPLER(sampler_VATPosition);
TEXTURE2D(_VATRotation); SAMPLER(sampler_VATRotation);

float3 RigidVATRotate(float4 q, float3 v)
{
    return v + 2.0 * cross(q.xyz, cross(q.xyz, v) + q.w * v);
}

void ApplyRigidVAT(inout float3 positionOS, inout float3 normalOS,
    inout float4 tangentOS, float2 lookup, float2 pivotXY, float2 pivotZID)
{
    [branch] if (_VATEnabled < 0.5) return;
    float frame = saturate(_VATProgress) * max(_VATFrameCount - 1.0, 0.0);
    float lo = floor(frame);
    float hi = min(lo + 1.0, _VATFrameCount - 1.0);
    float blend = frame - lo;
    float2 uv0 = lookup - float2(0.0, lo * _VATTextureSize.w);
    float2 uv1 = lookup - float2(0.0, hi * _VATTextureSize.w);
    float3 p0 = SAMPLE_TEXTURE2D_LOD(_VATPosition, sampler_VATPosition, uv0, 0).rgb;
    float3 p1 = SAMPLE_TEXTURE2D_LOD(_VATPosition, sampler_VATPosition, uv1, 0).rgb;
    float3 currentPivot = _VATPositionMin.xyz + lerp(p0, p1, blend) * _VATPositionRange.xyz;
    float4 q0 = SAMPLE_TEXTURE2D_LOD(_VATRotation, sampler_VATRotation, uv0, 0) * 2.0 - 1.0;
    float4 q1 = SAMPLE_TEXTURE2D_LOD(_VATRotation, sampler_VATRotation, uv1, 0) * 2.0 - 1.0;
    q1 *= dot(q0, q1) < 0.0 ? -1.0 : 1.0;
    float4 q = normalize(lerp(q0, q1, blend));
    // XZY is a reflection (det=-1): quaternion xyz is an axial vector.
    q.xyz = -q.xzy;
    float3 restPivot = float3(pivotXY, pivotZID.x).xzy;
    positionOS = currentPivot.xzy + RigidVATRotate(q, positionOS - restPivot);
    normalOS = RigidVATRotate(q, normalOS);
    tangentOS.xyz = RigidVATRotate(q, tangentOS.xyz);
    // The imported mesh already has Unity tangent handedness; rotation preserves w.
}
#endif
