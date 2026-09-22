using UnityEngine;

/// <summary>
/// Marks vertical level geometry as climbable. Describes the climb surface;
/// player locomotion in <see cref="PlayerMovement"/> performs the climb.
/// </summary>
[DisallowMultipleComponent]
public class LadderClimbable : MonoBehaviour
{
    private const string ClimbVolumeName = "ClimbVolume";

    [Header("Climb")]
    [SerializeField] private float climbSpeed = 3f;
    [SerializeField, Tooltip("How far the player stands from the climbable face.")]
    private float playerOffsetFromSurface = 0.32f;
    [SerializeField, Tooltip("Which side of the mesh is climbable. Auto uses the thin axis. Next Climb Side cycles the four faces.")]
    private LadderClimbFace climbFace = LadderClimbFace.Auto;
    [SerializeField] private bool allowJumpOff = true;

    [Header("Dismount Height")]
    [SerializeField, Tooltip("Player feet stop here and step off the top. Move this transform to set get-off height.")]
    private Transform topExitPoint;
    [SerializeField, Tooltip("Lowest climb position. Move this transform to set get-on/get-off height at the bottom.")]
    private Transform bottomExitPoint;

    [Header("Volume")]
    [SerializeField] private Collider climbTrigger;
    [SerializeField, Tooltip("Optional. If empty, uses a child named Visual Mesh or the first renderer.")]
    private Transform visualMesh;
    [SerializeField, Tooltip("Extra trigger height above the mesh so the player can grab the ladder from a top platform. Does not change climb height.")]
    private float topEntryPadding = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    private Collider[] solidColliders = System.Array.Empty<Collider>();

    public float ClimbSpeed => Mathf.Max(0.1f, climbSpeed);
    public float PlayerOffsetFromSurface => Mathf.Max(0.05f, playerOffsetFromSurface);
    public bool AllowJumpOff => allowJumpOff;
    public Transform TopExitPoint => topExitPoint;
    public Transform BottomExitPoint => bottomExitPoint;
    public Collider ClimbTrigger => ResolveClimbTrigger();
    public Vector3 ClimbAxis => transform.up;
    public Vector3 FaceNormal
    {
        get
        {
            switch (ResolveClimbFace())
            {
                case LadderClimbFace.Back:
                    return -transform.forward;
                case LadderClimbFace.Right:
                    return transform.right;
                case LadderClimbFace.Left:
                    return -transform.right;
                default:
                    return transform.forward;
            }
        }
    }

    public void FlipClimbSide()
    {
        CycleClimbSide();
    }

    public void CycleClimbSide()
    {
        switch (ResolveClimbFace())
        {
            case LadderClimbFace.Forward:
                climbFace = LadderClimbFace.Right;
                break;
            case LadderClimbFace.Right:
                climbFace = LadderClimbFace.Back;
                break;
            case LadderClimbFace.Back:
                climbFace = LadderClimbFace.Left;
                break;
            default:
                climbFace = LadderClimbFace.Forward;
                break;
        }

        EnsureClimbVolume();
        EnsureExitPoints();
    }

    public LadderClimbFace ResolveClimbFace()
    {
        if (climbFace != LadderClimbFace.Auto)
            return climbFace;

        GetVisualLocalBounds(out _, out Vector3 localSize);
        return localSize.x + 0.001f < localSize.z
            ? LadderClimbFace.Right
            : LadderClimbFace.Forward;
    }

    private void Reset()
    {
        EnsureClimbVolume();
        EnsureExitPoints();
    }

    private void Awake()
    {
        EnsureClimbVolume();
        EnsureExitPoints();
        CacheSolidColliders();
    }

    private void OnEnable()
    {
        EnsureClimbVolume();
        EnsureExitPoints();
        CacheSolidColliders();
    }

    private void OnValidate()
    {
        climbSpeed = Mathf.Max(0.1f, climbSpeed);
        playerOffsetFromSurface = Mathf.Max(0.05f, playerOffsetFromSurface);
        topEntryPadding = Mathf.Max(0f, topEntryPadding);
        if (climbTrigger != null)
            climbTrigger.isTrigger = true;
        FitAutoClimbVolume();
    }

