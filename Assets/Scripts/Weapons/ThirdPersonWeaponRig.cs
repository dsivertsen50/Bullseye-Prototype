using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Third-person weapon hold, left-hand IK, and upper-body aim.
/// Reconstructs the pose locally from networked gameplay state.
/// Does not control firing, movement, or first-person weapons.
/// </summary>
[DefaultExecutionOrder(80)]
public class ThirdPersonWeaponRig : MonoBehaviour
{
    [SerializeField] private Animator thirdPersonAnimator;
    [SerializeField] private PlayerVisualRig visualRig;
    [SerializeField] private PlayerAnimationState animationState;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private WorldWeaponView worldWeapon;
    [SerializeField] private WeaponPresentationCoordinator coordinator;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Transform weaponHandAnchor;

    [Header("Blending")]
    [SerializeField] private float poseBlendTime = 0.14f;
    [SerializeField] private float aimBlendTime = 0.12f;
    [SerializeField] private float switchBlendTime = 0.1f;

    [Header("Poles")]
    [SerializeField] private Vector3 rightElbowPoleOffset = new(0.42f, -0.16f, -0.08f);
    [SerializeField] private Vector3 leftElbowPoleOffset = new(-0.28f, -0.18f, 0.02f);

    [Header("Debug")]
    [SerializeField] private bool drawGizmos;
    [SerializeField, Range(0f, 1f)] private float debugPoseWeight;
    [SerializeField] private string debugWeapon;

    private NetworkObject networkObject;
    private Transform upperChest;
    private Transform chest;
    private Transform spine;
    private Transform rightUpper;
    private Transform rightLower;
    private Transform rightHand;
    private Transform leftUpper;
    private Transform leftLower;
    private Transform leftHand;
    private Transform rightShoulder;
    private float poseWeight;
    private float aimBlend;
    private float switchBlend = 1f;
    private float recoil;
    private float recoilTarget;
    private Vector3 debugHoldPosition;
    private Quaternion debugHoldRotation;

    public float WeaponPoseWeight => poseWeight;

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        if (animationState == null)
            animationState = GetComponent<PlayerAnimationState>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (worldWeapon == null)
            worldWeapon = GetComponent<WorldWeaponView>();
        if (coordinator == null)
            coordinator = GetComponent<WeaponPresentationCoordinator>();
        ResolveHierarchy();
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
        if (!CanPose())
            return;

        ResolveHierarchy();
        if (thirdPersonAnimator == null || rightHand == null || weaponSocket == null)
            return;

