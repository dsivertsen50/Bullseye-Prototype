using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Marker contract on a third-person weapon prefab. Grip_R / Grip_L tell
/// the arm rig where each hand belongs. Aim is +Z forward, +Y up.
/// </summary>
[DisallowMultipleComponent]
public class ThirdPersonWeaponVisual : MonoBehaviour
{
    [SerializeField, FormerlySerializedAs("rightHandGrip")] private Transform gripR;
    [SerializeField, FormerlySerializedAs("leftHandIkTarget")] private Transform gripL;
    [SerializeField, FormerlySerializedAs("aimTarget")] private Transform aim;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform rightElbowHint;
    [SerializeField, FormerlySerializedAs("leftElbowHint")] private Transform leftElbowHint;
    [SerializeField] private bool drawGizmos = true;

    public Transform GripR => gripR;
    public Transform GripL => gripL;
    public Transform Aim => aim;
    public Transform Muzzle => muzzle;
    public Transform RightElbowHint => rightElbowHint;
    public Transform LeftElbowHint => leftElbowHint;

    public Transform LeftHandIkTarget => gripL;
    public Transform LeftHandGrip => gripL;
    public Transform RightHandGrip => gripR;
    public Transform AimTarget => aim;

    public void Assign(
        Transform leftHand,
        Transform muzzlePoint,
        Transform grip = null,
        Transform aimPoint = null,
        Transform elbowHint = null)
    {
        gripL = leftHand;
        muzzle = muzzlePoint;
        gripR = grip;
        aim = aimPoint;
        leftElbowHint = elbowHint;
    }

    public void AssignMarkers(
        Transform rightGrip,
        Transform leftGrip,
        Transform aimPoint,
        Transform muzzlePoint,
        Transform rightHint = null,
        Transform leftHint = null)
    {
        gripR = rightGrip;
        gripL = leftGrip;
        aim = aimPoint;
        muzzle = muzzlePoint;
        rightElbowHint = rightHint;
        leftElbowHint = leftHint;
    }

    public void ResolveFallbacks()
    {
        if (gripR == null)
            gripR = ThirdPersonWeaponMarkers.Find(transform, ThirdPersonWeaponMarkers.GripRAliases);
        if (gripL == null)
            gripL = ThirdPersonWeaponMarkers.Find(transform, ThirdPersonWeaponMarkers.GripLAliases);
        if (aim == null)
            aim = ThirdPersonWeaponMarkers.Find(transform, ThirdPersonWeaponMarkers.AimAliases);
        if (muzzle == null)
            muzzle = ThirdPersonWeaponMarkers.Find(transform, ThirdPersonWeaponMarkers.MuzzleAliases);
        if (rightElbowHint == null)
            rightElbowHint = ThirdPersonWeaponMarkers.Find(transform, ThirdPersonWeaponMarkers.RightElbowHint);
        if (leftElbowHint == null)
            leftElbowHint = ThirdPersonWeaponMarkers.Find(transform, ThirdPersonWeaponMarkers.LeftElbowHint);
    }

    public ThirdPersonWeaponMarkerReport BuildReport(WeaponDefinition definition)
    {
        ResolveFallbacks();
        bool wantsLeft = definition == null || definition.UseLeftHandGrip;
        ThirdPersonWeaponHoldProfile profile = ThirdPersonWeaponHoldResolver.Resolve(
            definition,
            ThirdPersonWeaponPoseKind.Hold);
        var report = new ThirdPersonWeaponMarkerReport
        {
            weaponName = definition != null ? definition.DisplayName : name,
            holdClass = definition != null ? definition.ThirdPersonHoldClass : ThirdPersonWeaponPoseClass.LongGun,
            hasGripR = gripR != null,
            hasGripL = gripL != null,
            hasAim = aim != null,
            hasMuzzle = muzzle != null,
            hasHoldProfile = profile != null,
            usesLeftHand = wantsLeft,
            holdProfileName = profile != null ? profile.name : string.Empty
        };

        System.Text.StringBuilder issues = new System.Text.StringBuilder();
        if (!report.hasGripR)
            issues.Append("Missing Grip_R. ");
        if (wantsLeft && !report.hasGripL)
            issues.Append("Missing Grip_L. ");
        if (!report.hasAim)
            issues.Append("Missing Aim. ");
        if (!report.hasMuzzle)
            issues.Append("Missing Muzzle. ");
        if (!report.hasHoldProfile)
            issues.Append("Missing hold profile. ");
        report.issues = issues.ToString().Trim();
        return report;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        DrawMarker(gripR, new Color(0.3f, 1f, 0.35f), 0.02f);
        DrawMarker(gripL, new Color(0.2f, 0.75f, 1f), 0.025f);
        DrawMarker(aim, new Color(1f, 0.9f, 0.2f), 0.016f);
        DrawMarker(muzzle, new Color(1f, 0.45f, 0.15f), 0.018f);
        DrawMarker(rightElbowHint, new Color(1f, 0.55f, 0.2f), 0.02f);
        DrawMarker(leftElbowHint, new Color(0.95f, 0.45f, 1f), 0.022f);
    }

    private static void DrawMarker(Transform marker, Color color, float radius)
    {
        if (marker == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(marker.position, radius);
        Gizmos.DrawLine(marker.position, marker.position + marker.forward * radius * 3f);
        Gizmos.DrawLine(marker.position, marker.position + marker.up * radius * 2f);
    }
#endif
}
