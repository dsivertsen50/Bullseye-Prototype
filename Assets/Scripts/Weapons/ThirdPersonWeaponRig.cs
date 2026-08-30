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
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Transform weaponHandAnchor;

    [Header("Blending")]
    [SerializeField] private float poseBlendTime = 0.14f;
    [SerializeField] private float aimBlendTime = 0.12f;
    [SerializeField] private float switchBlendTime = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
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
    private float poseWeight;
    private float aimBlend;
    private float switchBlend = 1f;
    private Vector3 debugGunPosition;
    private Quaternion debugGunRotation;
    private Vector3 debugRightHandPosition;
    private Quaternion debugRightHandRotation;
    private Vector3 debugLeftHandPosition;
    private Quaternion debugLeftHandRotation;
    private Vector3 debugRightElbowPole;
    private Vector3 debugLeftElbowPole;
    private Quaternion debugFacing;
    private Quaternion debugPitchRot;

    public float WeaponPoseWeight => poseWeight;
    public bool DrawPoseGuides => drawGizmos;
    public float AimBlend => aimBlend;
    public WeaponDefinition ActiveDefinition => worldWeapon != null ? worldWeapon.Definition : null;

    public bool TryGetPoseGuide(out ThirdPersonPoseGuide guide)
    {
        guide = default;
        if (upperChest == null)
            return false;

        guide.gunPosition = debugGunPosition;
        guide.gunRotation = debugGunRotation;
        guide.rightHandPosition = debugRightHandPosition;
        guide.rightHandRotation = debugRightHandRotation;
        guide.leftHandPosition = debugLeftHandPosition;
        guide.leftHandRotation = debugLeftHandRotation;
        guide.rightElbowPole = debugRightElbowPole;
        guide.leftElbowPole = debugLeftElbowPole;
        guide.rightUpperPosition = rightUpper != null ? rightUpper.position : debugRightHandPosition;
        guide.leftUpperPosition = leftUpper != null ? leftUpper.position : debugLeftHandPosition;
        guide.definition = ActiveDefinition;
        guide.aimBlend = aimBlend;
        return true;
    }

    public Vector3 WorldToChest(Vector3 world)
    {
        if (upperChest == null)
            return Vector3.zero;

        return Quaternion.Inverse(debugPitchRot * debugFacing) * (world - upperChest.position);
    }

    public Vector3 WorldRotToChestEuler(Quaternion world)
    {
        Quaternion local = Quaternion.Inverse(debugPitchRot * debugFacing) * world;
        return local.eulerAngles;
    }

    public float ElbowYawFromPole(Vector3 upperPosition, Vector3 handPosition, Vector3 pole, bool left)
    {
        return SignedElbowYaw(upperPosition, handPosition, pole, left);
    }

    private void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        if (animationState == null)
            animationState = GetComponent<PlayerAnimationState>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (worldWeapon == null)
            worldWeapon = GetComponent<WorldWeaponView>();
        ResolveHierarchy();
    }

    private void LateUpdate()
    {
        if (!CanPose())
            return;

        ResolveHierarchy();
        if (thirdPersonAnimator == null || rightHand == null || weaponHandAnchor == null)
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
        CacheFacing(pose);
        ApplySpineAim(pose, appliedWeight);
        PlaceGun(pose);
        ApplyArm(pose, appliedWeight, right: true);
        ApplyArm(pose, appliedWeight, right: false);
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

        // Look pitch only. ADS raise is done by the aimed hand/gun
        // positions so the torso does not lean back.
        float pitch = ResolveClampedPitch(pose);
        float spinePitch = pitch * pose.spineAimWeight * weight;
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

    private void CacheFacing(ThirdPersonWeaponPose pose)
    {
        // Use the player facing, not the animated chest. Crouch clips hunch
        // the torso, which would otherwise drag holds downward.
        debugFacing = ResolveUprightFacing();
        debugPitchRot = Quaternion.AngleAxis(ResolveClampedPitch(pose), transform.right);
    }

    private void PlaceGun(ThirdPersonWeaponPose pose)
    {
        if (weaponHandAnchor == null || upperChest == null)
            return;

        Vector3 rightLocal = Vector3.Lerp(pose.rightHandPosition, pose.aimRightHandPosition, aimBlend);
        Vector3 gunOffset = Vector3.Lerp(
            pose.gunPosition - pose.rightHandPosition,
            pose.aimGunPosition - pose.aimRightHandPosition,
            aimBlend);
        Quaternion localRot = Quaternion.Slerp(
            Quaternion.Euler(pose.gunEuler),
            Quaternion.Euler(pose.aimGunEuler),
            aimBlend);

        if (weaponHandAnchor.parent != transform)
            weaponHandAnchor.SetParent(transform, false);

        Vector3 worldPos = ChestToWorld(rightLocal + gunOffset);
        Quaternion worldRot = ChestToWorldRotation(localRot);
        debugGunPosition = worldPos;
        debugGunRotation = worldRot;

        weaponHandAnchor.SetPositionAndRotation(worldPos, worldRot);
        weaponHandAnchor.localScale = pose.gunScale;
    }

    private void ApplyArm(ThirdPersonWeaponPose pose, float weight, bool right)
    {
        Transform upper = right ? rightUpper : leftUpper;
        Transform lower = right ? rightLower : leftLower;
        Transform hand = right ? rightHand : leftHand;
        if (upper == null || lower == null || hand == null || upperChest == null)
            return;

        Vector3 localPos = right
            ? Vector3.Lerp(pose.rightHandPosition, pose.aimRightHandPosition, aimBlend)
            : Vector3.Lerp(pose.leftHandPosition, pose.aimLeftHandPosition, aimBlend);
        Quaternion localRot = right
            ? Quaternion.Slerp(Quaternion.Euler(pose.rightWristEuler), Quaternion.Euler(pose.aimRightWristEuler), aimBlend)
            : Quaternion.Slerp(Quaternion.Euler(pose.leftWristEuler), Quaternion.Euler(pose.aimLeftWristEuler), aimBlend);

        Vector3 worldPos = ChestToWorld(localPos);
        Quaternion worldRot = ChestToWorldRotation(localRot);
        float yaw = right ? pose.rightElbowYaw : pose.leftElbowYaw;
        Vector3 pole = ElbowPole(upper.position, worldPos, yaw, !right);
        float reach = right ? pose.rightArmReach : pose.leftArmReach;

        if (right)
        {
            debugRightHandPosition = worldPos;
            debugRightHandRotation = worldRot;
            debugRightElbowPole = pole;
        }
        else
        {
            debugLeftHandPosition = worldPos;
            debugLeftHandRotation = worldRot;
            debugLeftElbowPole = pole;
        }

        ThirdPersonTwoBoneIK.Solve(upper, lower, hand, worldPos, pole, weight, reach);
        ThirdPersonTwoBoneIK.ApplyEndRotation(hand, worldRot, weight);
    }

    private Vector3 ChestToWorld(Vector3 local)
    {
        return upperChest.position + debugPitchRot * (debugFacing * local);
    }

    private Quaternion ChestToWorldRotation(Quaternion local)
    {
        return debugPitchRot * debugFacing * local;
    }

    private Vector3 ElbowPole(Vector3 upperPosition, Vector3 handPosition, float yaw, bool left)
    {
        Vector3 toHand = handPosition - upperPosition;
        if (toHand.sqrMagnitude < 0.0001f)
            toHand = transform.forward;
        toHand.Normalize();

        Vector3 side = Vector3.Cross(toHand, transform.up);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(toHand, transform.right);
        side.Normalize();
        if (left)
            side = -side;

        Vector3 poleDir = Quaternion.AngleAxis(yaw, toHand) * side;
        return upperPosition + poleDir * 0.4f;
    }

    private float SignedElbowYaw(Vector3 upperPosition, Vector3 handPosition, Vector3 pole, bool left)
    {
        Vector3 toHand = handPosition - upperPosition;
        if (toHand.sqrMagnitude < 0.0001f)
            return 0f;
        toHand.Normalize();

        Vector3 side = Vector3.Cross(toHand, transform.up);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(toHand, transform.right);
        side.Normalize();
        if (left)
            side = -side;

        Vector3 poleDir = Vector3.ProjectOnPlane(pole - upperPosition, toHand);
        if (poleDir.sqrMagnitude < 0.0001f)
            return 0f;

        return Vector3.SignedAngle(side, poleDir.normalized, toHand);
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
    private void OnDrawGizmos()
    {
        if (!drawGizmos || !CanPose())
            return;

        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
        Gizmos.DrawWireSphere(debugGunPosition, 0.02f);
        Gizmos.DrawLine(debugGunPosition, debugGunPosition + debugGunRotation * Vector3.forward * 0.16f);

        Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.95f);
        Gizmos.DrawWireSphere(debugRightHandPosition, 0.028f);
        if (rightUpper != null)
        {
            Gizmos.DrawLine(rightUpper.position, debugRightElbowPole);
            Gizmos.DrawLine(debugRightElbowPole, debugRightHandPosition);
        }

        Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.95f);
        Gizmos.DrawWireSphere(debugLeftHandPosition, 0.028f);
        if (leftUpper != null)
        {
            Gizmos.DrawLine(leftUpper.position, debugLeftElbowPole);
            Gizmos.DrawLine(debugLeftElbowPole, debugLeftHandPosition);
        }
    }
#endif
}

public struct ThirdPersonPoseGuide
{
    public Vector3 gunPosition;
    public Quaternion gunRotation;
    public Vector3 rightHandPosition;
    public Quaternion rightHandRotation;
    public Vector3 leftHandPosition;
    public Quaternion leftHandRotation;
    public Vector3 rightElbowPole;
    public Vector3 leftElbowPole;
    public Vector3 rightUpperPosition;
    public Vector3 leftUpperPosition;
    public WeaponDefinition definition;
    public float aimBlend;
}
