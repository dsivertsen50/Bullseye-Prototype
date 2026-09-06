using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Server-authoritative bullseye movement across a bone-anchored surface graph.
/// Clients reconstruct the same interpolated surface pose from replicated region state.
/// </summary>
public class BullseyeMover : NetworkBehaviour
{
    [Header("Surface")]
    [SerializeField] private BullseyeSurfaceMap surfaceMap;
    [SerializeField] private BullseyeSurfaceVisual surfaceVisual;
    [SerializeField] private Transform attachedHitTarget;
    [SerializeField] private Transform physicalBullseye;
    [SerializeField] private float bullseyeSize = 0.28f;
    [SerializeField] private float hitTargetThickness = 0.05f;

    [Header("Movement")]
    [SerializeField] private float baseMovementSpeed = 0.2f;
    [SerializeField, Range(0f, 1f)] private float pauseChance = 0f;
    [SerializeField] private float minPauseDuration = 0f;
    [SerializeField] private float maxPauseDuration = 0.25f;
    [SerializeField] private float randomDirectionWeight = 0.35f;
    [SerializeField] private float continueForwardWeight = 2.4f;
    [SerializeField] private float maxSpawnPhaseOffset = 0f;

    [Header("Jump Influence")]
    [SerializeField] private float jumpInfluenceAmount = 0.55f;
    [SerializeField] private float jumpInfluenceWindow = 1.15f;
    [SerializeField] private float maximumJumpInfluence = 1.8f;
    [SerializeField] private float jumpInfluenceDecayRate = 0.85f;

    [Header("Other Influence")]
    [SerializeField] private float crouchInfluenceStrength = 0.42f;
    [SerializeField] private float turnBullseyeInfluenceStrength = 0.55f;
    [SerializeField] private float turnInfluenceDelay = 0.35f;
    [SerializeField] private float minTurnRateForInfluence = 35f;
    [SerializeField] private float turnRateForFullInfluence = 180f;
    [SerializeField] private float turnInfluenceSmoothing = 4f;
    [SerializeField] private float turnInfluenceDecayRate = 2.5f;
    [SerializeField] private float hideFromOwnerCameraDistance = 0.6f;

    [Header("Debug")]
    [SerializeField] private bool debugVisualization;

