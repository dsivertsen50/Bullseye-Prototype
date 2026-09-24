using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Drives the external full-body Animator from gameplay state.
/// First-person weapon animation stays on WeaponPresentationController.
/// </summary>
[DefaultExecutionOrder(100)]
public class PlayerThirdPersonAnimator : MonoBehaviour
{
    private const string StandingIdleState = "Standing Idle";

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int MoveSpeedHash = Animator.StringToHash("MoveSpeed");
    private static readonly int LocomotionPlaySpeedHash = Animator.StringToHash("LocomotionPlaySpeed");
    private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int ForwardSpeedHash = Animator.StringToHash("ForwardSpeed");
    private static readonly int StrafeSpeedHash = Animator.StringToHash("StrafeSpeed");
    private static readonly int VerticalVelocityHash = Animator.StringToHash("VerticalVelocity");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsProneHash = Animator.StringToHash("IsProne");
    private static readonly int IsSprintingHash = Animator.StringToHash("IsSprinting");
    private static readonly int IsDolphinDivingHash = Animator.StringToHash("IsDolphinDiving");
    private static readonly int IsClimbingHash = Animator.StringToHash("IsClimbing");
    private static readonly int ClimbSpeedHash = Animator.StringToHash("ClimbSpeed");
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
    private static readonly int TurnSpeedHash = Animator.StringToHash("TurnSpeed");
    private static readonly int IsTurningLeftHash = Animator.StringToHash("IsTurningLeft");
    private static readonly int IsTurningRightHash = Animator.StringToHash("IsTurningRight");
    private static readonly int IsAirborneHash = Animator.StringToHash("IsAirborne");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int JumpFromSprintHash = Animator.StringToHash("JumpFromSprint");
    private static readonly int JumpTriggerHash = Animator.StringToHash("Jump");
    private static readonly int AimWeightHash = Animator.StringToHash("AimWeight");
    private static readonly int WeaponPoseWeightHash = Animator.StringToHash("WeaponPoseWeight");
    private static readonly int WeaponPoseStateHash = Animator.StringToHash("WeaponPoseState");
    private static readonly int PoseCategoryHash = Animator.StringToHash("PoseCategory");

    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private PlayerAnimationState animationState;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WeaponPresentationCoordinator coordinator;
    [SerializeField] private ThirdPersonWeaponRig thirdPersonRig;
    [SerializeField] private ThirdPersonWeaponPoseBinder poseBinder;
    [SerializeField] private bool evaluateHumanoidAnimation = true;
    [SerializeField] private float parameterDampTime = 0.08f;
    [SerializeField] private float walkReferenceSpeed = 3.25f;
    [SerializeField] private float sprintReferenceSpeed = 9f;
    [SerializeField] private float crouchReferenceSpeed = 2.2f;
    [SerializeField] private Vector2 locomotionPlaySpeedRange = new Vector2(0.8f, 2.2f);

    [Header("Fall And Climb")]
    [SerializeField] private float climbReferenceSpeed = 3f;
    [SerializeField] private float climbAnimationSpeedMultiplier = 1f;

    [Header("Head Look")]
    [SerializeField] private bool enableHeadLook = true;
    [SerializeField, Range(0.1f, 0.7f)] private float headPitchScale = 0.38f;
    [SerializeField] private float maxHeadPitch = 22f;
    [SerializeField] private float maxHeadYaw = 12f;
    [SerializeField] private float headLookSmoothTime = 0.16f;
    [SerializeField, Range(0f, 0.6f)] private float neckShare = 0.32f;

    [Header("Debug (runtime)")]
    [SerializeField] private string debugAnimatorState;
    [SerializeField] private float debugMoveX;
    [SerializeField] private float debugMoveY;
    [SerializeField] private float debugMoveSpeed;
    [SerializeField] private bool debugIsSprinting;
    [SerializeField] private bool debugIsCrouching;
    [SerializeField] private bool debugIsProne;
    [SerializeField] private float debugTurnSpeed;
    [SerializeField] private string debugJump;
    [SerializeField] private bool debugIsFalling;
    [SerializeField] private bool debugIsClimbing;
    [SerializeField] private float debugClimbPlayback;

