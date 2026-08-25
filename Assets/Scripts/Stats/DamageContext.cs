/// <summary>
/// Identifies where a damage event came from. Additional sources can be
/// added later without changing kill-tracking call sites.
/// </summary>
public enum DamageSourceType
{
    Unknown = 0,
    Firearm = 1,
    Grenade = 2,
    Environment = 3
}

/// <summary>
/// Server-side snapshot passed into PlayerHealth. Weapons fill this in;
/// the health/death system decides whether a death occurred; PlayerStats
/// records the result. Kills are one field among future Bullseye metrics.
/// </summary>
public struct DamageContext
{
    public const ulong NoAttackerId = ulong.MaxValue;

    public ulong AttackerClientId;
    public ulong VictimClientId;
    public int Amount;
    public DamageSourceType SourceType;
    public string SourceId;

    public bool HasAttacker => AttackerClientId != NoAttackerId;

    public static DamageContext FromFirearm(
        ulong attackerClientId,
        ulong victimClientId,
        int amount,
        string weaponId)
    {
        return new DamageContext
        {
            AttackerClientId = attackerClientId,
            VictimClientId = victimClientId,
            Amount = amount,
            SourceType = DamageSourceType.Firearm,
            SourceId = string.IsNullOrEmpty(weaponId) ? "unknown" : weaponId
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
            SourceId = "environment"
        };
    }
}
