using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner ping input, server-authoritative validation, and team-targeted
/// delivery of lightweight warning-mark state.
/// </summary>
public class PlayerPingController : NetworkBehaviour
{
    private const float DefaultMaxDistance = 200f;
    private const float SurfaceOffset = 0.14f;
    private const float FloatHeight = 0.28f;

    [Header("Timing")]
    [SerializeField] private float locationPingDuration = 5f;
    [SerializeField] private float enemyPingDuration = 4f;
    [SerializeField] private float pingCooldown = 0.5f;

    [Header("Ray")]
    [SerializeField] private float maxPingDistance = DefaultMaxDistance;
    [SerializeField] private float maxReportedOriginDistance = 4f;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference pingAction;

    [Header("Debug")]
    [SerializeField, Tooltip("Draws the ping ray and logs hit classification. Off for normal play.")]
    private bool showPingDebug;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly List<ulong> recipients = new(8);
    private readonly List<ulong> lastRecipients = new(8);

    private PlayerHealth playerHealth;
    private WeaponAccuracyController accuracy;
    private InputAction resolvedPingAction;
    private float nextPingAllowedTime;

    private bool serverHasPing;
    private TeamPingKind serverKind;
    private Vector3 serverPosition;
    private ulong serverTargetId;
    private double serverExpireTime;
    private double serverLastPingTime = -100d;
    private int serverTeamId;
    private ulong serverTargetOwnerId;
    private ulong[] recipientBuffer = System.Array.Empty<ulong>();