    private bool appliedDead;
    private bool wasDolphinDiving;
    private int lastJumpSerial;
    private bool hasJumpSerial;
    private float laggedPitch;
    private float laggedLookYaw;
    private float pitchLookVelocity;
    private float yawLookVelocity;
    private bool hasLaggedLookYaw;

    public Animator ThirdPersonAnimator => thirdPersonAnimator;

    private void Awake()
    {
        if (animationState == null)
            animationState = GetComponent<PlayerAnimationState>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (coordinator == null)
            coordinator = GetComponent<WeaponPresentationCoordinator>();
        if (thirdPersonRig == null)
            thirdPersonRig = GetComponent<ThirdPersonWeaponRig>();
        if (poseBinder == null)
            poseBinder = GetComponent<ThirdPersonWeaponPoseBinder>();
        if (thirdPersonAnimator == null)
            thirdPersonAnimator = FindThirdPersonAnimator();
        // REQ-049: locomotion stays on the base layer. Weapon holding is IK.

        // Keep the Animator enabled once the visual uses a T-pose Humanoid
        // rest pose. Disable this flag only if a bad avatar is reintroduced.
        if (thirdPersonAnimator != null && !evaluateHumanoidAnimation)
            thirdPersonAnimator.enabled = false;
        if (thirdPersonAnimator != null)
            thirdPersonAnimator.applyRootMotion = false;
    }

    private void LateUpdate()
    {
        if (thirdPersonAnimator == null)
            thirdPersonAnimator = FindThirdPersonAnimator();
        if (thirdPersonAnimator == null || !thirdPersonAnimator.enabled)
            return;

        bool dead = animationState != null ? animationState.IsDead : playerHealth != null && playerHealth.IsDead;
        if (dead)
        {
            FreezeCurrentVisualPose();
            return;
        }

        ApplyDeathFreeze(false);
        ApplyGameplayParameters(false);
        ApplyHeadLook(false);
    }

    public bool TryCapturePose(out int stateHash, out float normalizedTime)
    {
        stateHash = 0;
        normalizedTime = 0f;
        if (thirdPersonAnimator == null || !thirdPersonAnimator.isActiveAndEnabled)
            return false;

        AnimatorStateInfo info = thirdPersonAnimator.GetCurrentAnimatorStateInfo(0);
        stateHash = info.fullPathHash;
        normalizedTime = Mathf.Repeat(info.normalizedTime, 1f);
        return stateHash != 0;
    }

    public void FreezeCurrentVisualPose()
    {
        if (thirdPersonAnimator == null)
            return;

        thirdPersonAnimator.speed = 0f;
        thirdPersonAnimator.applyRootMotion = false;
        appliedDead = true;
    }

    public void ApplyFrozenPose(int stateHash, float normalizedTime)
    {
        FreezeCurrentVisualPose();
    }

