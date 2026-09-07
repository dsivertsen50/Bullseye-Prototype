Shader "Bullseye/MeshSurfaceStamp"
{
    Properties
    {
        _BullseyePosition ("Bullseye Position", Vector) = (0, 1, 0, 0)
        _BullseyeNormal ("Bullseye Normal", Vector) = (0, 0, 1, 0)
        _BullseyeRadius ("Bullseye Radius", Float) = 0.14
        _BullseyeEnabled ("Enabled", Float) = 0
        _Brightness ("Brightness", Float) = 1.35
        _Opacity ("Opacity", Float) = 1
        [HDR] _ColorRed ("Red", Color) = (1.15, 0.04, 0.04, 1)
        [HDR] _ColorWhite ("White", Color) = (1.2, 1.2, 1.2, 1)
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

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "BullseyeMeshStamp.hlsl"

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
                float4 color = EvaluateBullseyeStamp(input.positionWS, input.normalWS);
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

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "BullseyeMeshStamp.hlsl"

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
                float4 color = EvaluateBullseyeStamp(input.positionWS, input.normalWS);
                if (color.a < 0.01)
                    discard;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
