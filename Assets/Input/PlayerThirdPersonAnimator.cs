using UnityEngine;

/// <summary>
/// Drives the external full-body Animator from gameplay state.
/// First-person weapon animation stays on WeaponPresentationController.
/// </summary>
[DefaultExecutionOrder(100)]
public class PlayerThirdPersonAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ForwardSpeedHash = Animator.StringToHash("ForwardSpeed");
    private static readonly int StrafeSpeedHash = Animator.StringToHash("StrafeSpeed");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsProneHash = Animator.StringToHash("IsProne");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsDolphinDivingHash = Animator.StringToHash("IsDolphinDiving");
    private static readonly int ProneMoveSpeedHash = Animator.StringToHash("ProneMoveSpeed");
    private static readonly int DolphinDiveTriggerHash = Animator.StringToHash("DolphinDive");
    private static readonly int DiveTriggerHash = Animator.StringToHash("DiveTrigger");
    private static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
    private static readonly int IsReloadingHash = Animator.StringToHash("IsReloading");
    private static readonly int IsFiringHash = Animator.StringToHash("IsFiring");
    private static readonly int IsThrowingGrenadeHash = Animator.StringToHash("IsThrowingGrenade");
    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int AimPitchHash = Animator.StringToHash("AimPitch");
    private static readonly int CurrentWeaponHash = Animator.StringToHash("CurrentWeapon");
    private static readonly int FireTriggerHash = Animator.StringToHash("Fire");

    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private PlayerAnimationState animationState;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WeaponPresentationCoordinator coordinator;
    [SerializeField] private float firePresentationDuration = 0.12f;
    [SerializeField] private bool evaluateHumanoidAnimation = true;

    private bool appliedDead;
    private float fireUntil;
    private bool wasDolphinDiving;

    public Animator ThirdPersonAnimator => thirdPersonAnimator;

    private void Awake()
    {
        if (animationState == null)
            animationState = GetComponent<PlayerAnimationState>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (coordinator == null)
            coordinator = GetComponent<WeaponPresentationCoordinator>();
        if (thirdPersonAnimator == null)
            thirdPersonAnimator = FindThirdPersonAnimator();

        // Keep the Animator enabled once the visual uses a T-pose Humanoid
        // rest pose. Disable this flag only if a bad avatar is reintroduced.
        if (thirdPersonAnimator != null && !evaluateHumanoidAnimation)
            thirdPersonAnimator.enabled = false;
    }

    private void OnEnable()
    {
        if (coordinator != null)
            coordinator.Fired += OnFired;
    }

    private void OnDisable()
    {
        if (coordinator != null)
            coordinator.Fired -= OnFired;
    }

    private void LateUpdate()
    {
        if (thirdPersonAnimator == null)
            thirdPersonAnimator = FindThirdPersonAnimator();
        if (thirdPersonAnimator == null || !thirdPersonAnimator.enabled)
            return;

        bool dead = animationState != null ? animationState.IsDead : playerHealth != null && playerHealth.IsDead;
        ApplyDeathFreeze(dead);
        ApplyGameplayParameters(dead);
    }

    public void ResetAfterRespawn()
    {
        fireUntil = 0f;
        appliedDead = false;
        wasDolphinDiving = false;
        if (thirdPersonAnimator == null)
            return;

        thirdPersonAnimator.speed = 1f;
        thirdPersonAnimator.applyRootMotion = false;
        if (thirdPersonAnimator.runtimeAnimatorController != null)
            thirdPersonAnimator.Play("Idle", 0, 0f);
    }

    private void ApplyDeathFreeze(bool dead)
    {
        if (dead)
        {
            if (!appliedDead)
            {
                thirdPersonAnimator.speed = 0f;
                appliedDead = true;
            }

            return;
        }

        if (!appliedDead && Mathf.Abs(thirdPersonAnimator.speed - 1f) < 0.001f)
            return;

        thirdPersonAnimator.speed = 1f;
        if (appliedDead)
            ResetAfterRespawn();
        appliedDead = false;
    }

    private void ApplyGameplayParameters(bool dead)
    {
        if (animationState == null)
            return;

        bool firing = Time.time < fireUntil;
        thirdPersonAnimator.SetFloat(SpeedHash, animationState.Speed);
        thirdPersonAnimator.SetFloat(ForwardSpeedHash, animationState.ForwardSpeed);
        thirdPersonAnimator.SetFloat(StrafeSpeedHash, animationState.StrafeSpeed);
        thirdPersonAnimator.SetFloat(VerticalVelocityHash, animationState.VerticalVelocity);
        thirdPersonAnimator.SetBool(IsGroundedHash, animationState.IsGrounded);
        thirdPersonAnimator.SetBool(IsCrouchingHash, animationState.IsCrouching);
        thirdPersonAnimator.SetBool(IsProneHash, animationState.IsProne);
        thirdPersonAnimator.SetBool(IsSprintingHash, animationState.IsSprinting && !animationState.IsProne);
        thirdPersonAnimator.SetBool(IsDolphinDivingHash, animationState.IsDolphinDiving);
        thirdPersonAnimator.SetFloat(ProneMoveSpeedHash, animationState.ProneMoveSpeed);
        if (animationState.IsDolphinDiving && !wasDolphinDiving)
        {
            thirdPersonAnimator.SetTrigger(DolphinDiveTriggerHash);
            thirdPersonAnimator.SetTrigger(DiveTriggerHash);
        }
        wasDolphinDiving = animationState.IsDolphinDiving;
        thirdPersonAnimator.SetBool(IsAimingHash, animationState.IsAiming);
        thirdPersonAnimator.SetBool(IsReloadingHash, animationState.IsReloading);
        thirdPersonAnimator.SetBool(IsFiringHash, firing);
        thirdPersonAnimator.SetBool(IsThrowingGrenadeHash, animationState.IsThrowingGrenade);
        thirdPersonAnimator.SetBool(IsDeadHash, dead);
        thirdPersonAnimator.SetFloat(AimPitchHash, animationState.AimPitch);
        thirdPersonAnimator.SetInteger(CurrentWeaponHash, Animator.StringToHash(animationState.CurrentWeapon));
    }

    private void OnFired()
    {
        fireUntil = Time.time + Mathf.Max(0.02f, firePresentationDuration);
        if (thirdPersonAnimator == null || !thirdPersonAnimator.gameObject.activeInHierarchy)
            return;

        if (thirdPersonAnimator.runtimeAnimatorController == null)
            return;

        thirdPersonAnimator.SetTrigger(FireTriggerHash);
    }

    private Animator FindThirdPersonAnimator()
    {
        Transform visual = transform.Find("VisualRoot");
        if (visual == null)
            return GetComponentInChildren<Animator>(true);

        return visual.GetComponentInChildren<Animator>(true);
    }
}