    public void ResetAfterRespawn()
    {
        appliedDead = false;
        wasDolphinDiving = false;
        lastJumpSerial = animationState != null ? animationState.JumpSerial : 0;
        hasJumpSerial = true;
        laggedPitch = 0f;
        pitchLookVelocity = 0f;
        yawLookVelocity = 0f;
        hasLaggedLookYaw = false;
        if (thirdPersonAnimator == null)
            return;

        thirdPersonAnimator.speed = 1f;
        thirdPersonAnimator.applyRootMotion = false;
        if (thirdPersonAnimator.runtimeAnimatorController != null)
            thirdPersonAnimator.Play(StandingIdleState, 0, 0f);
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

        thirdPersonAnimator.applyRootMotion = false;
        float damp = Mathf.Max(0f, parameterDampTime);
        thirdPersonAnimator.SetFloat(MoveXHash, animationState.MoveX, damp, Time.deltaTime);
        thirdPersonAnimator.SetFloat(MoveYHash, animationState.MoveY, damp, Time.deltaTime);
        thirdPersonAnimator.SetFloat(MoveSpeedHash, animationState.MoveSpeed);
        thirdPersonAnimator.SetFloat(LocomotionPlaySpeedHash, ResolveLocomotionPlaySpeed());
        thirdPersonAnimator.SetBool(IsMovingHash, animationState.IsMoving);
        thirdPersonAnimator.SetFloat(SpeedHash, animationState.Speed);
        thirdPersonAnimator.SetFloat(ForwardSpeedHash, animationState.ForwardSpeed);
        thirdPersonAnimator.SetFloat(StrafeSpeedHash, animationState.StrafeSpeed);
        thirdPersonAnimator.SetFloat(VerticalVelocityHash, animationState.VerticalVelocity);
        thirdPersonAnimator.SetBool(IsGroundedHash, animationState.IsGrounded);
        thirdPersonAnimator.SetBool(IsCrouchingHash, animationState.IsCrouching);
        thirdPersonAnimator.SetBool(IsProneHash, animationState.IsProne);
        thirdPersonAnimator.SetBool(IsSprintingHash, animationState.IsSprinting && !animationState.IsProne && !animationState.IsCrouching);
        thirdPersonAnimator.SetBool(IsDolphinDivingHash, animationState.IsDolphinDiving);
        if (HasParameter(IsClimbingHash))
            thirdPersonAnimator.SetBool(IsClimbingHash, animationState.IsClimbing);
        if (HasParameter(ClimbSpeedHash))
            thirdPersonAnimator.SetFloat(ClimbSpeedHash, ResolveClimbPlayback());
        if (HasParameter(IsFallingHash))
            thirdPersonAnimator.SetBool(IsFallingHash, animationState.IsFalling);
        thirdPersonAnimator.SetFloat(ProneMoveSpeedHash, animationState.ProneMoveSpeed);
        thirdPersonAnimator.SetFloat(TurnSpeedHash, animationState.TurnSpeed, damp, Time.deltaTime);
        thirdPersonAnimator.SetBool(IsTurningLeftHash, animationState.IsTurningLeft);
        thirdPersonAnimator.SetBool(IsTurningRightHash, animationState.IsTurningRight);
        thirdPersonAnimator.SetBool(IsAirborneHash, animationState.IsAirborne);
        thirdPersonAnimator.SetBool(JumpFromSprintHash, animationState.JumpFromSprint);
        if (!hasJumpSerial)
        {
            lastJumpSerial = animationState.JumpSerial;
            hasJumpSerial = true;
        }
        else if (animationState.JumpSerial != lastJumpSerial)
        {
            lastJumpSerial = animationState.JumpSerial;
            thirdPersonAnimator.SetTrigger(JumpTriggerHash);
        }
        if (animationState.IsDolphinDiving && !wasDolphinDiving)
        {
            thirdPersonAnimator.SetTrigger(DolphinDiveTriggerHash);
            thirdPersonAnimator.SetTrigger(DiveTriggerHash);
        }
        wasDolphinDiving = animationState.IsDolphinDiving;
        thirdPersonAnimator.SetBool(IsAimingHash, animationState.IsAiming);
        thirdPersonAnimator.SetBool(IsReloadingHash, animationState.IsReloading);
        thirdPersonAnimator.SetBool(IsFiringHash, false);
        thirdPersonAnimator.SetBool(IsThrowingGrenadeHash, animationState.IsThrowingGrenade);
        thirdPersonAnimator.SetBool(IsDeadHash, dead);
        thirdPersonAnimator.SetFloat(AimPitchHash, animationState.AimPitch);
        thirdPersonAnimator.SetInteger(CurrentWeaponHash, Animator.StringToHash(animationState.CurrentWeapon));
        float aimWeight = thirdPersonRig != null ? thirdPersonRig.AimBlend : (animationState.IsAiming ? 1f : 0f);
        if (HasParameter(AimWeightHash))
            thirdPersonAnimator.SetFloat(AimWeightHash, aimWeight);

        if (HasParameter(WeaponPoseWeightHash))
            thirdPersonAnimator.SetFloat(WeaponPoseWeightHash, 0f);
        DisableLegacyWeaponPoseLayer();

        WriteDebug();
    }

