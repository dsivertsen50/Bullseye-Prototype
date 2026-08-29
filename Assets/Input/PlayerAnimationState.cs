using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner-authored gameplay animation state for third-person presentation.
/// Does not network bones or first-person weapon transforms. Animation
/// reflects gameplay; it does not control firing, reloading, or movement.
/// </summary>
public class PlayerAnimationState : NetworkBehaviour
{
    private const float SpeedQuantize = 0.05f;
    private const float SpeedSendThreshold = 0.08f;
    private const float TurnQuantize = 5f;
    private const float TurnSendThreshold = 8f;
    private const float ThrowPresentationDuration = 0.45f;

    private readonly NetworkVariable<float> speed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> forwardSpeed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> strafeSpeed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> verticalVelocity = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> isGrounded = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> isSprinting = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> isAiming = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> isThrowingGrenade = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<float> turnSpeed = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<int> jumpSerial = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> jumpFromSprint = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    [Header("Locomotion Presentation")]
    [SerializeField] private float movingThreshold = 0.2f;
    [SerializeField] private float moveDirectionDeadzone = 0.08f;
    [SerializeField] private float proneTurnThreshold = 45f;

    [Header("Debug (runtime)")]
    [SerializeField] private float debugMoveX;
    [SerializeField] private float debugMoveY;
    [SerializeField] private float debugMoveSpeed;
    [SerializeField] private bool debugIsMoving;
    [SerializeField] private bool debugIsSprinting;
    [SerializeField] private bool debugIsCrouching;
    [SerializeField] private bool debugIsProne;
    [SerializeField] private bool debugIsGrounded;
    [SerializeField] private float debugTurnSpeed;
    [SerializeField] private bool debugIsTurningLeft;
    [SerializeField] private bool debugIsTurningRight;

    private PlayerMovement movement;
    private PlayerHealth health;
    private PlayerAimZoom aimZoom;
    private PlayerWeaponInventory inventory;
    private WeaponPresentationCoordinator coordinator;
    private Rigidbody body;
    private float throwUntil;
    private float lastYaw;
    private bool hasLastYaw;

