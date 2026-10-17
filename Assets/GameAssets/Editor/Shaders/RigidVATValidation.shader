Shader "Hidden/Validation/RigidVAT"
{
    Properties
    {
        _VATPosition ("Position", 2D) = "black" {}
        _VATRotation ("Rotation", 2D) = "gray" {}
    }
    SubShader
    {
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "../../Rendering/Shaders/GargoyleVATVertex.hlsl"
            float4 _ValidationSize;
            float _ValidationMode;
            struct Varyings { float4 positionCS : SV_POSITION; float4 value : TEXCOORD0; float size : PSIZE; };
            Varyings Vertex(GargoyleAttributes input, uint id : SV_VertexID)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                GargoyleDeform(input);
                Varyings o;
                float2 pixel = float2(id % (uint)_ValidationSize.x, id / (uint)_ValidationSize.x);
                o.positionCS = float4((pixel + .5) / _ValidationSize.xy * 2 - 1, 0, 1);
#if UNITY_UV_STARTS_AT_TOP
                o.positionCS.y = -o.positionCS.y;
#endif
                o.size = 1;
                o.value = _ValidationMode > 2.5 ? input.color :
                    _ValidationMode < .5 ? float4(input.positionOS.xyz,1) :
                    (_ValidationMode < 1.5 ? float4(input.normalOS,1) : input.tangentOS);
                return o;
            }
            float4 Fragment(Varyings input) : SV_TARGET { return input.value; }
            ENDHLSL
        }
    }
}
