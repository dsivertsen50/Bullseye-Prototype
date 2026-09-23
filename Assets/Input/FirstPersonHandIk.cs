using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner-only driver for the dedicated first-person arms.
/// SM_StickMan_FPArms lives under the weapon mount, so aim, sprint, and reload
/// move the arms with the gun. Hands are placed with IK on the weapon grips.
/// World-view arms are not used. Nothing here is networked.
/// </summary>
[DefaultExecutionOrder(320)]
public class FirstPersonHandIk : NetworkBehaviour
{
    private const string ArmsPrefabPath = "Assets/Player/SM_StickMan_FPArms.fbx";

    [SerializeField] private WeaponPresentationController presentation;
    [SerializeField] private PlayerWeaponInventory inventory;
    [SerializeField] private GameObject fpArmsPrefab;
    [SerializeField] private float blendDuration = 0.12f;
    [SerializeField] private Vector3 fpArmsPositionOffset = new Vector3(0f, -0.05f, -0.45f);
    [SerializeField] private Vector3 fpArmsRotationOffset = Vector3.zero;
    [SerializeField] private Vector3 rightHandGripOffset = Vector3.zero;
    [SerializeField] private Vector3 leftHandGripOffset = Vector3.zero;
    [SerializeField] private Vector3 rightHandEulerOffset = new Vector3(0f, 0f, 90f);
    [SerializeField] private Vector3 leftHandEulerOffset = new Vector3(0f, 0f, -90f);
    [SerializeField] private float elbowOut = 0.22f;
    [SerializeField] private float elbowDown = 0.28f;

    private PlayerHealth playerHealth;
    private PlayerAimZoom aimZoom;
    private FirstPersonArmRig armRig;
    private float weight;

    public FirstPersonArmRig ArmRig => armRig;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        aimZoom = GetComponent<PlayerAimZoom>();
        if (presentation == null)
            presentation = GetComponent<WeaponPresentationController>();
        if (inventory == null)
            inventory = GetComponent<PlayerWeaponInventory>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        BuildArmRig();
    }

    public override void OnNetworkDespawn()
    {
        if (armRig != null)
            Destroy(armRig.gameObject);
        armRig = null;
    }

    private void LateUpdate()
    {
        if (!IsSpawned || !IsOwner || armRig == null)
            return;

        armRig.ApplyOffset(fpArmsPositionOffset, fpArmsRotationOffset);

        bool show = ShouldShowArms();
        float target = show ? 1f : 0f;
        if (show && weight <= 0.001f)
            weight = 1f;
        else
            weight = Mathf.MoveTowards(weight, target, Time.deltaTime / Mathf.Max(0.01f, blendDuration));

        armRig.SetVisible(show && weight > 0.02f);
        if (weight <= 0.001f || presentation == null)
            return;

        armRig.PoseHands(
            presentation.RightHandGrip,
            presentation.LeftHandGrip,
            presentation.WantsSupportHand,
            rightHandGripOffset,
            leftHandGripOffset,
            rightHandEulerOffset,
            leftHandEulerOffset,
            elbowOut,
            elbowDown,
            weight);
    }

    private bool ShouldShowArms()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return false;
        if (presentation == null || presentation.RightHandGrip == null)
            return false;
        if (IsScopeHidingViewmodel())
            return false;
        return true;
    }

    private bool IsScopeHidingViewmodel()
    {
        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        if (definition == null || !definition.AdsHidesViewmodel)
            return false;
        if (presentation != null && presentation.AimBlend > 0.25f)
            return true;
        return aimZoom != null && aimZoom.IsAiming;
    }

    private void BuildArmRig()
    {
        if (armRig != null)
            return;

        GameObject prefab = ResolveArmsPrefab();
        Transform mount = presentation != null ? presentation.WeaponMount : null;
        if (mount == null)
            mount = transform.Find("CameraRoot/CameraEffectsRoot/Camera/WeaponView/WeaponEffectsRoot/AimRoot/WeaponMount");

        armRig = FirstPersonArmRig.Create(mount, prefab, fpArmsPositionOffset, fpArmsRotationOffset);
        if (armRig == null)
            Debug.LogWarning("SM_StickMan_FPArms was not created. The local player will not see dedicated weapon arms.");
    }

    private GameObject ResolveArmsPrefab()
    {
        if (fpArmsPrefab != null)
            return fpArmsPrefab;

#if UNITY_EDITOR
        fpArmsPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ArmsPrefabPath);