        ThirdPersonWeaponPose pose = worldWeapon != null
            ? worldWeapon.ActiveThirdPersonPose
            : null;
        if (pose == null)
            pose = ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Pistol);

        float dt = Time.deltaTime;
        float targetWeight = ResolveTargetWeight(pose);
        poseWeight = MoveToward(poseWeight, targetWeight, dt, poseBlendTime);
        debugPoseWeight = poseWeight;

        float targetAim = animationState != null && animationState.IsAiming ? 1f : 0f;
        aimBlend = MoveToward(aimBlend, targetAim, dt, aimBlendTime);
        switchBlend = MoveToward(switchBlend, 1f, dt, switchBlendTime);

        float appliedWeight = poseWeight * switchBlend;
        TickRecoil(pose, dt);
        ApplySpineAim(pose, appliedWeight);
        ApplyUpperBodyRecoil(pose, appliedWeight);
        ApplyRightArmHold(pose, appliedWeight);
        AttachWeapon(pose);
        ApplyLeftHandSupport(pose, appliedWeight);
    }

    public void NotifyWeaponChanged()
    {
        switchBlend = 0f;
        worldWeapon?.BindThirdPersonVisual();
        debugWeapon = worldWeapon != null && worldWeapon.Definition != null
            ? worldWeapon.Definition.WeaponId
            : string.Empty;
    }

    public void ResetAfterRespawn()
    {
        poseWeight = 0f;
        aimBlend = 0f;
        switchBlend = 1f;
        recoil = 0f;
        recoilTarget = 0f;
    }

    private void OnFired()
    {
        recoilTarget = 1f;
    }

    private bool CanPose()
    {
        if (networkObject != null && networkObject.IsSpawned && networkObject.IsOwner)
            return false;
        if (playerHealth != null && playerHealth.AreDeathVisualsHidden)
            return false;
        if (worldWeapon != null && !worldWeapon.IsRemotePresentationActive)
            return false;
        return true;
    }

    private float ResolveTargetWeight(ThirdPersonWeaponPose pose)
    {
        if (playerHealth != null && playerHealth.IsDead)
            return 0f;
        if (animationState == null)
            return pose.defaultWeight;
        if (animationState.IsDolphinDiving)
            return pose.diveWeight;
        if (animationState.IsProne)
            return pose.proneWeight;
        if (animationState.IsAirborne)
            return pose.jumpWeight;
        if (animationState.IsSprinting)
            return pose.sprintWeight;
        if (animationState.IsCrouching)
            return pose.crouchWeight;
        return pose.defaultWeight;
    }

    private void ApplySpineAim(ThirdPersonWeaponPose pose, float weight)
    {
        if (weight <= 0.0001f || pose.spineAimWeight <= 0.0001f)
            return;

        float pitch = ResolveClampedPitch(pose);
        float spinePitch = pitch * pose.spineAimWeight * weight;
        spinePitch -= pose.aimRaisePitch * aimBlend * weight;
        if (Mathf.Abs(spinePitch) < 0.01f)
            return;

        Vector3 axis = transform.right;
        float chestShare = Mathf.Clamp01(pose.upperChestAimShare);
        float remainder = 1f - chestShare;
        if (spine != null)
            spine.rotation = Quaternion.AngleAxis(spinePitch * remainder * 0.35f, axis) * spine.rotation;
        if (chest != null)
            chest.rotation = Quaternion.AngleAxis(spinePitch * remainder * 0.65f, axis) * chest.rotation;
        if (upperChest != null)
            upperChest.rotation = Quaternion.AngleAxis(spinePitch * chestShare, axis) * upperChest.rotation;
    }

    private void ApplyRightArmHold(ThirdPersonWeaponPose pose, float weight)
    {
        if (rightUpper == null || rightLower == null || rightHand == null || upperChest == null)
            return;

        Vector3 holdLocal = Vector3.Lerp(pose.holdLocalPosition, pose.aimHoldLocalPosition, aimBlend);
        Quaternion holdLocalRot = Quaternion.Slerp(
            Quaternion.Euler(pose.holdLocalEuler),
            Quaternion.Euler(pose.aimHoldLocalEuler),
            aimBlend);

        float pitch = ResolveClampedPitch(pose);
        Quaternion pitchRot = Quaternion.AngleAxis(pitch, transform.right);
        // Use the player facing, not the animated chest. Crouch clips hunch
        // the torso, which would otherwise drag the weapon hold downward.
        Quaternion facing = ResolveUprightFacing();
        Vector3 holdWorld = upperChest.position + pitchRot * (facing * holdLocal);
        Quaternion holdWorldRot = pitchRot * facing * holdLocalRot;
        debugHoldPosition = holdWorld;
        debugHoldRotation = holdWorldRot;

        Vector3 pole = rightUpper.position + transform.rotation * rightElbowPoleOffset;
        ThirdPersonTwoBoneIK.Solve(rightUpper, rightLower, rightHand, holdWorld, pole, weight, pose.rightArmReach);
        ThirdPersonTwoBoneIK.ApplyEndRotation(rightHand, holdWorldRot, weight);
    }

    private void AttachWeapon(ThirdPersonWeaponPose pose)
    {
        if (weaponHandAnchor == null || weaponSocket == null)
            return;

        Vector3 localPos = Vector3.Lerp(pose.rightHandLocalPosition, pose.aimRightHandLocalPosition, aimBlend);
        Quaternion localRot = Quaternion.Slerp(
            Quaternion.Euler(pose.rightHandLocalEuler),
            Quaternion.Euler(pose.aimRightHandLocalEuler),
            aimBlend);

        if (weaponHandAnchor.parent != weaponSocket)
            weaponHandAnchor.SetParent(weaponSocket, false);

        weaponHandAnchor.localPosition = localPos;
        weaponHandAnchor.localRotation = localRot;
        ApplyWorldScale(weaponHandAnchor, weaponSocket, pose.rightHandLocalScale);
    }

    private void ApplyLeftHandSupport(ThirdPersonWeaponPose pose, float weight)
    {
        if (leftUpper == null || leftLower == null || leftHand == null)
            return;

        Transform target = worldWeapon != null ? worldWeapon.LeftHandIkTarget : null;
        if (target == null)
            return;

        Vector3 pole = leftUpper.position + transform.rotation * leftElbowPoleOffset;
        ThirdPersonTwoBoneIK.Solve(leftUpper, leftLower, leftHand, target.position, pole, weight, pose.leftArmReach);
        ThirdPersonTwoBoneIK.ApplyEndRotation(leftHand, target.rotation, weight * 0.85f);
    }

    private void TickRecoil(ThirdPersonWeaponPose pose, float dt)
    {
        if (recoil + 0.0001f < recoilTarget)
            recoil = MoveToward(recoil, recoilTarget, dt, pose.recoilInTime);
        else
        {
            recoilTarget = 0f;
            recoil = MoveToward(recoil, 0f, dt, pose.recoilOutTime);
        }
    }

    private void ApplyUpperBodyRecoil(ThirdPersonWeaponPose pose, float weight)
    {
        float amount = recoil * weight;
        if (amount <= 0.0001f)
            return;

        // Recoil is rebuilt from the animation pose every frame and capped at
        // one shot. Rapid fire refreshes the pulse instead of stacking.
        Vector3 right = transform.right;
        Vector3 up = transform.up;
        // Negative X rotation looks up in this project, so invert pitch
        // to kick the muzzle/upper body upward instead of downward.
        float pitch = -pose.recoilPitch * amount;
        float roll = pose.recoilRightRoll * amount;
        float yaw = pose.recoilYaw * amount;

        if (spine != null)
            spine.rotation = Quaternion.AngleAxis(pitch * 0.25f, right) * spine.rotation;
        if (chest != null)
            chest.rotation = Quaternion.AngleAxis(pitch * 0.35f, right) *
                Quaternion.AngleAxis(-roll * 0.35f, transform.forward) *
                chest.rotation;
        if (upperChest != null)
            upperChest.rotation = Quaternion.AngleAxis(pitch * 0.4f, right) *
                Quaternion.AngleAxis(-roll * 0.4f, transform.forward) *
                Quaternion.AngleAxis(yaw, up) *
                upperChest.rotation;
        if (rightShoulder != null)
            rightShoulder.rotation = Quaternion.AngleAxis(pitch * 0.35f, right) *
                Quaternion.AngleAxis(-roll, transform.forward) *
                rightShoulder.rotation;
        if (rightUpper != null)
            rightUpper.rotation = Quaternion.AngleAxis(pitch * 0.2f, right) * rightUpper.rotation;
    }

    private float ResolveClampedPitch(ThirdPersonWeaponPose pose)
    {
        float pitch = animationState != null ? animationState.AimPitch : 0f;
        return Mathf.Clamp(pitch, -pose.maxAimPitch, pose.maxAimPitch);
    }

    private Quaternion ResolveUprightFacing()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            return transform.rotation;
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void ResolveHierarchy()
    {
        if (visualRoot == null)
            visualRoot = transform.Find("VisualRoot");
        if (thirdPersonAnimator == null && visualRoot != null)
            thirdPersonAnimator = visualRoot.GetComponentInChildren<Animator>(true);
        if (thirdPersonAnimator == null)
            thirdPersonAnimator = GetComponentInChildren<Animator>(true);

        if (visualRig == null && thirdPersonAnimator != null)
            visualRig = thirdPersonAnimator.GetComponent<PlayerVisualRig>();
        if (visualRig == null)
            visualRig = GetComponentInChildren<PlayerVisualRig>(true);

        if (weaponSocket == null && visualRig != null)
            weaponSocket = visualRig.RightHandWeaponSocket;
        if (weaponSocket == null)
            weaponSocket = FindNamed(transform, "RightHandWeaponSocket");

        if (weaponHandAnchor == null && worldWeapon != null)
            weaponHandAnchor = worldWeapon.WeaponHandAnchor;
        if (weaponHandAnchor == null)
            weaponHandAnchor = transform.Find("WeaponHandAnchor");

        if (thirdPersonAnimator == null)
            return;

        if (upperChest == null)
            upperChest = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null)
            chest = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Chest);
        if (spine == null)
            spine = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Spine);
        if (upperChest == null)
            upperChest = chest;
        if (rightShoulder == null)
            rightShoulder = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
        if (rightUpper == null)
            rightUpper = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (rightLower == null)
            rightLower = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        if (rightHand == null)
            rightHand = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (leftUpper == null)
            leftUpper = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (leftLower == null)
            leftLower = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        if (leftHand == null)
            leftHand = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
    }

    private static void ApplyWorldScale(Transform child, Transform parent, Vector3 worldScale)
    {
        Vector3 parentScale = parent.lossyScale;
        child.localScale = new Vector3(
            worldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            worldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    private static float MoveToward(float current, float target, float dt, float duration)
    {
        if (duration <= 0.0001f)
            return target;
        return Mathf.MoveTowards(current, target, dt / duration);
    }

    private static Transform FindNamed(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(debugHoldPosition, 0.03f);
        Gizmos.DrawLine(debugHoldPosition, debugHoldPosition + debugHoldRotation * Vector3.forward * 0.12f);
        if (weaponSocket != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(weaponSocket.position, 0.02f);
        }
    }
#endif
}
