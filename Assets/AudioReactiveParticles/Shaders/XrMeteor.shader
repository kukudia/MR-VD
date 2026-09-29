Shader "MR-VD/XR Meteor Additive"
{
    Properties
    {
        _Emission ("Emission", Range(0,8)) = 7.5
        _NearFade ("Near Fade Start / End (metres)", Vector) = (0.85,1.4,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Emission;
            float4 _NearFade;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.uv * 2 - 1;
                float halo = pow(saturate(1 - dot(p,p)), 1.7);
                float core = exp(-dot(p,p) * 18);
                float nearFade = smoothstep(_NearFade.x, max(_NearFade.x + 0.01, _NearFade.y), distance(GetCameraPositionWS(), input.positionWS));
                float alpha = input.color.a * nearFade;
                return half4((input.color.rgb * halo + core * 0.35) * alpha * _Emission, 0);
            }
            ENDHLSL
        }
    }
}
