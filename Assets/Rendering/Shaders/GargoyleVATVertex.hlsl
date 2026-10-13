#ifndef GARGOYLE_VAT_VERTEX_INCLUDED
#define GARGOYLE_VAT_VERTEX_INCLUDED
#include "GargoyleCracksInput.hlsl"
#include "RigidVAT.hlsl"

struct GargoyleAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    float2 vatLookup : TEXCOORD1;
    float2 vatPivotXY : TEXCOORD2;
    float2 vatPivotZID : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

void GargoyleDeform(inout GargoyleAttributes input)
{
    ApplyRigidVAT(input.positionOS.xyz, input.normalOS, input.tangentOS,
        input.vatLookup, input.vatPivotXY, input.vatPivotZID);
}
#endif
