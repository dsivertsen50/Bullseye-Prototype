using UnityEngine;

/// <summary>
/// Snapshot of one resolved damage event. Kept for debug and future combat stats.
/// </summary>
public struct DamageInfo
{
    public ulong AttackerClientId;
    public ulong VictimClientId;
    public string WeaponId;
    public DamageSourceType SourceType;
    public float BaseDamage;
    public float Distance;
    public float DistanceMultiplier;
    public float LocationMultiplier;
    public BullseyeBodyZone HitRegion;
    public bool WasBullseyeHit;
    public bool WasHeadshot;
    public int PelletIndex;
    public float RawDamage;
    public int FinalDamage;
}

/// <summary>
/// Shared damage math. Reads weapon profiles and location multipliers only;
/// it never branches on weapon names.
/// </summary>
public static class WeaponDamageCalculator
{
    public static float EvaluateDistanceMultiplier(WeaponDamageSettings settings, float distance)
    {
        settings ??= WeaponDamageSettings.Fallback;
        float safeDistance = Mathf.Max(0f, distance);

        if (safeDistance > settings.MaximumRange)
        {
            return settings.BeyondMaxRange == BeyondMaxRangeDamage.NoDamage
                ? 0f
                : settings.MinimumDamageMultiplier;
        }

        if (safeDistance <= settings.FalloffStart || settings.FalloffEnd <= settings.FalloffStart)
            return 1f;

        float normalized = Mathf.InverseLerp(settings.FalloffStart, settings.FalloffEnd, safeDistance);
        float curve = Mathf.Clamp01(settings.FalloffCurve.Evaluate(normalized));
        return Mathf.Lerp(settings.MinimumDamageMultiplier, 1f, curve);
    }

    public static float EvaluateProjectileDamage(WeaponDamageSettings settings, float distance)
    {
        settings ??= WeaponDamageSettings.Fallback;
        return settings.ProjectileDamage * EvaluateDistanceMultiplier(settings, distance);
    }

    public static int ToHealthUnits(float rawDamage)
    {
        if (rawDamage <= 0.0001f)
            return 0;

        return Mathf.Max(0, Mathf.RoundToInt(rawDamage));
    }

    public static DamageInfo Evaluate(
        WeaponDamageSettings settings,
        WeaponDefinition weapon,
        float distance,
        BullseyeBodyZone zone,
        float locationMultiplier,
        int pelletIndex,
        ulong attackerClientId,
        ulong victimClientId)
    {
        settings ??= WeaponDamageSettings.Fallback;
        float distanceMultiplier = EvaluateDistanceMultiplier(settings, distance);
        float raw = settings.ProjectileDamage * distanceMultiplier * Mathf.Max(0f, locationMultiplier);
        int finalDamage = ToHealthUnits(raw);

        return new DamageInfo
        {
            AttackerClientId = attackerClientId,
            VictimClientId = victimClientId,
            WeaponId = weapon != null ? weapon.WeaponId : "unknown",
            SourceType = DamageSourceType.Firearm,
            BaseDamage = settings.ProjectileDamage,
            Distance = Mathf.Max(0f, distance),
            DistanceMultiplier = distanceMultiplier,
            LocationMultiplier = locationMultiplier,
            HitRegion = zone,
            WasBullseyeHit = true,
            WasHeadshot = zone == BullseyeBodyZone.Head,
            PelletIndex = pelletIndex,
            RawDamage = raw,
            FinalDamage = finalDamage
        };
    }
}
