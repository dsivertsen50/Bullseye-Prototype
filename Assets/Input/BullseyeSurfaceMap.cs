using UnityEngine;

/// <summary>
/// Data-driven graph of valid bullseye locations on the animated player mesh.
/// Each region is a bone-local surface sample so it follows animation without
/// baking a MeshCollider.
/// </summary>
public class BullseyeSurfaceMap : MonoBehaviour
{
    [SerializeField] private Transform bodyRoot;
    [SerializeField] private BullseyeSurfaceRegion[] regions = System.Array.Empty<BullseyeSurfaceRegion>();

    public Transform BodyRoot => bodyRoot != null ? bodyRoot : transform;
    public int RegionCount => regions != null ? regions.Length : 0;

    public void Assign(Transform root, BullseyeSurfaceRegion[] nextRegions)
    {
        bodyRoot = root;
        regions = nextRegions ?? System.Array.Empty<BullseyeSurfaceRegion>();
    }

    public bool TryGetRegion(int index, out BullseyeSurfaceRegion region)
    {
        if (regions == null || index < 0 || index >= regions.Length)
        {
            region = null;
            return false;
        }

        region = regions[index];
        return region != null;
    }

    public bool TryGetRegion(BullseyeSurfaceRegionId id, out BullseyeSurfaceRegion region)
    {
        return TryGetRegion((int)id, out region);
    }

    public bool TryEvaluate(int index, out Vector3 worldPosition, out Vector3 worldNormal)
    {
        if (!TryGetRegion(index, out BullseyeSurfaceRegion region))
        {
            worldPosition = Vector3.zero;
            worldNormal = Vector3.up;
            return false;
        }

        return region.TryEvaluate(out worldPosition, out worldNormal);
    }

    public bool TryEvaluateInterpolated(
        int fromIndex,
        int toIndex,
        float progress,
        out Vector3 worldPosition,
        out Vector3 worldNormal)
    {
        return TryEvaluateInterpolated(
            fromIndex,
            toIndex,
            progress,
            out worldPosition,
            out worldNormal,
            out _,
            out _);
    }

    public bool TryEvaluateInterpolated(
        int fromIndex,
        int toIndex,
        float progress,
        out Vector3 worldPosition,
        out Vector3 worldNormal,
        out Vector3 wrapAxis,
        out float wrapRadius)
    {
        progress = Mathf.Clamp01(progress);
        bool hasFrom = TryEvaluate(fromIndex, out Vector3 fromPos, out Vector3 fromNormal);
        bool hasTo = TryEvaluate(toIndex, out Vector3 toPos, out Vector3 toNormal);
        float fromRadius = GetWrapRadius(fromIndex);
        float toRadius = GetWrapRadius(toIndex);

        if (!hasFrom && !hasTo)
        {
            worldPosition = BodyRoot.position + Vector3.up;
            worldNormal = BodyRoot.forward;
            wrapAxis = BodyRoot.up;
            wrapRadius = 0.12f;
            return false;
        }

        if (!hasFrom)
        {
            worldPosition = toPos;
            worldNormal = toNormal;
            wrapAxis = GetWrapAxis(toIndex, toNormal);
            wrapRadius = toRadius;
            return true;
        }

        if (!hasTo || fromIndex == toIndex)
        {
            worldPosition = fromPos;
            worldNormal = fromNormal;
            wrapAxis = GetWrapAxis(fromIndex, fromNormal);
            wrapRadius = fromRadius;
            return true;
        }

        wrapRadius = Mathf.Lerp(fromRadius, toRadius, progress);
        wrapAxis = Vector3.Slerp(
            GetStableWrapAxis(fromIndex, fromNormal),
            GetStableWrapAxis(toIndex, toNormal),
            progress);
        if (wrapAxis.sqrMagnitude < 0.0001f)
            wrapAxis = BodyRoot.up;
        wrapAxis.Normalize();
        EvaluateCylinderSurface(
            fromPos,
            fromNormal,
            GetBonePosition(fromIndex),
            fromRadius,
            toPos,
            toNormal,
            GetBonePosition(toIndex),
            toRadius,
            wrapAxis,
            progress,
            out worldPosition,
            out worldNormal);
        return true;
    }

