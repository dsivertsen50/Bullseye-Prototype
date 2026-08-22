Shader "Hidden/Bullseye/AdsSightBlur"
{
    HLSLINCLUDE

    #pragma vertex Vert
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float _AdsBlurIntensity;
    float _AdsSharpRadius;
    float _AdsBlurOuterRadius;
    float _AdsBlurSize;

    float3 SampleBlurred(float2 uv, float blurAmount)
    {
        float radius = _AdsBlurSize * blurAmount;
        float2 aspect = float2(_ScreenSize.y * _ScreenSize.z, 1.0);
        float2 r = radius * aspect;

        float3 color = CustomPassSampleCameraColor(uv, 0);
        color += CustomPassSampleCameraColor(uv + float2(r.x, 0), 0);
        color += CustomPassSampleCameraColor(uv + float2(-r.x, 0), 0);
        color += CustomPassSampleCameraColor(uv + float2(0, r.y), 0);
        color += CustomPassSampleCameraColor(uv + float2(0, -r.y), 0);

        float2 d = r * 0.70710678;
        color += CustomPassSampleCameraColor(uv + float2(d.x, d.y), 0);
        color += CustomPassSampleCameraColor(uv + float2(-d.x, d.y), 0);
        color += CustomPassSampleCameraColor(uv + float2(d.x, -d.y), 0);
        color += CustomPassSampleCameraColor(uv + float2(-d.x, -d.y), 0);

        float2 outer = r * 1.85;
        color += CustomPassSampleCameraColor(uv + float2(outer.x, 0), 0);
        color += CustomPassSampleCameraColor(uv + float2(-outer.x, 0), 0);
        color += CustomPassSampleCameraColor(uv + float2(0, outer.y), 0);
        color += CustomPassSampleCameraColor(uv + float2(0, -outer.y), 0);

        return color * (1.0 / 13.0);
    }

    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);

        float2 uv = varyings.positionCS.xy * _ScreenSize.zw;
        float3 sharp = CustomPassLoadCameraColor(varyings.positionCS.xy, 0);

        float intensity = saturate(_AdsBlurIntensity);
        if (intensity <= 0.001)
            return float4(sharp, 1);

        float aspect = _ScreenSize.x * _ScreenSize.w;
        float2 centered = (uv - 0.5) * float2(aspect, 1.0);
        float dist = length(centered);
        float inner = max(0.001, _AdsSharpRadius);
        float outer = max(inner + 0.001, _AdsBlurOuterRadius);
        float mask = smoothstep(inner, outer, dist) * intensity;

        if (mask <= 0.001)
            return float4(sharp, 1);

        float3 blurred = SampleBlurred(uv, mask);
        return float4(lerp(sharp, blurred, mask), 1);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Blend Off
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
