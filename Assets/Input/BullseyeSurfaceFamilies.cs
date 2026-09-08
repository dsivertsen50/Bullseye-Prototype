using UnityEngine;

/// <summary>
/// Coarse body-part families used by the attached character-material bullseye.
/// Nearby limbs stay in a different family so a hip mark cannot paint an arm.
/// </summary>
public static class BullseyeSurfaceFamilies
{
    public const float Head = 0f;
    public const float Neck = 1f;
    public const float Torso = 2f;
    public const float LeftArm = 3f;
    public const float RightArm = 4f;
    public const float LeftLeg = 5f;
    public const float RightLeg = 6f;

    public static float FromRegion(BullseyeSurfaceRegionId id)
    {
        switch (id)
        {
            case BullseyeSurfaceRegionId.Head:
                return Head;
            case BullseyeSurfaceRegionId.Neck:
                return Neck;
            case BullseyeSurfaceRegionId.UpperChest:
            case BullseyeSurfaceRegionId.LowerChest:
            case BullseyeSurfaceRegionId.UpperBack:
            case BullseyeSurfaceRegionId.LowerBack:
                return Torso;
            case BullseyeSurfaceRegionId.LeftShoulder:
            case BullseyeSurfaceRegionId.LeftUpperArm:
            case BullseyeSurfaceRegionId.LeftForearm:
                return LeftArm;
            case BullseyeSurfaceRegionId.RightShoulder:
            case BullseyeSurfaceRegionId.RightUpperArm:
            case BullseyeSurfaceRegionId.RightForearm:
                return RightArm;
            case BullseyeSurfaceRegionId.LeftThigh:
            case BullseyeSurfaceRegionId.LeftLowerLeg:
                return LeftLeg;
            case BullseyeSurfaceRegionId.RightThigh:
            case BullseyeSurfaceRegionId.RightLowerLeg:
                return RightLeg;
            default:
                return Torso;
        }
    }

    public static float FacingValue(BullseyeFacing facing)
    {
        return facing == BullseyeFacing.Back ? -1f : 1f;
    }

    public static bool UsesCylindricalWrap(BullseyeSurfaceRegionId id)
    {
        return !UsesSphericalWrap(id);
    }

    public static bool UsesSphericalWrap(BullseyeSurfaceRegionId id)
    {
        return id == BullseyeSurfaceRegionId.Head;
    }

    public static float FromBoneName(string boneName)
    {
        if (string.IsNullOrEmpty(boneName))
            return Torso;

        string name = boneName;
        int colon = name.LastIndexOf(':');
        if (colon >= 0 && colon + 1 < name.Length)
            name = name.Substring(colon + 1);

        if (Contains(name, "Head") || Contains(name, "HeadTop"))
            return Head;
        if (Contains(name, "Neck"))
            return Neck;
        if (Contains(name, "Left"))
        {
            if (Contains(name, "UpLeg") || Contains(name, "Leg") || Contains(name, "Foot") || Contains(name, "Toe"))
                return LeftLeg;
            if (Contains(name, "Shoulder") || Contains(name, "Arm") || Contains(name, "Hand") || Contains(name, "ForeArm"))
                return LeftArm;
        }

        if (Contains(name, "Right"))
        {
            if (Contains(name, "UpLeg") || Contains(name, "Leg") || Contains(name, "Foot") || Contains(name, "Toe"))
                return RightLeg;
            if (Contains(name, "Shoulder") || Contains(name, "Arm") || Contains(name, "Hand") || Contains(name, "ForeArm"))
                return RightArm;
        }

        return Torso;
    }

    private static bool Contains(string value, string token)
    {
        return value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
