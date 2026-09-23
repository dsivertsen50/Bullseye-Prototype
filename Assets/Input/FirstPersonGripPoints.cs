using UnityEngine;

/// <summary>
/// RightHandGrip / LeftHandGrip markers on a first-person weapon.
/// Created when a weapon prefab does not already define them.
/// </summary>
public static class FirstPersonGripPoints
{
    public const string RightName = "RightHandGrip";
    public const string LeftName = "LeftHandGrip";

    public static void Ensure(Transform weaponRoot, Transform aimPoint, Transform muzzlePoint, bool supportHand)
    {
        if (weaponRoot == null)
            return;

        if (FindRight(weaponRoot) == null)
            Create(weaponRoot, RightName, EstimateRight(weaponRoot, aimPoint, muzzlePoint));

        if (supportHand && FindLeft(weaponRoot) == null)
            Create(weaponRoot, LeftName, EstimateLeft(weaponRoot, aimPoint, muzzlePoint));
    }

    public static Transform FindRight(Transform weaponRoot)
    {
        return ThirdPersonWeaponMarkers.Find(weaponRoot, ThirdPersonWeaponMarkers.GripRAliases);
    }

    public static Transform FindLeft(Transform weaponRoot)
    {
        return ThirdPersonWeaponMarkers.Find(weaponRoot, ThirdPersonWeaponMarkers.GripLAliases);
    }

    public static Vector3 EstimateRight(Transform weaponRoot, Transform aimPoint, Transform muzzlePoint)
    {
        Vector3 aim = LocalPoint(weaponRoot, aimPoint, new Vector3(0f, 0.02f, -0.05f));
        Vector3 forward = Forward(weaponRoot, aimPoint, muzzlePoint, aim);
        return aim - forward * 0.03f + new Vector3(0.03f, -0.045f, 0f);
    }

    public static Vector3 EstimateLeft(Transform weaponRoot, Transform aimPoint, Transform muzzlePoint)
    {
        Vector3 aim = LocalPoint(weaponRoot, aimPoint, new Vector3(0f, 0.02f, -0.05f));
        Vector3 muzzle = LocalPoint(weaponRoot, muzzlePoint, aim + Vector3.forward * 0.4f);
        return Vector3.Lerp(aim, muzzle, 0.42f) + new Vector3(-0.015f, -0.02f, 0f);
    }

    private static Vector3 Forward(Transform weaponRoot, Transform aimPoint, Transform muzzlePoint, Vector3 aim)
    {
        Vector3 muzzle = LocalPoint(weaponRoot, muzzlePoint, aim + Vector3.forward * 0.4f);
        Vector3 forward = muzzle - aim;
        if (forward.sqrMagnitude < 0.0001f)
            return Vector3.forward;
        return forward.normalized;
    }

    private static Vector3 LocalPoint(Transform weaponRoot, Transform point, Vector3 fallback)
    {
        if (weaponRoot == null || point == null)
            return fallback;
        return weaponRoot.InverseTransformPoint(point.position);
    }

    private static void Create(Transform parent, string name, Vector3 localPosition)
    {
        GameObject grip = new GameObject(name);
        grip.transform.SetParent(parent, false);
        grip.transform.localPosition = localPosition;
        grip.transform.localRotation = Quaternion.identity;
        grip.transform.localScale = Vector3.one;
    }
}