    public float EstimateSurfaceDistance(int fromIndex, int toIndex)
    {
        if (!TryEvaluate(fromIndex, out Vector3 fromPos, out Vector3 fromNormal) ||
            !TryEvaluate(toIndex, out Vector3 toPos, out Vector3 toNormal))
            return 0.28f;

        Vector3 axis = Vector3.Slerp(
            GetStableWrapAxis(fromIndex, fromNormal),
            GetStableWrapAxis(toIndex, toNormal),
            0.5f);
        if (axis.sqrMagnitude < 0.0001f)
            axis = BodyRoot.up;
        axis.Normalize();

        Vector3 fromBone = GetBonePosition(fromIndex);
        Vector3 toBone = GetBonePosition(toIndex);
        Vector3 fromRadial = Vector3.ProjectOnPlane(fromPos - fromBone, axis);
        Vector3 toRadial = Vector3.ProjectOnPlane(toPos - toBone, axis);
        float radius = Mathf.Max(
            GetWrapRadius(fromIndex),
            GetWrapRadius(toIndex),
            (fromRadial.magnitude + toRadial.magnitude) * 0.5f);
        float angle = 0f;
        if (fromRadial.sqrMagnitude > 0.0001f && toRadial.sqrMagnitude > 0.0001f)
            angle = Mathf.Abs(SignedOrbitAngle(fromRadial, toRadial, axis)) * Mathf.Deg2Rad;
        float along = Mathf.Abs(Vector3.Dot((toBone - fromBone) + (toPos - toBone) - (fromPos - fromBone), axis));
        return Mathf.Max(0.12f, angle * radius + along);
    }

    private void EvaluateCylinderSurface(
        Vector3 fromPos,
        Vector3 fromNormal,
        Vector3 fromBone,
        float fromWrap,
        Vector3 toPos,
        Vector3 toNormal,
        Vector3 toBone,
        float toWrap,
        Vector3 wrapAxis,
        float progress,
        out Vector3 worldPosition,
        out Vector3 worldNormal)
    {
        Vector3 axis = wrapAxis.sqrMagnitude > 0.0001f ? wrapAxis.normalized : BodyRoot.up;
        Vector3 fromRadial = Vector3.ProjectOnPlane(fromPos - fromBone, axis);
        Vector3 toRadial = Vector3.ProjectOnPlane(toPos - toBone, axis);

        if (fromRadial.sqrMagnitude < 0.0004f)
            fromRadial = Vector3.ProjectOnPlane(fromNormal, axis);
        if (toRadial.sqrMagnitude < 0.0004f)
            toRadial = Vector3.ProjectOnPlane(toNormal, axis);
        if (fromRadial.sqrMagnitude < 0.0004f)
            fromRadial = BodyRoot.forward;
        if (toRadial.sqrMagnitude < 0.0004f)
            toRadial = fromRadial;

        float fromR = Mathf.Max(fromWrap, fromRadial.magnitude);
        float toR = Mathf.Max(toWrap, toRadial.magnitude);
        float radius = Mathf.Lerp(fromR, toR, progress);
        Vector3 fromOnAxis = fromBone + axis * Vector3.Dot(fromPos - fromBone, axis);
        Vector3 toOnAxis = toBone + axis * Vector3.Dot(toPos - toBone, axis);
        Vector3 onAxis = Vector3.Lerp(fromOnAxis, toOnAxis, progress);
        float angle = SignedOrbitAngle(fromRadial, toRadial, axis);
        Vector3 radial = Quaternion.AngleAxis(angle * progress, axis) * fromRadial.normalized * radius;
        worldPosition = onAxis + radial;
        worldNormal = radial.sqrMagnitude > 0.0001f ? radial.normalized : fromNormal.normalized;
    }

    private float SignedOrbitAngle(Vector3 fromRadial, Vector3 toRadial, Vector3 axis)
    {
        float angle = Vector3.SignedAngle(fromRadial, toRadial, axis);
        if (Mathf.Abs(angle) <= 165f)
            return angle;

        Vector3 hint = Vector3.ProjectOnPlane(BodyRoot.right, axis);
        if (hint.sqrMagnitude < 0.0001f)
            hint = Vector3.right;
        float side = Mathf.Sign(Vector3.Dot(Vector3.Cross(fromRadial, hint), axis));
        if (Mathf.Approximately(side, 0f))
            side = 1f;
        return 180f * side;
    }

