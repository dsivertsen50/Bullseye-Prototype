using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Third-person weapon hold, left-hand IK, and upper-body aim.
/// Parents the world weapon to the right-hand socket and reconstructs
/// the pose locally from networked gameplay state.
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
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform leftElbowHint;
    [SerializeField] private Transform rightElbowHint;

    [Header("Blending")]
    [SerializeField] private float poseBlendTime = 0.14f;
    [SerializeField] private float aimBlendTime = 0.12f;
    [SerializeField] private float sprintBlendTime = 0.16f;
    [SerializeField] private float proneBlendTime = 0.18f;
    [SerializeField] private float switchBlendTime = 0.1f;
    [SerializeField, Range(0f, 1f)] private float ikWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float leftIkWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float aimIkWeight = 1f;

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
    private float sprintBlend;
    private float proneBlend;
    private float switchBlend = 1f;
    private float recoilWeight;
    private float previewPitch;
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

    public float WeaponPoseWeight => poseWeight * switchBlend;
    public bool DrawPoseGuides => drawGizmos;
    public float AimBlend => aimBlend;
    public Transform WeaponSocket => weaponSocket;
    public Transform AimTarget => aimTarget;
    public WeaponDefinition ActiveDefinition => worldWeapon != null ? worldWeapon.Definition : null;
    public bool IsEditorPreview { get; private set; }

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
        guide.sprintBlend = sprintBlend;
        guide.proneBlend = proneBlend;
        ThirdPersonWeaponPose pose = ActiveDefinition != null ? ActiveDefinition.ThirdPersonPose : null;
        Transform grip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : null;
        guide.leftHandFollowsGrip = pose != null &&
            (pose.leftHandFollowGrip || (pose.leftHandPosition.sqrMagnitude < 0.0001f && grip != null));
        return true;
    }

    public Vector3 WorldToChest(Vector3 world)
    {
        if (upperChest == null)
            return Vector3.zero;

        return Quaternion.Inverse(debugPitchRot * debugFacing) * (world - upperChest.position);
    }

    public Vector3 WorldToSocket(Vector3 world)
    {
        if (weaponSocket == null)
            return Vector3.zero;

        return Quaternion.Inverse(weaponSocket.rotation) * (world - weaponSocket.position);
    }

    public Vector3 WorldRotToSocketEuler(Quaternion world)
    {
        if (weaponSocket == null)
            return Vector3.zero;

        Quaternion extra = Quaternion.identity;
        ThirdPersonWeaponPose pose = ActiveDefinition != null ? ActiveDefinition.ThirdPersonPose : null;
        if (pose != null)
            extra = Quaternion.Euler(pose.gunEuler);

        Quaternion local = Quaternion.Inverse(weaponSocket.rotation * extra) * world;
        return local.eulerAngles;
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

    public bool TryGetLeftGripWorld(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        Transform grip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : null;
        if (grip == null)
            return false;

        position = grip.position;
        rotation = grip.rotation;
        return true;
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
        if (coordinator == null)
            coordinator = GetComponent<WeaponPresentationCoordinator>();
        ResolveHierarchy();
        EnsureGuideTransforms();
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
        if (thirdPersonAnimator == null || rightHand == null)
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

        bool sprinting = animationState != null && animationState.IsSprinting && !animationState.IsProne;
        bool aiming = animationState != null && animationState.IsAiming && !sprinting;
        bool prone = animationState != null && animationState.IsProne;
        aimBlend = MoveToward(aimBlend, aiming ? 1f : 0f, dt, aimBlendTime);
        sprintBlend = MoveToward(sprintBlend, sprinting ? 1f : 0f, dt, sprintBlendTime);
        proneBlend = MoveToward(proneBlend, prone ? 1f : 0f, dt, proneBlendTime);
        switchBlend = MoveToward(switchBlend, 1f, dt, switchBlendTime);
        recoilWeight = MoveToward(recoilWeight, 0f, dt, pose.recoilOutTime);

        float appliedWeight = poseWeight * switchBlend * ikWeight;
        CacheFacing(pose);
        ApplySpineAim(pose, appliedWeight);
        ApplyRightArm(pose, appliedWeight);
        worldWeapon?.AttachToSocket();
        ApplyLeftArm(pose, appliedWeight);
        UpdateGuideTransforms(pose);
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
        sprintBlend = 0f;
        proneBlend = 0f;
        switchBlend = 1f;
        recoilWeight = 0f;
    }

    public void BeginEditorPreview()
    {
        IsEditorPreview = true;
        drawGizmos = true;
        if (worldWeapon == null)
            worldWeapon = GetComponent<WorldWeaponView>();
        ResolveHierarchy();
        EnsureGuideTransforms();
    }

    public void ApplyEditorPreview(float aim, float sprint, float prone, float pitch, float weight = 1f)
    {
        IsEditorPreview = true;
        ResolveHierarchy();
        if (rightHand == null)
            return;

        ThirdPersonWeaponPose pose = worldWeapon != null
            ? worldWeapon.ActiveThirdPersonPose
            : null;
        if (pose == null)
            pose = ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Pistol);

        previewPitch = pitch;
        aimBlend = Mathf.Clamp01(aim);
        sprintBlend = Mathf.Clamp01(sprint);
        proneBlend = Mathf.Clamp01(prone);
        poseWeight = Mathf.Clamp01(weight);
        switchBlend = 1f;
        recoilWeight = 0f;
        debugPoseWeight = poseWeight;
        debugWeapon = ActiveDefinition != null ? ActiveDefinition.WeaponId : string.Empty;

        float appliedWeight = poseWeight * ikWeight;
        CacheFacing(pose);
        ApplySpineAim(pose, appliedWeight);
        ApplyRightArm(pose, appliedWeight);
        worldWeapon?.AttachToSocket();
        ApplyLeftArm(pose, appliedWeight);
        UpdateGuideTransforms(pose);
    }

    public void EndEditorPreview()
    {
        IsEditorPreview = false;
    }

    private void OnFired()
    {
        if (!CanPose())
            return;

        recoilWeight = 1f;
    }

    private bool CanPose()
    {
        if (IsEditorPreview)
            return true;
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
        pitch += pose.aimRaisePitch * aimBlend;
        pitch += pose.recoilPitch * recoilWeight;
        float spinePitch = pitch * pose.spineAimWeight * weight;
        float roll = pose.recoilRightRoll * recoilWeight * weight;
        float yaw = pose.recoilYaw * recoilWeight * weight;
        if (Mathf.Abs(spinePitch) < 0.01f && Mathf.Abs(roll) < 0.01f && Mathf.Abs(yaw) < 0.01f)
            return;

        Vector3 pitchAxis = transform.right;
        Vector3 yawAxis = transform.up;
        Vector3 rollAxis = transform.forward;
        float chestShare = Mathf.Clamp01(pose.upperChestAimShare);
        float remainder = 1f - chestShare;
        if (spine != null)
        {
            spine.rotation = Quaternion.AngleAxis(yaw * remainder * 0.35f, yawAxis) *
                Quaternion.AngleAxis(spinePitch * remainder * 0.35f, pitchAxis) *
                spine.rotation;
        }

        if (chest != null)
        {
            chest.rotation = Quaternion.AngleAxis(yaw * remainder * 0.65f, yawAxis) *
                Quaternion.AngleAxis(roll * 0.45f, rollAxis) *
                Quaternion.AngleAxis(spinePitch * remainder * 0.65f, pitchAxis) *
                chest.rotation;
        }

        if (upperChest != null)
        {
            upperChest.rotation = Quaternion.AngleAxis(yaw * chestShare, yawAxis) *
                Quaternion.AngleAxis(roll * 0.55f, rollAxis) *
                Quaternion.AngleAxis(spinePitch * chestShare, pitchAxis) *
                upperChest.rotation;
        }
    }

    private void CacheFacing(ThirdPersonWeaponPose pose)
    {
        debugFacing = ResolveUprightFacing();
        float pitch = ResolveClampedPitch(pose) + pose.aimRaisePitch * aimBlend;
        pitch = Mathf.Lerp(pitch, pose.ResolvedProneBodyPitch() + ResolveClampedPitch(pose) * 0.25f, proneBlend);
        debugPitchRot = Quaternion.AngleAxis(pitch, transform.right);
    }

    private void ApplyRightArm(ThirdPersonWeaponPose pose, float weight)
    {
        if (rightUpper == null || rightLower == null || rightHand == null || upperChest == null)
            return;

        Vector3 localPos = Vector3.Lerp(pose.rightHandPosition, pose.ResolvedSprintRightHand(), sprintBlend);
        localPos = Vector3.Lerp(localPos, pose.ResolvedProneRightHand(), proneBlend);
        localPos = Vector3.Lerp(localPos, pose.aimRightHandPosition, aimBlend * aimIkWeight);

        Quaternion localRot = Quaternion.Slerp(
            Quaternion.Euler(pose.rightWristEuler),
            Quaternion.Euler(pose.ResolvedSprintRightWrist()),
            sprintBlend);
        localRot = Quaternion.Slerp(localRot, Quaternion.Euler(pose.ResolvedProneRightWrist()), proneBlend);
        localRot = Quaternion.Slerp(localRot, Quaternion.Euler(pose.aimRightWristEuler), aimBlend * aimIkWeight);

        Vector3 worldPos = ChestToWorld(localPos);
        Quaternion worldRot = ChestToWorldRotation(localRot);
        Vector3 pole = ElbowPole(rightUpper.position, worldPos, pose.rightElbowYaw, false);

        debugRightHandPosition = worldPos;
        debugRightHandRotation = worldRot;
        debugRightElbowPole = pole;
        if (rightElbowHint != null)
            rightElbowHint.position = pole;

        ThirdPersonTwoBoneIK.Solve(rightUpper, rightLower, rightHand, worldPos, pole, weight, pose.rightArmReach);
        ThirdPersonTwoBoneIK.ApplyEndRotation(rightHand, worldRot, weight);
    }

    private void ApplyLeftArm(ThirdPersonWeaponPose pose, float weight)
    {
        if (leftUpper == null || leftLower == null || leftHand == null)
            return;

        Transform grip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : null;
        bool followGrip = pose.leftHandFollowGrip ||
                          (pose.leftHandPosition.sqrMagnitude < 0.0001f && grip != null);

        Vector3 worldPos;
        Quaternion worldRot;
        if (followGrip && grip != null)
        {
            worldPos = grip.position;
            worldRot = grip.rotation * Quaternion.Euler(
                Vector3.Lerp(pose.leftWristEuler, pose.aimLeftWristEuler, aimBlend));
        }
        else if (upperChest != null)
        {
            Vector3 localPos = Vector3.Lerp(pose.ResolvedLeftHand(), pose.ResolvedSprintLeftHand(), sprintBlend);
            localPos = Vector3.Lerp(localPos, pose.ResolvedProneLeftHand(), proneBlend);
            localPos = Vector3.Lerp(localPos, pose.ResolvedAimLeftHand(), aimBlend * aimIkWeight);

            Quaternion localRot = Quaternion.Slerp(
                Quaternion.Euler(pose.leftWristEuler),
                Quaternion.Euler(pose.ResolvedSprintLeftWrist()),
                sprintBlend);
            localRot = Quaternion.Slerp(localRot, Quaternion.Euler(pose.ResolvedProneLeftWrist()), proneBlend);
            localRot = Quaternion.Slerp(localRot, Quaternion.Euler(pose.aimLeftWristEuler), aimBlend * aimIkWeight);

            worldPos = ChestToWorld(localPos);
            worldRot = ChestToWorldRotation(localRot);
        }
        else
        {
            return;
        }

        float leftWeight = weight * leftIkWeight;
        if (sprintBlend > 0.01f)
            leftWeight *= Mathf.Lerp(1f, pose.ResolvedSprintLeftIkWeight(), sprintBlend);

        Vector3 pole = ElbowPole(leftUpper.position, worldPos, pose.leftElbowYaw, true);
        debugLeftHandPosition = worldPos;
        debugLeftHandRotation = worldRot;
        debugLeftElbowPole = pole;
        if (leftElbowHint != null)
            leftElbowHint.position = pole;

        ThirdPersonTwoBoneIK.Solve(leftUpper, leftLower, leftHand, worldPos, pole, leftWeight, pose.leftArmReach);
        ThirdPersonTwoBoneIK.ApplyEndRotation(leftHand, worldRot, leftWeight);
    }

    private void UpdateGuideTransforms(ThirdPersonWeaponPose pose)
    {
        if (worldWeapon != null && worldWeapon.WorldWeaponRoot != null)
        {
            debugGunPosition = worldWeapon.WorldWeaponRoot.position;
            debugGunRotation = worldWeapon.WorldWeaponRoot.rotation;
        }
        else if (weaponSocket != null)
        {
            debugGunPosition = weaponSocket.position;
            debugGunRotation = weaponSocket.rotation;
        }

        if (aimTarget == null)
            return;

        Vector3 origin = upperChest != null ? upperChest.position : transform.position + Vector3.up * 1.4f;
        aimTarget.position = origin + debugPitchRot * (debugFacing * Vector3.forward) * 2.2f;
        aimTarget.rotation = debugPitchRot * debugFacing;
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
        float pitch = IsEditorPreview
            ? previewPitch
            : animationState != null ? animationState.AimPitch : 0f;
        return Mathf.Clamp(pitch, -Mathf.Abs(pose.ResolvedMaxAimPitchDown()), Mathf.Abs(pose.maxAimPitch));
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
            weaponSocket = FindNamed(transform, "RightHandWeaponSocket")
                ?? FindNamed(transform, "WeaponSocket");

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

        EnsureWeaponSocket();
    }

    private void EnsureWeaponSocket()
    {
        if (weaponSocket != null || rightHand == null)
            return;

        Transform existing = rightHand.Find("WeaponSocket") ?? rightHand.Find("RightHandWeaponSocket");
        if (existing != null)
        {
            weaponSocket = existing;
            return;
        }

        GameObject socket = new GameObject("WeaponSocket");
        socket.transform.SetParent(rightHand, false);
        socket.transform.localPosition = Vector3.zero;
        socket.transform.localRotation = Quaternion.identity;
        socket.transform.localScale = Vector3.one;
        weaponSocket = socket.transform;
    }

    private void EnsureGuideTransforms()
    {
        Transform host = transform.Find("WorldWeaponRig");
        if (host == null)
        {
            GameObject rigGo = new GameObject("WorldWeaponRig");
            rigGo.transform.SetParent(transform, false);
            host = rigGo.transform;
        }

        if (aimTarget == null)
            aimTarget = FindNamed(transform, "AimTarget");
        if (aimTarget == null)
        {
            GameObject aim = new GameObject("AimTarget");
            aim.transform.SetParent(host, false);
            aimTarget = aim.transform;
        }

        if (leftElbowHint == null)
            leftElbowHint = FindNamed(transform, "LeftElbowHint");
        if (leftElbowHint == null)
        {
            GameObject hint = new GameObject("LeftElbowHint");
            hint.transform.SetParent(host, false);
            leftElbowHint = hint.transform;
        }

        if (rightElbowHint == null)
            rightElbowHint = FindNamed(transform, "RightElbowHint");
        if (rightElbowHint == null)
        {
            GameObject hint = new GameObject("RightElbowHint");
            hint.transform.SetParent(host, false);
            rightElbowHint = hint.transform;
        }
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

        if (weaponSocket != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.95f);
            Gizmos.DrawWireSphere(weaponSocket.position, 0.016f);
        }

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

        if (aimTarget != null)
        {
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(aimTarget.position, 0.03f);
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
    public float sprintBlend;
    public float proneBlend;
    public bool leftHandFollowsGrip;
}
