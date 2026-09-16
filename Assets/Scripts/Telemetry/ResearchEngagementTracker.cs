using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local-owner engagement detector and aim sampler.
///
/// Primary target selection:
/// 1. Recent firing target, if still visible and inside an expanded cone.
/// 2. The current engagement target while it remains valid.
/// 3. Closest living visible opponent to the crosshair inside the acquisition cone.
/// 4. Only one high-frequency aim stream runs at a time.
///
/// Acquisition uses a minimum dwell time. Loss uses a grace timeout so brief
/// cone exits do not open a new engagement for the same fight.
/// </summary>
[DefaultExecutionOrder(200)]
public class ResearchEngagementTracker : MonoBehaviour
{
    [Header("Acquisition")]
    [SerializeField, Range(5f, 40f)] private float acquisitionConeDegrees = 15f;
    [SerializeField, Range(0.05f, 0.5f)] private float minAcquisitionDuration = 0.15f;
    [SerializeField, Range(0.25f, 3f)] private float lossTimeout = 1.5f;
    [SerializeField, Range(2f, 90f)] private float maxEngagementDuration = 30f;
    [SerializeField, Range(0.05f, 1f)] private float targetSwitchHold = 0.25f;
    [SerializeField, Range(0.1f, 1.5f)] private float reacquireGrace = 0.45f;
    [SerializeField, Range(10f, 400f)] private float maxLineOfSightDistance = 200f;

    [Header("Aim Sampling")]
    [SerializeField, Tooltip("Aim samples per second during an active engagement only.")]
    private float sampleRateHz = 30f;
    [SerializeField, Range(16f, 60f)] private float sampleRateMin = 20f;
    [SerializeField, Range(20f, 120f)] private float sampleRateMax = 60f;

    [Header("Initial Aim")]
    [SerializeField] private float meaningfulAimMoveDegrees = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug;

    private readonly List<PlayerHealth> playerCache = new(8);
    private readonly List<AimSampleState> qaSamples = new(256);

    private Camera playerCamera;
    private PlayerHealth localHealth;
    private PlayerMovement localMovement;
    private PlayerWeaponInventory inventory;
    private PlayerAimZoom aimZoom;
    private WeaponAccuracyController accuracy;
    private Rigidbody localBody;
    private NetworkObject networkObject;
    private InputAction lookAction;
    private InputAction fireAction;

    private float playerCacheRefreshAt;
    private ResearchInputMethod lastInputMethod = ResearchInputMethod.Unknown;

    private PlayerHealth candidate;
    private float candidateAcquiredAt = -1f;
    private PlayerHealth switchCandidate;
    private float switchCandidateSince = -1f;

    private bool engagementActive;
    private string engagementId;
    private PlayerHealth engagementTarget;
    private float engagementStartRealtime;
    private float engagementStartMatchMs;
    private float lastSeenTargetAt;
    private Vector3 startCrosshair;
    private Vector3 startBullseye;
    private ResearchBodyRegion startBullseyeRegion;
    private float startBullseyeDisplacement;
    private float startDistance;
    private float sampleAccumulator;
    private int aimSampleCount;
    private bool initialAimWritten;

    private Vector3 firstMeaningfulAimDirection;
    private float firstAimMovementTimeMs = -1f;
    private Vector3 crosshairAt100;
    private Vector3 crosshairAt150;
    private Vector3 crosshairAt250;
    private bool has100;
    private bool has150;
    private bool has250;

    private int shotsFired;
    private int hits;
    private int bullseyeHits;
    private int bodyHits;
    private float timeToFirstShotMs = -1f;
    private float timeToFirstHitMs = -1f;
    private float timeToFirstBullseyeHitMs = -1f;
    private float timeToKillMs = -1f;
    private float firstShotError = -1f;
    private string lastShotId = "";
    private string lastWeaponId = "";
    private float lastShotRealtime = -100f;
    private ulong recentFireTargetId = ulong.MaxValue;
    private float recentFireUntil;
    private string lastClosedTargetKey;
    private float lastClosedAt = -100f;

    public bool HasActiveEngagement => engagementActive;
    public string CurrentEngagementId => engagementId;
    public PlayerHealth CurrentTarget => engagementTarget;
    public bool IsSampling => engagementActive;
    public float CurrentAngularErrorToBullseye { get; private set; }
    public float CurrentAngularErrorToHead { get; private set; }
    public float CurrentAngularErrorToCenterMass { get; private set; }
    public string LastShotId => lastShotId;

    private void Awake()
    {
        localHealth = GetComponent<PlayerHealth>();
        localMovement = GetComponent<PlayerMovement>();
        inventory = GetComponent<PlayerWeaponInventory>();
        aimZoom = GetComponent<PlayerAimZoom>();
        accuracy = GetComponent<WeaponAccuracyController>();
        localBody = GetComponent<Rigidbody>();
        networkObject = GetComponent<NetworkObject>();
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
            playerCamera = PlayerNetworkSetup.LocalOwnedCamera;

        if (TryGetComponent(out LocalPlayerInputBinding binding) && binding.PlayerActions != null)
        {
            lookAction = binding.PlayerActions.FindAction("Look");
            fireAction = binding.PlayerActions.FindAction("Attack") ?? binding.PlayerActions.FindAction("Fire");
        }
    }

    private void OnDisable()
    {
        if (engagementActive)
            EndEngagement(ResearchEngagementEndReason.MatchEnded);
    }