#endif
        return fpArmsPrefab;
    }
}

/// <summary>
/// Local first-person arms instantiated from SM_StickMan_FPArms.
/// The rig is parented to the first-person weapon mount. Hand IK follows
/// RightHandGrip and LeftHandGrip. The shoulder anchor stays in mount space
/// so the arm cuts sit outside the normal view.
/// </summary>
public class FirstPersonArmRig : MonoBehaviour
{
    private const string FirstPersonWeaponLayerName = "FirstPersonWeapon";

    private SkinnedMeshRenderer armRenderer;
    private Transform rightUpper;
    private Transform rightLower;
    private Transform rightHand;
    private Transform leftUpper;
    private Transform leftLower;
    private Transform leftHand;
    private Vector3 shoulderMidLocal;
    private bool warnedReach;

    public bool IsReady => rightUpper != null && rightHand != null && leftUpper != null && leftHand != null;
    public Transform RightHandBone => rightHand;
    public Transform LeftHandBone => leftHand;
    public float LastRightHandError { get; private set; }

    public static FirstPersonArmRig Create(
        Transform weaponMount,
        GameObject armsPrefab,
        Vector3 positionOffset,
        Vector3 rotationOffset)
    {
        if (weaponMount == null || armsPrefab == null)
            return null;

        GameObject instance = Instantiate(armsPrefab, weaponMount, false);
        instance.name = "FirstPersonArms";
        FirstPersonArmRig rig = instance.AddComponent<FirstPersonArmRig>();
        if (!rig.Bind())
        {
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
            return null;
        }

        rig.ApplyOffset(positionOffset, rotationOffset);
        SetLayerRecursively(instance, LayerMask.NameToLayer(FirstPersonWeaponLayerName));
        return rig;
    }

    public void ApplyOffset(Vector3 positionOffset, Vector3 rotationOffset)
    {
        Quaternion rotation = Quaternion.Euler(rotationOffset);
        transform.localRotation = rotation;
        transform.localPosition = positionOffset - rotation * shoulderMidLocal;
        transform.localScale = Vector3.one;
    }

    public void SetVisible(bool visible)
    {
        if (armRenderer != null && armRenderer.enabled != visible)
            armRenderer.enabled = visible;
    }

    public void PoseHands(
        Transform rightGrip,
        Transform leftGrip,
        bool useSupportHand,
        Vector3 rightGripOffset,
        Vector3 leftGripOffset,
        Vector3 rightHandEulerOffset,
        Vector3 leftHandEulerOffset,
        float elbowOut,
        float elbowDown,
        float weight)
    {
        if (!IsReady || weight <= 0.001f || rightGrip == null)
            return;

        Transform space = transform.parent != null ? transform.parent : transform;
        Vector3 rightTarget = rightGrip.TransformPoint(rightGripOffset);
        Quaternion rightRotation = GripHandRotation(rightGrip, rightHandEulerOffset);
        SolveArm(rightUpper, rightLower, rightHand, rightTarget, rightRotation, space, 1f, elbowOut, elbowDown, weight);
        SolveArm(rightUpper, rightLower, rightHand, rightTarget, rightRotation, space, 1f, elbowOut, elbowDown, weight);
        LastRightHandError = Vector3.Distance(rightHand.position, rightTarget);
        if (!warnedReach && LastRightHandError > 0.2f)
        {
            warnedReach = true;
            Debug.LogWarning("First-person right hand cannot comfortably reach RightHandGrip. Move the arm offset or the grip.");
        }

        Vector3 leftTarget;
        Quaternion leftRotation;
        if (useSupportHand && leftGrip != null)
        {
            leftTarget = leftGrip.TransformPoint(leftGripOffset);
            leftRotation = GripHandRotation(leftGrip, leftHandEulerOffset);
        }
        else
        {
            leftTarget = rightGrip.TransformPoint(rightGripOffset + new Vector3(-0.1f, -0.05f, 0.04f));
            leftRotation = GripHandRotation(rightGrip, leftHandEulerOffset);
        }

        SolveArm(leftUpper, leftLower, leftHand, leftTarget, leftRotation, space, -1f, elbowOut, elbowDown, weight);
        SolveArm(leftUpper, leftLower, leftHand, leftTarget, leftRotation, space, -1f, elbowOut, elbowDown, weight);
    }

