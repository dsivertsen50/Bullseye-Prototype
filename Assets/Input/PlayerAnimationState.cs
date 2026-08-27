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

    private PlayerMovement movement;
    private PlayerHealth health;
    private PlayerAimZoom aimZoom;
    private PlayerWeaponInventory inventory;
    private WeaponPresentationCoordinator coordinator;
    private Rigidbody body;
    private float throwUntil;

    public float Speed => speed.Value;
    public float ForwardSpeed => forwardSpeed.Value;
    public float StrafeSpeed => strafeSpeed.Value;
    public float VerticalVelocity => verticalVelocity.Value;
    public bool IsGrounded => isGrounded.Value;
    public bool IsSprinting => isSprinting.Value;
    public bool IsAiming => isAiming.Value;
    public bool IsThrowingGrenade => isThrowingGrenade.Value;
    public bool IsCrouching => movement != null && movement.IsCrouched;
    public bool IsProne => movement != null && movement.IsProne;
    public bool IsDolphinDiving => movement != null && movement.IsDolphinDiving;
    public float ProneMoveSpeed => IsProne ? Speed : 0f;
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

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        WriteLocomotion();
        WriteAiming();
        TickThrowPresentation();
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

    private static void WriteQuantized(NetworkVariable<float> variable, float value)
    {
        float quantized = Mathf.Round(value / SpeedQuantize) * SpeedQuantize;
        if (Mathf.Abs(quantized - variable.Value) < SpeedSendThreshold)
            return;

        variable.Value = quantized;
    }
}