    private void LateUpdate()
    {
        if (!IsLocalOwner())
            return;

        if (localHealth != null && localHealth.IsDead)
        {
            if (engagementActive)
                EndEngagement(ResearchEngagementEndReason.AttackerEliminated);
            return;
        }

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        RefreshPlayerCacheIfNeeded();
        lastInputMethod = ResearchTelemetrySnapshot.ResolveInputMethod(lookAction, fireAction, lastInputMethod);
        TickEngagement();

        if (engagementActive)
            TickSampling();
    }

    public string NotifyShotFired(string existingShotId = null)
    {
        ResearchTelemetryManager manager = ResearchTelemetryManager.Ensure();
        lastShotId = string.IsNullOrEmpty(existingShotId) ? manager.NextShotId() : existingShotId;
        lastWeaponId = inventory != null && inventory.ActiveDefinition != null
            ? inventory.ActiveDefinition.WeaponId
            : CombatEvent.NoWeaponId;

        if (engagementActive && engagementTarget != null)
            recentFireTargetId = engagementTarget.OwnerClientId;
        recentFireUntil = Time.realtimeSinceStartup + 0.75f;

        if (!engagementActive)
            TryForceEngagementFromShot();

        WriteShotFired(lastShotId);
        lastShotRealtime = Time.realtimeSinceStartup;
        return lastShotId;
    }

    public void NotifyHitscanImpact(
        Vector3 impactPoint,
        PlayerHealth hitHealth,
        bool hitBullseye,
        ResearchBodyRegion hitRegion,
        bool hitEnvironment,
        float damageDealt,
        bool wasLethal)
    {
        if (hitHealth != null)
        {
            recentFireTargetId = hitHealth.OwnerClientId;
            recentFireUntil = Time.realtimeSinceStartup + 0.75f;
            if (!engagementActive || engagementTarget != hitHealth)
                TryBeginOrSwitchTo(hitHealth, force: true);
        }

        WriteShotImpact(
            lastShotId,
            impactPoint,
            hitHealth,
            hitBullseye,
            hitRegion,
            hitEnvironment,
            damageDealt,
            wasLethal);
    }

    public void NotifyProjectileImpact(
        string shotId,
        Vector3 impactPoint,
        PlayerHealth hitHealth,
        bool hitBullseye,
        ResearchBodyRegion hitRegion,
        bool hitEnvironment,
        float damageDealt,
        bool wasLethal)
    {
        if (!string.IsNullOrEmpty(shotId))
            lastShotId = shotId;

        NotifyHitscanImpact(impactPoint, hitHealth, hitBullseye, hitRegion, hitEnvironment, damageDealt, wasLethal);
    }

    public string FormatDebugStatus()
    {
        string target = engagementTarget != null ? $"Player {engagementTarget.OwnerClientId}" : "none";
        return
            $"Engagement {(engagementActive ? engagementId : "idle")}  Target {target}\n" +
            $"Sampling {(engagementActive ? "ON" : "off")} @ {ResolvedSampleRate():0}Hz\n" +
            $"Error B:{CurrentAngularErrorToBullseye:0.0}° H:{CurrentAngularErrorToHead:0.0}° C:{CurrentAngularErrorToCenterMass:0.0}°\n" +
            $"Bullseye region {(engagementActive ? startBullseyeRegion : ResearchBodyRegion.Other)}\n" +
            $"Timer {(engagementActive ? (Time.realtimeSinceStartup - engagementStartRealtime) * 1000f : 0f):0}ms";
    }

    private void TickEngagement()
    {
        PlayerHealth preferred = SelectPrimaryTarget(out bool visibleInCone);
        if (engagementActive)
        {
            if (engagementTarget == null || engagementTarget.IsDead)
            {
                EndEngagement(ResearchEngagementEndReason.TargetEliminated);
                return;
            }

            if (Time.realtimeSinceStartup - engagementStartRealtime >= maxEngagementDuration)
            {
                EndEngagement(ResearchEngagementEndReason.Timeout);
                return;
            }

            bool stillValid = preferred == engagementTarget || IsTargetStillHeld(engagementTarget);
            if (stillValid)
            {
                lastSeenTargetAt = Time.realtimeSinceStartup;
                UpdateLiveErrors(engagementTarget);
                return;
            }

            if (preferred != null && preferred != engagementTarget && visibleInCone)
            {
                if (switchCandidate != preferred)
                {
                    switchCandidate = preferred;
                    switchCandidateSince = Time.realtimeSinceStartup;
                }

                if (Time.realtimeSinceStartup - switchCandidateSince >= targetSwitchHold)
                {
                    EndEngagement(ResearchEngagementEndReason.TargetSwitched);
                    BeginEngagement(preferred);
                }

                return;
            }

            switchCandidate = null;
            if (Time.realtimeSinceStartup - lastSeenTargetAt >= lossTimeout)
                EndEngagement(ResearchEngagementEndReason.TargetLost);
            else
                UpdateLiveErrors(engagementTarget);
            return;
        }

        if (preferred == null || !visibleInCone)
        {
            candidate = null;
            candidateAcquiredAt = -1f;
            return;
        }

        if (candidate != preferred)
        {
            candidate = preferred;
            candidateAcquiredAt = Time.realtimeSinceStartup;
        }

        if (Time.realtimeSinceStartup - candidateAcquiredAt >= minAcquisitionDuration)
            BeginEngagement(preferred);
    }

