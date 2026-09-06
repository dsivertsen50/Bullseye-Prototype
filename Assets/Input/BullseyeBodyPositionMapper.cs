using UnityEngine;

/// <summary>
/// Translates the live bullseye transform into a simplified body-space
/// location. Capsule dimensions are isolated here so a later humanoid
/// can replace them without rewriting the Body HUD.
/// </summary>
public class BullseyeBodyPositionMapper : MonoBehaviour
{
    [Header("Body References")]
    [Tooltip("Orientation root for player-local left/right and front/back.")]
    [SerializeField] private Transform bodyRoot;
    [Tooltip("Current capsule surface. Leave assigned for the prototype; swap for a humanoid bounds source later.")]
    [SerializeField] private CapsuleCollider bodyCapsule;
    [SerializeField] private Transform bullseye;

    [Header("Fallback Dimensions")]
    [Tooltip("Used when no capsule is assigned. Replace with humanoid height later.")]
    [SerializeField] private float bodyHeight = 2f;
    [Tooltip("Used when no capsule is assigned. Replace with humanoid half-width later.")]
    [SerializeField] private float bodyRadius = 0.5f;

    [Header("Front / Back")]
    [Tooltip("Fraction of body radius that must be crossed before FRONT/BACK flips.")]
    [SerializeField, Range(0.02f, 0.45f)] private float frontBackHysteresis = 0.16f;

    private BullseyeFacing lastFacing = BullseyeFacing.Front;
    private bool hasFacing;

    public Transform BullseyeTransform => bullseye;
    public Transform BodyRoot => bodyRoot != null ? bodyRoot : transform;
    public CapsuleCollider BodyCapsule => bodyCapsule;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        bodyHeight = Mathf.Max(0.2f, bodyHeight);
        bodyRadius = Mathf.Max(0.05f, bodyRadius);
        frontBackHysteresis = Mathf.Clamp(frontBackHysteresis, 0.02f, 0.45f);
    }

    public bool TryMap(out BullseyeBodyPosition position)
    {
        position = default;
        ResolveReferences();

        BullseyeMover mover = GetComponent<BullseyeMover>();
        BullseyeSurfaceMap map = GetComponent<BullseyeSurfaceMap>();
        if (mover != null && map != null && mover.TryGetSurfacePose(out Vector3 surfacePos, out _, out _))
        {
            position = map.ToBodyPosition(
                mover.CurrentRegionIndex,
                mover.TargetRegionIndex,
                mover.MovementProgress,
                surfacePos);
            return true;
        }

        if (bullseye == null)
            return false;

        if (!TryGetBodyFrame(out Transform orientationRoot, out Vector3 centerWorld, out float height, out float radius))
            return false;

        Vector3 local = Quaternion.Inverse(orientationRoot.rotation) * (bullseye.position - centerWorld);
        float normalizedHeight = bodyCapsule != null
            ? CapsuleBodySurface.GetNormalizedHeight(bodyCapsule, bullseye.position)
            : Mathf.InverseLerp(-height * 0.5f, height * 0.5f, local.y);

        float normalizedLateral = radius > 0.0001f
            ? Mathf.Clamp(local.x / radius, -1f, 1f)
            : 0f;

        BullseyeFacing facing = ClassifyFacing(local.z, radius);
        position = new BullseyeBodyPosition(normalizedHeight, normalizedLateral, facing, local);
        return true;
    }

    public void ResetFacing()
    {
        hasFacing = false;
        lastFacing = BullseyeFacing.Front;
    }

    private BullseyeFacing ClassifyFacing(float forwardOffset, float radius)
    {
        float threshold = Mathf.Max(0.01f, radius * frontBackHysteresis);

        if (!hasFacing)
        {
            lastFacing = forwardOffset >= 0f ? BullseyeFacing.Front : BullseyeFacing.Back;
            hasFacing = true;
            return lastFacing;
        }

        if (lastFacing == BullseyeFacing.Front && forwardOffset < -threshold)
            lastFacing = BullseyeFacing.Back;
        else if (lastFacing == BullseyeFacing.Back && forwardOffset > threshold)
            lastFacing = BullseyeFacing.Front;

        return lastFacing;
    }

    private bool TryGetBodyFrame(out Transform orientationRoot, out Vector3 centerWorld, out float height, out float radius)
    {
        orientationRoot = BodyRoot;
        if (orientationRoot == null)
        {
            centerWorld = Vector3.zero;
            height = 0f;
            radius = 0f;
            return false;
        }

        if (bodyCapsule != null)
        {
            Vector3 lossy = bodyCapsule.transform.lossyScale;
            radius = Mathf.Max(0.0001f, bodyCapsule.radius * Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z)));
            height = Mathf.Max(bodyCapsule.height, bodyCapsule.radius * 2f) * Mathf.Abs(lossy.y);
            centerWorld = bodyCapsule.transform.TransformPoint(bodyCapsule.center);
            return true;
        }

        radius = Mathf.Max(0.0001f, bodyRadius);
        height = Mathf.Max(radius * 2f, bodyHeight);
        centerWorld = orientationRoot.position + orientationRoot.up * (height * 0.5f);
        return true;
    }

    private void ResolveReferences()
    {
        if (bodyRoot == null)
            bodyRoot = transform;

        if (bodyCapsule == null)
            bodyCapsule = FindBodyCapsule();

        if (bullseye == null)
        {
            BullseyeTarget target = GetComponentInChildren<BullseyeTarget>(true);
            if (target != null)
                bullseye = target.transform;
        }
    }

    private CapsuleCollider FindBodyCapsule()
    {
        CapsuleCollider[] colliders = GetComponentsInChildren<CapsuleCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            CapsuleCollider collider = colliders[i];
            if (collider != null && collider.transform != transform)
                return collider;
        }

        return GetComponent<CapsuleCollider>();
    }
}