    public static bool TryGet(Collider collider, out LadderClimbable ladder)
    {
        ladder = null;
        if (collider == null)
            return false;

        ladder = collider.GetComponentInParent<LadderClimbable>();
        return ladder != null && ladder.isActiveAndEnabled;
    }

    public bool IsClimbTrigger(Collider collider)
    {
        Collider trigger = ResolveClimbTrigger();
        return collider != null && trigger != null && collider == trigger;
    }

    public bool OwnsCollider(Collider collider)
    {
        if (collider == null)
            return false;
        return collider.transform == transform || collider.transform.IsChildOf(transform);
    }

    public Collider[] GetSolidColliders()
    {
        if (solidColliders == null || solidColliders.Length == 0)
            CacheSolidColliders();
        return solidColliders;
    }

    public Vector3 GetAlignedPosition(Vector3 playerPosition)
    {
        GetClimbMetrics(out Vector3 origin, out Vector3 axis, out Vector3 right, out Vector3 face,
            out float minAlong, out float maxAlong, out float halfWidth, out float surfaceAlong);

        Vector3 toPlayer = playerPosition - origin;
        float along = Mathf.Clamp(Vector3.Dot(toPlayer, axis), minAlong, maxAlong);
        float lateral = Mathf.Clamp(Vector3.Dot(toPlayer, right), -halfWidth, halfWidth);
        return origin + axis * along + right * lateral + face * (surfaceAlong + PlayerOffsetFromSurface);
    }

    public bool IsAtTop(Vector3 playerPosition, float tolerance = 0.12f)
    {
        GetVerticalRange(out float minAlong, out float maxAlong);
        float along = Vector3.Dot(playerPosition - transform.position, ClimbAxis);
        return along >= maxAlong - Mathf.Max(0.02f, tolerance);
    }

    public bool IsAtBottom(Vector3 playerPosition, float tolerance = 0.18f)
    {
        GetVerticalRange(out float minAlong, out float maxAlong);
        float along = Vector3.Dot(playerPosition - transform.position, ClimbAxis);
        return along <= minAlong + Mathf.Max(0.02f, tolerance);
    }

    public bool IsNearTop(Vector3 playerPosition, float normalized = 0.82f)
    {
        float t = GetNormalizedHeight(playerPosition);
        return t >= Mathf.Clamp01(normalized);
    }

    public bool IsNearBottom(Vector3 playerPosition, float normalized = 0.2f)
    {
        float t = GetNormalizedHeight(playerPosition);
        return t <= Mathf.Clamp01(normalized);
    }

    public float GetNormalizedHeight(Vector3 playerPosition)
    {
        GetVerticalRange(out float minAlong, out float maxAlong);
        float along = Vector3.Dot(playerPosition - transform.position, ClimbAxis);
        float span = Mathf.Max(0.01f, maxAlong - minAlong);
        return Mathf.Clamp01((along - minAlong) / span);
    }

    public Vector3 GetTopExitPosition(Vector3 playerPosition)
    {
        if (topExitPoint != null)
            return topExitPoint.position;

        GetClimbMetrics(out Vector3 origin, out Vector3 axis, out _, out Vector3 face,
            out _, out float maxAlong, out _, out float surfaceAlong);
        return origin + axis * (maxAlong + 0.2f) + face * (surfaceAlong + PlayerOffsetFromSurface);
    }

    public Vector3 GetBottomExitPosition(Vector3 playerPosition)
    {
        if (bottomExitPoint != null)
            return bottomExitPoint.position;

        return playerPosition;
    }

    public float DistanceToSurface(Vector3 playerPosition)
    {
        Vector3 aligned = GetAlignedPosition(playerPosition);
        Vector3 axis = ClimbAxis;
        Vector3 planar = Vector3.ProjectOnPlane(aligned - playerPosition, axis);
        return planar.magnitude;
    }

    public void GetVerticalRange(out float minAlong, out float maxAlong)
    {
        GetClimbMetrics(out _, out _, out _, out _, out minAlong, out maxAlong, out _, out _);
    }

