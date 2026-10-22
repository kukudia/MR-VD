// Shared procedural surface for the aurora strips, floor pulses and spectrum-history grid.
// Vertex colors carry the authored palette/visibility; no camera image or external texture is read.
Shader "MR-VD/Spatial Audio Surface"
{
    Properties
    {
        _Emission ("Emission", Range(0, 3)) = 0.85
        _Opacity ("Opacity", Range(0, 1)) = 0.65
        [HideInInspector] _Style ("Style", Float) = 0
        [HideInInspector] _VisualTime ("Frozen visual time", Float) = 0
        [HideInInspector] _Detail ("Detail", Float) = 0
        [HideInInspector] _FiberDensity ("Fibers", Float) = 22
        [HideInInspector] _Turbulence ("Turbulence", Float) = 0.65
        [HideInInspector] _FlowSpeed ("Flow", Float) = 0.7
        [HideInInspector] _Sparkle ("Sparkle", Float) = 0.4
        [HideInInspector] _NearFadeStart ("Near fade start", Float) = 0.8
        [HideInInspector] _NearFadeEnd ("Near fade end", Float) = 1.4
        [HideInInspector] _ScreenEnabled ("Reading screen enabled", Float) = 0
        [HideInInspector] _SpectralShape ("Spectral shape", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Name "SpatialAudio"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Emission;
                float _Opacity;
                float _Style;
                float _VisualTime;
                float _Detail;
                float _FiberDensity;
                float _Turbulence;
                float _FlowSpeed;
                float _Sparkle;
                float _NearFadeStart;
                float _NearFadeEnd;
                float _ScreenEnabled;
                float4x4 _ScreenWorldToLocal;
                float4 _ScreenRect;
                float4 _SpectralShape;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
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

            float ReadingMask(float3 positionWS, float3 eyeWS)
            {
                if (_ScreenEnabled < 0.5)
                {
                    return 1.0;
                }
                float3 eye = mul(_ScreenWorldToLocal, float4(eyeWS, 1)).xyz;
                float3 samplePosition = mul(_ScreenWorldToLocal, float4(positionWS, 1)).xyz;
                float denominator = samplePosition.z - eye.z;
                if (abs(denominator) < 0.00001)
                {
                    return 1.0;
                }
                float t = -eye.z / denominator;
                // Also clear foreground effects along rays through the reading rectangle.
                if (t <= 0.0)
                {
                    return 1.0;
                }
                float2 p = lerp(eye.xy, samplePosition.xy, t);
                bool inside = p.x > _ScreenRect.x && p.y > _ScreenRect.y
                    && p.x < _ScreenRect.z && p.y < _ScreenRect.w;
                return inside ? 0.0 : 1.0;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 blend = local * local * (3.0 - 2.0 * local);
                return lerp(lerp(Hash21(cell), Hash21(cell + float2(1, 0)), blend.x),
                    lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1.0), blend.x), blend.y);
            }

            float Filament(float phase, float width)
            {
                float distanceToLine = abs(frac(phase) - 0.5);
                // Derivatives preserve subpixel strands during movement and in both XR eyes.
                float pixel = max(fwidth(phase), 0.001);
                return 1.0 - smoothstep(width, width + pixel, distanceToLine);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 eye = GetCameraPositionWS();
                float nearFade = smoothstep(_NearFadeStart, max(_NearFadeStart + 0.1, _NearFadeEnd),
                    distance(input.positionWS, eye));
                float shape;
                float highlight = 0.0;
                float flow = _VisualTime * _FlowSpeed * (1.0 + _SpectralShape.x * 0.5);
                if (_Style < 0.5)
                {
                    float edge = pow(saturate(1.0 - abs(input.uv.y * 2.0 - 1.0)), 1.8);
                    float2 drift = float2(input.uv.x * 10.0 - flow * 0.25, input.uv.y * 2.2);
                    float warp = Noise(drift) * 0.6 + Noise(drift * 2.1 + 13.7) * 0.3
                        + Noise(drift * 4.3 - 7.1) * 0.1;
                    float strand = input.uv.y * _FiberDensity + warp * _Turbulence * (3.0 + _SpectralShape.z * 2.0);
                    float fibers = Filament(strand, 0.045 + _Detail * 0.025);
                    float mist = Noise(float2(input.uv.x * 22.0 - flow * 0.7, input.uv.y * 4.0));
                    float currents = pow(saturate(sin(input.uv.x * 28.0 - flow + warp * 4.0) * 0.5 + 0.5), 8.0);
                    float glints = pow(saturate(Noise(drift * float2(6.0, 16.0) + flow * 0.2)), 14.0);
                    shape = edge * (0.18 + mist * 0.32 + fibers * (0.4 + currents * 0.7));
                    highlight = edge * (currents * fibers * 0.45 + glints * _Sparkle * (5.0 + _SpectralShape.w * 8.0));
                }
                else if (_Style < 1.5)
                {
                    float edge = pow(saturate(1.0 - abs(input.uv.y * 2.0 - 1.0)), 0.8);
                    float channels = Filament(input.uv.y * 3.0, 0.11);
                    float dashes = smoothstep(0.1, 0.3, sin(input.uv.x * 240.0 - flow * 2.0));
                    float sweep = pow(saturate(sin(input.uv.x * 18.8496 + flow) * 0.5 + 0.5), 18.0);
                    shape = edge * (0.3 + channels * 0.6) * (0.65 + dashes * 0.35);
                    highlight = edge * sweep * _Sparkle;
                }
                else if (_Style < 2.5)
                {
                    float2 cellDistance = abs(frac(input.uv + 0.5) - 0.5);
                    float2 pixelWidth = max(fwidth(input.uv), 0.0001);
                    float2 lines = 1.0 - smoothstep(pixelWidth * 0.5, pixelWidth * 1.5, cellDistance);
                    shape = 0.025 + max(lines.x, lines.y) * 0.7;
                    float contours = Filament(input.positionWS.y * 9.0 - flow * 0.12, 0.035);
                    float nodes = lines.x * lines.y;
                    float scanner = pow(saturate(sin(input.uv.y * 0.45 - flow) * 0.5 + 0.5), 24.0);
                    shape += contours * 0.12;
                    highlight = nodes * (0.1 + scanner * _Sparkle);
                }
                else if (_Style < 3.5)
                {
                    float edge = pow(saturate(1.0 - abs(input.uv.y * 2.0 - 1.0)), 0.7);
                    float carrier = sin(input.uv.x * 100.0 - flow * 1.8) * 0.5 + 0.5;
                    float packet = pow(saturate(sin(input.uv.x * 31.4159 + flow * 0.7) * 0.5 + 0.5), 18.0);
                    shape = edge * (0.35 + carrier * 0.35 + packet * 0.6);
                    highlight = edge * packet * (0.35 + _SpectralShape.w) * _Sparkle;
                }
                else
                {
                    float nodes = pow(abs(input.uv.x * 2.0 - 1.0), 20.0);
                    float packet = pow(saturate(sin(input.uv.x * 18.8496 - flow * 2.0) * 0.5 + 0.5), 12.0);
                    float ticks = Filament(input.uv.x * 14.0, 0.06);
                    shape = 0.4 + nodes * 0.8 + packet * 0.3 + ticks * 0.15;
                    highlight = nodes * 0.4 + packet * _Sparkle * 0.5;
                }
                float alpha = input.color.a * _Opacity * shape * nearFade * ReadingMask(input.positionWS, eye);
                float3 color = lerp(input.color.rgb, max(input.color.rgb, 0.65), saturate(highlight));
                return half4(color * _Emission, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