    private void TickSampling()
    {
        float interval = 1f / ResolvedSampleRate();
        sampleAccumulator += Time.unscaledDeltaTime;
        if (sampleAccumulator < interval)
        {
            CaptureInitialAimMarks();
            return;
        }

        sampleAccumulator -= interval;
        if (sampleAccumulator > interval)
            sampleAccumulator = 0f;

        WriteAimSample();
        CaptureInitialAimMarks();
        MaybeWriteInitialAim();
    }

    private PlayerHealth SelectPrimaryTarget(out bool inConeAndVisible)
    {
        inConeAndVisible = false;
        if (playerCamera == null)
            return null;

        Vector3 origin = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;
        PlayerHealth best = null;
        float bestAngle = float.MaxValue;

        for (int i = 0; i < playerCache.Count; i++)
        {
            PlayerHealth other = playerCache[i];
            if (!IsValidOpponent(other))
                continue;

            Vector3 aimPoint = ResearchTelemetrySnapshot.ResolveCenterMassPosition(other);
            float angle = Vector3.Angle(forward, aimPoint - origin);
            bool los = ResearchTelemetrySnapshot.HasLineOfSight(playerCamera, other, maxLineOfSightDistance);
            if (!los)
                continue;

            bool recentFire = Time.realtimeSinceStartup <= recentFireUntil &&
                              other.OwnerClientId == recentFireTargetId;
            float cone = recentFire ? acquisitionConeDegrees * 1.5f : acquisitionConeDegrees;
            if (angle > cone)
                continue;

            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = other;
            }
        }

        if (best != null)
            inConeAndVisible = true;

        if (engagementActive && engagementTarget != null && IsValidOpponent(engagementTarget))
        {
            Vector3 aimPoint = ResearchTelemetrySnapshot.ResolveCenterMassPosition(engagementTarget);
            float angle = Vector3.Angle(forward, aimPoint - origin);
            bool los = ResearchTelemetrySnapshot.HasLineOfSight(playerCamera, engagementTarget, maxLineOfSightDistance);
            if (los && angle <= acquisitionConeDegrees * 1.25f)
            {
                if (best == null || best == engagementTarget || bestAngle > 2f)
                    return engagementTarget;
            }
        }

