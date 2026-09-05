using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Weapon-first third-person hold. Locomotion keeps playing. The weapon sits
/// on ThirdPersonWeaponAnchor. Hands and elbows solve to Grip_R / Grip_L.
/// Does not control first-person weapons, firing, or movement.
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
    [SerializeField] private Transform weaponAnchor;
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform rightHandIkTarget;
    [SerializeField] private Transform leftHandIkTarget;
    [SerializeField] private Transform rightElbowHint;
    [SerializeField] private Transform leftElbowHint;
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private Rig weaponRig;
    [SerializeField] private TwoBoneIKConstraint rightHandIk;
    [SerializeField] private TwoBoneIKConstraint leftHandIk;
    [SerializeField] private MultiAimConstraint spineAim;

    [Header("Blending")]
    [SerializeField] private float poseBlendTime = 0.14f;
    [SerializeField] private float aimBlendTime = 0.12f;
    [SerializeField] private float sprintBlendTime = 0.16f;
    [SerializeField] private float proneBlendTime = 0.18f;
    [SerializeField] private float switchBlendTime = 0.1f;
    [SerializeField] private float reloadBlendTime = 0.12f;
    [SerializeField] private float lookPitchSmoothTime = 0.12f;
    [SerializeField, Range(0f, 1f)] private float ikWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float rightIkWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float leftIkWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float hintWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float aimIkWeight;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool showDebugOverlay;
    [SerializeField, Range(0f, 1f)] private float debugPoseWeight;
    [SerializeField] private string debugWeapon;
    [SerializeField] private string debugPoseCategory;
    [SerializeField] private string debugMovementState;
    [SerializeField] private float debugRigWeight;
    [SerializeField] private float debugRightIkWeight;
    [SerializeField] private float debugLeftIkWeight;

    private NetworkObject networkObject;
    private Transform upperChest;
    private string missingGripWarningId;
    private float poseWeight;
    private float aimBlend;
    private float sprintBlend;
    private float crouchBlend;
    private float proneBlend;
    private float reloadBlend;
    private float switchBlend = 1f;
    private float previewPitch;
    private float lookPitch;
    private float lookPitchVelocity;
    private ThirdPersonWeaponHoldPose blendedPose = ThirdPersonWeaponHoldPose.DefaultLongGun;
    private Vector3 debugGunPosition;
    private Quaternion debugGunRotation;

    public float WeaponPoseWeight => poseWeight * switchBlend;
    public bool DrawPoseGuides => drawGizmos;
    public float AimBlend => aimBlend;
    public float SprintBlend => sprintBlend;
    public float CrouchBlend => crouchBlend;
    public float ProneBlend => proneBlend;
    public float RightIkWeight => debugRightIkWeight;
    public float LeftIkWeight => debugLeftIkWeight;
    public float RigWeight => debugRigWeight;
    public Transform WeaponAnchor => weaponAnchor;
    public Transform WeaponSocket => weaponAnchor != null ? weaponAnchor : weaponSocket;
    public Transform AimTarget => aimTarget;
    public Transform RightHandIkTarget => rightHandIkTarget;
    public Transform LeftHandIkTarget => leftHandIkTarget;
    public Transform RightElbowHint => rightElbowHint;
    public Transform LeftElbowHint => leftElbowHint;
    public WeaponDefinition ActiveDefinition => worldWeapon != null ? worldWeapon.Definition : null;
    public bool IsEditorPreview { get; private set; }
    public ThirdPersonPoseCategory ActivePoseCategory =>
        ActiveDefinition != null ? ActiveDefinition.PoseCategory : ThirdPersonPoseCategory.Pistol;
    public ThirdPersonWeaponPoseClass ActivePoseClass =>
        ActiveDefinition != null ? ActiveDefinition.ThirdPersonHoldClass : ThirdPersonWeaponPoseClass.ShortGun;
    public ThirdPersonWeaponHoldPose ActiveHoldPose => blendedPose;

    public bool TryGetPoseGuide(out ThirdPersonPoseGuide guide)
    {
        guide = default;
        if (weaponAnchor == null && worldWeapon == null)
            return false;

        guide.gunPosition = debugGunPosition;
        guide.gunRotation = debugGunRotation;
        guide.socketPosition = weaponAnchor != null ? weaponAnchor.position : debugGunPosition;
        Transform rightGrip = worldWeapon != null ? worldWeapon.RightHandIkTarget : rightHandIkTarget;
        if (rightGrip != null)
        {
            guide.rightGripPosition = rightGrip.position;
            guide.rightGripRotation = rightGrip.rotation;
        }

        Transform leftGrip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : leftHandIkTarget;
        if (leftGrip != null)
        {
            guide.leftGripPosition = leftGrip.position;
            guide.leftGripRotation = leftGrip.rotation;
        }

        if (rightElbowHint != null)
            guide.rightElbowHintPosition = rightElbowHint.position;
        if (leftElbowHint != null)
            guide.leftElbowHintPosition = leftElbowHint.position;

        guide.definition = ActiveDefinition;
        guide.poseCategory = debugPoseCategory;
        guide.leftIkWeight = debugLeftIkWeight;
        guide.rightIkWeight = debugRightIkWeight;
        guide.rigWeight = debugRigWeight;
        return true;
    }

    public Vector3 WorldToAnchor(Vector3 world)
    {
        if (upperChest == null)
            return Vector3.zero;

        return Quaternion.Inverse(upperChest.rotation) * (world - upperChest.position);
    }

    public Vector3 WorldRotToAnchorEuler(Quaternion world)
    {
        if (upperChest == null)
            return Vector3.zero;

        Quaternion local = Quaternion.Inverse(upperChest.rotation) * world;
        return local.eulerAngles;
    }

    public Vector3 WorldToSocket(Vector3 world) => WorldToAnchor(world);
    public Vector3 WorldRotToSocketEuler(Quaternion world) => WorldRotToAnchorEuler(world);

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
        EnsureAnimationRig();
        DisableLegacyPoseLayer();
    }

    private void LateUpdate()
    {
        if (!CanPose())
        {
            ApplyRigWeights(0f, 0f, 0f, 0f, 0f);
            return;
        }

        ResolveHierarchy();
        if (thirdPersonAnimator == null || upperChest == null)
            return;

        WeaponDefinition definition = ActiveDefinition;
        float dt = Time.deltaTime;
        float blendTime = definition != null ? definition.IkBlendDuration : poseBlendTime;
        float targetWeight = ResolveTargetWeight(definition);
        poseWeight = MoveToward(poseWeight, targetWeight, dt, blendTime);
        debugPoseWeight = poseWeight;

        bool sprinting = animationState != null && animationState.IsSprinting && !animationState.IsProne;
        bool aiming = animationState != null && animationState.IsAiming && !sprinting;
        bool crouching = animationState != null && animationState.IsCrouching && !animationState.IsProne && !sprinting;
        bool prone = animationState != null && animationState.IsProne;
        bool reloading = animationState != null && animationState.IsReloading;
        aimBlend = MoveToward(aimBlend, aiming ? 1f : 0f, dt, aimBlendTime);
        sprintBlend = MoveToward(sprintBlend, sprinting ? 1f : 0f, dt, sprintBlendTime);
        crouchBlend = MoveToward(crouchBlend, crouching ? 1f : 0f, dt, poseBlendTime);
        proneBlend = MoveToward(proneBlend, prone ? 1f : 0f, dt, proneBlendTime);
        reloadBlend = MoveToward(reloadBlend, reloading ? 1f : 0f, dt, reloadBlendTime);
        switchBlend = MoveToward(switchBlend, 1f, dt, switchBlendTime);

        UpdateBlendedHold(definition);
        PlaceWeaponAnchor();
        worldWeapon?.AttachToAnchor();
        UpdateIkTargets(definition);
        UpdateAimTarget();
        ApplyResolvedWeights(definition);
        CacheDebug(definition);
    }

    public void NotifyWeaponChanged()
    {
        switchBlend = 0f;
        missingGripWarningId = null;
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
        crouchBlend = 0f;
        proneBlend = 0f;
        reloadBlend = 0f;
        switchBlend = 1f;
        lookPitch = 0f;
        lookPitchVelocity = 0f;
    }

    public void BeginEditorPreview()
    {
        IsEditorPreview = true;
        drawGizmos = true;
        if (worldWeapon == null)
            worldWeapon = GetComponent<WorldWeaponView>();
        ResolveHierarchy();
        EnsureGuideTransforms();
        EnsureAnimationRig();
        DisableLegacyPoseLayer();
    }

    public void ApplyEditorPreview(float aim, float sprint, float prone, float pitch, float weight = 1f, float crouch = 0f)
    {
        IsEditorPreview = true;
        ResolveHierarchy();
        if (upperChest == null)
            return;

        previewPitch = pitch;
        lookPitch = pitch;
        lookPitchVelocity = 0f;
        aimBlend = Mathf.Clamp01(aim);
        sprintBlend = Mathf.Clamp01(sprint);
        crouchBlend = Mathf.Clamp01(crouch);
        proneBlend = Mathf.Clamp01(prone);
        reloadBlend = 0f;
        poseWeight = Mathf.Clamp01(weight);
        switchBlend = 1f;
        debugPoseWeight = poseWeight;
        debugWeapon = ActiveDefinition != null ? ActiveDefinition.WeaponId : string.Empty;

        UpdateBlendedHold(ActiveDefinition);
        PlaceWeaponAnchor();
        worldWeapon?.AttachToAnchor();
        UpdateIkTargets(ActiveDefinition);
        UpdateAimTarget();
        ApplyResolvedWeights(ActiveDefinition);
        EvaluateEditorArmIk();
        CacheDebug(ActiveDefinition);
    }

    public void EndEditorPreview()
    {
        IsEditorPreview = false;
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

    private float ResolveTargetWeight(WeaponDefinition definition)
    {
        if (playerHealth != null && playerHealth.IsDead)
            return 0f;
        if (definition == null)
            return 0f;
        if (animationState == null)
            return 1f;
        if (animationState.IsDolphinDiving)
            return 0.12f;
        return 1f;
    }

    private void UpdateBlendedHold(WeaponDefinition definition)
    {
        ThirdPersonWeaponHoldPose hold = ThirdPersonWeaponHoldResolver.ResolvePose(
            definition,
            ThirdPersonWeaponPoseKind.Hold);
        ThirdPersonWeaponHoldPose sprint = ThirdPersonWeaponHoldResolver.ResolvePose(
            definition,
            ThirdPersonWeaponPoseKind.Sprint);
        ThirdPersonWeaponHoldPose aim = ThirdPersonWeaponHoldResolver.ResolvePose(
            definition,
            ThirdPersonWeaponPoseKind.Aim);
        ThirdPersonWeaponHoldPose prone = ThirdPersonWeaponHoldResolver.ResolvePose(
            definition,
            ThirdPersonWeaponPoseKind.Prone);

        blendedPose = hold;
        if (sprintBlend > 0.001f)
            blendedPose = ThirdPersonWeaponHoldPose.Lerp(blendedPose, sprint, sprintBlend);
        if (aimBlend > 0.001f)
            blendedPose = ThirdPersonWeaponHoldPose.Lerp(blendedPose, aim, aimBlend);
        if (proneBlend > 0.001f)
            blendedPose = ThirdPersonWeaponHoldPose.Lerp(blendedPose, prone, proneBlend);
    }

    private void PlaceWeaponAnchor()
    {
        if (weaponAnchor == null || upperChest == null)
            return;

        float pitch = ResolveLookPitch();
        float pitchScale = Mathf.Lerp(1f, 0.35f, proneBlend);
        Quaternion pitchRot = Quaternion.AngleAxis(pitch * pitchScale, transform.right);
        Quaternion chestRotation = upperChest.rotation;
        Vector3 worldPosition = upperChest.position + pitchRot * (chestRotation * blendedPose.weaponAnchorLocalPosition);
        Quaternion worldRotation = pitchRot * chestRotation * Quaternion.Euler(blendedPose.weaponAnchorLocalEuler);
        weaponAnchor.SetPositionAndRotation(worldPosition, worldRotation);
    }

    private float ResolveLookPitch()
    {
        if (IsEditorPreview)
        {
            lookPitch = Mathf.Clamp(previewPitch, -50f, 50f);
            return lookPitch;
        }

        float target = animationState != null ? animationState.AimPitch : 0f;
        target = Mathf.Clamp(target, -50f, 50f);
        float smooth = Mathf.Max(0.04f, lookPitchSmoothTime);
        lookPitch = Mathf.SmoothDamp(lookPitch, target, ref lookPitchVelocity, smooth, 180f, Time.deltaTime);
        return lookPitch;
    }

    private void ApplyResolvedWeights(WeaponDefinition definition)
    {
        float appliedRig = poseWeight * switchBlend * ikWeight;
        float targetRight = blendedPose.rightArmIkWeight * rightIkWeight;
        float targetLeft = 0f;
        if (blendedPose.useLeftHand && HasSupportGrip(definition, out _))
        {
            targetLeft = blendedPose.leftArmIkWeight * leftIkWeight;
            if (sprintBlend > 0.01f)
            {
                float sprintIk = definition != null ? definition.SprintSupportIkWeight : 0.55f;
                targetLeft *= Mathf.Lerp(1f, sprintIk, sprintBlend);
            }

            targetLeft *= 1f - reloadBlend;
        }

        float appliedRight = appliedRig * targetRight;
        float appliedLeft = appliedRig * targetLeft;
        float appliedHint = Mathf.Max(appliedRight, appliedLeft) * blendedPose.hintWeight * hintWeight;
        float lookAmount = Mathf.Clamp01(Mathf.Abs(ResolveLookPitch()) / 50f);
        float chestAim = Mathf.Max(aimIkWeight, blendedPose.chestInfluence);
        float appliedAim = appliedRig * Mathf.Max(aimBlend, lookAmount) * chestAim;
        ApplyRigWeights(appliedRig, appliedRight, appliedLeft, appliedHint, appliedAim);
    }

    private void ApplyRigWeights(float rig, float right, float left, float hint, float aim)
    {
        debugRigWeight = rig;
        debugRightIkWeight = right;
        debugLeftIkWeight = left;
        if (weaponRig != null)
            weaponRig.weight = rig;
        ApplyTwoBone(rightHandIk, right, hint);
        ApplyTwoBone(leftHandIk, left, hint);
        if (spineAim != null)
            spineAim.weight = aim;
    }

    private static void ApplyTwoBone(TwoBoneIKConstraint constraint, float weight, float hint)
    {
        if (constraint == null)
            return;

        constraint.weight = weight;
        TwoBoneIKConstraintData data = constraint.data;
        data.hintWeight = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = 1f;
        constraint.data = data;
    }

    private void UpdateIkTargets(WeaponDefinition definition)
    {
        Transform visualGripR = worldWeapon != null ? worldWeapon.RightHandIkTarget : null;
        if (rightHandIkTarget != null && visualGripR != null)
            rightHandIkTarget.SetPositionAndRotation(visualGripR.position, visualGripR.rotation);

        if (HasSupportGrip(definition, out Transform gripL))
        {
            if (leftHandIkTarget != null)
                leftHandIkTarget.SetPositionAndRotation(gripL.position, gripL.rotation);
        }

        PlaceElbowHint(rightElbowHint, blendedPose.rightElbowHintLocalPosition);
        PlaceElbowHint(leftElbowHint, blendedPose.leftElbowHintLocalPosition);
    }

    private void PlaceElbowHint(Transform hint, Vector3 chestLocal)
    {
        if (hint == null || upperChest == null)
            return;

        hint.position = upperChest.position + upperChest.rotation * chestLocal;
        hint.rotation = upperChest.rotation;
    }

    /// <summary>
    /// Animation Rigging jobs do not evaluate in edit-mode preview. Solve the
    /// arms here so Scene-view hand and elbow handles actually move the mesh.
    /// </summary>
    public void EvaluateEditorArmIk()
    {
        if (!IsEditorPreview || thirdPersonAnimator == null)
            return;

        SolveTwoBoneIk(
            thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm),
            thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm),
            thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightHand),
            rightHandIkTarget,
            rightElbowHint,
            debugRightIkWeight);

        SolveTwoBoneIk(
            thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
            thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
            thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftHand),
            leftHandIkTarget,
            leftElbowHint,
            debugLeftIkWeight);
    }

    private static void SolveTwoBoneIk(
        Transform root,
        Transform mid,
        Transform tip,
        Transform target,
        Transform hint,
        float weight)
    {
        if (root == null || mid == null || tip == null || target == null || weight <= 0.001f)
            return;

        Vector3 rootPos = root.position;
        Vector3 midPos = mid.position;
        float upperLen = Vector3.Distance(rootPos, midPos);
        float lowerLen = Vector3.Distance(midPos, tip.position);
        if (upperLen < 0.0001f || lowerLen < 0.0001f)
            return;

        Vector3 toTarget = target.position - rootPos;
        float maxReach = upperLen + lowerLen;
        float dist = Mathf.Clamp(toTarget.magnitude, 0.001f, maxReach - 0.0001f);
        Vector3 dir = toTarget.sqrMagnitude > 0.0000001f ? toTarget.normalized : root.forward;

        Vector3 hintVec = (hint != null ? hint.position : midPos) - rootPos;
        Vector3 bend = Vector3.Cross(Vector3.Cross(dir, hintVec), dir);
        if (bend.sqrMagnitude < 0.0000001f)
            bend = Vector3.Cross(dir, root.up);
        if (bend.sqrMagnitude < 0.0000001f)
            bend = Vector3.Cross(dir, Vector3.up);
        bend.Normalize();

        float along = (dist * dist + upperLen * upperLen - lowerLen * lowerLen) / (2f * dist);
        float heightSqr = upperLen * upperLen - along * along;
        float height = heightSqr > 0f ? Mathf.Sqrt(heightSqr) : 0f;
        Vector3 desiredMid = rootPos + dir * along + bend * height;

        RotateJointToward(root, midPos, desiredMid);
        RotateJointToward(mid, tip.position, target.position);
        tip.rotation = Quaternion.Slerp(tip.rotation, target.rotation, weight);
    }

    private static void RotateJointToward(Transform joint, Vector3 currentEnd, Vector3 desiredEnd)
    {
        Vector3 from = currentEnd - joint.position;
        Vector3 to = desiredEnd - joint.position;
        if (from.sqrMagnitude < 0.0000001f || to.sqrMagnitude < 0.0000001f)
            return;

        joint.rotation = Quaternion.FromToRotation(from.normalized, to.normalized) * joint.rotation;
    }

    private bool HasSupportGrip(WeaponDefinition definition, out Transform grip)
    {
        grip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : null;
        if (definition == null || !definition.UseLeftHandGrip)
            return false;
        if (grip != null)
            return true;

        if (missingGripWarningId != definition.WeaponId)
        {
            missingGripWarningId = definition.WeaponId;
            Debug.LogWarning(
                $"[ThirdPersonWeaponRig] {definition.DisplayName} wants a left-hand grip but Grip_L was not found.",
                this);
        }

        return false;
    }

    private void UpdateAimTarget()
    {
        if (aimTarget == null)
            return;

        Vector3 origin = upperChest != null
            ? upperChest.position
            : transform.position + Vector3.up * 1.4f;
        float pitch = ResolveLookPitch();
        Quaternion facing = ResolveUprightFacing();
        Quaternion pitchRot = Quaternion.AngleAxis(pitch, transform.right);
        aimTarget.position = origin + pitchRot * (facing * Vector3.forward) * 2.2f;
        aimTarget.rotation = pitchRot * facing;
    }

    private void CacheDebug(WeaponDefinition definition)
    {
        debugPoseCategory = definition != null ? definition.ThirdPersonHoldClass.ToString() : "None";
        debugWeapon = definition != null ? definition.WeaponId : string.Empty;
        if (weaponAnchor != null)
        {
            debugGunPosition = weaponAnchor.position;
            debugGunRotation = weaponAnchor.rotation;
        }
        else if (worldWeapon != null && worldWeapon.WorldWeaponRoot != null)
        {
            debugGunPosition = worldWeapon.WorldWeaponRoot.position;
            debugGunRotation = worldWeapon.WorldWeaponRoot.rotation;
        }

        if (animationState == null)
        {
            debugMovementState = IsEditorPreview ? "Preview" : "None";
            return;
        }

        if (animationState.IsProne)
            debugMovementState = "Prone";
        else if (animationState.IsSprinting)
            debugMovementState = "Sprint";
        else if (animationState.IsCrouching)
            debugMovementState = "Crouch";
        else if (animationState.IsAirborne)
            debugMovementState = "Jump";
        else if (animationState.IsMoving)
            debugMovementState = "Move";
        else
            debugMovementState = "Idle";
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
            upperChest = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.UpperChest)
                ?? thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Chest)
                ?? thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Spine);
        EnsureWeaponAnchor();
        CacheRig();
    }

    private void CacheRig()
    {
        if (thirdPersonAnimator == null)
            return;

        if (rigBuilder == null)
            rigBuilder = thirdPersonAnimator.GetComponent<RigBuilder>();
        if (weaponRig == null && thirdPersonAnimator != null)
            weaponRig = thirdPersonAnimator.GetComponentInChildren<Rig>(true);
        if (weaponRig != null)
        {
            if (rightHandIk == null)
            {
                Transform right = weaponRig.transform.Find("RightHandIK");
                if (right != null)
                    rightHandIk = right.GetComponent<TwoBoneIKConstraint>();
            }

            if (leftHandIk == null)
            {
                Transform left = weaponRig.transform.Find("LeftHandIK");
                if (left != null)
                    leftHandIk = left.GetComponent<TwoBoneIKConstraint>();
            }
            if (spineAim == null)
                spineAim = weaponRig.GetComponentInChildren<MultiAimConstraint>(true);
        }
    }

    private void EnsureAnimationRig()
    {
        CacheRig();
        if (thirdPersonAnimator == null)
            return;

        if (rigBuilder == null)
            rigBuilder = thirdPersonAnimator.GetComponent<RigBuilder>()
                ?? thirdPersonAnimator.gameObject.AddComponent<RigBuilder>();

        if (weaponRig == null)
        {
            Transform rigTransform = thirdPersonAnimator.transform.Find("ThirdPersonWeaponRig");
            if (rigTransform == null)
            {
                GameObject rigGo = new GameObject("ThirdPersonWeaponRig");
                rigGo.transform.SetParent(thirdPersonAnimator.transform, false);
                rigTransform = rigGo.transform;
            }

            weaponRig = rigTransform.GetComponent<Rig>() ?? rigTransform.gameObject.AddComponent<Rig>();
        }

        rightHandIk = EnsureTwoBone(
            "RightHandIK",
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            rightHandIkTarget,
            rightElbowHint,
            rightHandIk);
        leftHandIk = EnsureTwoBone(
            "LeftHandIK",
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            leftHandIkTarget,
            leftElbowHint,
            leftHandIk);

        if (spineAim == null)
        {
            Transform aimTransform = weaponRig.transform.Find("AimRig");
            if (aimTransform == null)
            {
                GameObject aimGo = new GameObject("AimRig");
                aimGo.transform.SetParent(weaponRig.transform, false);
                aimTransform = aimGo.transform;
            }

            spineAim = aimTransform.GetComponent<MultiAimConstraint>()
                ?? aimTransform.gameObject.AddComponent<MultiAimConstraint>();
            MultiAimConstraintData aimData = spineAim.data;
            if (aimData.constrainedObject == null)
            {
                aimData.constrainedObject = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Chest)
                    ?? thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Spine);
            }

            WeightedTransformArray sources = aimData.sourceObjects;
            if (aimTarget != null)
            {
                if (sources.Count == 0)
                    sources.Add(new WeightedTransform(aimTarget, 1f));
                else
                    sources[0] = new WeightedTransform(aimTarget, 1f);
            }

            aimData.sourceObjects = sources;
            spineAim.data = aimData;
            spineAim.weight = 0f;
        }

        bool hasLayer = false;
        if (rigBuilder.layers != null)
        {
            for (int i = 0; i < rigBuilder.layers.Count; i++)
            {
                if (rigBuilder.layers[i].rig == weaponRig)
                {
                    hasLayer = true;
                    break;
                }
            }
        }

        if (!hasLayer)
            rigBuilder.layers.Add(new RigLayer(weaponRig, true));

        if (Application.isPlaying)
            rigBuilder.Build();
    }

    private TwoBoneIKConstraint EnsureTwoBone(
        string childName,
        HumanBodyBones rootBone,
        HumanBodyBones midBone,
        HumanBodyBones tipBone,
        Transform target,
        Transform hint,
        TwoBoneIKConstraint existing)
    {
        TwoBoneIKConstraint constraint = existing;
        if (constraint == null)
        {
            Transform ikTransform = weaponRig.transform.Find(childName);
            if (ikTransform == null)
            {
                GameObject ikGo = new GameObject(childName);
                ikGo.transform.SetParent(weaponRig.transform, false);
                ikTransform = ikGo.transform;
            }

            constraint = ikTransform.GetComponent<TwoBoneIKConstraint>()
                ?? ikTransform.gameObject.AddComponent<TwoBoneIKConstraint>();
        }

        TwoBoneIKConstraintData ikData = constraint.data;
        if (ikData.root == null)
            ikData.root = thirdPersonAnimator.GetBoneTransform(rootBone);
        if (ikData.mid == null)
            ikData.mid = thirdPersonAnimator.GetBoneTransform(midBone);
        if (ikData.tip == null)
            ikData.tip = thirdPersonAnimator.GetBoneTransform(tipBone);
        ikData.target = target;
        ikData.hint = hint;
        ikData.targetPositionWeight = 1f;
        ikData.targetRotationWeight = 1f;
        ikData.hintWeight = 1f;
        constraint.data = ikData;
        return constraint;
    }

    private void EnsureWeaponAnchor()
    {
        if (weaponAnchor != null)
            return;

        if (visualRig != null && visualRig.ThirdPersonWeaponAnchor != null)
        {
            weaponAnchor = visualRig.ThirdPersonWeaponAnchor;
            return;
        }

        Transform existing = FindNamed(transform, "ThirdPersonWeaponAnchor");
        if (existing != null)
        {
            weaponAnchor = existing;
            return;
        }

        Transform host = transform.Find("WorldWeaponRig");
        if (host == null)
        {
            GameObject rigGo = new GameObject("WorldWeaponRig");
            rigGo.transform.SetParent(transform, false);
            host = rigGo.transform;
        }

        GameObject anchor = new GameObject("ThirdPersonWeaponAnchor");
        anchor.transform.SetParent(host, false);
        weaponAnchor = anchor.transform;
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

        if (weaponAnchor == null)
            weaponAnchor = FindNamed(transform, "ThirdPersonWeaponAnchor");
        if (weaponAnchor == null)
        {
            GameObject anchor = new GameObject("ThirdPersonWeaponAnchor");
            anchor.transform.SetParent(host, false);
            weaponAnchor = anchor.transform;
        }

        if (aimTarget == null)
            aimTarget = FindNamed(transform, "AimTarget");
        if (aimTarget == null)
        {
            GameObject aim = new GameObject("AimTarget");
            aim.transform.SetParent(host, false);
            aimTarget = aim.transform;
        }

        if (rightHandIkTarget == null)
            rightHandIkTarget = FindNamed(transform, "RightHandIKTarget");
        if (rightHandIkTarget == null)
        {
            GameObject target = new GameObject("RightHandIKTarget");
            target.transform.SetParent(host, false);
            rightHandIkTarget = target.transform;
        }

        if (leftHandIkTarget == null)
            leftHandIkTarget = FindNamed(transform, "LeftHandIKTarget");
        if (leftHandIkTarget == null)
        {
            GameObject target = new GameObject("LeftHandIKTarget");
            target.transform.SetParent(host, false);
            leftHandIkTarget = target.transform;
        }

        if (rightElbowHint == null)
            rightElbowHint = FindNamed(transform, "RightElbowHint");
        if (rightElbowHint == null)
        {
            GameObject hint = new GameObject("RightElbowHint");
            hint.transform.SetParent(host, false);
            rightElbowHint = hint.transform;
        }

        if (leftElbowHint == null)
            leftElbowHint = FindNamed(transform, "LeftElbowHint");
        if (leftElbowHint == null)
        {
            GameObject hint = new GameObject("LeftElbowHint");
            hint.transform.SetParent(host, false);
            leftElbowHint = hint.transform;
        }
    }

    private void DisableLegacyPoseLayer()
    {
        if (thirdPersonAnimator == null)
            return;

        int layer = thirdPersonAnimator.GetLayerIndex(ThirdPersonWeaponPoseBinder.LayerName);
        if (layer < 0)
            layer = thirdPersonAnimator.GetLayerIndex(ThirdPersonWeaponPoseBinder.LegacyLayerName);
        if (layer >= 0)
            thirdPersonAnimator.SetLayerWeight(layer, 0f);
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
        if (!drawGizmos || (!CanPose() && !IsEditorPreview))
            return;

        Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
        Gizmos.DrawWireSphere(debugGunPosition, 0.02f);
        Gizmos.DrawLine(debugGunPosition, debugGunPosition + debugGunRotation * Vector3.forward * 0.16f);
        Gizmos.DrawLine(debugGunPosition, debugGunPosition + debugGunRotation * Vector3.up * 0.08f);

        if (weaponAnchor != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.95f);
            Gizmos.DrawWireSphere(weaponAnchor.position, 0.016f);
        }

        Transform gripR = worldWeapon != null ? worldWeapon.RightHandIkTarget : rightHandIkTarget;
        if (gripR != null)
        {
            Gizmos.color = new Color(0.3f, 1f, 0.35f, 0.95f);
            Gizmos.DrawWireSphere(gripR.position, 0.022f);
            Gizmos.DrawLine(gripR.position, gripR.position + gripR.forward * 0.08f);
        }

        Transform gripL = worldWeapon != null ? worldWeapon.LeftHandIkTarget : leftHandIkTarget;
        if (gripL != null)
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.95f);
            Gizmos.DrawWireSphere(gripL.position, 0.024f);
            Gizmos.DrawLine(gripL.position, gripL.position + gripL.forward * 0.08f);
        }

        if (rightElbowHint != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.95f);
            Gizmos.DrawWireSphere(rightElbowHint.position, 0.02f);
            if (gripR != null)
                Gizmos.DrawLine(rightElbowHint.position, gripR.position);
        }

        if (leftElbowHint != null)
        {
            Gizmos.color = new Color(0.95f, 0.45f, 1f, 0.95f);
            Gizmos.DrawWireSphere(leftElbowHint.position, 0.02f);
            if (gripL != null)
                Gizmos.DrawLine(leftElbowHint.position, gripL.position);
        }
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !CanPose())
            return;

        GUI.color = Color.white;
        GUI.Label(
            new Rect(12f, 12f, 460f, 80f),
            $"TP Hold  {debugWeapon}  {debugPoseCategory}\n" +
            $"State {debugMovementState}  Rig {debugRigWeight:0.00}  R {debugRightIkWeight:0.00}  L {debugLeftIkWeight:0.00}\n" +
            "Weapon-first IK. Locomotion clips are not replaced.");
    }
#endif
}

public struct ThirdPersonPoseGuide
{
    public Vector3 gunPosition;
    public Quaternion gunRotation;
    public Vector3 rightGripPosition;
    public Quaternion rightGripRotation;
    public Vector3 leftGripPosition;
    public Quaternion leftGripRotation;
    public Vector3 rightElbowHintPosition;
    public Vector3 leftElbowHintPosition;
    public Vector3 socketPosition;
    public WeaponDefinition definition;
    public string poseCategory;
    public float leftIkWeight;
    public float rightIkWeight;
    public float rigWeight;
}
