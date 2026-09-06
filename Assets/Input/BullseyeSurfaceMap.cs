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
        progress = Mathf.Clamp01(progress);
        bool hasFrom = TryEvaluate(fromIndex, out Vector3 fromPos, out Vector3 fromNormal);
        bool hasTo = TryEvaluate(toIndex, out Vector3 toPos, out Vector3 toNormal);

        if (!hasFrom && !hasTo)
        {
            worldPosition = BodyRoot.position + Vector3.up;
            worldNormal = BodyRoot.forward;
            return false;
        }

        if (!hasFrom)
        {
            worldPosition = toPos;
            worldNormal = toNormal;
            return true;
        }

        if (!hasTo || fromIndex == toIndex)
        {
            worldPosition = fromPos;
            worldNormal = fromNormal;
            return true;
        }

        worldPosition = Vector3.Lerp(fromPos, toPos, progress);
        worldNormal = Vector3.Slerp(fromNormal, toNormal, progress).normalized;
        return true;
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
            neighbors = neighbors,
            localNormal = facing == BullseyeFacing.Back ? Vector3.back : Vector3.forward
        };
    }
}
