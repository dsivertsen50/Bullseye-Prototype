using Unity.Netcode;
using UnityEngine;

public partial class PlayerMovement
{
    private const float FallAscendGrace = 0.2f;

    [Header("Fall")]
    [SerializeField] private float fallingAnimationDelay = 2f;
    [SerializeField] private float bullseyeFallDetachDistance = 6.1f;

    [Header("Fall / Debug")]
    [SerializeField] private bool debugGrounded;
    [SerializeField] private bool debugIsFalling;
    [SerializeField] private float debugFallDuration;
    [SerializeField] private float debugFallStartHeight;
    [SerializeField] private float debugCurrentVerticalDrop;
    [SerializeField] private float debugClimbSpeed;
    [SerializeField] private float debugLastLandingFallDistance;
    [SerializeField] private bool debugBullseyeDetachTriggered;

    private readonly NetworkVariable<bool> falling = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private BullseyeDetachController fallDetachController;
    private float fallDuration;
    private float fallStartY;
    private bool trackingFall;
    private float fallAscendGrace;

    public bool IsFalling => falling.Value;
    public float FallDuration => fallDuration;
    public float FallStartHeight => fallStartY;
    public float CurrentVerticalDrop => trackingFall
        ? Mathf.Max(0f, fallStartY - transform.position.y)
        : 0f;
    public float LastLandingFallDistance { get; private set; }
    public bool LastLandingDetachedBullseye { get; private set; }
    public float BullseyeFallDetachDistance => bullseyeFallDetachDistance;

    private void TickFallTracking()
    {
        WriteFallDebug();

        if (IsClimbing || wallRunning || dolphinDiving.Value)
        {
            ClearFallTracking();
            return;
        }

        if (grounded)
        {
            if (trackingFall)
                ResolveFallLanding();
            ClearFallTracking();
            return;
        }

        float verticalVelocity = rb != null ? rb.linearVelocity.y : 0f;
        bool descending = verticalVelocity < 0f;
        if (descending)
        {
            if (!trackingFall)
            {
                trackingFall = true;
                fallStartY = transform.position.y;
                fallDuration = 0f;
            }

            fallAscendGrace = 0f;
            fallDuration += Time.deltaTime;
            bool wasFalling = falling.Value;
            SetFallingNetworked(fallDuration >= Mathf.Max(0.05f, fallingAnimationDelay));
            if (!wasFalling && falling.Value && Debug.isDebugBuild)
            {
                Debug.Log(
                    "Grounded: " + grounded +
                    "\nVertical Velocity: " + verticalVelocity.ToString("0.0") +
                    "\nFall Duration: " + fallDuration.ToString("0.00") +
                    "\nClimbing: " + IsClimbing +
                    "\nFalling Animator Param: true",
                    this);
            }
            WriteFallDebug();
            return;
        }

        fallAscendGrace += Time.deltaTime;
        if (fallAscendGrace >= FallAscendGrace)
            ClearFallTracking();
    }

    private void ResolveFallLanding()
    {
        float distance = Mathf.Max(0f, fallStartY - transform.position.y);
        LastLandingFallDistance = distance;
        debugLastLandingFallDistance = distance;
        LastLandingDetachedBullseye = false;
        debugBullseyeDetachTriggered = false;

        if (distance <= bullseyeFallDetachDistance)
            return;

        LastLandingDetachedBullseye = RequestFallImpactDetach(distance);
        debugBullseyeDetachTriggered = LastLandingDetachedBullseye;
        if (LastLandingDetachedBullseye && Debug.isDebugBuild)
            Debug.Log($"Large Fall Detected: {distance:0.0}m\nBullseye detached due to FallImpact", this);
    }

    private bool RequestFallImpactDetach(float fallDistance)
    {
        if (fallDetachController == null)
            fallDetachController = GetComponent<BullseyeDetachController>();
        if (fallDetachController == null || !fallDetachController.IsAttached)
            return false;
        if (!IsSpawned)
            return false;

        if (IsServer)
            return fallDetachController.TryDetachFromFallImpact();

        RequestFallImpactDetachServerRpc(fallDistance);
        return true;
    }

    [ServerRpc]
    private void RequestFallImpactDetachServerRpc(float fallDistance)
    {
        if (fallDistance <= bullseyeFallDetachDistance)
            return;

        if (fallDetachController == null)
            fallDetachController = GetComponent<BullseyeDetachController>();
        fallDetachController?.TryDetachFromFallImpact();
    }

    private void ClearFallTracking()
    {
        trackingFall = false;
        fallDuration = 0f;
        fallAscendGrace = 0f;
        SetFallingNetworked(false);
        WriteFallDebug();
    }

    private void SetFallingNetworked(bool value)
    {
        if (!IsSpawned || !IsOwner)
            return;
        if (falling.Value == value)
            return;

        falling.Value = value;
    }

    private void WriteFallDebug()
    {
        debugGrounded = grounded;
        debugIsFalling = falling.Value;
        debugFallDuration = fallDuration;
        debugFallStartHeight = fallStartY;
        debugCurrentVerticalDrop = CurrentVerticalDrop;
        debugIsClimbing = IsClimbing;
        debugClimbSpeed = ClimbSpeed;
    }
}
