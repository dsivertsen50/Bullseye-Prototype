#ifndef BULLSEYE_CHARACTER_SURFACE_INCLUDED
#define BULLSEYE_CHARACTER_SURFACE_INCLUDED

float4 BullseyePaintRings(float normalizedDistance)
{
    float n = saturate(normalizedDistance);
    float4 color = float4(0.90, 0.047, 0.047, 1.0);
    if (n >= 0.18 && n < 0.36)
        color = float4(1.0, 1.0, 1.0, 1.0);
    else if (n >= 0.36 && n < 0.52)
        color = float4(0.90, 0.047, 0.047, 1.0);
    else if (n >= 0.52 && n < 0.70)
        color = float4(1.0, 1.0, 1.0, 1.0);

    color.a *= 1.0 - smoothstep(0.92, 1.0, n);
    return color;
}

float3 SafeNormalize3(float3 value, float3 fallback)
{
    float lenSq = dot(value, value);
    return lenSq > 1e-8 ? value * rsqrt(lenSq) : fallback;
}

bool BullseyeIsHead(float family)
{
    return abs(family - 0.0) < 0.01;
}

bool BullseyeIsNeck(float family)
{
    return abs(family - 1.0) < 0.01;
}

bool BullseyeIsTorso(float family)
{
    return abs(family - 2.0) < 0.01;
}

bool BullseyeIsArm(float family)
{
    return abs(family - 3.0) < 0.01 || abs(family - 4.0) < 0.01;
}

bool BullseyeIsLeg(float family)
{
    return abs(family - 5.0) < 0.01 || abs(family - 6.0) < 0.01;
}

bool BullseyeSameFamily(float a, float b)
{
    return abs(a - b) < 0.01;
}

bool BullseyeInHeadChain(float family)
{
    return BullseyeIsHead(family) || BullseyeIsNeck(family) || BullseyeIsTorso(family);
}

bool BullseyeInPelvisChain(float family)
{
    return BullseyeIsTorso(family) || BullseyeIsLeg(family);
}

bool BullseyeFamilyMatches(float fragmentFamily, float stampFamily)
{
    if (BullseyeSameFamily(fragmentFamily, stampFamily))
        return true;

    // A 26 cm stamp is larger than the head or neck patch, so those
    // surfaces have to share paint. Torso still must not paint arms.
    if (BullseyeInHeadChain(fragmentFamily) && BullseyeInHeadChain(stampFamily))
        return true;

    if (BullseyeInPelvisChain(fragmentFamily) && BullseyeInPelvisChain(stampFamily))
        return true;

    // Shoulder/hip continuity when the stamp is already on the limb.
    if (BullseyeIsArm(stampFamily) && BullseyeIsTorso(fragmentFamily))
        return true;

    return false;
}

bool BullseyeFamilyAllowed(float fragmentFamily, float currentFamily, float targetFamily)
{
    return BullseyeFamilyMatches(fragmentFamily, currentFamily) ||
           BullseyeFamilyMatches(fragmentFamily, targetFamily);
}

bool BullseyeFacingAllowed(float fragmentFamily, float fragmentFacing, float currentFamily, float targetFamily, float bullseyeFacing)
{
    bool torsoFragment = abs(fragmentFamily - 2.0) <= 0.55;
    bool torsoBullseye = min(abs(currentFamily - 2.0), abs(targetFamily - 2.0)) <= 0.55;
    if (!torsoFragment || !torsoBullseye || abs(bullseyeFacing) < 0.1)
        return true;

    return fragmentFacing * bullseyeFacing >= -0.15;
}

bool BullseyeEvaluateCylinder(
    float3 PositionWS,
    float3 center,
    float3 stampNormal,
    float3 tangent,
    float3 wrapAxis,
    float wrapRadius,
    float radius,
    out float surfaceDistance,
    out float2 stampUV,
    out float wrapAngle)
{
    surfaceDistance = 0.0;
    stampUV = float2(0.5, 0.5);
    wrapAngle = 0.0;

    float3 refRadial = stampNormal - wrapAxis * dot(stampNormal, wrapAxis);
    refRadial = SafeNormalize3(refRadial, tangent);
    float3 axisPoint = center - refRadial * wrapRadius;
    float3 fromAxis = PositionWS - axisPoint;
    float axial = dot(fromAxis, wrapAxis);
    float3 fragFromAxis = fromAxis - wrapAxis * axial;
    float fragRadius = length(fragFromAxis);
    if (fragRadius < 1e-5)
        return false;

    float3 fragDir = fragFromAxis / fragRadius;
    wrapAngle = atan2(dot(wrapAxis, cross(refRadial, fragDir)), dot(refRadial, fragDir));
    float wrapU = wrapAngle * wrapRadius;
    surfaceDistance = sqrt(wrapU * wrapU + axial * axial);
    if (surfaceDistance > radius)
        return false;

    float thin = wrapRadius < radius * 0.85 ? 1.0 : 0.0;
    float shellLimit = lerp(max(0.05, wrapRadius * 0.85), max(0.11, wrapRadius * 2.0), thin);
    if (abs(fragRadius - wrapRadius) > shellLimit)
        return false;

    stampUV = saturate(float2(wrapU, axial) / radius * 0.5 + 0.5);
    return true;
}