    private Vector3 GetBonePosition(int index)
    {
        if (TryGetRegion(index, out BullseyeSurfaceRegion region) && region.bone != null)
            return region.bone.position;
        return BodyRoot.position;
    }

    private Vector3 GetStableWrapAxis(int index, Vector3 worldNormal)
    {
        if (UsesBodyOrbit(index))
        {
            Vector3 axis = BodyRoot.up;
            if (TryGetRegion(index, out BullseyeSurfaceRegion region) && region.bone != null)
            {
                Vector3 boneUp = region.bone.up;
                if (Vector3.Dot(boneUp, axis) < 0f)
                    boneUp = -boneUp;
                if (Mathf.Abs(Vector3.Dot(boneUp, axis)) > 0.35f)
                    axis = Vector3.Slerp(axis, boneUp, 0.4f);
            }

            return axis.normalized;
        }

        return GetWrapAxis(index, worldNormal);
    }

    private static bool UsesBodyOrbit(int index)
    {
        switch ((BullseyeSurfaceRegionId)index)
        {
            case BullseyeSurfaceRegionId.Head:
            case BullseyeSurfaceRegionId.Neck:
            case BullseyeSurfaceRegionId.UpperChest:
            case BullseyeSurfaceRegionId.LowerChest:
            case BullseyeSurfaceRegionId.UpperBack:
            case BullseyeSurfaceRegionId.LowerBack:
                return true;
            default:
                return false;
        }
    }

    public float GetWrapRadius(int index)
    {
        return TryGetRegion(index, out BullseyeSurfaceRegion region)
            ? region.ResolvedWrapRadius
            : 0.12f;
    }

    public Vector3 GetWrapAxis(int index, Vector3 worldNormal)
    {
        if (TryGetRegion(index, out BullseyeSurfaceRegion region))
            return region.ResolvedWrapAxis(worldNormal);
        return BodyRoot.up;
    }

    private static Vector3 OrbitNormal(Vector3 fromNormal, Vector3 toNormal, Vector3 wrapAxis, float progress)
    {
        fromNormal.Normalize();
        toNormal.Normalize();
        Vector3 pivot = Vector3.Cross(fromNormal, toNormal);
        if (pivot.sqrMagnitude < 0.0005f)
        {
            if (Vector3.Dot(fromNormal, toNormal) > 0f)
                return fromNormal;

            Vector3 orbit = wrapAxis.sqrMagnitude > 0.0001f ? wrapAxis.normalized : Vector3.up;
            if (Mathf.Abs(Vector3.Dot(orbit, fromNormal)) > 0.9f)
                orbit = Vector3.Cross(fromNormal, Vector3.right).sqrMagnitude > 0.01f
                    ? Vector3.Cross(fromNormal, Vector3.right)
                    : Vector3.Cross(fromNormal, Vector3.up);
            return (Quaternion.AngleAxis(180f * progress, orbit.normalized) * fromNormal).normalized;
        }

        float angle = Vector3.Angle(fromNormal, toNormal);
        return (Quaternion.AngleAxis(angle * progress, pivot.normalized) * fromNormal).normalized;
    }

    public Quaternion RotationFromNormal(Vector3 worldNormal)
    {
        Transform root = BodyRoot;
        Vector3 upHint = Mathf.Abs(Vector3.Dot(worldNormal, root.up)) > 0.95f
            ? root.forward
            : root.up;
        if (worldNormal.sqrMagnitude < 0.0001f)
            worldNormal = root.forward;
        return Quaternion.LookRotation(worldNormal.normalized, upHint);
    }

    public float GetVertical(int index)
    {
        return TryGetRegion(index, out BullseyeSurfaceRegion region) ? region.vertical : 0.5f;
    }

    public float GetLateral(int index)
    {
        return TryGetRegion(index, out BullseyeSurfaceRegion region) ? region.lateral : 0f;
    }

    public BullseyeFacing GetFacing(int index)
    {
        return TryGetRegion(index, out BullseyeSurfaceRegion region)
            ? region.facing
            : BullseyeFacing.Front;
    }

    public BullseyeBodyZone GetZone(int index)
    {
        return TryGetRegion(index, out BullseyeSurfaceRegion region)
            ? region.zone
            : BullseyeBodyZone.Torso;
    }

