#ifndef BULLSEYE_PHYSICAL_DISC_INCLUDED
#define BULLSEYE_PHYSICAL_DISC_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

float4 _RimColor;
float _Brightness;
float4 _EmissiveColor;
float _WrapMode;
float _WrapRadius;
float _StampRadius;

float4 SampleBullseye(float2 uv)
{
    float4 decal = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
    float3 faceColor = decal.rgb;
    if (decal.a < 0.08)
        faceColor = _RimColor.rgb;

    float3 color = faceColor * max(1.0, _Brightness) + _EmissiveColor.rgb;
    return float4(color, 1.0);
}

float4 EvaluatePhysicalDisc(float3 positionOS, float3 normalOS)
{
    if (_WrapMode > 0.5)
    {
        float wrapR = max(0.03, _WrapRadius);
        float stampR = max(0.04, _StampRadius);
        float angle = atan2(positionOS.x, positionOS.z);
        float2 surface = float2(angle * wrapR, positionOS.y * stampR * 2.0);
        if (length(surface) > stampR)
            clip(-1);
        return SampleBullseye(surface / (stampR * 2.0) + 0.5);
    }

    float3 normal = normalize(normalOS);
    float capAmount = smoothstep(0.35, 0.7, abs(normal.z));

    float2 capUv = float2(positionOS.x, positionOS.y) + 0.5;
    if (normal.z < 0.0)
        capUv.x = 1.0 - capUv.x;

    float4 decal = SampleBullseye(capUv);
    float3 color = lerp(_RimColor.rgb * max(1.0, _Brightness) + _EmissiveColor.rgb, decal.rgb, capAmount);
    return float4(color, 1.0);
}

#endif
