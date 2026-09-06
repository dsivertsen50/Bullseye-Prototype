using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Detects a dolphin-dive landing on a prone enemy and asks the server to
/// apply an authoritative body-slam kill through PlayerHealth.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class BodySlamDetector : NetworkBehaviour
{
    private const float LandingGraceSeconds = 0.3f;
    private const float MaxSnapshotDrift = 1.75f;

    [Header("Body Slam")]
    [SerializeField] private bool bodySlamEnabled = true;
    [SerializeField, Tooltip("Minimum downward speed in m/s required at impact.")]
    private float bodySlamMinimumDownwardVelocity = 2.25f;
    [SerializeField, Tooltip("Max XZ distance from the prone player's body center.")]
    private float bodySlamHorizontalTolerance = 0.5f;
    [SerializeField, Tooltip("Attacker must be above the victim, but no farther than this.")]
    private float bodySlamMaximumVerticalSeparation = 1.35f;

    [Header("Debug")]
    [SerializeField, Tooltip("Editor gizmos and console traces for slam detection. Off in play builds.")]
    private bool showBodySlamDebug;

    private readonly NetworkVariable<int> diveSequence = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly Collider[] overlapHits = new Collider[16];

    private PlayerMovement movement;
    private PlayerHealth health;
    private Rigidbody body;
    private CapsuleCollider capsule;

    private bool ownerResolvedThisDive;
    private int serverResolvedDiveId = -1;
    private int serverActiveDiveId;
    private bool serverWasDiving;
    private double serverDiveEndedAt = -1d;
    private float cachedDownwardSpeed;
    private Vector3 lastProbeCenter;
    private Vector3 lastVictimCenter;
    private bool lastProbeHadVictim;
    private bool lastVictimWasProne;
    private string lastRejectReason = string.Empty;

    public override void OnNetworkSpawn()
    {
        CacheReferences();
        BindMovementEvents();

        if (IsServer)
        {
            serverWasDiving = movement != null && movement.IsDolphinDiving;
            serverDiveEndedAt = -1d;
        }
    }

    public override void OnNetworkDespawn()
    {
        UnbindMovementEvents();
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        BindMovementEvents();
    }

    private void OnDisable()
    {
        UnbindMovementEvents();
    }

    private void CacheReferences()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
        if (health == null)
            health = GetComponent<PlayerHealth>();
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (capsule == null)
            capsule = GetComponent<CapsuleCollider>();
    }

    private void BindMovementEvents()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();
        if (movement == null)
            return;

        movement.DolphinDiveStarted -= OnOwnerDiveStarted;
        movement.DolphinDiveLanded -= OnOwnerDiveLanded;
        movement.DolphinDiveStarted += OnOwnerDiveStarted;
        movement.DolphinDiveLanded += OnOwnerDiveLanded;
    }

    private void UnbindMovementEvents()
    {
        if (movement == null)
            return;

        movement.DolphinDiveStarted -= OnOwnerDiveStarted;
        movement.DolphinDiveLanded -= OnOwnerDiveLanded;
    }

    private void Update()
    {
        if (IsServer)
            TickServerDiveWindow();
    }

    private void FixedUpdate()
    {
        if (!bodySlamEnabled || movement == null)
            return;

        if (IsOwner)
            SampleOwnerDiveVelocity();

        if (!IsOwner || ownerResolvedThisDive || !movement.IsDolphinDiving)
            return;

        if (cachedDownwardSpeed < bodySlamMinimumDownwardVelocity)
            return;

        TryDetectAndRequest("airborne");
    }

    private void TickServerDiveWindow()
    {
        if (movement == null)
            return;

        bool diving = movement.IsDolphinDiving;
        if (diving && diveSequence.Value > 0)
            serverActiveDiveId = diveSequence.Value;

        if (serverWasDiving && !diving && NetworkManager != null)
            serverDiveEndedAt = NetworkManager.ServerTime.Time;

        serverWasDiving = diving;
    }

    private void SampleOwnerDiveVelocity()
    {
        if (!movement.IsDolphinDiving)
            return;

        if (body == null || body.isKinematic)
            return;

        cachedDownwardSpeed = Mathf.Max(0f, -body.linearVelocity.y);
    }

    private void OnOwnerDiveStarted()
    {
        if (!IsOwner || !IsSpawned)
            return;

        ownerResolvedThisDive = false;
        cachedDownwardSpeed = 0f;
        lastRejectReason = string.Empty;
        lastProbeHadVictim = false;
        diveSequence.Value = diveSequence.Value + 1;
    }

    private void OnOwnerDiveLanded()
    {
        if (!IsOwner || ownerResolvedThisDive)
            return;

        TryDetectAndRequest("landing");
    }

    private void TryDetectAndRequest(string phase)
    {
        if (!bodySlamEnabled || (health != null && health.IsDead))
            return;

        if (!TryFindValidVictim(out PlayerHealth victim, out Vector3 attackerCenter, out Vector3 victimCenter))
        {
            LogDebug($"Body slam {phase} rejected: {lastRejectReason}");
            return;
        }

        ownerResolvedThisDive = true;
        LogDebug($"Body slam {phase} requested on {victim.name}.");
        RequestBodySlamServerRpc(
            victim.NetworkObjectId,
            cachedDownwardSpeed,
            attackerCenter,
            victimCenter,
            diveSequence.Value);
    }

    private bool TryFindValidVictim(
        out PlayerHealth victim,
        out Vector3 attackerCenter,
        out Vector3 victimCenter)
    {
        victim = null;
        attackerCenter = ResolveCenter(transform, capsule);
        victimCenter = default;
        lastProbeCenter = attackerCenter;
        lastProbeHadVictim = false;
        lastVictimWasProne = false;
        lastRejectReason = "no_target";

        float probeRadius = bodySlamHorizontalTolerance + 0.15f;
        Vector3 probeBottom = attackerCenter - Vector3.up * Mathf.Max(0.2f, bodySlamMaximumVerticalSeparation);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            attackerCenter,
            probeBottom,
            probeRadius,
            overlapHits,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestHorizontal = float.MaxValue;
        PlayerHealth bestVictim = null;
        Vector3 bestVictimCenter = default;
        string bestReject = lastRejectReason;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapHits[i];
            if (hit == null || IsOwnCollider(hit))
                continue;

            PlayerHealth candidate = hit.GetComponentInParent<PlayerHealth>();
            if (candidate == null || candidate == health || !candidate.IsSpawned)
                continue;

            PlayerMovement otherMovement = candidate.GetComponent<PlayerMovement>();
            if (otherMovement == null)
                continue;

            lastProbeHadVictim = true;
            lastVictimWasProne = otherMovement.IsProne;
            Vector3 otherCenter = ResolveCenter(candidate.transform, candidate.GetComponent<CapsuleCollider>());

            if (candidate.IsDead || candidate.CurrentHealth <= 0)
            {
                bestReject = "victim_dead";
                continue;
            }

            if (!otherMovement.IsProne)
            {
                bestReject = "victim_not_prone";
                continue;
            }

            if (!BodySlamValidation.TryValidateLanding(
                    attackerCenter,
                    otherCenter,
                    cachedDownwardSpeed,
                    bodySlamMinimumDownwardVelocity,
                    bodySlamHorizontalTolerance,
                    bodySlamMaximumVerticalSeparation,
                    out string reject))
            {
                bestReject = reject;
                continue;
            }

            float horizontal = Vector3.Distance(
                BodySlamValidation.Flatten(attackerCenter),
                BodySlamValidation.Flatten(otherCenter));
            if (horizontal >= bestHorizontal)
                continue;

            bestHorizontal = horizontal;
            bestVictim = candidate;
            bestVictimCenter = otherCenter;
            bestReject = BodySlamValidation.Ok;
        }

        lastRejectReason = bestReject;
        lastVictimCenter = bestVictim != null ? bestVictimCenter : lastVictimCenter;
        victim = bestVictim;
        victimCenter = bestVictimCenter;
        return victim != null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestBodySlamServerRpc(
        ulong victimNetworkObjectId,
        float reportedDownwardSpeed,
        Vector3 reportedAttackerCenter,
        Vector3 reportedVictimCenter,
        int reportedDiveSequence,
        RpcParams rpcParams = default)
    {
        if (!bodySlamEnabled)
            return;

        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (health != null && health.IsDead)
            return;

        if (!IsAttackerDiveEligible(reportedDiveSequence))
        {
            LogDebug($"Body slam server rejected: dive window closed (seq {reportedDiveSequence}).");
            return;
        }

        if (reportedDiveSequence == serverResolvedDiveId)
        {
            LogDebug("Body slam server rejected: already resolved this dive.");
            return;
        }

        if (!TryGetSpawnedPlayer(victimNetworkObjectId, out PlayerHealth victim, out PlayerMovement victimMovement))
        {
            LogDebug("Body slam server rejected: victim missing.");
            return;
        }

        if (victim.OwnerClientId == OwnerClientId)
            return;

        if (victim.IsDead || victim.CurrentHealth <= 0)
        {
            LogDebug("Body slam server rejected: victim already dead.");
            return;
        }

        if (!victimMovement.IsProne)
        {
            LogDebug("Body slam server rejected: victim not prone.");
            return;
        }

        Vector3 liveAttackerCenter = ResolveCenter(transform, capsule);
        Vector3 liveVictimCenter = ResolveCenter(victim.transform, victim.GetComponent<CapsuleCollider>());

        if (Vector3.Distance(reportedAttackerCenter, liveAttackerCenter) > MaxSnapshotDrift ||
            Vector3.Distance(reportedVictimCenter, liveVictimCenter) > MaxSnapshotDrift)
        {
            LogDebug("Body slam server rejected: snapshot drifted from live poses.");
            return;
        }

        float downwardSpeed = ResolveAuthoritativeDownwardSpeed(reportedDownwardSpeed);
        if (!BodySlamValidation.TryValidateLanding(
                reportedAttackerCenter,
                reportedVictimCenter,
                downwardSpeed,
                bodySlamMinimumDownwardVelocity,
                bodySlamHorizontalTolerance,
                bodySlamMaximumVerticalSeparation,
                out string reject))
        {
            LogDebug($"Body slam server rejected: {reject}.");
            return;
        }

        serverResolvedDiveId = reportedDiveSequence;
        int lethal = victim.MaxHealth;
        victim.ApplyDamage(DamageContext.FromBodySlam(OwnerClientId, victim.OwnerClientId, lethal));
        LogDebug($"Body slam applied. Attacker {OwnerClientId} -> victim {victim.OwnerClientId}.");
    }

    private bool IsAttackerDiveEligible(int reportedDiveSequence)
    {
        if (movement == null || reportedDiveSequence <= 0)
            return false;

        if (diveSequence.Value != reportedDiveSequence)
            return false;

        if (movement.IsDolphinDiving)
            return true;

        if (serverActiveDiveId != reportedDiveSequence)
            return false;

        if (serverWasDiving)
            return true;

        if (NetworkManager == null || serverDiveEndedAt < 0d)
            return false;

        return NetworkManager.ServerTime.Time - serverDiveEndedAt <= LandingGraceSeconds;
    }

    private float ResolveAuthoritativeDownwardSpeed(float reportedDownwardSpeed)
    {
        if (body != null && !body.isKinematic)
        {
            float live = Mathf.Max(0f, -body.linearVelocity.y);
            if (live >= bodySlamMinimumDownwardVelocity)
                return live;

            // Landing already zeroed owner velocity; accept the reported
            // pre-impact sample when it is still in a plausible dive range.
            if (movement != null && !movement.IsDolphinDiving)
                return Mathf.Clamp(reportedDownwardSpeed, 0f, 30f);
        }

        return Mathf.Clamp(reportedDownwardSpeed, 0f, 30f);
    }

    private static bool TryGetSpawnedPlayer(
        ulong networkObjectId,
        out PlayerHealth health,
        out PlayerMovement movement)
    {
        health = null;
        movement = null;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.SpawnManager == null)
            return false;

        if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
            return false;

        if (networkObject == null || !networkObject.TryGetComponent(out health))
            return false;

        return health.TryGetComponent(out movement) && health.IsSpawned;
    }

    private static Vector3 ResolveCenter(Transform root, CapsuleCollider playerCapsule)
    {
        return BodySlamValidation.ResolveBodyCenter(root, playerCapsule);
    }

    private bool IsOwnCollider(Collider other)
    {
        return other != null && other.transform.IsChildOf(transform);
    }

    private void LogDebug(string message)
    {
        if (!showBodySlamDebug)
            return;

        Debug.Log($"[BodySlam] {message}", this);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showBodySlamDebug)
            return;

        CacheReferences();
        Vector3 probe = Application.isPlaying
            ? lastProbeCenter
            : ResolveCenter(transform, capsule);
        if (probe.sqrMagnitude < 0.0001f)
            probe = transform.position + Vector3.up * 0.45f;

        Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(probe, Mathf.Max(0.05f, bodySlamHorizontalTolerance));
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.12f);
        Gizmos.DrawSphere(probe, Mathf.Max(0.05f, bodySlamHorizontalTolerance));

        Vector3 high = probe + Vector3.up * 0.02f;
        Vector3 low = probe - Vector3.up * Mathf.Max(0.1f, bodySlamMaximumVerticalSeparation);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(high, low);

        if (Application.isPlaying && lastProbeHadVictim)
        {
            Gizmos.color = lastVictimWasProne ? Color.green : Color.red;
            Gizmos.DrawWireSphere(lastVictimCenter, 0.12f);
            Gizmos.DrawLine(probe, lastVictimCenter);
        }
    }
#endif
}
