Shader "Hidden/Validation/GargoyleCracks"
{
    Properties { _CrackMap("Cracks",2D)="black" {} _VATPosition("Position",2D)="black" {} _VATRotation("Rotation",2D)="gray" {} }
    SubShader { Pass { Cull Off ZWrite On ZTest LEqual
        HLSLPROGRAM
        #pragma target 4.5
        #pragma vertex GargoyleVertex
        #pragma fragment Diagnostic
        #include "../../Rendering/Shaders/GargoyleCracksPasses.hlsl"
        float4 Diagnostic(GargoyleVaryings input):SV_Target
        {
            return float4(GargoyleCrackMask(input), input.interior, _CrackProgress, 1);
        }
        ENDHLSL
    } }
}
