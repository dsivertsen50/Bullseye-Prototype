Shader "Bullseye/DigitalDespawnDissolve"
{
    Properties
    {
        _BaseColorMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.45, 1.7, 2.1, 1)
        _Dissolve ("Dissolve", Range(0, 1)) = 0
        _EdgeWidth ("Edge Width", Range(0.001, 0.35)) = 0.11
        _NoiseScale ("Noise Scale", Float) = 18
        _PixelSize ("Pixel Size", Float) = 28
        _Direction ("Direction", Float) = 0
        _GlitchStrength ("Glitch Strength", Range(0, 1)) = 0.35
        _ScanLineStrength ("Scan Line Strength", Range(0, 1)) = 0.45
        _BoundsMin ("Bounds Min", Vector) = (0, 0, 0, 0)
        _BoundsMax ("Bounds Max", Vector) = (1, 2, 1, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "ForwardOnly" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);

            float4 _BaseColor;
            float4 _EdgeColor;
            float _Dissolve;
            float _EdgeWidth;
            float _NoiseScale;
            float _PixelSize;
            float _Direction;
            float _GlitchStrength;
            float _ScanLineStrength;
            float4 _BoundsMin;
            float4 _BoundsMax;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float DigitalNoise(float3 worldPos)
            {
                float pixel = max(1.0, _PixelSize);
                float3 quantized = floor(worldPos * pixel);
                float n = Hash13(quantized);
                float fine = Hash13(worldPos * max(1.0, _NoiseScale));
                return saturate(n * 0.72 + fine * 0.28);
            }

            float DirectionMask(float3 worldPos)
            {
                float3 bmin = _BoundsMin.xyz;
                float3 bmax = _BoundsMax.xyz;
                float3 size = max(bmax - bmin, float3(0.001, 0.001, 0.001));
                float3 local = saturate((worldPos - bmin) / size);
                int dir = (int)round(_Direction);

                if (dir == 1)
                    return 1.0 - local.y;
                if (dir == 2)
                    return DigitalNoise(worldPos);
                if (dir == 3)
                {
                    float3 center = (bmin + bmax) * 0.5;
                    float3 ext = size * 0.5;
                    float maxDist = length(ext);
                    return saturate(distance(worldPos, center) / max(0.001, maxDist));
                }

                return local.y;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                ZERO_INITIALIZE(Varyings, output);
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float dissolve = saturate(_Dissolve);
                float mask = DirectionMask(input.positionWS);
                float noise = DigitalNoise(input.positionWS);
                float threshold = saturate(mask * 0.78 + noise * 0.22);

                float glitch = 0.0;
                if (_GlitchStrength > 0.001 && dissolve > 0.02)
                {
                    float band = frac(input.positionWS.y * 22.0 + _TimeParameters.x * 9.0);
                    glitch = step(0.86, band) * _GlitchStrength * dissolve;
                }

                float cut = threshold - dissolve - glitch * 0.18;
                clip(cut);

                float4 albedo = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv) * _BaseColor;
                float edge = saturate(1.0 - smoothstep(0.0, max(0.001, _EdgeWidth), cut));
                float scan = 0.0;
                if (_ScanLineStrength > 0.001)
                {
                    float scanWave = abs(frac(input.positionWS.y * 36.0 - _TimeParameters.x * 4.0) - 0.5) * 2.0;
                    scan = (1.0 - scanWave) * _ScanLineStrength * dissolve;
                }

                float3 color = albedo.rgb;
                color = lerp(color, _EdgeColor.rgb, saturate(edge + scan));
                color += _EdgeColor.rgb * edge * 0.95;
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);

            float4 _BaseColor;
            float4 _EdgeColor;
            float _Dissolve;
            float _EdgeWidth;
            float _NoiseScale;
            float _PixelSize;
            float _Direction;
            float _GlitchStrength;
            float _ScanLineStrength;
            float4 _BoundsMin;
            float4 _BoundsMax;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float DigitalNoise(float3 worldPos)
            {
                float pixel = max(1.0, _PixelSize);
                float3 quantized = floor(worldPos * pixel);
                float n = Hash13(quantized);
                float fine = Hash13(worldPos * max(1.0, _NoiseScale));
                return saturate(n * 0.72 + fine * 0.28);
            }

            float DirectionMask(float3 worldPos)
            {
                float3 bmin = _BoundsMin.xyz;
                float3 bmax = _BoundsMax.xyz;
                float3 size = max(bmax - bmin, float3(0.001, 0.001, 0.001));
                float3 local = saturate((worldPos - bmin) / size);
                int dir = (int)round(_Direction);

                if (dir == 1)
                    return 1.0 - local.y;
                if (dir == 2)
                    return DigitalNoise(worldPos);
                if (dir == 3)
                {
                    float3 center = (bmin + bmax) * 0.5;
                    float3 ext = size * 0.5;
                    float maxDist = length(ext);
                    return saturate(distance(worldPos, center) / max(0.001, maxDist));
                }

                return local.y;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float dissolve = saturate(_Dissolve);
                float mask = DirectionMask(input.positionWS);
                float noise = DigitalNoise(input.positionWS);
                float threshold = saturate(mask * 0.78 + noise * 0.22);
                float cut = threshold - dissolve;
                clip(cut);

                float4 albedo = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv) * _BaseColor;
                float edge = saturate(1.0 - smoothstep(0.0, max(0.001, _EdgeWidth), cut));
                float3 color = lerp(albedo.rgb, _EdgeColor.rgb, edge);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