    private readonly NetworkVariable<byte> currentRegion = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<byte> targetRegion = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> moveStartTime = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> travelDuration = new(
        0.35f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> pauseUntilTime = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> jumpInfluence = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> lastJumpTime = new(
        -100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> turnInfluence = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private BullseyeDetachController detachController;
    private BullseyeTarget attachedTarget;
    private Collider attachedCollider;
    private float lastYaw;
    private bool hasLastYaw;
    private float lastSentTurnRate;
    private float turnSampleSendCooldown;
    private float localTurnInfluence;
    private Vector3 lastPosition;
    private Vector3 lastNormal = Vector3.forward;
    private Quaternion lastRotation = Quaternion.identity;
    private int lastAttachedRegion;
    private int previousRegion = -1;

    public BullseyeSurfaceMap SurfaceMap => surfaceMap;
    public Transform PhysicalBullseye => physicalBullseye;
    public Transform AttachedHitTarget => attachedHitTarget;
    public int CurrentRegionIndex => currentRegion.Value;
    public int TargetRegionIndex => targetRegion.Value;
    public float MovementProgress => GetMovementProgress();
    public float JumpInfluence => jumpInfluence.Value;
    public float TurnInfluence => IsServer ? turnInfluence.Value : localTurnInfluence;
    public float BullseyeSize => bullseyeSize;
    public bool DebugVisualization => debugVisualization;

    public Vector3 CurrentWorldPosition => lastPosition;
    public Vector3 CurrentWorldNormal => lastNormal;
    public Quaternion CurrentWorldRotation => lastRotation;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        detachController = GetComponent<BullseyeDetachController>();

        if (surfaceMap == null)
            surfaceMap = GetComponent<BullseyeSurfaceMap>();
        if (surfaceVisual == null)
            surfaceVisual = GetComponent<BullseyeSurfaceVisual>();
        if (physicalBullseye == null)
        {
            BullseyeTarget physical = FindPhysicalTarget();
            if (physical != null)
                physicalBullseye = physical.transform;
        }

        if (attachedHitTarget != null)
        {
            attachedTarget = attachedHitTarget.GetComponent<BullseyeTarget>();
            attachedCollider = attachedHitTarget.GetComponent<Collider>();
        }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            RestartIndependentRandomization();

        ResetTurnTracking();
        ApplySurfacePose();
    }

    public void NotifyJump()
    {
        if (!IsSpawned)
            return;

        RecordJumpServerRpc();
    }

    public void NotifyCrouchChanged()
    {
        // Crouch is read live from PlayerMovement on the server when picking targets.
    }

    public void ClearInfluence()
    {
        if (!IsServer || !IsSpawned)
            return;

        jumpInfluence.Value = 0f;
        lastJumpTime.Value = -100f;
        turnInfluence.Value = 0f;
    }

    public void RestartIndependentRandomization()
    {
        if (!IsServer || !IsSpawned)
            return;

        ClearInfluence();
        previousRegion = -1;
        int start = PickWeightedRegion(exclude: -1);
        lastAttachedRegion = start;
        currentRegion.Value = (byte)start;
        float now = ServerNow();
        int next = PickNextRegion(start);
        if (next == start)
            next = PickWeightedRegion(start);

        targetRegion.Value = (byte)next;
        moveStartTime.Value = now - (maxSpawnPhaseOffset > 0f ? Random.Range(0f, maxSpawnPhaseOffset) : 0f);
        travelDuration.Value = EstimateTravelDuration(start, next);
        pauseUntilTime.Value = 0f;
    }

    public void ResetTurnTracking()
    {
        hasLastYaw = false;
        lastSentTurnRate = 0f;
        turnSampleSendCooldown = 0f;
        lastYaw = transform.eulerAngles.y;
        localTurnInfluence = 0f;
    }

    public bool TryGetSurfacePose(out Vector3 position, out Vector3 normal, out Quaternion rotation)
    {
        position = lastPosition;
        normal = lastNormal;
        rotation = lastRotation;
        return lastNormal.sqrMagnitude > 0.0001f;
    }

    public int ConsumeReattachRegion(Vector3 worldHint)
    {
        if (surfaceMap == null)
            return 0;

        int region = lastAttachedRegion;
        if (!surfaceMap.TryGetRegion(region, out _))
            region = surfaceMap.FindNearestRegion(worldHint);

        if (IsServer)
        {
            currentRegion.Value = (byte)region;
            targetRegion.Value = (byte)region;
            moveStartTime.Value = ServerNow();
            travelDuration.Value = 0.01f;
            pauseUntilTime.Value = ServerNow() + RandomPause();
        }

        return region;
    }

    public void RememberAttachedRegion()
    {
        lastAttachedRegion = currentRegion.Value;
    }

    [Rpc(SendTo.Server)]
    private void RecordJumpServerRpc()
    {
        float now = ServerNow();
        float next = jumpInfluence.Value + jumpInfluenceAmount;
        if (now - lastJumpTime.Value <= Mathf.Max(0.05f, jumpInfluenceWindow))
            next += jumpInfluenceAmount * 0.5f;

        jumpInfluence.Value = Mathf.Min(maximumJumpInfluence, next);
        lastJumpTime.Value = now;
    }

    [Rpc(SendTo.Server)]
    private void RecordTurnSampleServerRpc(float yawRate)
    {
        float target = ScaledTurnInfluence(yawRate);
        turnInfluence.Value = target;
    }

    private void Update()
    {
        if (!IsSpawned)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (IsOwner)
            SampleOwnerYaw();

        if (IsServer)
            TickServerMovement();

        if (!IsServer)
            TickClientTurnFollow();
    }

    private void LateUpdate()
    {
        if (!IsSpawned)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        ApplySurfacePose();
    }

    private void TickServerMovement()
    {
        DecayJumpInfluence(Time.deltaTime);
        DecayTurnInfluence(Time.deltaTime);

        if (detachController != null && !detachController.IsSurfaceDriven)
            return;

        if (surfaceMap == null || surfaceMap.RegionCount == 0)
            return;

        float now = ServerNow();
        if (now < pauseUntilTime.Value)
            return;

        float progress = GetMovementProgress();
        if (progress < 1f && currentRegion.Value != targetRegion.Value)
            return;

        int arrived = targetRegion.Value;
        previousRegion = currentRegion.Value;
        currentRegion.Value = (byte)arrived;
        lastAttachedRegion = arrived;

        if (pauseChance > 0f && Random.value < pauseChance)
        {
            pauseUntilTime.Value = now + RandomPause();
            return;
        }

        BeginTravelFrom(arrived, now);
    }

    private void BeginTravelFrom(int from, float now)
    {
        int next = PickNextRegion(from);
        if (next == from)
            next = PickWeightedRegion(from);

        targetRegion.Value = (byte)next;
        moveStartTime.Value = now;
        travelDuration.Value = EstimateTravelDuration(from, next);
        pauseUntilTime.Value = 0f;
    }

    private float EstimateTravelDuration(int from, int to)
    {
        float distance = 0.28f;
        if (surfaceMap != null &&
            surfaceMap.TryEvaluate(from, out Vector3 start, out _) &&
            surfaceMap.TryEvaluate(to, out Vector3 end, out _))
        {
            distance = Mathf.Max(0.16f, Vector3.Distance(start, end));
        }

        return distance / Mathf.Max(0.05f, baseMovementSpeed);
    }

    private void DecayJumpInfluence(float dt)
    {
        if (jumpInfluence.Value <= 0f)
            return;

        float next = jumpInfluence.Value - Mathf.Max(0f, jumpInfluenceDecayRate) * dt;
        jumpInfluence.Value = Mathf.Max(0f, next);
    }

    private void DecayTurnInfluence(float dt)
    {
        float target = 0f;
        float rate = turnInfluenceDecayRate;
        turnInfluence.Value = Mathf.MoveTowards(turnInfluence.Value, target, Mathf.Max(0f, rate) * dt);
    }

    private int PickNextRegion(int from)
    {
        if (surfaceMap == null)
            return from;

        int neighborCount = surfaceMap.GetNeighborCount(from);
        if (neighborCount <= 0)
            return PickWeightedRegion(from);

        float verticalBias = jumpInfluence.Value;
        if (playerMovement != null && playerMovement.IsCrouched)
            verticalBias -= crouchInfluenceStrength;

        float lateralBias = turnInfluence.Value;
        float total = 0f;
        float[] weights = new float[neighborCount];

        for (int i = 0; i < neighborCount; i++)
        {
            if (!surfaceMap.TryGetNeighbor(from, i, out int neighbor))
            {
                weights[i] = 0f;
                continue;
            }

            float weight = Mathf.Max(0.01f, surfaceMap.GetWeight(neighbor));
            float verticalDelta = surfaceMap.GetVertical(neighbor) - surfaceMap.GetVertical(from);
            float lateral = surfaceMap.GetLateral(neighbor);

            weight *= 1f + Mathf.Max(-0.85f, verticalBias * verticalDelta * 2.4f);
            weight *= 1f + Mathf.Max(-0.75f, lateralBias * lateral);
            if (neighbor == previousRegion)
                weight *= 0.12f;
            else if (previousRegion >= 0)
                weight *= Mathf.Max(1f, continueForwardWeight);
            weight = Mathf.Lerp(weight, 1f, Mathf.Clamp01(randomDirectionWeight));
            weights[i] = Mathf.Max(0.01f, weight);
            total += weights[i];
        }

        if (total <= 0f)
            return from;

        float pick = Random.value * total;
        for (int i = 0; i < neighborCount; i++)
        {
            pick -= weights[i];
            if (pick > 0f)
                continue;

            return surfaceMap.TryGetNeighbor(from, i, out int neighbor) ? neighbor : from;
        }

        return surfaceMap.TryGetNeighbor(from, neighborCount - 1, out int last) ? last : from;
    }

    private int PickWeightedRegion(int exclude)
    {
        if (surfaceMap == null || surfaceMap.RegionCount == 0)
            return 0;

        float total = 0f;
        int count = surfaceMap.RegionCount;
        for (int i = 0; i < count; i++)
        {
            if (i == exclude)
                continue;
            total += Mathf.Max(0.01f, surfaceMap.GetWeight(i));
        }

        float pick = Random.value * Mathf.Max(0.01f, total);
        for (int i = 0; i < count; i++)
        {
            if (i == exclude)
                continue;

            pick -= Mathf.Max(0.01f, surfaceMap.GetWeight(i));
            if (pick <= 0f)
                return i;
        }

        return surfaceMap.DefaultAttachedRegion();
    }

    private float RandomPause()
    {
        float min = Mathf.Max(0f, minPauseDuration);
        float max = Mathf.Max(min, maxPauseDuration);
        return Random.Range(min, max);
    }

    private float GetMovementProgress()
    {
        float duration = Mathf.Max(0.01f, travelDuration.Value);
        float now = ServerNow();
        if (now < pauseUntilTime.Value && currentRegion.Value == targetRegion.Value)
            return 1f;

        return Mathf.Clamp01((now - moveStartTime.Value) / duration);
    }

    private void ApplySurfacePose()
    {
        bool attached = detachController == null || detachController.IsSurfaceDriven;
        if (surfaceMap == null)
            return;

        float progress = GetMovementProgress();
        if (!surfaceMap.TryEvaluateInterpolated(
                currentRegion.Value,
                targetRegion.Value,
                progress,
                out Vector3 position,
                out Vector3 normal))
        {
            return;
        }

        Quaternion rotation = surfaceMap.RotationFromNormal(normal);
        lastPosition = position;
        lastNormal = normal;
        lastRotation = rotation;

        if (!attached)
        {
            SetAttachedGameplayActive(false);
            if (surfaceVisual != null)
                surfaceVisual.SetAttachedVisible(false);
            return;
        }

        lastAttachedRegion = currentRegion.Value;
        SetAttachedGameplayActive(true);
        ApplyHitTarget(position, rotation);
        ApplyPhysicalHidden();

        if (surfaceVisual != null)
        {
            surfaceVisual.StampRadius = bullseyeSize * 0.5f;
            surfaceVisual.SetAttachedVisible(true);
            surfaceVisual.ApplyPose(position, normal, rotation);
        }

        ApplyOwnerVisibility(position);
    }

    private void ApplyHitTarget(Vector3 position, Quaternion rotation)
    {
        if (attachedHitTarget == null)
            return;

        attachedHitTarget.SetPositionAndRotation(position, rotation);
        float diameter = Mathf.Max(0.08f, bullseyeSize);
        attachedHitTarget.localScale = new Vector3(diameter, diameter, Mathf.Max(0.03f, hitTargetThickness));
    }

    private void ApplyPhysicalHidden()
    {
        if (physicalBullseye == null)
            return;

        Renderer[] renderers = physicalBullseye.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].forceRenderingOff = true;
        }
    }

    private void SetAttachedGameplayActive(bool active)
    {
        if (attachedCollider != null)
            attachedCollider.enabled = active && (playerHealth == null || !playerHealth.IsDead);

        if (attachedHitTarget != null && attachedHitTarget.gameObject.activeSelf != (active || attachedHitTarget.gameObject.activeSelf))
            attachedHitTarget.gameObject.SetActive(true);
    }

    private void ApplyOwnerVisibility(Vector3 position)
    {
        bool show = true;
        if (IsOwner)
        {
            Camera cam = PlayerNetworkSetup.LocalOwnedCamera;
            if (cam == null)
                cam = GetComponentInChildren<Camera>();

            if (cam == null || !cam.enabled)
                show = false;
            else
                show = Vector3.Distance(position, cam.transform.position) >= Mathf.Max(0f, hideFromOwnerCameraDistance);
        }

        if (surfaceVisual != null)
            surfaceVisual.SetSuppressedForOwner(!show);

        if (attachedTarget != null)
            attachedTarget.SetVisibleToLocalViewer(show);
    }

    private void SampleOwnerYaw()
    {
        float yaw = transform.eulerAngles.y;
        if (!hasLastYaw)
        {
            lastYaw = yaw;
            hasLastYaw = true;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0.0001f)
            return;

        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
        lastYaw = yaw;

        if (Mathf.Abs(yawRate) < minTurnRateForInfluence)
            yawRate = 0f;

        turnSampleSendCooldown -= dt;
        bool wasMoving = Mathf.Abs(lastSentTurnRate) >= 0.01f;
        bool isMoving = Mathf.Abs(yawRate) >= 0.01f;
        bool crossedZero = wasMoving != isMoving;
        bool reversed = isMoving && wasMoving && Mathf.Sign(yawRate) != Mathf.Sign(lastSentTurnRate);
        bool changedAlot = Mathf.Abs(yawRate - lastSentTurnRate) >= 25f;
        bool canSend = turnSampleSendCooldown <= 0f || crossedZero || reversed;

        if ((crossedZero || reversed || changedAlot) && canSend)
        {
            lastSentTurnRate = yawRate;
            turnSampleSendCooldown = 0.07f;
            RecordTurnSampleServerRpc(yawRate);
        }
    }

    private void TickClientTurnFollow()
    {
        float target = turnInfluence.Value;
        float approach = Mathf.Abs(target) < Mathf.Abs(localTurnInfluence) - 0.0001f
            ? turnInfluenceDecayRate
            : turnInfluenceSmoothing;
        localTurnInfluence = Mathf.MoveTowards(localTurnInfluence, target, Mathf.Max(0f, approach) * Time.deltaTime);
    }

    private float ScaledTurnInfluence(float yawRateDegrees)
    {
        if (Mathf.Abs(yawRateDegrees) < minTurnRateForInfluence)
            return 0f;

        float fullRate = Mathf.Max(1f, turnRateForFullInfluence);
        float scaled = Mathf.Clamp(yawRateDegrees / fullRate, -1f, 1f);
        return scaled * turnBullseyeInfluenceStrength;
    }

    private float ServerNow()
    {
        if (NetworkManager == null)
            return Time.time;
        return (float)NetworkManager.ServerTime.Time;
    }

    private BullseyeTarget FindPhysicalTarget()
    {
        BullseyeTarget[] targets = GetComponentsInChildren<BullseyeTarget>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null &&
                (attachedHitTarget == null || targets[i].transform != attachedHitTarget))
            {
                return targets[i];
            }
        }

        return null;
    }

    public string DebugRegionName(int index)
    {
        if (surfaceMap != null && surfaceMap.TryGetRegion(index, out BullseyeSurfaceRegion region))
            return string.IsNullOrEmpty(region.displayName) ? region.id.ToString() : region.displayName;
        return index.ToString();
    }

    private void OnGUI()
    {
        if (!debugVisualization || !IsSpawned)
            return;

        string state = detachController != null ? detachController.State.ToString() : "Attached";
        string text =
            $"Bullseye {state}\n" +
            $"{DebugRegionName(CurrentRegionIndex)} -> {DebugRegionName(TargetRegionIndex)}\n" +
            $"Progress {MovementProgress:0.00}  Jump {JumpInfluence:0.00}  Turn {TurnInfluence:0.00}";

        GUI.color = Color.black;
        GUI.Label(new Rect(12f, 12f, 420f, 70f), text);
        GUI.color = Color.white;
        GUI.Label(new Rect(10f, 10f, 420f, 70f), text);
    }

    private void OnDrawGizmos()
    {
        if (!debugVisualization)
            return;

        if (surfaceMap != null && lastNormal.sqrMagnitude > 0.0001f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lastPosition, 0.03f);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(lastPosition, lastPosition + lastNormal * 0.25f);

            if (surfaceMap.TryEvaluate(targetRegion.Value, out Vector3 targetPos, out _))
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(targetPos, 0.025f);
                Gizmos.DrawLine(lastPosition, targetPos);
            }
        }

        if (attachedHitTarget != null && attachedCollider != null && attachedCollider.enabled)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
            Gizmos.matrix = attachedHitTarget.localToWorldMatrix;
            Gizmos.DrawSphere(Vector3.zero, 0.5f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }

    private void OnValidate()
    {
        bullseyeSize = Mathf.Max(0.06f, bullseyeSize);
        hitTargetThickness = Mathf.Max(0.02f, hitTargetThickness);
        baseMovementSpeed = Mathf.Max(0.05f, baseMovementSpeed);
        pauseChance = Mathf.Clamp01(pauseChance);
        minPauseDuration = Mathf.Max(0f, minPauseDuration);
        maxPauseDuration = Mathf.Max(minPauseDuration, maxPauseDuration);
        randomDirectionWeight = Mathf.Clamp01(randomDirectionWeight);
        continueForwardWeight = Mathf.Max(1f, continueForwardWeight);
        jumpInfluenceAmount = Mathf.Max(0f, jumpInfluenceAmount);
        jumpInfluenceWindow = Mathf.Max(0.05f, jumpInfluenceWindow);
        maximumJumpInfluence = Mathf.Max(jumpInfluenceAmount, maximumJumpInfluence);
        jumpInfluenceDecayRate = Mathf.Max(0f, jumpInfluenceDecayRate);
        crouchInfluenceStrength = Mathf.Max(0f, crouchInfluenceStrength);
        hideFromOwnerCameraDistance = Mathf.Max(0f, hideFromOwnerCameraDistance);
    }
}