bool BullseyeEvaluateSphere(
    float3 PositionWS,
    float3 center,
    float3 stampNormal,
    float3 tangent,
    float3 bitangent,
    float wrapRadius,
    float radius,
    out float surfaceDistance,
    out float2 stampUV)
{
    surfaceDistance = 0.0;
    stampUV = float2(0.5, 0.5);

    float3 sphereCenter = center - stampNormal * wrapRadius;
    float3 stampVec = center - sphereCenter;
    float stampR = max(length(stampVec), wrapRadius);
    float3 toFrag = PositionWS - sphereCenter;
    float fragR = length(toFrag);
    if (fragR < 1e-5)
        return false;

    if (abs(fragR - stampR) > max(0.07, stampR * 0.65))
        return false;

    float3 a = stampVec / stampR;
    float3 b = toFrag / fragR;
    float cosAng = clamp(dot(a, b), -1.0, 1.0);
    float ang = atan2(length(cross(a, b)), cosAng);
    surfaceDistance = ang * stampR;
    if (surfaceDistance > radius)
        return false;

    float3 t = SafeNormalize3(tangent - a * dot(tangent, a), float3(1.0, 0.0, 0.0));
    float3 bt = SafeNormalize3(bitangent - a * dot(bitangent, a), cross(a, t));
    t = SafeNormalize3(cross(bt, a), t);
    bt = cross(a, t);

    float3 perp = b - a * dot(a, b);
    float perpLen = length(perp);
    if (perpLen < 1e-5)
    {
        stampUV = float2(0.5, 0.5);
        return true;
    }

    float3 dir = perp / perpLen;
    float u = surfaceDistance * dot(dir, t);
    float v = surfaceDistance * dot(dir, bt);
    stampUV = saturate(float2(u, v) / radius * 0.5 + 0.5);
    return true;
}

void EvaluateBullseyeCharacterSurface_float(
    float3 BaseColor,
    float3 PositionWS,
    float3 NormalWS,
    float4 RegionCoords,
    UnityTexture2D BullseyeTex,
    float4 CenterEnabled,
    float4 NormalRadius,
    float3 TangentWS,
    float3 BitangentWS,
    float4 WrapAxisRadius,
    float4 RegionState,
    float2 WrapFlash,
    out float3 OutColor)
{
    OutColor = BaseColor;

    float enabled = CenterEnabled.w;
    if (enabled < 0.5)
        return;

    // UV2.x stores family + 1. Zero means "no body mask" (weapons, missing data).
    float familyCode = RegionCoords.x;
    if (familyCode < 0.5)
        return;

    float family = familyCode - 1.0;
    float fragmentFacing = RegionCoords.y;
    float currentFamily = RegionState.x;
    float targetFamily = RegionState.y;
    float bullseyeFacing = RegionState.w;
    if (!BullseyeFamilyAllowed(family, currentFamily, targetFamily))
        return;
    if (!BullseyeFacingAllowed(family, fragmentFacing, currentFamily, targetFamily, bullseyeFacing))
        return;

    float3 center = CenterEnabled.xyz;
    float radius = max(0.02, NormalRadius.w * (1.0 + 0.15 * saturate(WrapFlash.y)));
    float3 stampNormal = SafeNormalize3(NormalRadius.xyz, float3(0.0, 0.0, 1.0));
    float3 tangent = SafeNormalize3(TangentWS, float3(1.0, 0.0, 0.0));
    float3 bitangent = SafeNormalize3(BitangentWS, float3(0.0, 0.0, 1.0));
    float3 wrapAxis = SafeNormalize3(WrapAxisRadius.xyz, float3(0.0, 1.0, 0.0));
    float wrapRadius = max(0.025, WrapAxisRadius.w);
    float sphereBlend = saturate(WrapFlash.x);

    // Keep a pelvis stamp on the front of the thighs instead of smearing
    // through the inner crotch when both legs split off the torso.
    bool settledTorso = BullseyeIsTorso(currentFamily) && BullseyeIsTorso(targetFamily);
    if (settledTorso && BullseyeIsLeg(family) && dot(NormalWS, stampNormal) < 0.12)
        return;

    float surfaceDistance;
    float2 stampUV;
    bool hit = false;
    if (sphereBlend >= 0.45)
    {
        hit = BullseyeEvaluateSphere(
            PositionWS, center, stampNormal, tangent, bitangent, wrapRadius, radius,
            surfaceDistance, stampUV);
    }
    else
    {
        float wrapAngle;
        hit = BullseyeEvaluateCylinder(
            PositionWS, center, stampNormal, tangent, wrapAxis, wrapRadius, radius,
            surfaceDistance, stampUV, wrapAngle);
    }

    if (!hit)
        return;

    float4 stamp = SAMPLE_TEXTURE2D(BullseyeTex.tex, BullseyeTex.samplerstate, stampUV);
    if (stamp.a < 0.02)
        stamp = BullseyePaintRings(surfaceDistance / radius);

    float alpha = saturate(stamp.a * enabled);
    OutColor = lerp(BaseColor, stamp.rgb, alpha);
}

#endif