    private bool Bind()
    {
        Transform leftShoulder = FindBone(transform, "mixamorig:LeftShoulder");
        Transform rightShoulder = FindBone(transform, "mixamorig:RightShoulder");
        rightUpper = FindBone(transform, "mixamorig:RightArm");
        rightLower = FindBone(transform, "mixamorig:RightForeArm");
        rightHand = FindBone(transform, "mixamorig:RightHand");
        leftUpper = FindBone(transform, "mixamorig:LeftArm");
        leftLower = FindBone(transform, "mixamorig:LeftForeArm");
        leftHand = FindBone(transform, "mixamorig:LeftHand");
        if (!IsReady || leftShoulder == null || rightShoulder == null)
            return false;

        Vector3 mid = (leftShoulder.position + rightShoulder.position) * 0.5f;
        shoulderMidLocal = transform.InverseTransformPoint(mid);

        armRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (armRenderer == null)
            return false;

        armRenderer.updateWhenOffscreen = true;
        armRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        armRenderer.receiveShadows = false;
        armRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        armRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                animators[i].enabled = false;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        return true;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == boneName)
                return transforms[i];
        }

        return null;
    }

    private static Quaternion GripHandRotation(Transform grip, Vector3 eulerOffset)
    {
        Quaternion fingersAlongBarrel = Quaternion.LookRotation(grip.up, grip.forward);
        return fingersAlongBarrel * Quaternion.Euler(eulerOffset);
    }

    private static void SolveArm(
        Transform upper,
        Transform lower,
        Transform hand,
        Vector3 targetPosition,
        Quaternion targetRotation,
        Transform space,
        float side,
        float elbowOut,
        float elbowDown,
        float weight)
    {
        Vector3 hint = upper.position + space.right * (elbowOut * side) - space.up * elbowDown;
        SolveTwoBone(upper, lower, hand, targetPosition, hint, weight);
        hand.rotation = Quaternion.Slerp(hand.rotation, targetRotation, weight);
    }

    private static void SolveTwoBone(
        Transform root,
        Transform mid,
        Transform tip,
        Vector3 targetPosition,
        Vector3 hintPosition,
        float weight)
    {
        if (weight <= 0.001f)
            return;

        Vector3 rootPos = root.position;
        Vector3 midPos = mid.position;
        float upperLen = Vector3.Distance(rootPos, midPos);
        float lowerLen = Vector3.Distance(midPos, tip.position);
        if (upperLen < 0.0001f || lowerLen < 0.0001f)
            return;

        Vector3 toTarget = targetPosition - rootPos;
        float maxReach = upperLen + lowerLen - 0.0001f;
        float dist = Mathf.Clamp(toTarget.magnitude, 0.001f, Mathf.Max(0.001f, maxReach));
        Vector3 dir = toTarget.sqrMagnitude > 0.0000001f ? toTarget.normalized : root.forward;

        Vector3 hintVec = hintPosition - rootPos;
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

        RotateJointToward(root, midPos, Vector3.Lerp(midPos, desiredMid, weight));
        RotateJointToward(mid, tip.position, Vector3.Lerp(tip.position, targetPosition, weight));
    }

    private static void RotateJointToward(Transform joint, Vector3 currentEnd, Vector3 desiredEnd)
    {
        Vector3 from = currentEnd - joint.position;
        Vector3 to = desiredEnd - joint.position;
        if (from.sqrMagnitude < 0.0000001f || to.sqrMagnitude < 0.0000001f)
            return;

        joint.rotation = Quaternion.FromToRotation(from.normalized, to.normalized) * joint.rotation;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
    }
}
