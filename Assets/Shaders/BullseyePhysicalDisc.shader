Shader "Bullseye/PhysicalDisc"
{
    Properties
    {
        _BaseMap ("Decal", 2D) = "white" {}
        _RimColor ("Rim Color", Color) = (0.902, 0.047, 0.047, 1)
        _Brightness ("Brightness", Float) = 1.15
        [HDR] _EmissiveColor ("Emissive Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "ForwardOnly" }

            Blend One Zero
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "BullseyePhysicalDisc.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                ZERO_INITIALIZE(Varyings, output);
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return EvaluatePhysicalDisc(input.positionOS, input.normalOS);
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "BullseyePhysicalDisc.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalOS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return EvaluatePhysicalDisc(input.positionOS, input.normalOS);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
