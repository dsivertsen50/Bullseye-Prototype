#ifndef BULLSEYE_MESH_STAMP_INCLUDED
#define BULLSEYE_MESH_STAMP_INCLUDED

float4 _BullseyePosition;
float4 _BullseyeNormal;
float _BullseyeRadius;
float _BullseyeEnabled;
float _Brightness;
float _Opacity;
float4 _ColorRed;
float4 _ColorWhite;

float4 EvaluateBullseyeStamp(float3 positionWS, float3 normalWS)
{
    if (_BullseyeEnabled < 0.5)
        return 0;

    float radius = max(0.01, _BullseyeRadius);
    float dist = length(positionWS - _BullseyePosition.xyz);
    if (dist > radius)
        return 0;

    float3 stampNormal = normalize(_BullseyeNormal.xyz + 1e-5);
    float facing = dot(normalize(normalWS), stampNormal);
    if (facing < -0.2)
        return 0;

    // Classic target: red dot, white, red ring, white, red ring.
    float n = saturate(dist / radius);
    float4 color = _ColorRed;
    if (n >= 0.18 && n < 0.36)
        color = _ColorWhite;
    else if (n >= 0.36 && n < 0.52)
        color = _ColorRed;
    else if (n >= 0.52 && n < 0.70)
        color = _ColorWhite;
    else if (n >= 0.70)
        color = _ColorRed;

    float edge = 1.0 - smoothstep(0.92, 1.0, n);
    color.rgb *= max(1.0, _Brightness);
    color.a = saturate(max(0.85, _Opacity) * edge);
    return color;
}

#endif
