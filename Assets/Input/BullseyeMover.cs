using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BullseyeMover : NetworkBehaviour
{
    private const float SimulationDt = 1f / 30f;
    private const float VerticalMargin = 0.06f;
    private const float MinHeadingDelta = 50f * Mathf.Deg2Rad;

    [SerializeField] private Transform bullseye;
    [SerializeField] private CapsuleCollider bodyCapsule;

    [SerializeField] private float moveSpeed = 0.75f;
    [SerializeField] private float surfaceOffset = 0.08f;
    [SerializeField] private float minDirectionChangeInterval = 1.5f;
    [SerializeField] private float maxDirectionChangeInterval = 3.5f;
    [SerializeField] private float directionSmoothingTime = 0.6f;
    [SerializeField] private float jumpInfluenceStrength = 0.55f;
    [SerializeField] private float jumpInfluenceDuration = 1.15f;
    [SerializeField] private float crouchInfluenceStrength = 0.42f;
    [SerializeField] private float turnBullseyeInfluenceStrength = 0.55f;
    [SerializeField] private float turnInfluenceDelay = 0.35f;
    [SerializeField] private float minTurnRateForInfluence = 35f;
    [SerializeField] private float turnRateForFullInfluence = 180f;
    [SerializeField] private float turnInfluenceSmoothing = 4f;
    [SerializeField] private float turnInfluenceDecayRate = 2.5f;
    [SerializeField] private float maxSpawnPhaseOffset = 2.5f;
    [SerializeField] private float hideFromOwnerCameraDistance = 0.6f;

    private readonly NetworkVariable<float> pathStartTime = new(0f);
    private readonly NetworkVariable<int> randomSeed = new(0);

    private static int nextPathEntropy;

    private NetworkList<float> jumpInfluenceTimes;
    private NetworkList<float> crouchToggleTimes;
    private NetworkList<float> turnSampleTimes;
    private NetworkList<float> turnSampleRates;

    private readonly List<Leg> legs = new();
    private uint rngState;
    private float u;
    private float v;
    private int cachedStep;
    private int currentLegIndex;
    private TurnSimState turnSim;
    private float lastYaw;
    private float lastSentTurnRate;
    private float turnSampleSendCooldown;
    private bool hasLastYaw;
    private PlayerHealth playerHealth;
    private BullseyeTarget bullseyeTarget;
    private BullseyeDetachController detachController;

    private struct Leg
    {
        public float startTime;
        public float duration;
        public float fromHeading;
        public float toHeading;
    }

    private struct TurnSimState
    {
        public float influence;
        public int sampleIndex;
    }

    private void Awake()
    {
        if (bodyCapsule == null)
            bodyCapsule = GetComponentInChildren<CapsuleCollider>();

        playerHealth = GetComponent<PlayerHealth>();
        detachController = GetComponent<BullseyeDetachController>();
        if (bullseye != null)
            bullseyeTarget = bullseye.GetComponent<BullseyeTarget>();
        if (bullseyeTarget == null)
            bullseyeTarget = GetComponentInChildren<BullseyeTarget>();

        jumpInfluenceTimes = new NetworkList<float>();
        crouchToggleTimes = new NetworkList<float>();
        turnSampleTimes = new NetworkList<float>();
        turnSampleRates = new NetworkList<float>();
    }

    public override void OnNetworkSpawn()
    {
        pathStartTime.OnValueChanged += OnSyncChanged;
        randomSeed.OnValueChanged += OnSyncChanged;
        jumpInfluenceTimes.OnListChanged += OnInfluenceChanged;
        crouchToggleTimes.OnListChanged += OnInfluenceChanged;
        turnSampleTimes.OnListChanged += OnInfluenceChanged;
        turnSampleRates.OnListChanged += OnInfluenceChanged;

        if (IsServer)
            AssignIndependentRandomization();

        ResetTurnTracking();
        RebuildSimulation();
        SimulateAndApply();
    }

    public override void OnNetworkDespawn()
    {
        pathStartTime.OnValueChanged -= OnSyncChanged;
        randomSeed.OnValueChanged -= OnSyncChanged;
        jumpInfluenceTimes.OnListChanged -= OnInfluenceChanged;
        crouchToggleTimes.OnListChanged -= OnInfluenceChanged;
        turnSampleTimes.OnListChanged -= OnInfluenceChanged;
        turnSampleRates.OnListChanged -= OnInfluenceChanged;
    }

    public void NotifyJump()
    {
        if (!IsSpawned)
            return;

        RecordJumpServerRpc();
    }

    public void NotifyCrouchChanged()
    {
        if (!IsSpawned)
            return;

        RecordCrouchToggleServerRpc();
    }

    public void ClearInfluence()
    {
        if (!IsServer || !IsSpawned)
            return;

        jumpInfluenceTimes.Clear();
        crouchToggleTimes.Clear();
        turnSampleTimes.Clear();
        turnSampleRates.Clear();
    }

    public void RestartIndependentRandomization()
    {
        if (!IsServer || !IsSpawned)
            return;

        ClearInfluence();
        AssignIndependentRandomization();
    }

    private void AssignIndependentRandomization()
    {
        unchecked
        {
            nextPathEntropy++;
            int entropy = nextPathEntropy * 16777619 + Random.Range(1, int.MaxValue);
            randomSeed.Value = MixSeed(NetworkObjectId, OwnerClientId, entropy);
        }

        float phaseOffset = maxSpawnPhaseOffset > 0f
            ? Random.Range(0f, maxSpawnPhaseOffset)
            : 0f;
        pathStartTime.Value = (float)NetworkManager.ServerTime.Time - phaseOffset;
    }

    private static int MixSeed(ulong networkObjectId, ulong ownerClientId, int entropy)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)networkObjectId) * 16777619u;
            hash = (hash ^ (uint)(networkObjectId >> 32)) * 16777619u;
            hash = (hash ^ (uint)ownerClientId) * 16777619u;
            hash = (hash ^ (uint)(ownerClientId >> 32)) * 16777619u;
            hash = (hash ^ (uint)entropy) * 16777619u;
            if (hash == 0u)
                hash = 1u;
            return (int)hash;
        }
    }

    public void ResetTurnTracking()
    {
        hasLastYaw = false;
        lastSentTurnRate = 0f;
        turnSampleSendCooldown = 0f;
        lastYaw = transform.eulerAngles.y;
    }

    [Rpc(SendTo.Server)]
    private void RecordJumpServerRpc()
    {
        jumpInfluenceTimes.Add((float)NetworkManager.ServerTime.Time);
    }

    [Rpc(SendTo.Server)]
    private void RecordCrouchToggleServerRpc()
    {
        crouchToggleTimes.Add((float)NetworkManager.ServerTime.Time);
    }

    [Rpc(SendTo.Server)]
    private void RecordTurnSampleServerRpc(float yawRate)
    {
        if (turnSampleRates.Count > 0 &&
            Mathf.Abs(turnSampleRates[turnSampleRates.Count - 1] - yawRate) < 0.01f)
            return;

        turnSampleTimes.Add((float)NetworkManager.ServerTime.Time);
        turnSampleRates.Add(yawRate);
    }

    private void OnSyncChanged(float previous, float next)
    {
        RebuildSimulation();
        SimulateAndApply();
    }

    private void OnSyncChanged(int previous, int next)
    {
        RebuildSimulation();
        SimulateAndApply();
    }

    private void OnInfluenceChanged(NetworkListEvent<float> changeEvent)
    {
        RebuildSimulation();
        SimulateAndApply();
    }

    private void Update()
    {
        if (!IsSpawned || bullseye == null || bodyCapsule == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (detachController != null && !detachController.IsSurfaceDriven)
        {
            ApplyOwnerVisibility();
            return;
        }

        if (IsOwner)
            SampleOwnerYaw();

        SimulateAndApply();
    }

    private void RebuildSimulation()
    {
        rngState = (uint)randomSeed.Value;
        if (rngState == 0u)
            rngState = 1u;

        legs.Clear();
        currentLegIndex = 0;
        cachedStep = 0;

        turnSim = default;

        u = NextFloat();
        v = VerticalMargin + NextFloat() * (1f - 2f * VerticalMargin);

        float heading = WrapAngle(NextFloat() * Mathf.PI * 2f);
        legs.Add(new Leg
        {
            startTime = 0f,
            duration = NextInterval(),
            fromHeading = heading,
            toHeading = heading
        });
    }

    private void SimulateAndApply()
    {
        if (NetworkManager == null || legs.Count == 0)
            return;

        float elapsed = (float)NetworkManager.ServerTime.Time - pathStartTime.Value;
        if (elapsed < 0f)
            elapsed = 0f;

        int targetStep = (int)(elapsed / SimulationDt);
        while (cachedStep < targetStep)
        {
            float stepTime = cachedStep * SimulationDt;
            Integrate(ref u, ref v, ref turnSim, SimulationDt, stepTime, reflectLegs: true);
            cachedStep++;
        }

        float remainder = elapsed - targetStep * SimulationDt;
        float drawU = u;
        float drawV = v;
        TurnSimState drawTurn = turnSim;
        if (remainder > 0.0001f)
            Integrate(ref drawU, ref drawV, ref drawTurn, remainder, elapsed - remainder, reflectLegs: false);

        ApplySurfacePose(drawU, drawV);
        ApplyOwnerVisibility();
    }

    private void Integrate(
        ref float uu,
        ref float vv,
        ref TurnSimState turn,
        float dt,
        float time,
        bool reflectLegs)
    {
        CapsuleBodySurface.GetUvScales(bodyCapsule, vv, surfaceOffset, out float metersPerU, out float metersPerV);

        float heading = GetHeading(time);
        float circumferential = moveSpeed * Mathf.Cos(heading) + StepTurnInfluence(ref turn, dt, time);
        float vertical = moveSpeed * Mathf.Sin(heading) + GetVerticalInfluence(time);
        uu = Mathf.Repeat(uu + (circumferential / metersPerU) * dt, 1f);
        vv += (vertical / metersPerV) * dt;

        float minV = VerticalMargin;
        float maxV = 1f - VerticalMargin;
        if (vv >= minV && vv <= maxV)
            return;

        vv = Mathf.Clamp(vv, minV, maxV);
        if (reflectLegs)
            BounceVertically(time, vv);
    }

    private void BounceVertically(float time, float clampedV)
    {
        float heading = GetHeading(time);
        float circ = Mathf.Cos(heading);
        float vert = Mathf.Sin(heading);
        bool atBottom = clampedV <= VerticalMargin + 0.0001f;
        bool atTop = clampedV >= 1f - VerticalMargin - 0.0001f;

        if (atBottom && vert < 0.25f)
            vert = Mathf.Max(0.75f, Mathf.Abs(vert));
        else if (atTop && vert > -0.25f)
            vert = -Mathf.Max(0.75f, Mathf.Abs(vert));
        else
            return;

        float bounced = Mathf.Atan2(vert, circ);
        Leg leg = legs[currentLegIndex];
        leg.fromHeading = bounced;
        leg.toHeading = bounced;
        legs[currentLegIndex] = leg;
    }

    private float GetVerticalInfluence(float elapsed)
    {
        float influence = 0f;
        float serverTime = pathStartTime.Value + elapsed;

        if (jumpInfluenceTimes != null)
        {
            float duration = Mathf.Max(0f, jumpInfluenceDuration);
            for (int i = 0; i < jumpInfluenceTimes.Count; i++)
            {
                float start = jumpInfluenceTimes[i];
                if (serverTime >= start && serverTime < start + duration)
                    influence += jumpInfluenceStrength;
            }
        }

        if (IsCrouchedAt(serverTime))
            influence -= crouchInfluenceStrength;

        return influence;
    }

    private bool IsCrouchedAt(float serverTime)
    {
        if (crouchToggleTimes == null)
            return false;

        bool isCrouched = false;
        for (int i = 0; i < crouchToggleTimes.Count; i++)
        {
            if (crouchToggleTimes[i] <= serverTime)
                isCrouched = !isCrouched;
        }

        return isCrouched;
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

    private float StepTurnInfluence(ref TurnSimState turn, float dt, float time)
    {
        float serverTime = pathStartTime.Value + time;
        float delayedRate = GetYawRateAt(serverTime - turnInfluenceDelay, ref turn.sampleIndex);
        float target = ScaledTurnInfluence(delayedRate);

        float approachRate = Mathf.Abs(target) < Mathf.Abs(turn.influence) - 0.0001f
            ? turnInfluenceDecayRate
            : turnInfluenceSmoothing;
        approachRate = Mathf.Max(0f, approachRate);
        turn.influence = Mathf.MoveTowards(turn.influence, target, approachRate * dt);
        return turn.influence;
    }

    private float ScaledTurnInfluence(float yawRateDegrees)
    {
        if (Mathf.Abs(yawRateDegrees) < minTurnRateForInfluence)
            return 0f;

        float fullRate = Mathf.Max(1f, turnRateForFullInfluence);
        float scaled = Mathf.Clamp(yawRateDegrees / fullRate, -1f, 1f);
        return scaled * turnBullseyeInfluenceStrength;
    }

    private float GetYawRateAt(float serverTime, ref int sampleIndex)
    {
        if (turnSampleTimes == null || turnSampleTimes.Count == 0 || turnSampleTimes[0] > serverTime)
            return 0f;

        while (sampleIndex + 1 < turnSampleTimes.Count &&
               turnSampleTimes[sampleIndex + 1] <= serverTime)
        {
            sampleIndex++;
        }

        if (sampleIndex >= turnSampleRates.Count)
            return 0f;

        return turnSampleRates[sampleIndex];
    }

    private float GetHeading(float time)
    {
        EnsureLegs(time);

        while (currentLegIndex < legs.Count - 1 &&
               time >= legs[currentLegIndex].startTime + legs[currentLegIndex].duration)
        {
            currentLegIndex++;
        }

        Leg leg = legs[currentLegIndex];
        float blendDuration = Mathf.Max(0.01f, directionSmoothingTime);
        float blend = Mathf.Clamp01((time - leg.startTime) / blendDuration);
        blend = blend * blend * (3f - 2f * blend);

        float delta = Mathf.DeltaAngle(leg.fromHeading * Mathf.Rad2Deg, leg.toHeading * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        return leg.fromHeading + delta * blend;
    }

    private void EnsureLegs(float time)
    {
        while (true)
        {
            Leg last = legs[legs.Count - 1];
            float end = last.startTime + last.duration;
            if (end > time + 1f)
                return;

            legs.Add(new Leg
            {
                startTime = end,
                duration = NextInterval(),
                fromHeading = last.toHeading,
                toHeading = NextHeading(last.toHeading)
            });
        }
    }

    private float NextHeading(float current)
    {
        float span = Mathf.PI * 2f - 2f * MinHeadingDelta;
        return WrapAngle(current + MinHeadingDelta + NextFloat() * span);
    }

    private static float WrapAngle(float angle)
    {
        angle %= Mathf.PI * 2f;
        if (angle < 0f)
            angle += Mathf.PI * 2f;
        return angle;
    }

    private float NextInterval()
    {
        float min = Mathf.Max(0.2f, minDirectionChangeInterval);
        float max = Mathf.Max(min, maxDirectionChangeInterval);
        return Mathf.Lerp(min, max, NextFloat());
    }

    private float NextFloat()
    {
        rngState = rngState * 1664525u + 1013904223u;
        return (rngState & 0x00FFFFFFu) / 16777216f;
    }

    private void ApplySurfacePose(float orbit, float vertical)
    {
        CapsuleBodySurface.Evaluate(
            bodyCapsule,
            orbit,
            vertical,
            out Vector3 capsuleLocalPosition,
            out Vector3 capsuleLocalNormal);

        Vector3 worldPosition = bodyCapsule.transform.TransformPoint(
            capsuleLocalPosition + capsuleLocalNormal * surfaceOffset);
        Vector3 worldNormal = bodyCapsule.transform.TransformDirection(capsuleLocalNormal).normalized;

        bullseye.position = worldPosition;

        Vector3 upHint = Mathf.Abs(Vector3.Dot(worldNormal, transform.up)) > 0.95f
            ? transform.forward
            : transform.up;
        bullseye.rotation = Quaternion.LookRotation(worldNormal, upHint);
    }

    private void ApplyOwnerVisibility()
    {
        if (bullseyeTarget == null)
            return;

        if (!IsOwner || (detachController != null && !detachController.IsAttached))
        {
            bullseyeTarget.SetVisibleToLocalViewer(true);
            return;
        }

        Camera cam = PlayerNetworkSetup.LocalOwnedCamera;
        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        if (cam == null || !cam.enabled)
        {
            bullseyeTarget.SetVisibleToLocalViewer(false);
            return;
        }

        float hideDistance = Mathf.Max(0f, hideFromOwnerCameraDistance);
        bool coveringCamera = Vector3.Distance(bullseye.position, cam.transform.position) < hideDistance;
        bullseyeTarget.SetVisibleToLocalViewer(!coveringCamera);
    }
}
