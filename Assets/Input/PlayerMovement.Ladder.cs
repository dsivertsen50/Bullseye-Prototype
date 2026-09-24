using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement
{
    private const float ClimbAlignSpeed = 8f;
    private const float ClimbEnterDot = 0.2f;
    private const float ClimbLostGrace = 0.18f;
    private const float ClimbExitSnapLimit = 1.6f;

    [Header("Ladder Climb")]
    [SerializeField] private float ladderRotationSpeed = 720f;
    [SerializeField] private float ladderJumpAwayForce = 6.5f;
    [SerializeField] private float ladderJumpUpForce = 8f;
    [SerializeField] private float ladderReattachCooldown = 0.4f;
    [SerializeField, Tooltip("Moves the climb mesh left of the ladder. Applied in the character's local space so looking around cannot orbit the body.")]
    private float climbMeshLeftOffset = 0.14f;
    [SerializeField, Tooltip("Pulls the climb mesh back out of the ladder. Applied in the character's local space.")]
    private float climbMeshBackOffset = 0.22f;
    [SerializeField] private float ladderMaxCameraYaw = 70f;
    [SerializeField] private float ladderLookUpLimit = 60f;
    [SerializeField] private float ladderLookDownLimit = 50f;
    [SerializeField] private float headYawLimit = 45f;
    [SerializeField] private float headLookUpLimit = 35f;
    [SerializeField] private float headLookDownLimit = 30f;
    [SerializeField] private float headLookSmoothSpeed = 10f;

    [Header("Ladder Climb / Debug")]
    [SerializeField] private bool debugIsClimbing;
    [SerializeField] private bool debugLadderLook;
    [SerializeField] private string debugClimbStatus = string.Empty;
    [SerializeField] private float debugLadderBodyYaw;
    [SerializeField] private float debugLadderCameraYaw;
    [SerializeField] private float debugHeadYaw;
    [SerializeField] private float debugHeadPitch;

    private readonly NetworkVariable<bool> climbing = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> climbFacingYaw = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> climbLookYaw = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> climbLookPitch = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> networkedClimbSpeed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly HashSet<LadderClimbable> nearbyLadders = new();
    private readonly List<LadderClimbable> nearbyBuffer = new(4);

    private LadderClimbable currentLadder;
    private LadderClimbable lastLadder;
    private float ladderReattachRemaining;
    private float climbLostTimer;
    private float climbInputSpeed;
    private bool climbVisualApplied;
    private bool climbCollisionsIgnored;

    public bool IsClimbing => climbing.Value;
    public LadderClimbable CurrentLadder => currentLadder;
    public float ClimbSpeed => IsSpawned && !IsOwner ? networkedClimbSpeed.Value : climbInputSpeed;
    public float LadderBodyYaw => climbFacingYaw.Value;
    public float LadderLookYaw => climbLookYaw.Value;
    public float LadderLookPitch => climbLookPitch.Value;
    public float LadderMaxCameraYaw => Mathf.Clamp(ladderMaxCameraYaw, 0f, 89f);
    public float LadderLookUpLimit => Mathf.Clamp(ladderLookUpLimit, 0f, 89f);
    public float LadderLookDownLimit => Mathf.Clamp(ladderLookDownLimit, 0f, 89f);
    public float HeadYawLimit => Mathf.Clamp(headYawLimit, 0f, 80f);
    public float HeadLookUpLimit => Mathf.Clamp(headLookUpLimit, 0f, 80f);
    public float HeadLookDownLimit => Mathf.Clamp(headLookDownLimit, 0f, 80f);
    public float HeadLookSmoothSpeed => Mathf.Max(0.5f, headLookSmoothSpeed);

    private void TickClimbLifecycle()
    {
        PruneNearbyLadders();
        if (ladderReattachRemaining > 0f)
            ladderReattachRemaining -= Time.deltaTime;

        if (!IsClimbing)
        {
            currentLadder = null;
            debugIsClimbing = false;
            debugClimbStatus = nearbyLadders.Count > 0 ? "Near ladder." : "No ladder.";
            return;
        }

        if (currentLadder == null)
        {
            EndClimb(false);
            return;
        }

        debugIsClimbing = true;
        debugClimbStatus = "Climbing.";
    }

    private void TryBeginClimb()
    {
        if (IsClimbing || nearbyLadders.Count == 0)
            return;
        if (playerHealth != null && playerHealth.IsDead)
            return;
        if (LocalPlayerMenuState.IsOpen(this))
            return;
        if (dolphinDiving.Value)
            return;
        if (knockbackTimer > 0f)
            return;

        if (!TrySelectEnterableLadder(out LadderClimbable ladder))
            return;

        BeginClimb(ladder);
    }

    private bool TrySelectEnterableLadder(out LadderClimbable selected)
    {
        selected = null;
        float bestScore = float.MinValue;
        Vector2 input = ReadMoveInput();
        Vector3 wish = Vector3.ProjectOnPlane(
            transform.forward * input.y + transform.right * input.x,
            Vector3.up);
        Vector3 planarVel = HorizontalVelocity();

        foreach (LadderClimbable ladder in nearbyLadders)
        {
            if (ladder == null)
                continue;
            if (ladder == lastLadder && ladderReattachRemaining > 0f)
                continue;

            float score = ScoreLadderEntry(ladder, wish, planarVel, input);
            if (score <= 0f)
                continue;
            if (score > bestScore)
            {
                bestScore = score;
                selected = ladder;
            }
        }

        return selected != null;
    }

    private float ScoreLadderEntry(
        LadderClimbable ladder,
        Vector3 wish,
        Vector3 planarVel,
        Vector2 input)
    {
        Vector3 face = Vector3.ProjectOnPlane(ladder.FaceNormal, Vector3.up);
        if (face.sqrMagnitude < 0.0001f)
            face = ladder.FaceNormal;
        face.Normalize();
        Vector3 towardLadder = -face;

        float wishToward = wish.sqrMagnitude > 0.0001f
            ? Vector3.Dot(wish.normalized, towardLadder)
            : 0f;
        float velToward = planarVel.sqrMagnitude > 0.01f
            ? Vector3.Dot(planarVel.normalized, towardLadder)
            : 0f;

        bool movingToward = wishToward >= ClimbEnterDot || velToward >= 0.35f;
        bool nearTop = ladder.IsNearTop(transform.position);
        bool fallingIn = !grounded && rb != null && rb.linearVelocity.y < -0.35f;
        bool climbingDownFromTop = nearTop && (input.y < -0.2f || fallingIn);
        bool grabMid = movingToward && Vector3.Dot(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized, towardLadder) > 0.15f;

        if (!movingToward && !climbingDownFromTop)
            return 0f;

        if (climbingDownFromTop)
            return 2.5f + Mathf.Abs(input.y);
        if (grabMid)
            return 1f + Mathf.Max(wishToward, velToward) - ladder.DistanceToSurface(transform.position) * 0.1f;

        return 0f;
    }

    private void BeginClimb(LadderClimbable ladder)
    {
        if (ladder == null)
            return;

        currentLadder = ladder;
        lastLadder = ladder;
        climbLostTimer = 0f;
        climbInputSpeed = 0f;
        SetClimbingNetworked(true);
        WriteClimbFacing(ladder);

        EndSlide();
        EndWallRun();
        CancelDolphinDive(false);
        weaponInventory?.InterruptReload();
        IsSprinting = false;
        sprintToggledOn = false;
        hasJumped = false;
        LastJumpFromSprint = false;
        ClearFallTracking();

        if (IsSpawned && IsOwner)
        {
            if (prone.Value)
                prone.Value = false;
            if (crouched.Value)
                crouched.Value = false;
        }

        IgnoreLadderCollisions(ladder, true);

        if (rb != null && !rb.isKinematic)
        {
            rb.useGravity = false;
            Vector3 aligned = ladder.GetAlignedPosition(transform.position);
            Vector3 blended = Vector3.Lerp(transform.position, aligned, 0.4f);
            rb.position = blended;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        LogMovement("Began ladder climb.");
    }

    private void TickClimbPhysics()
    {
        if (!IsClimbing || currentLadder == null || rb == null || rb.isKinematic)
            return;

        rb.useGravity = false;

        Vector2 input = LocalPlayerMenuState.IsOpen(this) ? Vector2.zero : ReadMoveInput();
        float vertical = input.y;
        climbInputSpeed = vertical * currentLadder.ClimbSpeed;

        Vector3 axis = currentLadder.ClimbAxis;
        float dt = Time.fixedDeltaTime;
        Vector3 proposed = transform.position + axis * (climbInputSpeed * dt);
        Vector3 aligned = currentLadder.GetAlignedPosition(proposed);

        Vector3 toAligned = aligned - rb.position;
        float along = Vector3.Dot(toAligned, axis);
        Vector3 offAxis = toAligned - axis * along;
        float maxOff = ClimbAlignSpeed * dt;
        if (offAxis.magnitude > maxOff)
            offAxis = offAxis.normalized * maxOff;

        Vector3 target = rb.position + axis * along + offAxis;
        Vector3 velocity = (target - rb.position) / Mathf.Max(0.0001f, dt);
        rb.linearVelocity = velocity;

        PublishClimbSpeed();
    }

    private void TickClimbExits()
    {
        if (!IsClimbing || currentLadder == null)
            return;

        if (!nearbyLadders.Contains(currentLadder))
        {
            climbLostTimer += Time.fixedDeltaTime;
            if (climbLostTimer >= ClimbLostGrace)
            {
                LogMovement("Ladder climb ended — left volume.");
                EndClimb(false);
            }

            return;
        }

        climbLostTimer = 0f;
        Vector2 input = LocalPlayerMenuState.IsOpen(this) ? Vector2.zero : ReadMoveInput();

        if (input.y > 0.15f && currentLadder.IsAtTop(transform.position))
        {
            ExitClimbTop();
            return;
        }

        if (input.y < -0.15f && currentLadder.IsAtBottom(transform.position))
        {
            ExitClimbBottom();
        }
    }

    private void ExitClimbTop()
    {
        if (currentLadder == null)
        {
            EndClimb(false);
            return;
        }

        Vector3 exit = currentLadder.GetTopExitPosition(transform.position);
        Vector3 delta = exit - transform.position;
        if (delta.magnitude > ClimbExitSnapLimit)
            exit = transform.position + delta.normalized * ClimbExitSnapLimit;

        LadderClimbable ladder = currentLadder;
        EndClimb(false);

        if (rb != null && !rb.isKinematic)
        {
            rb.position = exit;
            Vector3 away = Vector3.ProjectOnPlane(ladder.FaceNormal, Vector3.up);
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            rb.linearVelocity = away.normalized * 1.4f + Vector3.up * 1.25f;
        }

        LogMovement("Exited ladder at top.");
    }

    private void ExitClimbBottom()
    {
        Vector3 exit = currentLadder != null
            ? currentLadder.GetBottomExitPosition(transform.position)
            : transform.position;

        EndClimb(false);

        if (rb != null && !rb.isKinematic)
        {
            Vector3 delta = exit - rb.position;
            if (delta.sqrMagnitude > 0.0001f && delta.magnitude < ClimbExitSnapLimit)
                rb.position = exit;
            Vector3 vel = rb.linearVelocity;
            vel.y = Mathf.Min(vel.y, 0f);
            rb.linearVelocity = vel;
        }

        LogMovement("Exited ladder at bottom.");
    }

    private void PerformLadderJump()
    {
        if (!IsClimbing || currentLadder == null)
            return;
        if (!currentLadder.AllowJumpOff)
            return;

        Vector3 away = Vector3.ProjectOnPlane(currentLadder.FaceNormal, Vector3.up);
        if (away.sqrMagnitude < 0.0001f)
            away = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
        away.Normalize();

        Vector3 jumpVelocity = away * Mathf.Max(0f, ladderJumpAwayForce)
            + Vector3.up * Mathf.Max(0f, ladderJumpUpForce);

        EndClimb(true);

        if (rb != null && !rb.isKinematic)
            rb.linearVelocity = jumpVelocity;

        hasJumped = true;
        LastJumpFromSprint = false;
        jumpAvailable = false;
        jumpCooldownTimer = jumpCooldown;
        jumpIgnoreTimer = JumpIgnoreDuration;
        grounded = false;
        EndSlide();

        if (bullseyeMover != null)
            bullseyeMover.NotifyJump();

        Jumped?.Invoke();
        LogMovement("Jumped off ladder.");
    }

    private void EndClimb(bool jumpedAway)
    {
        if (!IsClimbing && currentLadder == null)
            return;

        LadderClimbable used = currentLadder;
        IgnoreLadderCollisions(used, false);
        currentLadder = null;
        climbLostTimer = 0f;
        climbInputSpeed = 0f;
        PublishClimbSpeed();
        SetLadderLook(0f, 0f);
        SetClimbingNetworked(false);
        lastLadder = used;
        ladderReattachRemaining = Mathf.Max(0.05f, ladderReattachCooldown);

        if (rb != null && !rb.isKinematic && !jumpedAway)
            rb.useGravity = true;
        else if (rb != null && !rb.isKinematic)
            rb.useGravity = true;
    }

    private void ClearClimbState()
    {
        IgnoreLadderCollisions(currentLadder, false);
        nearbyLadders.Clear();
        currentLadder = null;
        lastLadder = null;
        climbLostTimer = 0f;
        climbInputSpeed = 0f;
        PublishClimbSpeed();
        SetLadderLook(0f, 0f);
        ladderReattachRemaining = 0f;
        SetClimbingNetworked(false);
        RestoreClimbVisual();
        debugIsClimbing = false;
        debugClimbStatus = string.Empty;
    }

    private void TickClimbVisual()
    {
        if (climbing.Value && IsMovementOwner)
            AlignBodyToLadder();

        if (bodyVisual == null)
            return;

        if (climbing.Value)
        {
            Vector3 local = standingBodyPosition + new Vector3(-climbMeshLeftOffset, 0f, -climbMeshBackOffset);
            bodyVisual.localPosition = local;
            bodyVisual.localRotation = standingBodyRotation;
            PlantClimbFeet();
            climbVisualApplied = true;
            DrawLadderDebug();
            return;
        }

        RestoreClimbVisual();
    }

    private void AlignBodyToLadder()
    {
        if (currentLadder != null)
            WriteClimbFacing(currentLadder);

        Vector3 facing = LadderFacingDirection();
        if (facing.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(facing, Vector3.up);
        float step = Mathf.Max(1f, ladderRotationSpeed) * Time.deltaTime;
        Quaternion next = Quaternion.RotateTowards(transform.rotation, target, step);
        Quaternion cameraWorld = playerCamera != null ? playerCamera.rotation : Quaternion.identity;
        transform.rotation = next;
        if (playerCamera != null)
            playerCamera.rotation = cameraWorld;
        if (rb != null && !rb.isKinematic)
        {
            rb.rotation = next;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private Vector3 LadderFacingDirection()
    {
        if (currentLadder != null)
        {
            Vector3 toward = Vector3.ProjectOnPlane(-currentLadder.FaceNormal, Vector3.up);
            if (toward.sqrMagnitude > 0.0001f)
                return toward.normalized;
        }

        return Quaternion.Euler(0f, climbFacingYaw.Value, 0f) * Vector3.forward;
    }

    public void SetLadderLook(float relativeYaw, float pitch)
    {
        debugLadderCameraYaw = relativeYaw;
        debugHeadPitch = pitch;
        if (!IsSpawned || !IsOwner)
            return;

        if (Mathf.Abs(Mathf.DeltaAngle(climbLookYaw.Value, relativeYaw)) >= 0.5f)
            climbLookYaw.Value = relativeYaw;
        if (Mathf.Abs(climbLookPitch.Value - pitch) >= 0.5f)
            climbLookPitch.Value = pitch;
    }

    private void RestoreClimbVisual()
    {
        if (!climbVisualApplied)
            return;

        RestoreBodyVisualPose();
        climbVisualApplied = false;
    }

    private void PlantClimbFeet()
    {
        Animator animator = bodyVisual.GetComponentInChildren<Animator>();
        if (animator == null || playerCapsule == null)
            return;

        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        if (leftFoot == null || rightFoot == null)
            return;

        float footY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
        Vector3 center = transform.TransformPoint(playerCapsule.center);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        float height = playerCapsule.height * scaleY;
        float radius = playerCapsule.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.z));
        float floorY = center.y - Mathf.Max(0f, height * 0.5f - radius);
        bodyVisual.position += Vector3.up * (floorY - footY);
    }

    private void WriteClimbFacing(LadderClimbable ladder)
    {
        if (ladder == null || !IsSpawned || !IsOwner)
            return;

        Vector3 look = Vector3.ProjectOnPlane(-ladder.FaceNormal, Vector3.up);
        if (look.sqrMagnitude < 0.0001f)
            return;

        float yaw = Quaternion.LookRotation(look.normalized, Vector3.up).eulerAngles.y;
        debugLadderBodyYaw = yaw;
        if (Mathf.Abs(Mathf.DeltaAngle(climbFacingYaw.Value, yaw)) < 0.5f)
            return;

        climbFacingYaw.Value = yaw;
    }

    private void DrawLadderDebug()
    {
        if (!debugLadderLook && !Debug.isDebugBuild)
            return;
        if (!debugLadderLook)
            return;

        Vector3 origin = transform.position + Vector3.up * 1.4f;
        Vector3 ladderForward = LadderFacingDirection();
        Debug.DrawRay(origin, ladderForward * 1.2f, Color.red);
        Debug.DrawRay(origin, transform.forward * 1.2f, Color.blue);
        if (playerCamera != null)
            Debug.DrawRay(playerCamera.position, playerCamera.forward * 1.2f, Color.green);

        debugLadderBodyYaw = transform.eulerAngles.y;
        debugHeadYaw = climbLookYaw.Value;
    }

    private void PublishClimbSpeed()
    {
        if (!IsSpawned || !IsOwner)
            return;

        float value = Mathf.Abs(climbInputSpeed) < 0.08f ? 0f : climbInputSpeed;
        if (Mathf.Abs(value - networkedClimbSpeed.Value) < 0.05f)
            return;

        networkedClimbSpeed.Value = value;
    }

    private void SetClimbingNetworked(bool value)
    {
        if (!IsSpawned || !IsOwner)
            return;
        if (climbing.Value == value)
            return;
        climbing.Value = value;
    }

    private void IgnoreLadderCollisions(LadderClimbable ladder, bool ignore)
    {
        if (playerCapsule == null)
            return;

        if (!ignore && !climbCollisionsIgnored)
            return;

        if (ladder != null)
        {
            Collider[] solids = ladder.GetSolidColliders();
            for (int i = 0; i < solids.Length; i++)
            {
                if (solids[i] == null || solids[i] == playerCapsule)
                    continue;
                Physics.IgnoreCollision(playerCapsule, solids[i], ignore);
            }
        }

        climbCollisionsIgnored = ignore && ladder != null;
    }

    private void RegisterNearbyLadder(Collider other)
    {
        if (!LadderClimbable.TryGet(other, out LadderClimbable ladder))
            return;
        if (!ladder.IsClimbTrigger(other))
            return;
        nearbyLadders.Add(ladder);
    }

    private void UnregisterNearbyLadder(Collider other)
    {
        if (!LadderClimbable.TryGet(other, out LadderClimbable ladder))
            return;
        if (!ladder.IsClimbTrigger(other))
            return;
        nearbyLadders.Remove(ladder);
    }

    private void PruneNearbyLadders()
    {
        if (nearbyLadders.Count == 0)
            return;

        nearbyBuffer.Clear();
        foreach (LadderClimbable ladder in nearbyLadders)
        {
            if (ladder == null)
                nearbyBuffer.Add(ladder);
        }

        for (int i = 0; i < nearbyBuffer.Count; i++)
            nearbyLadders.Remove(nearbyBuffer[i]);
    }

    private bool IsCurrentLadderCollider(Collider collider)
    {
        return currentLadder != null && currentLadder.OwnsCollider(collider);
    }

    private static bool IsLadderGroundHit(Collider collider)
    {
        if (!LadderClimbable.TryGet(collider, out _))
            return false;
        return collider.gameObject.name != "TopPlatform";
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterNearbyLadder(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterNearbyLadder(other);
    }

    private void OnTriggerExit(Collider other)
    {
        UnregisterNearbyLadder(other);
    }
}
