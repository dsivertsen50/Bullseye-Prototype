#ifndef BULLSEYE_MESH_STAMP_INCLUDED
#define BULLSEYE_MESH_STAMP_INCLUDED

float4 _BullseyePosition;
float4 _BullseyeNormal;
float4 _BullseyeAxis;
float _BullseyeRadius;
float _WrapRadius;
float _ShellThickness;
float _MaxWrapAngle;
float _SurfaceOffset;
float _BullseyeEnabled;
float _Brightness;
float _Opacity;
float4 _ColorRed;
float4 _ColorWhite;

float4 PaintBullseye(float dist, float radius)
{
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

float4 EvaluateBullseyeStamp(float3 positionWS, float3 normalWS)
{
    if (_BullseyeEnabled < 0.5)
        return 0;

    float radius = max(0.01, _BullseyeRadius);
    float wrapRadius = max(0.03, _WrapRadius);
    float3 stampNormal = normalize(_BullseyeNormal.xyz + 1e-5);
    float3 stampPos = _BullseyePosition.xyz + stampNormal * max(0.0, _SurfaceOffset);
    float3 meshNormal = normalize(normalWS + 1e-5);
    if (dot(meshNormal, stampNormal) < -0.85)
        return 0;

    float3 axis = _BullseyeAxis.xyz;
    if (dot(axis, axis) < 1e-6)
        axis = float3(0.0, 1.0, 0.0);
    axis = normalize(axis);
    if (abs(dot(axis, stampNormal)) > 0.94)
    {
        float3 fallback = abs(stampNormal.y) < 0.88 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
        axis = normalize(fallback - stampNormal * dot(fallback, stampNormal));
    }

    float3 nRad = stampNormal - axis * dot(stampNormal, axis);
    float nRadLen = length(nRad);
    if (nRadLen < 1e-4)
        return 0;
    nRad /= nRadLen;

    float3 center = stampPos - nRad * wrapRadius;
    float3 fromCenter = positionWS - center;
    float axial = dot(fromCenter, axis);
    float3 fragRad = fromCenter - axis * axial;
    float fragRadius = length(fragRad);
    if (fragRadius < 1e-4)
        return 0;

    float3 fragDir = fragRad / fragRadius;
    float angle = atan2(dot(axis, cross(nRad, fragDir)), dot(nRad, fragDir));
    float dist = sqrt(angle * angle * wrapRadius * wrapRadius + axial * axial);
    if (dist > radius)
        return 0;

    return PaintBullseye(dist, radius);
}

#endif
