Shader "MR-VD/Audio Reactive GPU Stardust"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Particle
            {
                float3 position;
                float size;
                float3 velocity;
                float seed;
                float4 color;
            };

            StructuredBuffer<Particle> _Particles;
            float3 _ParticleCenterWS;
            float3 _CameraPositionWS;
            float4x4 _OcclusionWorldToLocal;
            float4 _OcclusionHalfSize;
            float _OcclusionEnabled;
            float _ParticleSize;
            float _Emission;
            float _Opacity;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint rawInstanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
                nointerpolation float occluded : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 GetQuadCorner(uint vertexID)
            {
                static const float2 corners[6] =
                {
                    float2(-1.0, -1.0),
                    float2(-1.0, 1.0),
                    float2(1.0, 1.0),
                    float2(-1.0, -1.0),
                    float2(1.0, 1.0),
                    float2(1.0, -1.0)
                };
                return corners[vertexID];
            }

            float IsScreenOccluded(float3 particlePositionWS)
            {
                if (_OcclusionEnabled < 0.5)
                {
                    return 0.0;
                }

                float3 cameraPositionLS = mul(_OcclusionWorldToLocal,
                    float4(_CameraPositionWS, 1.0)).xyz;
                float3 particlePositionLS = mul(_OcclusionWorldToLocal,
                    float4(particlePositionWS, 1.0)).xyz;
                float denominator = particlePositionLS.z - cameraPositionLS.z;
                if (abs(denominator) < 0.0001)
                {
                    return 0.0;
                }

                float intersectionT = -cameraPositionLS.z / denominator;
                if (intersectionT <= 0.0 || intersectionT >= 1.0)
                {
                    return 0.0;
                }

                float2 intersectionXY = lerp(cameraPositionLS.xy, particlePositionLS.xy, intersectionT);
                return abs(intersectionXY.x) <= _OcclusionHalfSize.x
                    && abs(intersectionXY.y) <= _OcclusionHalfSize.y
                    ? 1.0
                    : 0.0;
            }

            Varyings Vert(Attributes input)
            {
                uint particleIndex = input.rawInstanceID;
                #if UNITY_ANY_INSTANCING_ENABLED
                    UnitySetupInstanceID(input.rawInstanceID);
                    particleIndex = unity_InstanceID;
                #endif

                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                Particle particle = _Particles[particleIndex];
                float2 corner = GetQuadCorner(input.vertexID);
                float size = _ParticleSize * particle.size;
                float3 centerWS = _ParticleCenterWS + particle.position;
                float3 cameraRightWS = normalize(float3(
                    UNITY_MATRIX_I_V[0].x,
                    UNITY_MATRIX_I_V[1].x,
                    UNITY_MATRIX_I_V[2].x));
                float3 cameraUpWS = normalize(float3(
                    UNITY_MATRIX_I_V[0].y,
                    UNITY_MATRIX_I_V[1].y,
                    UNITY_MATRIX_I_V[2].y));
                float3 positionWS = centerWS + (cameraRightWS * corner.x + cameraUpWS * corner.y) * size;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = corner * 0.5 + 0.5;
                output.color = particle.color;
                output.occluded = IsScreenOccluded(centerWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(0.5 - input.occluded);

                float2 centered = input.uv * 2.0 - 1.0;
                float radiusSquared = dot(centered, centered);
                clip(1.0 - radiusSquared);

                float softDisc = pow(saturate(1.0 - radiusSquared), 2.2);
                float alpha = softDisc * input.color.a * _Opacity;
                float3 color = input.color.rgb * alpha * _Emission;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