    private void GetClimbMetrics(
        out Vector3 origin,
        out Vector3 axis,
        out Vector3 right,
        out Vector3 face,
        out float minAlong,
        out float maxAlong,
        out float halfWidth,
        out float surfaceAlong)
    {
        origin = transform.position;
        axis = ClimbAxis.sqrMagnitude > 0.0001f ? ClimbAxis.normalized : Vector3.up;
        face = Vector3.ProjectOnPlane(FaceNormal, axis);
        if (face.sqrMagnitude < 0.0001f)
            face = Vector3.ProjectOnPlane(transform.forward, axis);
        if (face.sqrMagnitude < 0.0001f)
            face = Vector3.forward;
        face.Normalize();

        right = Vector3.Cross(axis, face);
        if (right.sqrMagnitude < 0.0001f)
            right = transform.right;
        right.Normalize();

        Vector3[] corners = GetVisualWorldCorners();
        bool found = TryProjectBounds(
            corners,
            origin,
            axis,
            right,
            out minAlong,
            out maxAlong,
            out halfWidth);

        if (!found)
        {
            corners = GetColliderWorldCorners(ResolveClimbTrigger());
            found = TryProjectBounds(
                corners,
                origin,
                axis,
                right,
                out minAlong,
                out maxAlong,
                out halfWidth);
        }

        if (!found)
        {
            minAlong = 0f;
            maxAlong = 1f;
            halfWidth = 0.35f;
        }

        surfaceAlong = 0f;
        if (corners != null && corners.Length > 0)
        {
            float maxFace = float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                float alongFace = Vector3.Dot(corners[i] - origin, face);
                if (alongFace > maxFace)
                    maxFace = alongFace;
            }

            if (maxFace > float.MinValue * 0.5f)
                surfaceAlong = maxFace;
        }

        if (bottomExitPoint != null)
            minAlong = Vector3.Dot(bottomExitPoint.position - origin, axis);
        if (topExitPoint != null)
            maxAlong = Vector3.Dot(topExitPoint.position - origin, axis);

