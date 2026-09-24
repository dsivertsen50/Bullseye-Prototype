using UnityEngine;

/// <summary>
/// Identifies where a damage event came from. Additional sources can be
/// added later without changing elimination-tracking call sites.
/// </summary>
public enum DamageSourceType
{
    Unknown = 0,
    Firearm = 1,
    Grenade = 2,
    Environment = 3,
    BodySlam = 4
}

/// <summary>
/// Server-side snapshot passed into PlayerHealth. Weapons fill this in;
/// the health/death system decides whether a death occurred; combat
/// telemetry records the result.
/// </summary>
public struct DamageContext
{
    public const ulong NoAttackerId = ulong.MaxValue;

    public ulong AttackerClientId;
    public ulong VictimClientId;
    public int Amount;
    public DamageSourceType SourceType;
    public string SourceId;
    public float Distance;
    public int RicochetCount;
    public string RicochetSurfaceId;
    public float ProjectilePathDistance;

    public bool HasAttacker => AttackerClientId != NoAttackerId;

    /// <summary>
    /// True when this shot bounced at least once. Kept consistent with
    /// <see cref="RicochetCount"/> so a positive count cannot be recorded as a direct hit.
    /// </summary>
    public bool WasRicochet => RicochetCount > 0;

    public static DamageContext FromFirearm(
        ulong attackerClientId,
        ulong victimClientId,
        int amount,
        string weaponId,
        float distance = 0f,
        int ricochetCount = 0,
        string ricochetSurfaceId = null,
        float projectilePathDistance = 0f)
    {
        int bounces = ricochetCount > 0 ? ricochetCount : 0;
        return new DamageContext
        {
            AttackerClientId = attackerClientId,
            VictimClientId = victimClientId,
            Amount = amount,
            SourceType = DamageSourceType.Firearm,
            SourceId = string.IsNullOrEmpty(weaponId) ? "unknown" : weaponId,
            Distance = distance,
            RicochetCount = bounces,
            RicochetSurfaceId = bounces > 0 ? ricochetSurfaceId : null,
            ProjectilePathDistance = Mathf.Max(0f, projectilePathDistance)
        };
    }

    public static DamageContext Unattributed(ulong victimClientId, int amount)
    {
        return new DamageContext
        {
            AttackerClientId = NoAttackerId,
            VictimClientId = victimClientId,
            Amount = amount,
            SourceType = DamageSourceType.Environment,
            SourceId = "environment",
            Distance = 0f
        };
    }

    public static DamageContext FromBodySlam(
        ulong attackerClientId,
        ulong victimClientId,
        int amount)
    {
        return new DamageContext
        {
            AttackerClientId = attackerClientId,
            VictimClientId = victimClientId,
            Amount = amount,
            SourceType = DamageSourceType.BodySlam,
            SourceId = "dolphin_dive_body_slam",
            Distance = 0f
        };
    }
}