    private float ResolveClimbPlayback()
    {
        if (animationState == null || !animationState.IsClimbing)
            return 0f;

        float reference = Mathf.Max(0.1f, climbReferenceSpeed);
        return (animationState.ClimbSpeed / reference) * climbAnimationSpeedMultiplier;
    }

    private float ResolveLocomotionPlaySpeed()
    {
        float reference = walkReferenceSpeed;
        if (animationState.IsProne)
            return 1f;
        if (animationState.IsCrouching)
            reference = crouchReferenceSpeed;
        else if (animationState.IsSprinting)
            reference = sprintReferenceSpeed;

        if (reference <= 0.01f || !animationState.IsMoving)
            return 1f;

        float speed = animationState.MoveSpeed / reference;
        return Mathf.Clamp(speed, locomotionPlaySpeedRange.x, locomotionPlaySpeedRange.y);
    }

    private void ApplyHeadLook(bool dead)
    {
        if (!enableHeadLook || dead || thirdPersonAnimator == null || !thirdPersonAnimator.enabled)
            return;

        bool climbing = animationState != null && animationState.IsClimbing;
        if (IsLocalOwner() && !climbing)
            return;
        if (!climbing && animationState != null &&
            (animationState.IsProne || animationState.IsDolphinDiving || animationState.IsFalling))
            return;

        Transform head = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return;

        TryGetComponent(out PlayerMovement movement);
        float bodyYaw = transform.eulerAngles.y;
        float lookYaw = bodyYaw;
        float pitchLimit = maxHeadPitch;
        float yawLimit = maxHeadYaw;
        float downLimit = maxHeadPitch;
        float targetPitch = 0f;

        if (climbing && movement != null)
        {
            bodyYaw = movement.LadderBodyYaw;
            lookYaw = bodyYaw + movement.LadderLookYaw;
            yawLimit = movement.HeadYawLimit;
            pitchLimit = movement.HeadLookUpLimit;
            downLimit = movement.HeadLookDownLimit;
            targetPitch = Mathf.Clamp(movement.LadderLookPitch, -downLimit, pitchLimit);
        }
        else if (animationState != null)
        {
            targetPitch = Mathf.Clamp(animationState.AimPitch * headPitchScale, -pitchLimit, pitchLimit);
            if (animationState.IsCrouching)
                targetPitch *= 0.55f;
        }

        if (!hasLaggedLookYaw)
        {
            laggedLookYaw = lookYaw;
            hasLaggedLookYaw = true;
        }

        float smooth = climbing && movement != null
            ? 1f / movement.HeadLookSmoothSpeed
            : Mathf.Max(0.04f, headLookSmoothTime);
        laggedPitch = Mathf.SmoothDamp(laggedPitch, targetPitch, ref pitchLookVelocity, smooth);
        laggedLookYaw = Mathf.SmoothDampAngle(laggedLookYaw, lookYaw, ref yawLookVelocity, smooth);
        float extraYaw = Mathf.Clamp(Mathf.DeltaAngle(bodyYaw, laggedLookYaw), -yawLimit, yawLimit);
        float extraPitch = Mathf.Clamp(laggedPitch, -downLimit, pitchLimit);

        Vector3 pitchAxis = transform.right;
        Vector3 yawAxis = Vector3.up;
        float chestShare = climbing ? 0.28f : 0f;
        Transform chest = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest != null && chestShare > 0.01f)
        {
            chest.rotation = Quaternion.AngleAxis(extraYaw * chestShare, yawAxis) *
                Quaternion.AngleAxis(extraPitch * chestShare, pitchAxis) *
                chest.rotation;
        }

