using UnityEngine;

public enum BullseyeSurfaceRegionId : byte
{
    Head = 0,
    Neck = 1,
    UpperChest = 2,
    LowerChest = 3,
    UpperBack = 4,
    LowerBack = 5,
    LeftShoulder = 6,
    RightShoulder = 7,
    LeftUpperArm = 8,
    RightUpperArm = 9,
    LeftForearm = 10,
    RightForearm = 11,
    LeftThigh = 12,
    RightThigh = 13,
    LeftLowerLeg = 14,
    RightLowerLeg = 15
}

[System.Serializable]
public class BullseyeSurfaceRegion
{
    public BullseyeSurfaceRegionId id;
    public string displayName;
    public Transform bone;
    public Vector3 localPosition;
    public Vector3 localNormal = Vector3.forward;
    [Range(0f, 1f)] public float vertical;
    [Range(-1f, 1f)] public float lateral;
    public BullseyeFacing facing = BullseyeFacing.Front;
    public BullseyeBodyZone zone = BullseyeBodyZone.Torso;
    [Min(0f)] public float selectionWeight = 1f;
    [Min(0f)] public float wrapRadius;
    public BullseyeSurfaceRegionId[] neighbors = System.Array.Empty<BullseyeSurfaceRegionId>();

    public float ResolvedWrapRadius => wrapRadius > 0.001f ? wrapRadius : DefaultWrapRadius(id);

    public static float DefaultWrapRadius(BullseyeSurfaceRegionId id)
    {
        switch (id)
        {
            case BullseyeSurfaceRegionId.Head:
                return 0.12f;
            case BullseyeSurfaceRegionId.Neck:
                return 0.07f;
            case BullseyeSurfaceRegionId.UpperChest:
            case BullseyeSurfaceRegionId.UpperBack:
                return 0.17f;
            case BullseyeSurfaceRegionId.LowerChest:
            case BullseyeSurfaceRegionId.LowerBack:
                return 0.15f;
            case BullseyeSurfaceRegionId.LeftShoulder:
            case BullseyeSurfaceRegionId.RightShoulder:
                return 0.075f;
            case BullseyeSurfaceRegionId.LeftUpperArm:
            case BullseyeSurfaceRegionId.RightUpperArm:
                return 0.052f;
            case BullseyeSurfaceRegionId.LeftForearm:
            case BullseyeSurfaceRegionId.RightForearm:
                return 0.042f;
            case BullseyeSurfaceRegionId.LeftThigh:
            case BullseyeSurfaceRegionId.RightThigh:
                return 0.085f;
            case BullseyeSurfaceRegionId.LeftLowerLeg:
            case BullseyeSurfaceRegionId.RightLowerLeg:
                return 0.052f;
            default:
                return 0.1f;
        }
    }

    public Vector3 ResolvedWrapAxis(Vector3 worldNormal)
    {
        Vector3 along = bone != null ? bone.up : Vector3.up;
        if (Mathf.Abs(Vector3.Dot(along.normalized, worldNormal)) > 0.92f)
        {
            along = Mathf.Abs(worldNormal.y) < 0.88f
                ? Vector3.up
                : (bone != null ? bone.right : Vector3.right);
        }

        if (along.sqrMagnitude < 0.0001f)
            along = Vector3.up;
        return along.normalized;
    }

    public bool TryEvaluate(out Vector3 worldPosition, out Vector3 worldNormal)
    {
        if (bone == null)
        {
            worldPosition = Vector3.zero;
            worldNormal = Vector3.up;
            return false;
        }

        worldPosition = bone.TransformPoint(localPosition);
        worldNormal = bone.TransformDirection(localNormal);
        if (worldNormal.sqrMagnitude < 0.0001f)
            worldNormal = bone.forward;
        worldNormal.Normalize();
        return true;
    }
}