    public float Speed => speed.Value;
    public float ForwardSpeed => forwardSpeed.Value;
    public float StrafeSpeed => strafeSpeed.Value;
    public float VerticalVelocity => verticalVelocity.Value;
    public float MoveSpeed => speed.Value;
    public float MoveX => ReadMoveDirection().x;
    public float MoveY => ReadMoveDirection().y;
    public bool IsMoving => speed.Value >= movingThreshold;
    public bool IsGrounded => isGrounded.Value;
    public bool IsSprinting => isSprinting.Value;
    public bool IsAiming => isAiming.Value;
    public bool IsThrowingGrenade => isThrowingGrenade.Value;
    public bool IsCrouching => movement != null && movement.IsCrouched;
    public bool IsProne => movement != null && movement.IsProne;
    public bool IsDolphinDiving => movement != null && movement.IsDolphinDiving;
    public float ProneMoveSpeed => IsProne ? Speed : 0f;
    public float TurnSpeed => turnSpeed.Value;
    public bool IsTurningLeft => !IsMoving && turnSpeed.Value <= -proneTurnThreshold;
    public bool IsTurningRight => !IsMoving && turnSpeed.Value >= proneTurnThreshold;
    public bool IsAirborne => !isGrounded.Value;
    public int JumpSerial => jumpSerial.Value;
    public bool JumpFromSprint => jumpFromSprint.Value;
    public bool IsDead => health != null && health.IsDead;
    public bool IsReloading => inventory != null && inventory.IsReloading;
    public float AimPitch => coordinator != null ? coordinator.AimPitch : 0f;
    public string CurrentWeapon => coordinator != null
        ? coordinator.CurrentWeaponId
        : string.Empty;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<PlayerHealth>();
        aimZoom = GetComponent<PlayerAimZoom>();
        inventory = GetComponent<PlayerWeaponInventory>();
        coordinator = GetComponent<WeaponPresentationCoordinator>();
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (movement != null)
            movement.Jumped += OnJumped;
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.Jumped -= OnJumped;
    }

    private void Update()
    {
        if (IsSpawned && IsOwner)
        {
            WriteLocomotion();
            WriteAiming();
            TickThrowPresentation();
        }

        WriteDebug();
    }

    public void NotifyThrowStarted()
    {
        if (!IsSpawned || !IsOwner)
            return;

        throwUntil = Time.time + ThrowPresentationDuration;
        if (!isThrowingGrenade.Value)
            isThrowingGrenade.Value = true;
    }

    private void WriteLocomotion()
    {
        Vector3 worldVelocity = body != null ? body.linearVelocity : Vector3.zero;
        Vector3 planar = new Vector3(worldVelocity.x, 0f, worldVelocity.z);
        Vector3 local = transform.InverseTransformDirection(planar);

        WriteQuantized(speed, planar.magnitude);
        WriteQuantized(forwardSpeed, local.z);
        WriteQuantized(strafeSpeed, local.x);
        WriteQuantized(verticalVelocity, worldVelocity.y);

        bool grounded = movement == null || movement.Grounded;
        if (isGrounded.Value != grounded)
            isGrounded.Value = grounded;

        bool sprinting = movement != null && movement.IsSprinting;
        if (isSprinting.Value != sprinting)
            isSprinting.Value = sprinting;

        WriteTurnSpeed();
    }

    private void OnJumped()
    {
        if (!IsSpawned || !IsOwner)
            return;

        bool sprintJump = movement != null && movement.LastJumpFromSprint;
        if (jumpFromSprint.Value != sprintJump)
            jumpFromSprint.Value = sprintJump;
        jumpSerial.Value++;
    }

    private void WriteTurnSpeed()
    {
        float yaw = transform.eulerAngles.y;
        if (!hasLastYaw)
        {
            lastYaw = yaw;
            hasLastYaw = true;
            return;
        }

        float delta = Mathf.DeltaAngle(lastYaw, yaw);
        lastYaw = yaw;
        float angular = Time.deltaTime > 0.0001f ? delta / Time.deltaTime : 0f;
        WriteQuantized(turnSpeed, angular, TurnQuantize, TurnSendThreshold);
    }

    private void WriteAiming()
    {
        bool aiming = aimZoom != null && aimZoom.IsAiming;
        if (isAiming.Value != aiming)
            isAiming.Value = aiming;
    }

    private void TickThrowPresentation()
    {
        if (!isThrowingGrenade.Value)
            return;

        if (Time.time < throwUntil)
            return;

        isThrowingGrenade.Value = false;
        throwUntil = 0f;
    }

    private Vector2 ReadMoveDirection()
    {
        Vector2 planar = new Vector2(strafeSpeed.Value, forwardSpeed.Value);
        float magnitude = planar.magnitude;
        if (magnitude < moveDirectionDeadzone)
            return Vector2.zero;

        return planar / magnitude;
    }

    private void WriteDebug()
    {
        debugMoveX = MoveX;
        debugMoveY = MoveY;
        debugMoveSpeed = MoveSpeed;
        debugIsMoving = IsMoving;
        debugIsSprinting = IsSprinting;
        debugIsCrouching = IsCrouching;
        debugIsProne = IsProne;
        debugIsGrounded = IsGrounded;
        debugTurnSpeed = TurnSpeed;
        debugIsTurningLeft = IsTurningLeft;
        debugIsTurningRight = IsTurningRight;
    }

    private static void WriteQuantized(NetworkVariable<float> variable, float value)
    {
        WriteQuantized(variable, value, SpeedQuantize, SpeedSendThreshold);
    }

    private static void WriteQuantized(
        NetworkVariable<float> variable,
        float value,
        float quantize,
        float sendThreshold)
    {
        float quantized = Mathf.Round(value / quantize) * quantize;
        if (Mathf.Abs(quantized - variable.Value) < sendThreshold)
            return;

        variable.Value = quantized;
    }
}
