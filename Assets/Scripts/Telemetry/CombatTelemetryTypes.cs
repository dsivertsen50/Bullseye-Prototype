/// <summary>
/// Match-scoped combat events recorded by CombatTelemetryManager.
/// </summary>
public enum CombatEventType
{
    ShotFired = 0,
    BullseyeHit = 1,
    BullseyeDamaged = 2,
    BullseyeDetached = 3,
    Elimination = 4,
    AssistAwarded = 5,
    PlayerDeath = 6
}

/// <summary>
/// Authoritative bullseye attach state at the moment an event is recorded.
/// Returning discs count as detached for telemetry.
/// </summary>
public enum BullseyeCombatState
{
    Attached = 0,
    Detached = 1
}

/// <summary>
/// How an elimination occurred. New mechanics should add a value here
/// rather than introducing a separate tracker.
/// </summary>
public enum EliminationSourceType
{
    Firearm = 0,
    BodySlam = 1,
    Grenade = 2,
    Environmental = 3,
    Other = 4
}

/// <summary>
/// How a bullseye left the player. New detach mechanics should add a value.
/// </summary>
public enum BullseyeDetachMethod
{
    CombustionGrenade = 0,
    MagnetismGrenade = 1,
    Other = 2,
    RocketExplosion = 3
}
