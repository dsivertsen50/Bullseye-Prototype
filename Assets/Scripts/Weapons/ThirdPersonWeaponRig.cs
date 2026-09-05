using Unity.Netcode;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Third-person weapon architecture: locomotion drives the body, the
/// right-hand socket carries the weapon, and Animation Rigging Two Bone IK
/// places the support hand on the weapon grip.
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
    [SerializeField] private Transform weaponSocket;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform leftHandIkTarget;
    [SerializeField] private Transform leftElbowHint;
    [SerializeField] private RigBuilder rigBuilder;
    [SerializeField] private Rig weaponRig;
    [SerializeField] private TwoBoneIKConstraint leftHandIk;
    [SerializeField] private MultiAimConstraint spineAim;

    [Header("Blending")]
    [SerializeField] private float poseBlendTime = 0.14f;
    [SerializeField] private float aimBlendTime = 0.12f;
    [SerializeField] private float sprintBlendTime = 0.16f;
    [SerializeField] private float proneBlendTime = 0.18f;
    [SerializeField] private float switchBlendTime = 0.1f;
    [SerializeField] private float reloadBlendTime = 0.12f;
    [SerializeField, Range(0f, 1f)] private float ikWeight = 1f;
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
    private Vector3 debugGunPosition;
    private Quaternion debugGunRotation;

    public float WeaponPoseWeight => poseWeight * switchBlend;
    public bool DrawPoseGuides => drawGizmos;
    public float AimBlend => aimBlend;
    public float SprintBlend => sprintBlend;
    public float CrouchBlend => crouchBlend;
    public float ProneBlend => proneBlend;
    public float LeftIkWeight => debugLeftIkWeight;
    public float RigWeight => debugRigWeight;
    public Transform WeaponSocket => weaponSocket;
    public Transform AimTarget => aimTarget;
    public Transform LeftHandIkTarget => leftHandIkTarget;
    public Transform LeftElbowHint => leftElbowHint;
    public WeaponDefinition ActiveDefinition => worldWeapon != null ? worldWeapon.Definition : null;
    public bool IsEditorPreview { get; private set; }
    public ThirdPersonPoseCategory ActivePoseCategory =>
        ActiveDefinition != null ? ActiveDefinition.PoseCategory : ThirdPersonPoseCategory.Pistol;

    public bool TryGetPoseGuide(out ThirdPersonPoseGuide guide)
    {
        guide = default;
        if (weaponSocket == null && worldWeapon == null)
            return false;

        guide.gunPosition = debugGunPosition;
        guide.gunRotation = debugGunRotation;
        guide.socketPosition = weaponSocket != null ? weaponSocket.position : debugGunPosition;
        Transform grip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : leftHandIkTarget;
        if (grip != null)
        {
            guide.leftGripPosition = grip.position;
            guide.leftGripRotation = grip.rotation;
        }

        Transform hint = worldWeapon != null && worldWeapon.LeftElbowHint != null
            ? worldWeapon.LeftElbowHint
            : leftElbowHint;
        if (hint != null)
            guide.leftElbowHintPosition = hint.position;

        guide.definition = ActiveDefinition;
        guide.poseCategory = debugPoseCategory;
        guide.leftIkWeight = debugLeftIkWeight;
        guide.rigWeight = debugRigWeight;
        return true;
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

        Quaternion local = Quaternion.Inverse(weaponSocket.rotation) * world;
        return local.eulerAngles;
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
        EnsureAnimationRig();
    }

    private void LateUpdate()
    {
        if (!CanPose())
        {
            ApplyRigWeights(0f, 0f, 0f, 0f);
            return;
        }

        ResolveHierarchy();
        if (thirdPersonAnimator == null || weaponSocket == null)
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

        worldWeapon?.AttachToSocket();
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
    }

    public void ApplyEditorPreview(float aim, float sprint, float prone, float pitch, float weight = 1f, float crouch = 0f)
    {
        IsEditorPreview = true;
        ResolveHierarchy();
        if (weaponSocket == null)
            return;

        previewPitch = pitch;
        aimBlend = Mathf.Clamp01(aim);
        sprintBlend = Mathf.Clamp01(sprint);
        crouchBlend = Mathf.Clamp01(crouch);
        proneBlend = Mathf.Clamp01(prone);
        reloadBlend = 0f;
        poseWeight = Mathf.Clamp01(weight);
        switchBlend = 1f;
        debugPoseWeight = poseWeight;
        debugWeapon = ActiveDefinition != null ? ActiveDefinition.WeaponId : string.Empty;

        worldWeapon?.AttachToSocket();
        UpdateIkTargets(ActiveDefinition);
        UpdateAimTarget();
        ApplyResolvedWeights(ActiveDefinition);
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

    private void ApplyResolvedWeights(WeaponDefinition definition)
    {
        float appliedRig = poseWeight * switchBlend * ikWeight;
        bool support = definition != null && definition.UsesSupportHandIk;
        float targetLeft = 0f;
        if (support && HasSupportGrip(definition, out _))
        {
            targetLeft = leftIkWeight;
            if (sprintBlend > 0.01f)
            {
                float sprintIk = definition != null ? definition.SprintSupportIkWeight : 0.55f;
                targetLeft *= Mathf.Lerp(1f, sprintIk, sprintBlend);
            }

            targetLeft *= 1f - reloadBlend;
        }

        float appliedLeft = appliedRig * targetLeft;
        float appliedHint = appliedLeft * hintWeight;
        float appliedAim = appliedRig * aimBlend * aimIkWeight;
        ApplyRigWeights(appliedRig, appliedLeft, appliedHint, appliedAim);
    }

    private void ApplyRigWeights(float rig, float left, float hint, float aim)
    {
        debugRigWeight = rig;
        debugLeftIkWeight = left;
        if (weaponRig != null)
            weaponRig.weight = rig;
        if (leftHandIk != null)
        {
            leftHandIk.weight = left;
            TwoBoneIKConstraintData data = leftHandIk.data;
            data.hintWeight = hint;
            leftHandIk.data = data;
        }

        if (spineAim != null)
            spineAim.weight = aim;
    }

    private void UpdateIkTargets(WeaponDefinition definition)
    {
        if (!HasSupportGrip(definition, out Transform grip))
            return;

        if (leftHandIkTarget != null)
        {
            leftHandIkTarget.SetPositionAndRotation(grip.position, grip.rotation);
        }

        Transform weaponHint = worldWeapon != null ? worldWeapon.LeftElbowHint : null;
        if (leftElbowHint != null && weaponHint != null)
            leftElbowHint.SetPositionAndRotation(weaponHint.position, weaponHint.rotation);
        else if (leftElbowHint != null && leftHandIkTarget != null && upperChest != null)
        {
            Vector3 toGrip = leftHandIkTarget.position - upperChest.position;
            Vector3 side = Vector3.Cross(toGrip.sqrMagnitude > 0.0001f ? toGrip.normalized : transform.forward, transform.up);
            if (side.sqrMagnitude < 0.0001f)
                side = -transform.right;
            leftElbowHint.position = upperChest.position - side.normalized * 0.22f + transform.forward * 0.08f;
        }
    }

    private bool HasSupportGrip(WeaponDefinition definition, out Transform grip)
    {
        grip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : null;
        if (definition == null || !definition.UsesSupportHandIk)
            return false;
        if (grip != null)
            return true;

        if (missingGripWarningId != definition.WeaponId)
        {
            missingGripWarningId = definition.WeaponId;
            Debug.LogWarning(
                $"[ThirdPersonWeaponRig] {definition.DisplayName} is configured as a two-handed weapon but no LeftHandGrip target was found.",
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
        float pitch = IsEditorPreview
            ? previewPitch
            : animationState != null ? animationState.AimPitch : 0f;
        pitch = Mathf.Clamp(pitch, -50f, 50f);
        Quaternion facing = ResolveUprightFacing();
        Quaternion pitchRot = Quaternion.AngleAxis(pitch, transform.right);
        aimTarget.position = origin + pitchRot * (facing * Vector3.forward) * 2.2f;
        aimTarget.rotation = pitchRot * facing;
    }

    private void CacheDebug(WeaponDefinition definition)
    {
        debugPoseCategory = definition != null ? definition.PoseCategory.ToString() : "None";
        debugWeapon = definition != null ? definition.WeaponId : string.Empty;
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
                ?? thirdPersonAnimator.GetBoneTransform(HumanBodyBones.Chest);
        EnsureWeaponSocket();
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
        if (leftHandIk == null && weaponRig != null)
            leftHandIk = weaponRig.GetComponentInChildren<TwoBoneIKConstraint>(true);
        if (spineAim == null && weaponRig != null)
            spineAim = weaponRig.GetComponentInChildren<MultiAimConstraint>(true);
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

        if (leftHandIk == null)
        {
            Transform ikTransform = weaponRig.transform.Find("LeftHandIK");
            if (ikTransform == null)
            {
                GameObject ikGo = new GameObject("LeftHandIK");
                ikGo.transform.SetParent(weaponRig.transform, false);
                ikTransform = ikGo.transform;
            }

            leftHandIk = ikTransform.GetComponent<TwoBoneIKConstraint>()
                ?? ikTransform.gameObject.AddComponent<TwoBoneIKConstraint>();
        }

        TwoBoneIKConstraintData ikData = leftHandIk.data;
        if (ikData.root == null)
            ikData.root = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        if (ikData.mid == null)
            ikData.mid = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        if (ikData.tip == null)
            ikData.tip = thirdPersonAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        ikData.target = leftHandIkTarget;
        ikData.hint = leftElbowHint;
        ikData.targetPositionWeight = 1f;
        ikData.targetRotationWeight = 1f;
        ikData.hintWeight = 1f;
        leftHandIk.data = ikData;

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

    private void EnsureWeaponSocket()
    {
        if (weaponSocket != null)
            return;

        Transform rightHand = thirdPersonAnimator != null
            ? thirdPersonAnimator.GetBoneTransform(HumanBodyBones.RightHand)
            : null;
        if (rightHand == null)
            return;

        Transform existing = rightHand.Find("WeaponSocket") ?? rightHand.Find("RightHandWeaponSocket");
        if (existing != null)
        {
            weaponSocket = existing;
            return;
        }

        GameObject socket = new GameObject("RightHandWeaponSocket");
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

        if (leftHandIkTarget == null)
            leftHandIkTarget = FindNamed(transform, "LeftHandIKTarget");
        if (leftHandIkTarget == null)
        {
            GameObject target = new GameObject("LeftHandIKTarget");
            target.transform.SetParent(host, false);
            leftHandIkTarget = target.transform;
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

        if (weaponSocket != null)
        {
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.95f);
            Gizmos.DrawWireSphere(weaponSocket.position, 0.016f);
        }

        Transform grip = worldWeapon != null ? worldWeapon.LeftHandIkTarget : leftHandIkTarget;
        if (grip != null)
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.95f);
            Gizmos.DrawWireSphere(grip.position, 0.024f);
            Gizmos.DrawLine(grip.position, grip.position + grip.forward * 0.08f);
        }

        Transform hint = worldWeapon != null && worldWeapon.LeftElbowHint != null
            ? worldWeapon.LeftElbowHint
            : leftElbowHint;
        if (hint != null)
        {
            Gizmos.color = new Color(0.95f, 0.45f, 1f, 0.95f);
            Gizmos.DrawWireSphere(hint.position, 0.02f);
            if (grip != null)
                Gizmos.DrawLine(hint.position, grip.position);
        }
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !CanPose())
            return;

        GUI.color = Color.white;
        GUI.Label(
            new Rect(12f, 12f, 420f, 70f),
            $"TP Weapon  {debugWeapon}  {debugPoseCategory}\n" +
            $"State {debugMovementState}  Rig {debugRigWeight:0.00}  LeftIK {debugLeftIkWeight:0.00}");
    }
#endif
}

public struct ThirdPersonPoseGuide
{
    public Vector3 gunPosition;
    public Quaternion gunRotation;
    public Vector3 leftGripPosition;
    public Quaternion leftGripRotation;
    public Vector3 leftElbowHintPosition;
    public Vector3 socketPosition;
    public WeaponDefinition definition;
    public string poseCategory;
    public float leftIkWeight;
    public float rigWeight;
}