        float neckPortion = Mathf.Clamp01(neckShare) * (1f - chestShare);
        Transform neck = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Neck);
        if (neck != null && neckPortion > 0.01f)
        {
            neck.rotation = Quaternion.AngleAxis(extraYaw * neckPortion, yawAxis) *
                Quaternion.AngleAxis(extraPitch * neckPortion, pitchAxis) *
                neck.rotation;
        }

        float headShare = Mathf.Max(0.05f, 1f - chestShare - neckPortion);
        head.rotation = Quaternion.AngleAxis(extraYaw * headShare, yawAxis) *
            Quaternion.AngleAxis(extraPitch * headShare, pitchAxis) *
            head.rotation;
    }

    private void WriteDebug()
    {
        debugMoveX = animationState.MoveX;
        debugMoveY = animationState.MoveY;
        debugMoveSpeed = animationState.MoveSpeed;
        debugIsSprinting = animationState.IsSprinting;
        debugIsCrouching = animationState.IsCrouching;
        debugIsProne = animationState.IsProne;
        debugTurnSpeed = animationState.TurnSpeed;
        debugJump = animationState.JumpFromSprint ? "sprint" : "idle";
        debugIsFalling = animationState.IsFalling;
        debugIsClimbing = animationState.IsClimbing;
        debugClimbPlayback = ResolveClimbPlayback();

        if (thirdPersonAnimator == null || !thirdPersonAnimator.isActiveAndEnabled)
        {
            debugAnimatorState = string.Empty;
            return;
        }

        AnimatorStateInfo info = thirdPersonAnimator.GetCurrentAnimatorStateInfo(0);
        debugAnimatorState = InfoToStateName(info);
    }

    private static string InfoToStateName(AnimatorStateInfo info)
    {
        if (info.IsName("Standing Idle")) return "Standing Idle";
        if (info.IsName("Standing Locomotion")) return "Standing Locomotion";
        if (info.IsName("Sprint Locomotion")) return "Sprint Locomotion";
        if (info.IsName("Sprint Forward")) return "Sprint Locomotion";
        if (info.IsName("Standing to Crouching")) return "Standing to Crouching";
        if (info.IsName("Crouching Idle")) return "Crouching Idle";
        if (info.IsName("Crouching Locomotion")) return "Crouching Locomotion";
        if (info.IsName("Crouching to Standing")) return "Crouching to Standing";
        if (info.IsName("Crouching to Prone")) return "Crouching to Prone";
        if (info.IsName("Prone Idle")) return "Prone Idle";
        if (info.IsName("Prone Forward")) return "Prone Forward";
        if (info.IsName("Prone Backward")) return "Prone Backward";
        if (info.IsName("Prone Left Turn")) return "Prone Left Turn";
        if (info.IsName("Prone Right Turn")) return "Prone Right Turn";
        if (info.IsName("Prone Locomotion")) return "Prone Locomotion";
        if (info.IsName("Prone to Crouching")) return "Prone to Crouching";
        if (info.IsName("Idle to Jump")) return "Idle to Jump";
        if (info.IsName("Sprint to Jump")) return "Sprint to Jump";
        if (info.IsName("Falling")) return "Falling";
        if (info.IsName("Ladder Climbing")) return "Ladder Climbing";
        if (info.IsName("Airborne Slack")) return "Airborne (pending)";
        if (info.IsName("Airborne (pending)")) return "Airborne (pending)";
        if (info.IsName("Dolphin Dive (pending)")) return "Dolphin Dive (pending)";
        return info.shortNameHash.ToString();
    }

    private bool HasParameter(int hash)
    {
        if (thirdPersonAnimator == null)
            return false;

        AnimatorControllerParameter[] parameters = thirdPersonAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash)
                return true;
        }

        return false;
    }

    private void DisableLegacyWeaponPoseLayer()
    {
        if (thirdPersonAnimator == null)
            return;

        int layer = thirdPersonAnimator.GetLayerIndex(ThirdPersonWeaponPoseBinder.LayerName);
        if (layer < 0)
            layer = thirdPersonAnimator.GetLayerIndex(ThirdPersonWeaponPoseBinder.LegacyLayerName);
        if (layer >= 0)
            thirdPersonAnimator.SetLayerWeight(layer, 0f);
    }

    private bool IsLocalOwner()
    {
        NetworkObject networkObject = GetComponent<NetworkObject>();
        return networkObject != null && networkObject.IsSpawned && networkObject.IsOwner;
    }

    private Animator FindThirdPersonAnimator()
    {
        Transform visual = transform.Find("VisualRoot");
        if (visual == null)
            return GetComponentInChildren<Animator>(true);

        return visual.GetComponentInChildren<Animator>(true);
    }
}
