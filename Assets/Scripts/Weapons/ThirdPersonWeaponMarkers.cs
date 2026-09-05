using UnityEngine;

/// <summary>
/// Standardized third-person weapon marker names. Aim uses +Z forward, +Y up.
/// </summary>
public static class ThirdPersonWeaponMarkers
{
    public const string GripR = "Grip_R";
    public const string GripL = "Grip_L";
    public const string Aim = "Aim";
    public const string Muzzle = "Muzzle";
    public const string RightElbowHint = "RightElbowHint";
    public const string LeftElbowHint = "LeftElbowHint";

    public static readonly string[] GripRAliases = { GripR, "RightHandGrip" };
    public static readonly string[] GripLAliases = { GripL, "LeftHandGrip", "LeftHandIKTarget" };
    public static readonly string[] AimAliases = { Aim, "AimTarget", "AimPoint" };
    public static readonly string[] MuzzleAliases = { Muzzle, "MuzzlePoint" };

    public static Transform Find(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == names[n])
                    return children[i];
            }
        }

        return null;
    }

    public static Transform FindOrCreate(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        Transform existing = Find(parent, name);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        created.transform.localPosition = localPosition;
        created.transform.localRotation = localRotation;
        created.transform.localScale = Vector3.one;
        return created.transform;
    }
}

public struct ThirdPersonWeaponMarkerReport
{
    public string weaponName;
    public ThirdPersonWeaponPoseClass holdClass;
    public bool hasGripR;
    public bool hasGripL;
    public bool hasAim;
    public bool hasMuzzle;
    public bool hasHoldProfile;
    public bool usesLeftHand;
    public string holdProfileName;
    public string issues;

    public bool IsValid
    {
        get
        {
            if (!hasGripR || !hasAim || !hasMuzzle || !hasHoldProfile)
                return false;
            if (usesLeftHand && !hasGripL)
                return false;
            return true;
        }
    }
}