    public override void OnNetworkSpawn()
    {
        playerHealth = GetComponent<PlayerHealth>();
        accuracy = GetComponent<WeaponAccuracyController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (IsOwner)
        {
            BindPingAction();
            resolvedPingAction?.Enable();
            TeamPingHud.Ensure();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            ClearActivePing();

        if (IsOwner)
            TeamPingHud.Instance?.RemoveOwner(OwnerClientId);

        resolvedPingAction = null;
    }

    private void Update()
    {
        if (IsServer)
            TickServerPing();

        if (!IsOwner)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        if (resolvedPingAction == null || !resolvedPingAction.WasPressedThisFrame())
            return;

        TryPing();
    }

    private void TickServerPing()
    {
        if (!serverHasPing)
            return;

        if (NetworkManager.ServerTime.Time >= serverExpireTime || IsServerTargetInvalid())
            ClearActivePing();
    }

    private void TryPing()
    {
        if (Time.time < nextPingAllowedTime)
            return;

        if (!TryBuildAimRay(out Ray aimRay))
            return;

        if (!TryResolveHit(aimRay.origin, aimRay.direction, NetworkObject, out PingRequest request))
            return;

        nextPingAllowedTime = Time.time + Mathf.Max(0.05f, pingCooldown);
        ApplyLocalPrediction(request);
        RequestPingServerRpc(aimRay.origin, aimRay.direction, (byte)request.Kind, request.WorldPosition, request.TargetNetworkObjectId);

        if (showPingDebug)
            LogDebug(aimRay, request, "local");
    }

    [Rpc(SendTo.Server)]
    private void RequestPingServerRpc(
        Vector3 reportedOrigin,
        Vector3 reportedDirection,
        byte reportedKind,
        Vector3 reportedPosition,
        ulong reportedTargetId,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (!SanitizeAim(reportedOrigin, reportedDirection, out Vector3 origin, out Vector3 direction))
            return;

        if (!TryResolveHit(origin, direction, NetworkObject, out PingRequest request))
            return;

        if (!ValidateAgainstServer(ref request, reportedKind, reportedTargetId, origin))
            return;

        double now = NetworkManager.ServerTime.Time;
        if (now < serverLastPingTime + pingCooldown)
            return;

        serverLastPingTime = now;
        PublishPing(request);
        if (showPingDebug)
            Debug.Log($"[Ping] server accepted {request.Kind} from client {OwnerClientId} at {request.WorldPosition}");
    }

    private bool ValidateAgainstServer(
        ref PingRequest request,
        byte reportedKind,
        ulong reportedTargetId,
        Vector3 origin)
    {
        float range = Mathf.Max(1f, maxPingDistance) + 4f;
        if ((request.WorldPosition - origin).sqrMagnitude > range * range)
            return false;

        if (request.Kind == TeamPingKind.Enemy)
        {
            if (!TryGetSpawnedObject(request.TargetNetworkObjectId, out NetworkObject target) ||
                !target.TryGetComponent(out PlayerHealth targetHealth) ||
                targetHealth.IsDead ||
                targetHealth.CurrentHealth <= 0 ||
                PlayerTeamRules.AreAllies(NetworkManager, OwnerClientId, target.OwnerClientId))
            {
                return false;
            }

            return (target.transform.position - origin).sqrMagnitude <= range * range;
        }

        if ((TeamPingKind)reportedKind == TeamPingKind.Enemy && reportedTargetId != 0)
        {
            request.Kind = TeamPingKind.Location;
            request.TargetNetworkObjectId = 0;
        }

        return true;
    }

    private void PublishPing(PingRequest request)
    {
        CollectRecipients();
        if (recipients.Count == 0)
            return;

        serverHasPing = true;
        serverKind = request.Kind;
        serverPosition = request.WorldPosition;
        serverTargetId = request.TargetNetworkObjectId;
        serverTeamId = PlayerTeamRules.GetTeamId(NetworkObject);
        serverExpireTime = NetworkManager.ServerTime.Time + GetDuration(request.Kind);
        serverTargetOwnerId = 0;
        if (request.Kind == TeamPingKind.Enemy &&
            TryGetSpawnedObject(request.TargetNetworkObjectId, out NetworkObject target))
        {
            serverTargetOwnerId = target.OwnerClientId;
        }

        lastRecipients.Clear();
        lastRecipients.AddRange(recipients);
        SendApply();
    }

    private void ClearActivePing()
    {
        if (!serverHasPing)
            return;

        serverHasPing = false;
        serverTargetId = 0;
        serverTargetOwnerId = 0;
        if (lastRecipients.Count == 0)
            return;

        CopyRecipients(lastRecipients);
        SendClear();
        lastRecipients.Clear();
    }

    private bool IsServerTargetInvalid()
    {
        if (serverKind != TeamPingKind.Enemy)
            return false;

        if (!TryGetSpawnedObject(serverTargetId, out NetworkObject target))
            return true;

        if (target.TryGetComponent(out PlayerHealth health) && (health.IsDead || health.CurrentHealth <= 0))
            return true;

        return serverTargetOwnerId != 0 && target.OwnerClientId != serverTargetOwnerId;
    }

    private void CollectRecipients()
    {
        recipients.Clear();
        if (NetworkManager == null)
            return;

        IReadOnlyList<ulong> clients = NetworkManager.ConnectedClientsIds;
        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i];
            if (PlayerTeamRules.CanReceiveTeamPing(NetworkManager, OwnerClientId, clientId))
                recipients.Add(clientId);
        }
    }

    private void SendApply()
    {
        if (lastRecipients.Count == 1)
        {
            ApplyPingRpc(
                OwnerClientId,
                (byte)serverKind,
                serverPosition,
                serverTargetId,
                serverExpireTime,
                serverTeamId,
                RpcTarget.Single(lastRecipients[0], RpcTargetUse.Temp));
            return;
        }

        CopyRecipients(lastRecipients);
        ApplyPingRpc(
            OwnerClientId,
            (byte)serverKind,
            serverPosition,
            serverTargetId,
            serverExpireTime,
            serverTeamId,
            RpcTarget.Group(recipientBuffer, RpcTargetUse.Temp));
    }

    private void SendClear()
    {
        if (recipientBuffer.Length == 1)
        {
            ClearPingRpc(OwnerClientId, RpcTarget.Single(recipientBuffer[0], RpcTargetUse.Temp));
            return;
        }

        if (recipientBuffer.Length > 1)
            ClearPingRpc(OwnerClientId, RpcTarget.Group(recipientBuffer, RpcTargetUse.Temp));
    }

    private void CopyRecipients(List<ulong> clientIds)
    {
        if (recipientBuffer.Length != clientIds.Count)
            recipientBuffer = new ulong[clientIds.Count];

        for (int i = 0; i < clientIds.Count; i++)
            recipientBuffer[i] = clientIds[i];
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ApplyPingRpc(
        ulong ownerClientId,
        byte kind,
        Vector3 worldPosition,
        ulong targetNetworkObjectId,
        double expireServerTime,
        int teamId,
        RpcParams rpcParams = default)
    {
        TeamPingHud.Ensure().Upsert(new TeamPingHud.VisiblePing
        {
            OwnerClientId = ownerClientId,
            Kind = (TeamPingKind)kind,
            WorldPosition = worldPosition,
            TargetNetworkObjectId = targetNetworkObjectId,
            ExpireServerTime = expireServerTime,
            TeamId = teamId
        });
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void ClearPingRpc(ulong ownerClientId, RpcParams rpcParams = default)
    {
        TeamPingHud.Instance?.RemoveOwner(ownerClientId);
    }

    private void ApplyLocalPrediction(PingRequest request)
    {
        double expire = Time.unscaledTimeAsDouble + GetDuration(request.Kind);
        if (NetworkManager != null && NetworkManager.IsListening)
            expire = NetworkManager.ServerTime.Time + GetDuration(request.Kind);

        TeamPingHud.Ensure().Upsert(new TeamPingHud.VisiblePing
        {
            OwnerClientId = OwnerClientId,
            Kind = request.Kind,
            WorldPosition = request.WorldPosition,
            TargetNetworkObjectId = request.TargetNetworkObjectId,
            ExpireServerTime = expire,
            TeamId = PlayerTeamRules.GetTeamId(NetworkObject)
        });
    }

    private bool TryBuildAimRay(out Ray ray)
    {
        if (accuracy != null && playerCamera != null)
        {
            ray = accuracy.GetCenterHitscanRay(playerCamera);
            return true;
        }

        if (playerCamera != null)
        {
            ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            return true;
        }

        ray = new Ray(transform.position + Vector3.up * 1.6f, transform.forward);
        return true;
    }

    private bool SanitizeAim(Vector3 reportedOrigin, Vector3 reportedDirection, out Vector3 origin, out Vector3 direction)
    {
        TryBuildAimRay(out Ray fallback);
        origin = reportedOrigin;
        if ((reportedOrigin - fallback.origin).sqrMagnitude > maxReportedOriginDistance * maxReportedOriginDistance)
            origin = fallback.origin;

        if ((origin - transform.position).sqrMagnitude > 6f * 6f)
            origin = fallback.origin;

        direction = reportedDirection.sqrMagnitude > 0.0001f
            ? reportedDirection.normalized
            : fallback.direction;
        return direction.sqrMagnitude > 0.0001f;
    }

    private bool TryResolveHit(Vector3 origin, Vector3 direction, NetworkObject ignoreOwner, out PingRequest request)
    {
        request = default;
        int mask = HitscanRicochet.BuildHitscanMask();
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            hits,
            Mathf.Max(1f, maxPingDistance),
            mask,
            QueryTriggerInteraction.Collide);

        int bestIndex = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null)
                continue;

            if (IsOwnCollider(hit.collider, ignoreOwner))
                continue;

            if (IsAllyCollider(hit.collider))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            bestIndex = i;
        }

        if (bestIndex < 0)
            return false;

        RaycastHit selected = hits[bestIndex];
        if (TryGetEnemyHealth(selected.collider, out PlayerHealth enemy) &&
            enemy.NetworkObject != null &&
            enemy.NetworkObject.IsSpawned)
        {
            request = new PingRequest
            {
                Kind = TeamPingKind.Enemy,
                WorldPosition = ResolveFollowFallback(enemy.NetworkObject),
                TargetNetworkObjectId = enemy.NetworkObject.NetworkObjectId
            };
            return true;
        }

        Vector3 normal = selected.normal.sqrMagnitude > 0.0001f ? selected.normal.normalized : Vector3.up;
        request = new PingRequest
        {
            Kind = TeamPingKind.Location,
            WorldPosition = selected.point + normal * SurfaceOffset + Vector3.up * FloatHeight,
            TargetNetworkObjectId = 0
        };
        return true;
    }

    private bool IsAllyCollider(Collider collider)
    {
        PlayerHealth health = collider.GetComponentInParent<PlayerHealth>();
        if (health == null || health.NetworkObject == null)
            return false;

        if (health.NetworkObject == NetworkObject)
            return true;

        return PlayerTeamRules.AreAllies(NetworkManager, OwnerClientId, health.OwnerClientId);
    }

    private bool TryGetEnemyHealth(Collider collider, out PlayerHealth health)
    {
        health = collider != null ? collider.GetComponentInParent<PlayerHealth>() : null;
        if (health == null || !health.IsSpawned || health.IsDead)
            return false;

        if (health.NetworkObject == NetworkObject)
            return false;

        return !PlayerTeamRules.AreAllies(NetworkManager, OwnerClientId, health.OwnerClientId);
    }

    private static bool IsOwnCollider(Collider collider, NetworkObject owner)
    {
        if (collider == null || owner == null)
            return false;

        NetworkObject ownerObject = collider.GetComponentInParent<NetworkObject>();
        return ownerObject != null && ownerObject == owner;
    }

    private static Vector3 ResolveFollowFallback(NetworkObject target)
    {
        return target.transform.position + Vector3.up * 1.85f;
    }

    private float GetDuration(TeamPingKind kind)
    {
        return kind == TeamPingKind.Enemy
            ? Mathf.Max(0.25f, enemyPingDuration)
            : Mathf.Max(0.25f, locationPingDuration);
    }

    private void BindPingAction()
    {
        InputActionAsset actions = null;
        if (TryGetComponent(out LocalPlayerInputBinding binding))
            actions = binding.PlayerActions;

        if (pingAction != null && pingAction.action != null)
            resolvedPingAction = pingAction.action;
        else if (actions != null)
            resolvedPingAction = actions.FindAction("Ping");
    }

    private bool TryGetSpawnedObject(ulong networkObjectId, out NetworkObject networkObject)
    {
        networkObject = null;
        if (networkObjectId == 0 || NetworkManager == null || NetworkManager.SpawnManager == null)
            return false;

        return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out networkObject)
               && networkObject != null
               && networkObject.IsSpawned;
    }

    private void LogDebug(Ray ray, PingRequest request, string stage)
    {
        Color color = request.Kind == TeamPingKind.Enemy ? Color.red : Color.yellow;
        Debug.DrawRay(ray.origin, ray.direction * Mathf.Min(maxPingDistance, 40f), color, 1.2f);
        Debug.DrawLine(ray.origin, request.WorldPosition, color, 1.2f);
        Debug.Log($"[Ping] {stage} {request.Kind} pos={request.WorldPosition} target={request.TargetNetworkObjectId}");
    }

    private void OnValidate()
    {
        locationPingDuration = Mathf.Max(0.25f, locationPingDuration);
        enemyPingDuration = Mathf.Max(0.25f, enemyPingDuration);
        pingCooldown = Mathf.Max(0.05f, pingCooldown);
        maxPingDistance = Mathf.Max(1f, maxPingDistance);
        maxReportedOriginDistance = Mathf.Max(0.5f, maxReportedOriginDistance);
    }

    private struct PingRequest
    {
        public TeamPingKind Kind;
        public Vector3 WorldPosition;
        public ulong TargetNetworkObjectId;
    }
}
