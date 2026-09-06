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
    public BullseyeSurfaceRegionId[] neighbors = System.Array.Empty<BullseyeSurfaceRegionId>();

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