        return best;
    }

    private bool IsTargetStillHeld(PlayerHealth target)
    {
        if (!IsValidOpponent(target) || playerCamera == null)
            return false;

        Vector3 origin = playerCamera.transform.position;
        Vector3 aimPoint = ResearchTelemetrySnapshot.ResolveCenterMassPosition(target);
        float angle = Vector3.Angle(playerCamera.transform.forward, aimPoint - origin);
        if (angle <= acquisitionConeDegrees * 1.35f &&
            ResearchTelemetrySnapshot.HasLineOfSight(playerCamera, target, maxLineOfSightDistance))
        {
            return true;
        }

        return false;
    }

    private void BeginEngagement(PlayerHealth target)
    {
        if (target == null)
            return;

        string key = TargetKey(target);
        if (key == lastClosedTargetKey && Time.realtimeSinceStartup - lastClosedAt < reacquireGrace)
            return;

        ResearchTelemetryManager manager = ResearchTelemetryManager.Ensure();
        engagementActive = true;
        engagementId = manager.NextEngagementId();
        engagementTarget = target;
        engagementStartRealtime = Time.realtimeSinceStartup;
        engagementStartMatchMs = manager.MatchTimeMs;
        lastSeenTargetAt = engagementStartRealtime;
        sampleAccumulator = 0f;
        aimSampleCount = 0;
        initialAimWritten = false;
        firstMeaningfulAimDirection = Vector3.zero;
        firstAimMovementTimeMs = -1f;
        has100 = has150 = has250 = false;
        shotsFired = 0;
        hits = 0;
        bullseyeHits = 0;
        bodyHits = 0;
        timeToFirstShotMs = -1f;
        timeToFirstHitMs = -1f;
        timeToFirstBullseyeHitMs = -1f;
        timeToKillMs = -1f;
        firstShotError = -1f;
        qaSamples.Clear();
        candidate = null;
        switchCandidate = null;

        PopulateLiveGeometry(
            target,
            out Vector3 cameraPos,
            out Vector3 forward,
            out Vector3 bullseyeWorld,
            out Vector3 bullseyeLocal,
            out Vector3 bullseyeNormal,
            out ResearchBodyRegion region,
            out ResearchBullseyeState bullseyeState,
            out bool attached,
            out Vector3 head,
            out Vector3 center,
            out float distance,
            out bool los);

        startCrosshair = forward;
        startBullseye = bullseyeWorld;
        startBullseyeRegion = region;
        startDistance = distance;
        startBullseyeDisplacement = Vector3.Angle(center - cameraPos, bullseyeWorld - cameraPos);
        UpdateLiveErrors(target);

        WeaponDefinition weapon = inventory != null ? inventory.ActiveDefinition : null;
        ResearchTelemetrySnapshot.FillAimAssist(
            lastInputMethod,
            out bool assistEnabled,
            out float assistStrength,
            out bool magnetism,
            out bool friction,
            out float sensX,
            out float sensY,
            out float adsModifier,
            out float deadzone);

        var record = new ResearchEngagementStartedRecord
        {
            EngagementId = engagementId,
            AttackerPlayerId = OwnerClientId(),
            TargetPlayerId = target.OwnerClientId,
            WeaponId = weapon != null ? weapon.WeaponId : CombatEvent.NoWeaponId,
            WeaponClass = ResearchTelemetrySnapshot.ResolveWeaponClass(weapon).ToString(),
            WeaponCatalogId = ResearchTelemetrySnapshot.ResolveWeaponCatalogId(weapon != null ? weapon.WeaponId : "").ToString(),
            FireMode = ResearchTelemetrySnapshot.ResolveFireMode(weapon).ToString(),
            InputMethod = lastInputMethod.ToString(),
            AttackerPosition = ResearchVec3.From(transform.position),
            TargetPosition = ResearchVec3.From(target.transform.position),
            DistanceToTarget = distance,
            AttackerMovementState = ResearchTelemetrySnapshot.ResolveMovementState(localMovement, localBody).ToString(),
            TargetMovementState = ResearchTelemetrySnapshot.ResolveMovementState(target.GetComponent<PlayerMovement>(), target.GetComponent<Rigidbody>()).ToString(),
            AttackerVelocity = ResearchVec3.From(ResearchTelemetrySnapshot.ResolveVelocity(localBody)),
            TargetVelocity = ResearchVec3.From(ResearchTelemetrySnapshot.ResolveVelocity(target.GetComponent<Rigidbody>())),
            IsAttackerGrounded = localMovement != null && localMovement.Grounded,
            IsTargetGrounded = target.TryGetComponent(out PlayerMovement targetMove) && targetMove.Grounded,
            AttackerStance = ResearchTelemetrySnapshot.ResolveStance(localMovement).ToString(),
            TargetStance = ResearchTelemetrySnapshot.ResolveStance(target.GetComponent<PlayerMovement>()).ToString(),
            AttackerIsADS = aimZoom != null && aimZoom.IsAiming,
            CurrentZoomLevel = ResearchTelemetrySnapshot.CurrentZoom(aimZoom, weapon),
            SpreadBloom = accuracy != null ? accuracy.CurrentSpread : 0f,
            TargetBullseyeAttached = attached,
            BullseyeState = bullseyeState.ToString(),
            TargetBullseyeWorldPosition = ResearchVec3.From(bullseyeWorld),
            TargetBullseyeLocalPosition = ResearchVec3.From(bullseyeLocal),
            TargetBullseyeBodyRegion = region.ToString(),
            TargetBullseyeSurfaceNormal = ResearchVec3.From(bullseyeNormal),
            BullseyeDistanceFromCenterMass = Vector3.Distance(bullseyeWorld, center),
            BullseyeAngularDisplacementFromCenterMass = startBullseyeDisplacement,
            BullseyeAngularDisplacementFromHead = Vector3.Angle(head - cameraPos, bullseyeWorld - cameraPos),
            CrosshairWorldDirection = ResearchVec3.From(forward),
            CrosshairScreenPosition = ResearchVec3.From(ResearchTelemetrySnapshot.ScreenCenter(playerCamera)),
            AngularErrorToBullseye = CurrentAngularErrorToBullseye,
            AngularErrorToHead = CurrentAngularErrorToHead,
            AngularErrorToCenterMass = CurrentAngularErrorToCenterMass,
            LineOfSightToTarget = los,
            AimAssistEnabled = assistEnabled,
            AimAssistStrength = assistStrength,
            AimMagnetismActive = magnetism,
            ReticleFrictionActive = friction,
            SensitivityHorizontal = sensX,
            SensitivityVertical = sensY,
            ADSModifier = adsModifier,
            DeadzoneSetting = deadzone
        };

        manager.WriteEngagementStarted(record);
        WriteAimSample();
    }

    private void EndEngagement(ResearchEngagementEndReason reason)
    {
        if (!engagementActive)
            return;

        MaybeWriteInitialAim(force: true);

        float durationMs = (Time.realtimeSinceStartup - engagementStartRealtime) * 1000f;
        bool targetDead = engagementTarget != null && engagementTarget.IsDead;
        bool attackerDead = localHealth != null && localHealth.IsDead;
        if (targetDead && timeToKillMs < 0f)
            timeToKillMs = durationMs;

        float finalDistance = startDistance;
        if (engagementTarget != null && playerCamera != null)
            finalDistance = Vector3.Distance(playerCamera.transform.position, engagementTarget.transform.position);

        var record = new ResearchEngagementEndedRecord
        {
            EngagementId = engagementId,
            DurationMs = durationMs,
            EndReason = reason.ToString(),
            ShotsFired = shotsFired,
            Hits = hits,
            BullseyeHits = bullseyeHits,
            BodyHits = bodyHits,
            TargetEliminated = targetDead || reason == ResearchEngagementEndReason.TargetEliminated,
            AttackerEliminated = attackerDead || reason == ResearchEngagementEndReason.AttackerEliminated,
            FinalDistance = finalDistance,
            TimeToFirstShotMs = timeToFirstShotMs,
            TimeToFirstHitMs = timeToFirstHitMs,
            TimeToFirstBullseyeHitMs = timeToFirstBullseyeHitMs,
            TimeToKillMs = timeToKillMs,
            WeaponUsedForElimination = timeToKillMs >= 0f ? lastWeaponId : ""
        };

        ResearchTelemetryManager manager = ResearchTelemetryManager.Ensure();
        manager.WriteEngagementEnded(record);
        manager.ReportCompletedEngagement(new ResearchEngagementQaSummary
        {
            EngagementId = engagementId,
            TargetPlayerId = engagementTarget != null ? engagementTarget.OwnerClientId : 0,
            Distance = startDistance,
            BullseyeRegion = startBullseyeRegion.ToString(),
            BullseyeDisplacementFromCenterMass = startBullseyeDisplacement,
            TimeToFirstShotMs = Mathf.Max(0f, timeToFirstShotMs),
            FirstShotError = Mathf.Max(0f, firstShotError),
            Shots = shotsFired,
            Hits = hits,
            BullseyeHits = bullseyeHits,
            TimeToKillMs = timeToKillMs,
            EndReason = reason.ToString(),
            StartCrosshairDirection = ResearchVec3.From(startCrosshair),
            StartBullseyePosition = ResearchVec3.From(startBullseye),
            AimSampleCount = aimSampleCount
        });

        lastClosedTargetKey = engagementTarget != null ? TargetKey(engagementTarget) : "";
        lastClosedAt = Time.realtimeSinceStartup;
        engagementActive = false;
        engagementId = null;
        engagementTarget = null;
        qaSamples.Clear();
    }

    private void WriteAimSample()
    {
        if (!engagementActive || engagementTarget == null || playerCamera == null)
            return;

        PopulateLiveGeometry(
            engagementTarget,
            out Vector3 cameraPos,
            out Vector3 forward,
            out Vector3 bullseyeWorld,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out float distance,
            out _);

        UpdateLiveErrors(engagementTarget);
        float elapsedMs = (Time.realtimeSinceStartup - engagementStartRealtime) * 1000f;
        WeaponDefinition weapon = inventory != null ? inventory.ActiveDefinition : null;

        var record = new ResearchAimSampleRecord
        {
            EngagementId = engagementId,
            MillisecondsSinceEngagementStart = elapsedMs,
            CrosshairWorldDirection = ResearchVec3.From(forward),
            CameraWorldPosition = ResearchVec3.From(cameraPos),
            CameraForwardVector = ResearchVec3.From(forward),
            CrosshairScreenPosition = ResearchVec3.From(ResearchTelemetrySnapshot.ScreenCenter(playerCamera)),
            AngularErrorToBullseye = CurrentAngularErrorToBullseye,
            AngularErrorToHead = CurrentAngularErrorToHead,
            AngularErrorToCenterMass = CurrentAngularErrorToCenterMass,
            TargetBullseyeWorldPosition = ResearchVec3.From(bullseyeWorld),
            DistanceToTarget = distance,
            AttackerVelocity = ResearchVec3.From(ResearchTelemetrySnapshot.ResolveVelocity(localBody)),
            TargetVelocity = ResearchVec3.From(ResearchTelemetrySnapshot.ResolveVelocity(engagementTarget.GetComponent<Rigidbody>())),
            AttackerMovementState = ResearchTelemetrySnapshot.ResolveMovementState(localMovement, localBody).ToString(),
            TargetMovementState = ResearchTelemetrySnapshot.ResolveMovementState(engagementTarget.GetComponent<PlayerMovement>(), engagementTarget.GetComponent<Rigidbody>()).ToString(),
            IsADS = aimZoom != null && aimZoom.IsAiming,
            CurrentZoomLevel = ResearchTelemetrySnapshot.CurrentZoom(aimZoom, weapon),
            WeaponId = weapon != null ? weapon.WeaponId : CombatEvent.NoWeaponId
        };

        ResearchTelemetryManager.Ensure().WriteAimSample(record);
        aimSampleCount++;
        if (qaSamples.Count < 512)
        {
            qaSamples.Add(new AimSampleState
            {
                ElapsedMs = elapsedMs,
                Direction = forward
            });
        }
    }

    private void WriteShotFired(string shotId)
    {
        PlayerHealth target = engagementTarget;
        if (target == null && recentFireTargetId != ulong.MaxValue)
            target = FindCachedPlayer(recentFireTargetId);

        PopulateLiveGeometry(
            target,
            out Vector3 cameraPos,
            out Vector3 forward,
            out Vector3 bullseyeWorld,
            out _,
            out _,
            out ResearchBodyRegion region,
            out _,
            out _,
            out Vector3 head,
            out Vector3 center,
            out float distance,
            out _);

        if (target != null)
            UpdateLiveErrors(target);

        float elapsedMs = engagementActive
            ? (Time.realtimeSinceStartup - engagementStartRealtime) * 1000f
            : 0f;
        shotsFired++;
        if (timeToFirstShotMs < 0f)
        {
            timeToFirstShotMs = elapsedMs;
            firstShotError = CurrentAngularErrorToBullseye;
        }

        WeaponDefinition weapon = inventory != null ? inventory.ActiveDefinition : null;
        ResearchTelemetrySnapshot.FillAimAssist(
            lastInputMethod,
            out bool assistEnabled,
            out float assistStrength,
            out bool magnetism,
            out bool friction,
            out float sensX,
            out float sensY,
            out float adsModifier,
            out float deadzone);

        float timeSincePrevious = lastShotRealtime > -10f
            ? (Time.realtimeSinceStartup - lastShotRealtime) * 1000f
            : -1f;

        var record = new ResearchShotFiredRecord
        {
            ShotId = shotId,
            EngagementId = engagementId ?? "",
            MillisecondsSinceEngagementStart = elapsedMs,
            WeaponId = weapon != null ? weapon.WeaponId : CombatEvent.NoWeaponId,
            WeaponClass = ResearchTelemetrySnapshot.ResolveWeaponClass(weapon).ToString(),
            WeaponCatalogId = ResearchTelemetrySnapshot.ResolveWeaponCatalogId(weapon != null ? weapon.WeaponId : "").ToString(),
            FireMode = ResearchTelemetrySnapshot.ResolveFireMode(weapon).ToString(),
            InputMethod = lastInputMethod.ToString(),
            AttackerPosition = ResearchVec3.From(transform.position),
            TargetPosition = ResearchVec3.From(target != null ? target.transform.position : Vector3.zero),
            DistanceToTarget = distance,
            CrosshairWorldDirection = ResearchVec3.From(forward),
            CrosshairScreenPosition = ResearchVec3.From(ResearchTelemetrySnapshot.ScreenCenter(playerCamera)),
            AttackerMovementState = ResearchTelemetrySnapshot.ResolveMovementState(localMovement, localBody).ToString(),
            TargetMovementState = target != null
                ? ResearchTelemetrySnapshot.ResolveMovementState(target.GetComponent<PlayerMovement>(), target.GetComponent<Rigidbody>()).ToString()
                : ResearchMovementState.Other.ToString(),
            AttackerVelocity = ResearchVec3.From(ResearchTelemetrySnapshot.ResolveVelocity(localBody)),
            TargetVelocity = ResearchVec3.From(target != null ? ResearchTelemetrySnapshot.ResolveVelocity(target.GetComponent<Rigidbody>()) : Vector3.zero),
            AttackerStance = ResearchTelemetrySnapshot.ResolveStance(localMovement).ToString(),
            TargetStance = ResearchTelemetrySnapshot.ResolveStance(target != null ? target.GetComponent<PlayerMovement>() : null).ToString(),
            IsADS = aimZoom != null && aimZoom.IsAiming,
            ZoomLevel = ResearchTelemetrySnapshot.CurrentZoom(aimZoom, weapon),
            SpreadBloom = accuracy != null ? accuracy.CurrentSpread : 0f,
            BullseyeWorldPosition = ResearchVec3.From(bullseyeWorld),
            BullseyeBodyRegion = region.ToString(),
            AngularErrorToBullseyeAtTrigger = CurrentAngularErrorToBullseye,
            AngularErrorToHeadAtTrigger = CurrentAngularErrorToHead,
            AngularErrorToCenterMassAtTrigger = CurrentAngularErrorToCenterMass,
            TimeSincePreviousShot = timeSincePrevious,
            ShotNumberWithinEngagement = shotsFired,
            IsFirstShotOfEngagement = shotsFired == 1,
            AimAssistEnabled = assistEnabled,
            AimAssistStrength = assistStrength,
            AimMagnetismActive = magnetism,
            ReticleFrictionActive = friction,
            SensitivityHorizontal = sensX,
            SensitivityVertical = sensY,
            ADSModifier = adsModifier,
            DeadzoneSetting = deadzone
        };

        ResearchTelemetryManager.Ensure().WriteShotFired(record);
        _ = cameraPos;
        _ = head;
        _ = center;
    }

    private void WriteShotImpact(
        string shotId,
        Vector3 impactPoint,
        PlayerHealth hitHealth,
        bool hitBullseye,
        ResearchBodyRegion hitRegion,
        bool hitEnvironment,
        float damageDealt,
        bool wasLethal)
    {
        bool hitPlayer = hitHealth != null;
        bool hitTarget = engagementTarget != null && hitHealth == engagementTarget;
        bool hitBody = hitPlayer && !hitBullseye;
        bool missed = !hitPlayer && !hitEnvironment;

        if (hitPlayer)
        {
            hits++;
            if (timeToFirstHitMs < 0f)
                timeToFirstHitMs = ElapsedMs();
        }

        if (hitBullseye)
        {
            bullseyeHits++;
            if (timeToFirstBullseyeHitMs < 0f)
                timeToFirstBullseyeHitMs = ElapsedMs();
        }

        if (hitBody)
            bodyHits++;

        if (wasLethal && timeToKillMs < 0f)
            timeToKillMs = ElapsedMs();

        PlayerHealth geometryTarget = hitHealth != null ? hitHealth : engagementTarget;
        Vector3 bullseye = startBullseye;
        Vector3 head = geometryTarget != null ? ResearchTelemetrySnapshot.ResolveHeadPosition(geometryTarget) : Vector3.zero;
        Vector3 center = geometryTarget != null ? ResearchTelemetrySnapshot.ResolveCenterMassPosition(geometryTarget) : Vector3.zero;
        ResearchBullseyeState state = ResearchBullseyeState.TemporarilyUnavailable;
        bool attached = false;
        if (geometryTarget != null &&
            ResearchTelemetrySnapshot.TryGetBullseyePose(geometryTarget, out bullseye, out _, out _, out _, out state, out attached))
        {
        }

        float distance = geometryTarget != null
            ? Vector3.Distance(transform.position, geometryTarget.transform.position)
            : 0f;

        var record = new ResearchShotImpactRecord
        {
            ShotId = shotId ?? lastShotId,
            EngagementId = engagementId ?? "",
            MillisecondsSinceEngagementStart = ElapsedMs(),
            ImpactWorldPosition = ResearchVec3.From(impactPoint),
            HitPlayer = hitPlayer,
            HitTargetPlayer = hitTarget,
            HitBullseye = hitBullseye,
            HitBody = hitBody,
            HitEnvironment = hitEnvironment && !hitPlayer,
            MissedCompletely = missed,
            HitBodyRegion = hitPlayer ? hitRegion.ToString() : "",
            DamageDealt = damageDealt,
            WasLethal = wasLethal,
            TargetBullseyeAttached = attached,
            BullseyeState = state.ToString(),
            DistanceFromImpactToBullseye = Vector3.Distance(impactPoint, bullseye),
            DistanceFromImpactToCenterMass = center.sqrMagnitude > 0f ? Vector3.Distance(impactPoint, center) : -1f,
            DistanceFromImpactToHead = head.sqrMagnitude > 0f ? Vector3.Distance(impactPoint, head) : -1f,
            DistanceToTarget = distance
        };

        ResearchTelemetryManager.Ensure().WriteShotImpact(record);

        if (wasLethal && engagementActive && hitTarget)
            EndEngagement(ResearchEngagementEndReason.TargetEliminated);
    }

    private void CaptureInitialAimMarks()
    {
        if (!engagementActive || playerCamera == null)
            return;

        Vector3 forward = playerCamera.transform.forward;
        float elapsedMs = ElapsedMs();

        if (firstAimMovementTimeMs < 0f && Vector3.Angle(startCrosshair, forward) >= meaningfulAimMoveDegrees)
        {
            firstAimMovementTimeMs = elapsedMs;
            firstMeaningfulAimDirection = forward;
        }

        if (!has100 && elapsedMs >= 100f)
        {
            crosshairAt100 = forward;
            has100 = true;
        }

        if (!has150 && elapsedMs >= 150f)
        {
            crosshairAt150 = forward;
            has150 = true;
        }

        if (!has250 && elapsedMs >= 250f)
        {
            crosshairAt250 = forward;
            has250 = true;
        }
    }

    private void MaybeWriteInitialAim(bool force = false)
    {
        if (initialAimWritten || !engagementActive)
            return;
        if (!force && !has250 && timeToFirstShotMs < 0f)
            return;

        Vector3 evaluate = has250 ? crosshairAt250 : (has150 ? crosshairAt150 : (has100 ? crosshairAt100 : (playerCamera != null ? playerCamera.transform.forward : startCrosshair)));
        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        float startToBullseye = Vector3.Angle(startCrosshair, startBullseye - origin);
        float nowToBullseye = Vector3.Angle(evaluate, startBullseye - origin);
        Vector3 head = engagementTarget != null ? ResearchTelemetrySnapshot.ResolveHeadPosition(engagementTarget) : origin + startCrosshair;
        Vector3 center = engagementTarget != null ? ResearchTelemetrySnapshot.ResolveCenterMassPosition(engagementTarget) : origin + startCrosshair;
        float startToHead = Vector3.Angle(startCrosshair, head - origin);
        float nowToHead = Vector3.Angle(evaluate, head - origin);
        float startToCenter = Vector3.Angle(startCrosshair, center - origin);
        float nowToCenter = Vector3.Angle(evaluate, center - origin);

        float towardBullseye = startToBullseye - nowToBullseye;
        float towardHead = startToHead - nowToHead;
        float towardCenter = startToCenter - nowToCenter;

        ResearchInitialAimTargetPreference preference = ResearchInitialAimTargetPreference.Indeterminate;
        if (firstAimMovementTimeMs >= 0f)
        {
            float best = Mathf.Max(towardBullseye, Mathf.Max(towardHead, towardCenter));
            if (best < 0.15f && towardBullseye <= 0f && towardHead <= 0f && towardCenter <= 0f)
                preference = ResearchInitialAimTargetPreference.AwayFromTarget;
            else if (best == towardBullseye)
                preference = ResearchInitialAimTargetPreference.TowardBullseye;
            else if (best == towardHead)
                preference = ResearchInitialAimTargetPreference.TowardHead;
            else
                preference = ResearchInitialAimTargetPreference.TowardCenterMass;
        }

        var record = new ResearchInitialAimRecord
        {
            EngagementId = engagementId,
            CrosshairAtEngagementStart = ResearchVec3.From(startCrosshair),
            FirstMeaningfulAimDirection = ResearchVec3.From(firstMeaningfulAimDirection),
            CrosshairAt100ms = ResearchVec3.From(has100 ? crosshairAt100 : Vector3.zero),
            CrosshairAt150ms = ResearchVec3.From(has150 ? crosshairAt150 : Vector3.zero),
            CrosshairAt250ms = ResearchVec3.From(has250 ? crosshairAt250 : Vector3.zero),
            AngularDistanceMovedTowardBullseye = towardBullseye,
            AngularDistanceMovedTowardHead = towardHead,
            AngularDistanceMovedTowardCenterMass = towardCenter,
            InitialAimDirection = firstMeaningfulAimDirection.sqrMagnitude > 0f
                ? firstMeaningfulAimDirection.ToString("F3")
                : "none",
            InitialAimTargetPreference = preference.ToString(),
            FirstAimMovementTimeMs = firstAimMovementTimeMs
        };

        ResearchTelemetryManager.Ensure().WriteInitialAim(record);
        initialAimWritten = true;
    }

    private void TryForceEngagementFromShot()
    {
        PlayerHealth preferred = SelectPrimaryTarget(out bool visible);
        if (preferred == null && recentFireTargetId != ulong.MaxValue)
            preferred = FindCachedPlayer(recentFireTargetId);
        if (preferred != null)
            BeginEngagement(preferred);
        _ = visible;
    }

    private void TryBeginOrSwitchTo(PlayerHealth target, bool force)
    {
        if (target == null)
            return;
        if (engagementActive && engagementTarget == target)
            return;
        if (engagementActive)
            EndEngagement(ResearchEngagementEndReason.TargetSwitched);
        if (force)
            lastClosedTargetKey = null;
        BeginEngagement(target);
    }

    private void PopulateLiveGeometry(
        PlayerHealth target,
        out Vector3 cameraPos,
        out Vector3 forward,
        out Vector3 bullseyeWorld,
        out Vector3 bullseyeLocal,
        out Vector3 bullseyeNormal,
        out ResearchBodyRegion region,
        out ResearchBullseyeState state,
        out bool attached,
        out Vector3 head,
        out Vector3 center,
        out float distance,
        out bool los)
    {
        cameraPos = playerCamera != null ? playerCamera.transform.position : transform.position;
        forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        bullseyeWorld = target != null ? target.transform.position : cameraPos + forward;
        bullseyeLocal = Vector3.zero;
        bullseyeNormal = Vector3.forward;
        region = ResearchBodyRegion.Other;
        state = ResearchBullseyeState.TemporarilyUnavailable;
        attached = false;
        head = bullseyeWorld;
        center = bullseyeWorld;
        distance = 0f;
        los = false;

        if (target == null)
            return;

        ResearchTelemetrySnapshot.TryGetBullseyePose(
            target,
            out bullseyeWorld,
            out bullseyeLocal,
            out bullseyeNormal,
            out region,
            out state,
            out attached);
        head = ResearchTelemetrySnapshot.ResolveHeadPosition(target);
        center = ResearchTelemetrySnapshot.ResolveCenterMassPosition(target);
        distance = Vector3.Distance(cameraPos, target.transform.position);
        los = ResearchTelemetrySnapshot.HasLineOfSight(playerCamera, target, maxLineOfSightDistance);
    }

    private void UpdateLiveErrors(PlayerHealth target)
    {
        if (playerCamera == null || target == null)
            return;

        Vector3 origin = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;
        ResearchTelemetrySnapshot.TryGetBullseyePose(target, out Vector3 bullseye, out _, out _, out _, out _, out _);
        Vector3 head = ResearchTelemetrySnapshot.ResolveHeadPosition(target);
        Vector3 center = ResearchTelemetrySnapshot.ResolveCenterMassPosition(target);
        CurrentAngularErrorToBullseye = ResearchTelemetrySnapshot.AngleBetween(forward, bullseye, origin);
        CurrentAngularErrorToHead = ResearchTelemetrySnapshot.AngleBetween(forward, head, origin);
        CurrentAngularErrorToCenterMass = ResearchTelemetrySnapshot.AngleBetween(forward, center, origin);

        if (drawDebug)
        {
            Debug.DrawLine(origin, bullseye, Color.red, 0f, false);
            Debug.DrawLine(origin, head, Color.cyan, 0f, false);
            Debug.DrawLine(origin, center, Color.yellow, 0f, false);
        }
    }

    private void RefreshPlayerCacheIfNeeded()
    {
        if (Time.unscaledTime < playerCacheRefreshAt)
            return;

        playerCacheRefreshAt = Time.unscaledTime + 0.25f;
        playerCache.Clear();
        PlayerHealth[] found = FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                playerCache.Add(found[i]);
        }
    }

    private PlayerHealth FindCachedPlayer(ulong clientId)
    {
        for (int i = 0; i < playerCache.Count; i++)
        {
            if (playerCache[i] != null && playerCache[i].OwnerClientId == clientId)
                return playerCache[i];
        }

        return null;
    }

    private bool IsValidOpponent(PlayerHealth other)
    {
        if (other == null || other == localHealth || other.IsDead)
            return false;
        if (networkObject != null && other.TryGetComponent(out NetworkObject otherNet) &&
            otherNet.OwnerClientId == networkObject.OwnerClientId)
            return false;
        return true;
    }

    private bool IsLocalOwner()
    {
        if (networkObject != null && networkObject.IsSpawned)
            return networkObject.IsOwner;
        return playerCamera != null && playerCamera == PlayerNetworkSetup.LocalOwnedCamera;
    }

    private ulong OwnerClientId()
    {
        return networkObject != null ? networkObject.OwnerClientId : 0;
    }

    private float ElapsedMs()
    {
        return engagementActive ? (Time.realtimeSinceStartup - engagementStartRealtime) * 1000f : 0f;
    }

    private float ResolvedSampleRate()
    {
        return Mathf.Clamp(sampleRateHz, sampleRateMin, sampleRateMax);
    }

    private static string TargetKey(PlayerHealth target)
    {
        return target != null ? target.OwnerClientId.ToString() : "";
    }

    private void OnValidate()
    {
        acquisitionConeDegrees = Mathf.Clamp(acquisitionConeDegrees, 5f, 40f);
        minAcquisitionDuration = Mathf.Clamp(minAcquisitionDuration, 0.05f, 0.5f);
        lossTimeout = Mathf.Clamp(lossTimeout, 0.25f, 3f);
        maxEngagementDuration = Mathf.Clamp(maxEngagementDuration, 2f, 90f);
        sampleRateHz = Mathf.Clamp(sampleRateHz, 20f, 60f);
        sampleRateMin = Mathf.Clamp(sampleRateMin, 16f, 60f);
        sampleRateMax = Mathf.Max(sampleRateMin, sampleRateMax);
    }

    private struct AimSampleState
    {
        public float ElapsedMs;
        public Vector3 Direction;
    }
}
