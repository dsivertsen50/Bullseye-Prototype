Shader "Bullseye/MeshSurfaceStamp"
{
    Properties
    {
        _BullseyePosition ("Bullseye Position", Vector) = (0, 1, 0, 0)
        _BullseyeNormal ("Bullseye Normal", Vector) = (0, 0, 1, 0)
        _BullseyeRadius ("Bullseye Radius", Float) = 0.13
        _BullseyeEnabled ("Enabled", Float) = 0
        _Brightness ("Brightness", Float) = 3.5
        [HDR] _RingOuter ("Outer Ring", Color) = (8, 0.18, 0.08, 1)
        [HDR] _RingMid ("Mid Ring", Color) = (6.5, 6.5, 6.5, 1)
        [HDR] _RingInner ("Inner Ring", Color) = (9, 0.28, 0.1, 1)
        [HDR] _CenterColor ("Center", Color) = (10, 8.5, 1.4, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "ForwardOnly" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _BullseyePosition;
            float4 _BullseyeNormal;
            float _BullseyeRadius;
            float _BullseyeEnabled;
            float _Brightness;
            float4 _RingOuter;
            float4 _RingMid;
            float4 _RingInner;
            float4 _CenterColor;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                ZERO_INITIALIZE(Varyings, output);
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                if (_BullseyeEnabled < 0.5)
                    discard;

                float radius = max(0.01, _BullseyeRadius);
                float3 toPoint = input.positionWS - _BullseyePosition.xyz;
                float dist = length(toPoint);
                if (dist > radius)
                    discard;

                float3 stampNormal = normalize(_BullseyeNormal.xyz + 1e-5);
                float facing = dot(normalize(input.normalWS), stampNormal);
                if (facing < -0.15)
                    discard;

                float n = saturate(dist / radius);
                float4 color = _RingOuter;
                if (n < 0.18)
                    color = _CenterColor;
                else if (n < 0.38)
                    color = _RingInner;
                else if (n < 0.62)
                    color = _RingMid;

                float edge = 1.0 - smoothstep(0.78, 1.0, n);
                float wrap = saturate((facing + 0.15) / 0.45);
                color.rgb *= max(1.0, _Brightness);
                color.a = saturate(edge * lerp(0.75, 1.0, wrap));
                if (color.a < 0.01)
                    discard;

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "SRPDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            float4 _BullseyePosition;
            float4 _BullseyeNormal;
            float _BullseyeRadius;
            float _BullseyeEnabled;
            float _Brightness;
            float4 _RingOuter;
            float4 _RingMid;
            float4 _RingInner;
            float4 _CenterColor;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                if (_BullseyeEnabled < 0.5)
                    discard;

                float radius = max(0.01, _BullseyeRadius);
                float3 toPoint = input.positionWS - _BullseyePosition.xyz;
                float dist = length(toPoint);
                if (dist > radius)
                    discard;

                float3 stampNormal = normalize(_BullseyeNormal.xyz + 1e-5);
                float facing = dot(normalize(input.normalWS), stampNormal);
                if (facing < -0.15)
                    discard;

                float n = saturate(dist / radius);
                float4 color = _RingOuter;
                if (n < 0.18)
                    color = _CenterColor;
                else if (n < 0.38)
                    color = _RingInner;
                else if (n < 0.62)
                    color = _RingMid;

                float edge = 1.0 - smoothstep(0.78, 1.0, n);
                float wrap = saturate((facing + 0.15) / 0.45);
                color.rgb *= max(1.0, _Brightness);
                color.a = saturate(edge * lerp(0.75, 1.0, wrap));
                if (color.a < 0.01)
                    discard;

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
