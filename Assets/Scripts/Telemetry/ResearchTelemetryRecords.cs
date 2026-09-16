using System;
using UnityEngine;

/// <summary>
/// Serializable research records written as separate JSONL streams.
/// These are raw behavioral primitives; derived analytics belong elsewhere.
/// </summary>
[Serializable]
public class ResearchVec3
{
    public float x;
    public float y;
    public float z;

    public static ResearchVec3 From(Vector3 value)
    {
        return new ResearchVec3 { x = value.x, y = value.y, z = value.z };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public class ResearchRecordHeader
{
    public string EventKind;
    public string TelemetrySchemaVersion = ResearchTelemetryConstants.SchemaVersion;
    public string GameVersion;
    public string GameplayConfigVersion = ResearchTelemetryConstants.GameplayConfigVersion;
    public string ResearchParticipantId;
    public string MatchId;
    public float Timestamp;
    public float MatchTime;
    public float MatchTimeMs;
    public string ExperimentId;
    public string ExperimentalConditionId;
}

[Serializable]
public class ResearchMatchRecord
{
    public ResearchRecordHeader Header = new();
    public bool ResearchConsentActive;
    public string InputMethodAtMatchStart;
}

[Serializable]
public class ResearchExperimentalAssignmentRecord
{
    public ResearchRecordHeader Header = new();
    public string ExperimentId;
    public string ExperimentalConditionId;
    public int AssignmentRandomSeed;
    public string AssignmentNote;
}

[Serializable]
public class ResearchEngagementStartedRecord
{
    public ResearchRecordHeader Header = new();
    public string EngagementId;
    public ulong AttackerPlayerId;
    public ulong TargetPlayerId;
    public string WeaponId;
    public string WeaponClass;
    public string WeaponCatalogId;
    public string FireMode;
    public string InputMethod;
    public ResearchVec3 AttackerPosition;
    public ResearchVec3 TargetPosition;
    public float DistanceToTarget;
    public string AttackerMovementState;
    public string TargetMovementState;
    public ResearchVec3 AttackerVelocity;
    public ResearchVec3 TargetVelocity;
    public bool IsAttackerGrounded;
    public bool IsTargetGrounded;
    public string AttackerStance;
    public string TargetStance;
    public bool AttackerIsADS;
    public float CurrentZoomLevel;
    public float SpreadBloom;
    public bool TargetBullseyeAttached;
    public string BullseyeState;
    public ResearchVec3 TargetBullseyeWorldPosition;
    public ResearchVec3 TargetBullseyeLocalPosition;
    public string TargetBullseyeBodyRegion;
    public ResearchVec3 TargetBullseyeSurfaceNormal;
    public float BullseyeDistanceFromCenterMass;
    public float BullseyeAngularDisplacementFromCenterMass;
    public float BullseyeAngularDisplacementFromHead;
    public ResearchVec3 CrosshairWorldDirection;
    public ResearchVec3 CrosshairScreenPosition;
    public float AngularErrorToBullseye;
    public float AngularErrorToHead;
    public float AngularErrorToCenterMass;
    public bool LineOfSightToTarget;
    public bool AimAssistEnabled;
    public float AimAssistStrength;
    public bool AimMagnetismActive;
    public bool ReticleFrictionActive;
    public float SensitivityHorizontal;
    public float SensitivityVertical;
    public float ADSModifier;
    public float DeadzoneSetting;
}

[Serializable]
public class ResearchAimSampleRecord
{
    public ResearchRecordHeader Header = new();
    public string EngagementId;
    public float MillisecondsSinceEngagementStart;
    public ResearchVec3 CrosshairWorldDirection;
    public ResearchVec3 CameraWorldPosition;
    public ResearchVec3 CameraForwardVector;
    public ResearchVec3 CrosshairScreenPosition;
    public float AngularErrorToBullseye;
    public float AngularErrorToHead;
    public float AngularErrorToCenterMass;
    public ResearchVec3 TargetBullseyeWorldPosition;
    public float DistanceToTarget;
    public ResearchVec3 AttackerVelocity;
    public ResearchVec3 TargetVelocity;
    public string AttackerMovementState;
    public string TargetMovementState;
    public bool IsADS;
    public float CurrentZoomLevel;
    public string WeaponId;
}

[Serializable]
public class ResearchInitialAimRecord
{
    public ResearchRecordHeader Header = new();
    public string EngagementId;
    public ResearchVec3 CrosshairAtEngagementStart;
    public ResearchVec3 FirstMeaningfulAimDirection;
    public ResearchVec3 CrosshairAt100ms;
    public ResearchVec3 CrosshairAt150ms;
    public ResearchVec3 CrosshairAt250ms;
    public float AngularDistanceMovedTowardBullseye;
    public float AngularDistanceMovedTowardHead;
    public float AngularDistanceMovedTowardCenterMass;
    public string InitialAimDirection;
    public string InitialAimTargetPreference;
    public float FirstAimMovementTimeMs;
}

[Serializable]
public class ResearchShotFiredRecord
{
    public ResearchRecordHeader Header = new();
    public string ShotId;
    public string EngagementId;
    public float MillisecondsSinceEngagementStart;
    public string WeaponId;
    public string WeaponClass;
    public string WeaponCatalogId;
    public string FireMode;
    public string InputMethod;
    public ResearchVec3 AttackerPosition;
    public ResearchVec3 TargetPosition;
    public float DistanceToTarget;
    public ResearchVec3 CrosshairWorldDirection;
    public ResearchVec3 CrosshairScreenPosition;
    public string AttackerMovementState;
    public string TargetMovementState;
    public ResearchVec3 AttackerVelocity;
    public ResearchVec3 TargetVelocity;
    public string AttackerStance;
    public string TargetStance;
    public bool IsADS;
    public float ZoomLevel;
    public float SpreadBloom;
    public ResearchVec3 BullseyeWorldPosition;
    public string BullseyeBodyRegion;
    public float AngularErrorToBullseyeAtTrigger;
    public float AngularErrorToHeadAtTrigger;
    public float AngularErrorToCenterMassAtTrigger;
    public float TimeSincePreviousShot;
    public int ShotNumberWithinEngagement;
    public bool IsFirstShotOfEngagement;
    public bool AimAssistEnabled;
    public float AimAssistStrength;
    public bool AimMagnetismActive;
    public bool ReticleFrictionActive;
    public float SensitivityHorizontal;
    public float SensitivityVertical;
    public float ADSModifier;
    public float DeadzoneSetting;
}

[Serializable]
public class ResearchShotImpactRecord
{
    public ResearchRecordHeader Header = new();
    public string ShotId;
    public string EngagementId;
    public float MillisecondsSinceEngagementStart;
    public ResearchVec3 ImpactWorldPosition;
    public bool HitPlayer;
    public bool HitTargetPlayer;
    public bool HitBullseye;
    public bool HitBody;
    public bool HitEnvironment;
    public bool MissedCompletely;
    public string HitBodyRegion;
    public float DamageDealt;
    public bool WasLethal;
    public bool TargetBullseyeAttached;
    public string BullseyeState;
    public float DistanceFromImpactToBullseye;
    public float DistanceFromImpactToCenterMass;
    public float DistanceFromImpactToHead;
    public float DistanceToTarget;
}

[Serializable]
public class ResearchEliminationRecord
{
    public ResearchRecordHeader Header = new();
    public string EngagementId;
    public string ShotId;
    public ulong AttackerPlayerId;
    public ulong TargetPlayerId;
    public string WeaponId;
    public string SourceType;
    public string BullseyeState;
    public float Distance;
    public ResearchVec3 BullseyeWorldPosition;
}

[Serializable]
public class ResearchEngagementEndedRecord
{
    public ResearchRecordHeader Header = new();
    public string EngagementId;
    public float DurationMs;
    public string EndReason;
    public int ShotsFired;
    public int Hits;
    public int BullseyeHits;
    public int BodyHits;
    public bool TargetEliminated;
    public bool AttackerEliminated;
    public float FinalDistance;
    public float TimeToFirstShotMs;
    public float TimeToFirstHitMs;
    public float TimeToFirstBullseyeHitMs;
    public float TimeToKillMs;
    public string WeaponUsedForElimination;
}

[Serializable]
public class ResearchBullseyeAssignmentRecord
{
    public ResearchRecordHeader Header = new();
    public string BullseyeAssignmentId;
    public ulong TargetPlayerId;
    public int BullseyeAssignmentRandomSeed;
    public string BullseyeAssignedBodyRegion;
    public ResearchVec3 BullseyeAssignedLocalPosition;
    public ResearchVec3 BullseyeActualLocalPosition;
    public string BullseyeAssignmentReason;
}

/// <summary>
/// In-memory QA summary for one completed engagement. Not a raw telemetry event.
/// </summary>
public sealed class ResearchEngagementQaSummary
{
    public string EngagementId;
    public ulong TargetPlayerId;
    public float Distance;
    public string BullseyeRegion;
    public float BullseyeDisplacementFromCenterMass;
    public float TimeToFirstShotMs;
    public float FirstShotError;
    public int Shots;
    public int Hits;
    public int BullseyeHits;
    public float TimeToKillMs;
    public string EndReason;
    public ResearchVec3 StartCrosshairDirection;
    public ResearchVec3 StartBullseyePosition;
    public int AimSampleCount;

    public string Format()
    {
        string ttk = TimeToKillMs > 0f ? $"{TimeToKillMs:0}ms" : "n/a";
        return
            $"Engagement: {EngagementId}\n" +
            $"Target: Player {TargetPlayerId}\n" +
            $"Distance: {Distance:0.0}m\n" +
            $"Bullseye Region: {BullseyeRegion}\n" +
            $"Bullseye Displacement from Center Mass: {BullseyeDisplacementFromCenterMass:0.0}°\n" +
            $"Time to First Shot: {TimeToFirstShotMs:0}ms\n" +
            $"First Shot Error: {FirstShotError:0.0}°\n" +
            $"Shots: {Shots}\n" +
            $"Hits: {Hits}\n" +
            $"Bullseye Hits: {BullseyeHits}\n" +
            $"Time to Kill: {ttk}\n" +
            $"End: {EndReason}\n" +
            $"Aim samples: {AimSampleCount}";
    }
}
