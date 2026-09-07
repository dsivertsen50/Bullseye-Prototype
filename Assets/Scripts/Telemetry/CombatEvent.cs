using UnityEngine;

/// <summary>
/// One authoritative combat outcome recorded for the current match.
/// Extra unused fields stay at their defaults so future consumers can
/// reconstruct a sequence from a single list.
/// </summary>
public sealed class CombatEvent
{
    public const string NoWeaponId = "";

    public CombatEventType Type;
    public float MatchTime;
    public ulong ActorClientId;
    public ulong TargetClientId;
    public EliminationSourceType SourceType;
    public string WeaponId;
    public BullseyeCombatState BullseyeState;
    public BullseyeDetachMethod DetachMethod;
    public float Distance;
    public Vector3 WorldPosition;
    public int Amount;

    public string Format()
    {
        string time = $"{MatchTime:0.00}s";
        string weapon = string.IsNullOrEmpty(WeaponId) ? "None" : WeaponId;
        switch (Type)
        {
            case CombatEventType.ShotFired:
                return $"{time} — Client {ActorClientId} fired {weapon}";
            case CombatEventType.BullseyeHit:
                return $"{time} — Client {ActorClientId} hit Client {TargetClientId} bullseye ({weapon}, {Distance:0.0}m)";
            case CombatEventType.BullseyeDamaged:
                return $"{time} — Client {ActorClientId} damaged Client {TargetClientId} for {Amount}";
            case CombatEventType.BullseyeDetached:
                return $"{time} — Client {ActorClientId} detached Client {TargetClientId} bullseye ({DetachMethod})";
            case CombatEventType.Elimination:
                return $"{time} — Client {ActorClientId} eliminated Client {TargetClientId} ({SourceType}, {weapon}, {BullseyeState}, {Distance:0.0}m)";
            case CombatEventType.AssistAwarded:
                return $"{time} — Client {ActorClientId} assist on Client {TargetClientId}";
            case CombatEventType.PlayerDeath:
                return $"{time} — Client {TargetClientId} died";
            default:
                return $"{time} — {Type}";
        }
    }
}