        halfWidth = Mathf.Max(0.05f, halfWidth);
        if (maxAlong < minAlong + 0.2f)
            maxAlong = minAlong + 0.2f;
    }

    public void EnsureClimbVolume()
    {
        Transform existing = transform.Find(ClimbVolumeName);
        if (climbTrigger != null && !IsAutoClimbVolume(climbTrigger))
        {
            climbTrigger.isTrigger = true;
            return;
        }

        if (existing == null)
        {
            GameObject volume = new GameObject(ClimbVolumeName);
            volume.transform.SetParent(transform, false);
            volume.layer = gameObject.layer;
            volume.AddComponent<BoxCollider>();
            existing = volume.transform;
        }

        existing.localPosition = Vector3.zero;
        existing.localRotation = Quaternion.identity;
        existing.localScale = Vector3.one;

        BoxCollider box = existing.GetComponent<BoxCollider>();
        if (box == null)
            box = existing.gameObject.AddComponent<BoxCollider>();

        FitBoxToVisual(box);
        climbTrigger = box;
    }

    public void EnsureExitPoints()
    {
        GetVisualLocalBounds(out Vector3 localCenter, out Vector3 localSize);
        Vector3 localFace = ResolveLocalFace();
        float halfThick = HalfThickness(localSize, localFace);
        Vector3 stand = localCenter + localFace * (halfThick + PlayerOffsetFromSurface);

        if (topExitPoint == null)
        {
            Transform existing = transform.Find("TopExitPoint");
            if (existing != null)
            {
                topExitPoint = existing;
            }
            else
            {
                GameObject marker = new GameObject("TopExitPoint");
                marker.transform.SetParent(transform, false);
                marker.transform.localPosition = new Vector3(
                    stand.x,
                    localCenter.y + localSize.y * 0.5f,
                    stand.z);
                topExitPoint = marker.transform;
            }
        }

        if (bottomExitPoint == null)
        {
            Transform existing = transform.Find("BottomPoint");
            if (existing != null)
            {
                bottomExitPoint = existing;
            }
            else
            {
                GameObject marker = new GameObject("BottomPoint");
                marker.transform.SetParent(transform, false);
                marker.transform.localPosition = new Vector3(
                    stand.x,
                    localCenter.y - localSize.y * 0.5f,
                    stand.z);
                bottomExitPoint = marker.transform;
            }
        }
    }

    private Vector3 ResolveLocalFace()
    {
        switch (ResolveClimbFace())
        {
            case LadderClimbFace.Back:
                return Vector3.back;
            case LadderClimbFace.Right:
                return Vector3.right;
            case LadderClimbFace.Left:
                return Vector3.left;
            default:
                return Vector3.forward;
        }
    }

    private static float HalfThickness(Vector3 localSize, Vector3 localFace)
    {
        return 0.5f * (Mathf.Abs(localFace.x) * localSize.x + Mathf.Abs(localFace.z) * localSize.z);
    }

    private void FitAutoClimbVolume()
    {
        if (transform.Find(ClimbVolumeName) == null)
            return;
        EnsureClimbVolume();
    }

    private bool IsAutoClimbVolume(Collider collider)
    {
        return collider != null && collider.gameObject.name == ClimbVolumeName;
    }

    private void FitBoxToVisual(BoxCollider box)
    {
        GetVisualLocalBounds(out Vector3 localCenter, out Vector3 localSize);
        Vector3 localFace = ResolveLocalFace();
        float depth = Mathf.Max(0.34f, PlayerOffsetFromSurface + 0.08f);
        float height = Mathf.Max(0.5f, localSize.y) + topEntryPadding;
        float halfThick = HalfThickness(localSize, localFace);
        Vector3 frontCenter = localCenter + localFace * halfThick;
        frontCenter.y = localCenter.y + topEntryPadding * 0.5f;

        box.isTrigger = true;
        box.center = frontCenter + localFace * (depth * 0.5f - 0.04f);
        if (Mathf.Abs(localFace.z) >= 0.5f)
        {
            float width = Mathf.Max(0.4f, localSize.x + 0.1f);
            box.size = new Vector3(width, height, depth);
        }
        else
        {
            float width = Mathf.Max(0.4f, localSize.z + 0.1f);
            box.size = new Vector3(depth, height, width);
        }
    }

    private Transform FindVisualTransform()
    {
        if (visualMesh != null)
            return visualMesh;

        Transform named = transform.Find("Visual Mesh");
        if (named != null)
            return named;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (renderer.gameObject.name == ClimbVolumeName || renderer.gameObject.name == "TopPlatform")
                continue;
            if (renderer.transform == transform)
                return renderer.transform;
            return renderer.transform;
        }

        return transform;
    }

    private void GetVisualLocalBounds(out Vector3 localCenter, out Vector3 localSize)
    {
        Vector3[] corners = GetVisualWorldCorners();
        if (corners.Length == 0)
        {
            localCenter = new Vector3(0f, 2f, 0f);
            localSize = new Vector3(0.9f, 4f, 0.18f);
            return;
        }

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = transform.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        localCenter = (min + max) * 0.5f;
        localSize = max - min;
        localSize = new Vector3(
            Mathf.Max(0.05f, localSize.x),
            Mathf.Max(0.05f, localSize.y),
            Mathf.Max(0.05f, localSize.z));
    }

    private Vector3[] GetVisualWorldCorners()
    {
        Transform visual = FindVisualTransform();
        if (visual == null)
            return System.Array.Empty<Vector3>();

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
            return GetBoundsCorners(renderer.bounds);

        Collider collider = visual.GetComponent<Collider>();
        if (collider != null && collider.gameObject.name != ClimbVolumeName)
            return GetColliderWorldCorners(collider);

        return System.Array.Empty<Vector3>();
    }

    private static bool TryProjectBounds(
        Vector3[] corners,
        Vector3 origin,
        Vector3 axis,
        Vector3 right,
        out float minAlong,
        out float maxAlong,
        out float halfWidth)
    {
        minAlong = 0f;
        maxAlong = 1f;
        halfWidth = 0.35f;
        if (corners == null || corners.Length == 0)
            return false;

        minAlong = float.MaxValue;
        maxAlong = float.MinValue;
        halfWidth = 0.05f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 to = corners[i] - origin;
            minAlong = Mathf.Min(minAlong, Vector3.Dot(to, axis));
            maxAlong = Mathf.Max(maxAlong, Vector3.Dot(to, axis));
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(to, right)));
        }

        return maxAlong > minAlong;
    }

    private Collider ResolveClimbTrigger()
    {
        if (climbTrigger != null)
            return climbTrigger;

        Transform existing = transform.Find(ClimbVolumeName);
        if (existing != null)
            climbTrigger = existing.GetComponent<Collider>();

        return climbTrigger;
    }

    private void CacheSolidColliders()
    {
        Collider[] all = GetComponentsInChildren<Collider>(true);
        int count = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsBlockingClimbCollider(all[i]))
                count++;
        }

        solidColliders = new Collider[count];
        int index = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (IsBlockingClimbCollider(all[i]))
                solidColliders[index++] = all[i];
        }
    }

    private static bool IsBlockingClimbCollider(Collider collider)
    {
        if (collider == null || collider.isTrigger)
            return false;
        if (collider.gameObject.name == "TopPlatform")
            return false;
        return true;
    }

    private static Vector3[] GetColliderWorldCorners(Collider collider)
    {
        if (collider == null)
            return System.Array.Empty<Vector3>();

        if (collider is BoxCollider box)
        {
            Vector3 c = box.center;
            Vector3 e = box.size * 0.5f;
            Vector3[] local =
            {
                c + new Vector3(-e.x, -e.y, -e.z),
                c + new Vector3(-e.x, -e.y, e.z),
                c + new Vector3(-e.x, e.y, -e.z),
                c + new Vector3(-e.x, e.y, e.z),
                c + new Vector3(e.x, -e.y, -e.z),
                c + new Vector3(e.x, -e.y, e.z),
                c + new Vector3(e.x, e.y, -e.z),
                c + new Vector3(e.x, e.y, e.z)
            };

            Vector3[] world = new Vector3[8];
            for (int i = 0; i < 8; i++)
                world[i] = box.transform.TransformPoint(local[i]);
            return world;
        }

        return GetBoundsCorners(collider.bounds);
    }

    private static Vector3[] GetBoundsCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        GetClimbMetrics(out Vector3 origin, out Vector3 axis, out Vector3 right, out Vector3 face,
            out float minAlong, out float maxAlong, out float halfWidth, out float surfaceAlong);

        Vector3 bottom = origin + axis * minAlong;
        Vector3 top = origin + axis * maxAlong;
        Vector3 climbCenter = (bottom + top) * 0.5f;
        float height = Vector3.Distance(bottom, top);
        Vector3 faceCenter = climbCenter + face * surfaceAlong;
        Vector3 standPoint = faceCenter + face * PlayerOffsetFromSurface;

        Matrix4x4 old = Gizmos.matrix;
        Quaternion rotation = axis.sqrMagnitude > 0.0001f && face.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(face, axis)
            : transform.rotation;

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.18f);
        Gizmos.matrix = Matrix4x4.TRS(faceCenter + face * 0.06f, rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, new Vector3(halfWidth * 2f, height, 0.12f));
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(halfWidth * 2f, height, 0.12f));
        Gizmos.matrix = old;

        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(faceCenter, face * 1.4f);
        Gizmos.color = Color.green;
        Gizmos.DrawRay(bottom, axis * height);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(standPoint, 0.12f);

        Gizmos.color = Color.white;
        Vector3 topMark = topExitPoint != null ? topExitPoint.position : top;
        Gizmos.DrawSphere(topMark, 0.12f);
        Gizmos.color = Color.gray;
        Vector3 bottomMark = bottomExitPoint != null ? bottomExitPoint.position : bottom;
        Gizmos.DrawSphere(bottomMark, 0.1f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(faceCenter + face * 1.45f, "Climb this side");
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(topMark + Vector3.up * 0.15f, "Top Exit (get off)");
        UnityEditor.Handles.Label(bottomMark, "Bottom");
#endif
    }
}

public enum LadderClimbFace
{
    Auto,
    Forward,
    Back,
    Right,
    Left
}
