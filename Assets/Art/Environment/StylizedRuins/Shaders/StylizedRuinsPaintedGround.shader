Shader "StylizedRuins/Painted Ground Transition"
{
    Properties
    {
        [MainTexture] _GroundPainting("Fixed Camera Ground Painting",2D)="white"{}
        _GroundFogColor("Ground Atmosphere",Color)=(.22,.11,.16,1)
        [HideInInspector] _PaintingOrigin("Origin",Vector)=(0,0,0,0)
        [HideInInspector] _PaintingRight("Right",Vector)=(1,0,0,0)
        [HideInInspector] _PaintingUp("Up",Vector)=(0,1,0,0)
        [HideInInspector] _PaintingForward("Forward",Vector)=(0,0,1,0)
        [HideInInspector] _PaintingProjection("Projection",Vector)=(.45,1.77778,.64,1)
        [HideInInspector] _PaintingCrop("Uniform Crop",Vector)=(1,1,0,0)
        [HideInInspector] _GroundBlend("Depth Blend",Vector)=(12,24,.8,0)
        [HideInInspector] _GroundFog("Depth Height Fog",Vector)=(7,22,.28,.6)
        [HideInInspector] _GroundFogFalloff("Falloff",Float)=.5
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_GroundPainting);SAMPLER(sampler_GroundPainting);
        CBUFFER_START(UnityPerMaterial)
            float4 _PaintingOrigin,_PaintingRight,_PaintingUp,_PaintingForward,_PaintingProjection,_PaintingCrop;
            float4 _GroundBlend,_GroundFog;
            half4 _GroundFogColor;
            float _GroundFogFalloff;
        CBUFFER_END
        struct A{float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
        V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
        void GroundCoverage(float3 world,out float depth,out float2 uv,out float alpha)
        {
            float3 relative=world-_PaintingOrigin.xyz;
            depth=dot(relative,_PaintingForward.xyz);
            clip(depth-.01);
            uv=float2(dot(relative,_PaintingRight.xyz)/_PaintingProjection.y,dot(relative,_PaintingUp.xyz));
            uv=uv/(2*depth*_PaintingProjection.x)+.5;
            float screenY=uv.y;
            uv=float2((uv.x-.5)*_PaintingCrop.x+.5,uv.y*_PaintingCrop.y)+_PaintingCrop.zw;
            // Stationary broad edge variation avoids a straight seam or swimming screen-space noise.
            float breakup=(sin(world.x*.81+world.z*.3)+sin(world.z*1.17-world.x*.46))*.5*_GroundBlend.z;
            alpha=1-smoothstep(_GroundBlend.x,_GroundBlend.y,depth+breakup);
            alpha*=1-smoothstep(_PaintingProjection.z-.12,_PaintingProjection.z+.02,screenY);
        }
        ENDHLSL
        Pass
        {
            Name "PaintedGround"
            Tags {"LightMode"="UniversalForwardOnly"}
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float depth,alpha;float2 uv;
                GroundCoverage(i.world,depth,uv,alpha);
                half3 paint=SAMPLE_TEXTURE2D(_GroundPainting,sampler_GroundPainting,saturate(uv)).rgb*_PaintingProjection.w;
                float heightDensity=exp2(-max(0,i.world.y-_GroundFog.w)*_GroundFogFalloff);
                float fog=smoothstep(_GroundFog.x,_GroundFog.y,depth)*heightDensity*_GroundFog.z;
                paint=lerp(paint,_GroundFogColor.rgb,saturate(fog));
                return half4(paint*alpha,alpha);
            }
            ENDHLSL
        }
        Pass
        {
            Name "GroundSolidDepth"
            Tags {"LightMode"="StylizedRuinsGroundDepth"}
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(V i):SV_Target
            {
                float depth,alpha;float2 uv;
                GroundCoverage(i.world,depth,uv,alpha);
                clip(alpha-.9999);
                return 0;
            }
            ENDHLSL
        }
    }
}
