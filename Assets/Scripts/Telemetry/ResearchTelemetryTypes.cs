/// <summary>
/// Research-layer classifications for REQ-061. These are independent of
/// match-stat enums in CombatTelemetryTypes so REQ-055 stays unchanged.
/// </summary>
public static class ResearchTelemetryConstants
{
    public const string SchemaVersion = "1.0";
    public const string GameplayConfigVersion = "prototype-061";
    public const string ParticipantPrefsKey = "Bullseye_ResearchParticipantId";
    public const string UnknownId = "";
}

public enum ResearchInputMethod
{
    Unknown = 0,
    MouseKeyboard = 1,
    Gamepad = 2
}

public enum ResearchWeaponClass
{
    Unknown = 0,
    Short = 1,
    Long = 2,
    Heavy = 3
}

public enum ResearchWeaponCatalogId
{
    Unknown = 0,
    Pistol = 1,
    AK = 2,
    DMR = 3,
    Shotgun = 4,
    Sniper = 5,
    Bazooka = 6
}

public enum ResearchFireMode
{
    Unknown = 0,
    Hitscan = 1,
    Projectile = 2
}

public enum ResearchMovementState
{
    Stationary = 0,
    Walking = 1,
    Sprinting = 2,
    Crouching = 3,
    Prone = 4,
    Jumping = 5,
    Falling = 6,
    Sliding = 7,
    DolphinDiving = 8,
    WallRunning = 9,
    Other = 10,
    Climbing = 11
}

public enum ResearchStance
{
    Standing = 0,
    Crouching = 1,
    Prone = 2,
    Other = 3
}

public enum ResearchBodyRegion
{
    Head = 0,
    UpperTorso = 1,
    LowerTorso = 2,
    LeftArm = 3,
    RightArm = 4,
    LeftLeg = 5,
    RightLeg = 6,
    Other = 7
}

public enum ResearchBullseyeState
{
    Attached = 0,
    Detached = 1,
    Returning = 2,
    TemporarilyUnavailable = 3
}

public enum ResearchEngagementEndReason
{
    TargetEliminated = 0,
    AttackerEliminated = 1,
    TargetLost = 2,
    TargetSwitched = 3,
    MatchEnded = 4,
    Timeout = 5
}

public enum ResearchInitialAimTargetPreference
{
    Indeterminate = 0,
    TowardBullseye = 1,
    TowardHead = 2,
    TowardCenterMass = 3,
    AwayFromTarget = 4
}

public enum ResearchBullseyeAssignmentReason
{
    Spawn = 0,
    Respawn = 1,
    NaturalCrawl = 2,
    ExperimentalRandomization = 3,
    ReturnedAfterDetach = 4
}

public enum ResearchTelemetryEventKind
{
    MatchStarted = 0,
    ExperimentalAssignment = 1,
    EngagementStarted = 2,
    AimTrajectorySample = 3,
    InitialAimSnapshot = 4,
    ShotFired = 5,
    ShotImpact = 6,
    Elimination = 7,
    EngagementEnded = 8,
    BullseyeAssignment = 9,
    MatchEnded = 10
}
