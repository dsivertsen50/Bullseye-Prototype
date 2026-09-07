#ifndef BULLSEYE_PHYSICAL_DISC_INCLUDED
#define BULLSEYE_PHYSICAL_DISC_INCLUDED

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

float4 _RimColor;
float _Brightness;
float4 _EmissiveColor;

float4 EvaluatePhysicalDisc(float3 positionOS, float3 normalOS)
{
    float3 normal = normalize(normalOS);
    float capAmount = smoothstep(0.35, 0.7, abs(normal.z));

    float2 capUv = float2(positionOS.x, positionOS.y) + 0.5;
    if (normal.z < 0.0)
        capUv.x = 1.0 - capUv.x;

    float4 decal = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, capUv);
    float3 faceColor = decal.rgb;
    if (decal.a < 0.08)
        faceColor = _RimColor.rgb;

    float3 color = lerp(_RimColor.rgb, faceColor, capAmount);
    color *= max(1.0, _Brightness);
    color += _EmissiveColor.rgb;
    return float4(color, 1.0);
}

#endif