    public float GetWeight(int index)
    {
        if (!TryGetRegion(index, out BullseyeSurfaceRegion region))
            return 0f;
        return Mathf.Max(0f, region.selectionWeight);
    }

    public int GetNeighborCount(int index)
    {
        if (!TryGetRegion(index, out BullseyeSurfaceRegion region) || region.neighbors == null)
            return 0;
        return region.neighbors.Length;
    }

    public bool TryGetNeighbor(int index, int neighborSlot, out int neighborIndex)
    {
        neighborIndex = -1;
        if (!TryGetRegion(index, out BullseyeSurfaceRegion region) || region.neighbors == null)
            return false;
        if (neighborSlot < 0 || neighborSlot >= region.neighbors.Length)
            return false;

        neighborIndex = (int)region.neighbors[neighborSlot];
        return TryGetRegion(neighborIndex, out _);
    }

    public int FindNearestRegion(Vector3 worldPosition)
    {
        int best = 0;
        float bestSq = float.MaxValue;
        int count = RegionCount;
        for (int i = 0; i < count; i++)
        {
            if (!TryEvaluate(i, out Vector3 position, out _))
                continue;

            float sq = (position - worldPosition).sqrMagnitude;
            if (sq >= bestSq)
                continue;

            bestSq = sq;
            best = i;
        }

        return best;
    }

    public int DefaultAttachedRegion()
    {
        int chest = (int)BullseyeSurfaceRegionId.UpperChest;
        return TryGetRegion(chest, out _) ? chest : 0;
    }

    public BullseyeBodyPosition ToBodyPosition(
        int fromIndex,
        int toIndex,
        float progress,
        Vector3 worldPosition)
    {
        progress = Mathf.Clamp01(progress);
        float height = Mathf.Lerp(GetVertical(fromIndex), GetVertical(toIndex), progress);
        float lateral = Mathf.Lerp(GetLateral(fromIndex), GetLateral(toIndex), progress);
        BullseyeFacing facing = progress < 0.5f ? GetFacing(fromIndex) : GetFacing(toIndex);

        Transform root = BodyRoot;
        Vector3 local = Quaternion.Inverse(root.rotation) * (worldPosition - root.position);
        return new BullseyeBodyPosition(height, lateral, facing, local);
    }

