Shader "MR-VD/Audio Reactive GPU Particles"
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
                float life;
                float3 velocity;
                float size;
                float4 color;
                float seed;
                float kind;
                float spectrumT;
                float beatId;
            };

            StructuredBuffer<Particle> _Particles;
            float4x4 _LocalToWorld;
            float _ParticleSize;
            float _BackgroundSizeScale;
            float _BeatSizeScale;
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
                float sizeScale = particle.kind < 0.5
                    ? 1.0
                    : (particle.kind < 1.5 ? _BeatSizeScale : _BackgroundSizeScale);
                float size = _ParticleSize * particle.size * sizeScale;

                float3 centerWS = mul(_LocalToWorld, float4(particle.position, 1.0)).xyz;
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
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

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