    public static BullseyeSurfaceRegion[] CreateDefaultRegions()
    {
        return new[]
        {
            Region(BullseyeSurfaceRegionId.Head, "Head", 0.96f, 0f, BullseyeFacing.Front, BullseyeBodyZone.Head, 0.7f,
                BullseyeSurfaceRegionId.Neck),
            Region(BullseyeSurfaceRegionId.Neck, "Neck", 0.88f, 0f, BullseyeFacing.Front, BullseyeBodyZone.Head, 0.85f,
                BullseyeSurfaceRegionId.Head, BullseyeSurfaceRegionId.UpperChest, BullseyeSurfaceRegionId.UpperBack),
            Region(BullseyeSurfaceRegionId.UpperChest, "Upper Chest", 0.72f, 0f, BullseyeFacing.Front, BullseyeBodyZone.Torso, 1.15f,
                BullseyeSurfaceRegionId.Neck, BullseyeSurfaceRegionId.LowerChest, BullseyeSurfaceRegionId.LeftShoulder, BullseyeSurfaceRegionId.RightShoulder),
            Region(BullseyeSurfaceRegionId.LowerChest, "Lower Chest", 0.58f, 0f, BullseyeFacing.Front, BullseyeBodyZone.Torso, 1.1f,
                BullseyeSurfaceRegionId.UpperChest, BullseyeSurfaceRegionId.LeftThigh, BullseyeSurfaceRegionId.RightThigh, BullseyeSurfaceRegionId.LowerBack),
            Region(BullseyeSurfaceRegionId.UpperBack, "Upper Back", 0.72f, 0f, BullseyeFacing.Back, BullseyeBodyZone.Torso, 1.05f,
                BullseyeSurfaceRegionId.Neck, BullseyeSurfaceRegionId.LowerBack, BullseyeSurfaceRegionId.LeftShoulder, BullseyeSurfaceRegionId.RightShoulder),
            Region(BullseyeSurfaceRegionId.LowerBack, "Lower Back", 0.56f, 0f, BullseyeFacing.Back, BullseyeBodyZone.Torso, 1f,
                BullseyeSurfaceRegionId.UpperBack, BullseyeSurfaceRegionId.LowerChest, BullseyeSurfaceRegionId.LeftThigh, BullseyeSurfaceRegionId.RightThigh),
            Region(BullseyeSurfaceRegionId.LeftShoulder, "Left Shoulder", 0.74f, -0.7f, BullseyeFacing.Front, BullseyeBodyZone.Torso, 0.9f,
                BullseyeSurfaceRegionId.UpperChest, BullseyeSurfaceRegionId.UpperBack, BullseyeSurfaceRegionId.LeftUpperArm, BullseyeSurfaceRegionId.Neck),
            Region(BullseyeSurfaceRegionId.RightShoulder, "Right Shoulder", 0.74f, 0.7f, BullseyeFacing.Front, BullseyeBodyZone.Torso, 0.9f,
                BullseyeSurfaceRegionId.UpperChest, BullseyeSurfaceRegionId.UpperBack, BullseyeSurfaceRegionId.RightUpperArm, BullseyeSurfaceRegionId.Neck),
            Region(BullseyeSurfaceRegionId.LeftUpperArm, "Left Upper Arm", 0.66f, -1f, BullseyeFacing.Front, BullseyeBodyZone.Torso, 0.75f,
                BullseyeSurfaceRegionId.LeftShoulder, BullseyeSurfaceRegionId.LeftForearm),
            Region(BullseyeSurfaceRegionId.RightUpperArm, "Right Upper Arm", 0.66f, 1f, BullseyeFacing.Front, BullseyeBodyZone.Torso, 0.75f,
                BullseyeSurfaceRegionId.RightShoulder, BullseyeSurfaceRegionId.RightForearm),
            Region(BullseyeSurfaceRegionId.LeftForearm, "Left Forearm", 0.5f, -1f, BullseyeFacing.Front, BullseyeBodyZone.LowerBody, 0.45f,
                BullseyeSurfaceRegionId.LeftUpperArm),
            Region(BullseyeSurfaceRegionId.RightForearm, "Right Forearm", 0.5f, 1f, BullseyeFacing.Front, BullseyeBodyZone.LowerBody, 0.45f,
                BullseyeSurfaceRegionId.RightUpperArm),
            Region(BullseyeSurfaceRegionId.LeftThigh, "Left Thigh", 0.32f, -0.45f, BullseyeFacing.Front, BullseyeBodyZone.LowerBody, 0.8f,
                BullseyeSurfaceRegionId.LowerChest, BullseyeSurfaceRegionId.LowerBack, BullseyeSurfaceRegionId.LeftLowerLeg),
            Region(BullseyeSurfaceRegionId.RightThigh, "Right Thigh", 0.32f, 0.45f, BullseyeFacing.Front, BullseyeBodyZone.LowerBody, 0.8f,
                BullseyeSurfaceRegionId.LowerChest, BullseyeSurfaceRegionId.LowerBack, BullseyeSurfaceRegionId.RightLowerLeg),
            Region(BullseyeSurfaceRegionId.LeftLowerLeg, "Left Lower Leg", 0.12f, -0.4f, BullseyeFacing.Front, BullseyeBodyZone.LowerBody, 0.4f,
                BullseyeSurfaceRegionId.LeftThigh),
            Region(BullseyeSurfaceRegionId.RightLowerLeg, "Right Lower Leg", 0.12f, 0.4f, BullseyeFacing.Front, BullseyeBodyZone.LowerBody, 0.4f,
                BullseyeSurfaceRegionId.RightThigh)
        };
    }

    private static BullseyeSurfaceRegion Region(
        BullseyeSurfaceRegionId id,
        string name,
        float vertical,
        float lateral,
        BullseyeFacing facing,
        BullseyeBodyZone zone,
        float weight,
        params BullseyeSurfaceRegionId[] neighbors)
    {
        return new BullseyeSurfaceRegion
        {
            id = id,
            displayName = name,
            vertical = vertical,
            lateral = lateral,
            facing = facing,
            zone = zone,
            selectionWeight = weight,
            wrapRadius = BullseyeSurfaceRegion.DefaultWrapRadius(id),
            neighbors = neighbors,
            localNormal = facing == BullseyeFacing.Back ? Vector3.back : Vector3.forward
        };
    }
}
