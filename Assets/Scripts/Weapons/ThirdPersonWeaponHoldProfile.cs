using UnityEngine;

/// <summary>
/// Procedural third-person hold configuration. Positions the weapon and
/// configures arm IK. This is not a locomotion animation.
/// </summary>
[CreateAssetMenu(
    fileName = "ThirdPersonWeaponHoldProfile",
    menuName = "Bullseye/Weapons/Third-Person Weapon Hold Profile")]
public class ThirdPersonWeaponHoldProfile : ScriptableObject
{
    [SerializeField] private ThirdPersonWeaponPoseClass holdClass = ThirdPersonWeaponPoseClass.LongGun;
    [SerializeField] private ThirdPersonWeaponPoseKind holdKind = ThirdPersonWeaponPoseKind.Hold;

    [Header("Weapon Anchor")]
    [SerializeField] private Vector3 weaponAnchorLocalPosition = new(0.16f, 0.06f, 0.26f);
    [SerializeField] private Vector3 weaponAnchorLocalEuler = Vector3.zero;

    [Header("Elbow Hints (chest-local)")]
    [SerializeField] private Vector3 rightElbowHintLocalPosition = new(0.32f, -0.04f, 0.06f);
    [SerializeField] private Vector3 leftElbowHintLocalPosition = new(-0.16f, 0.02f, 0.18f);

    [Header("Arm IK")]
    [SerializeField, Range(0f, 1f)] private float rightArmIkWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float leftArmIkWeight = 1f;
    [SerializeField, Range(0f, 1f)] private float hintWeight = 1f;
    [SerializeField] private bool useLeftHand = true;

    [Header("Body Influence")]
    [SerializeField, Range(0f, 0.5f)] private float shoulderInfluence = 0.12f;
    [SerializeField, Range(0f, 0.5f)] private float chestInfluence = 0.08f;
    [SerializeField, Min(0.1f)] private float maxArmReach = 0.72f;

    [Header("Blending")]
    [SerializeField, Min(0.01f)] private float blendDuration = 0.14f;

    public ThirdPersonWeaponPoseClass HoldClass => holdClass;
    public ThirdPersonWeaponPoseKind HoldKind => holdKind;
    public Vector3 WeaponAnchorLocalPosition => weaponAnchorLocalPosition;
    public Vector3 WeaponAnchorLocalEuler => weaponAnchorLocalEuler;
    public Vector3 RightElbowHintLocalPosition => rightElbowHintLocalPosition;
    public Vector3 LeftElbowHintLocalPosition => leftElbowHintLocalPosition;
    public float RightArmIkWeight => Mathf.Clamp01(rightArmIkWeight);
    public float LeftArmIkWeight => Mathf.Clamp01(leftArmIkWeight);
    public float HintWeight => Mathf.Clamp01(hintWeight);
    public bool UseLeftHand => useLeftHand;
    public float ShoulderInfluence => Mathf.Clamp(shoulderInfluence, 0f, 0.5f);
    public float ChestInfluence => Mathf.Clamp(chestInfluence, 0f, 0.5f);
    public float MaxArmReach => Mathf.Max(0.1f, maxArmReach);
    public float BlendDuration => Mathf.Max(0.01f, blendDuration);

    public void AssignIdentity(ThirdPersonWeaponPoseClass poseClass, ThirdPersonWeaponPoseKind kind)
    {
        holdClass = poseClass;
        holdKind = kind;
    }

    public void CopyFrom(ThirdPersonWeaponHoldProfile other)
    {
        if (other == null)
            return;

        weaponAnchorLocalPosition = other.weaponAnchorLocalPosition;
        weaponAnchorLocalEuler = other.weaponAnchorLocalEuler;
        rightElbowHintLocalPosition = other.rightElbowHintLocalPosition;
        leftElbowHintLocalPosition = other.leftElbowHintLocalPosition;
        rightArmIkWeight = other.rightArmIkWeight;
        leftArmIkWeight = other.leftArmIkWeight;
        hintWeight = other.hintWeight;
        useLeftHand = other.useLeftHand;
        shoulderInfluence = other.shoulderInfluence;
        chestInfluence = other.chestInfluence;
        maxArmReach = other.maxArmReach;
        blendDuration = other.blendDuration;
    }

    public static ThirdPersonWeaponHoldPose Lerp(
        ThirdPersonWeaponHoldProfile from,
        ThirdPersonWeaponHoldProfile to,
        float t)
    {
        ThirdPersonWeaponHoldPose a = from != null ? ThirdPersonWeaponHoldPose.From(from) : ThirdPersonWeaponHoldPose.DefaultLongGun;
        ThirdPersonWeaponHoldPose b = to != null ? ThirdPersonWeaponHoldPose.From(to) : a;
        return ThirdPersonWeaponHoldPose.Lerp(a, b, t);
    }
}

public struct ThirdPersonWeaponHoldPose
{
    public Vector3 weaponAnchorLocalPosition;
    public Vector3 weaponAnchorLocalEuler;
    public Vector3 rightElbowHintLocalPosition;
    public Vector3 leftElbowHintLocalPosition;
    public float rightArmIkWeight;
    public float leftArmIkWeight;
    public float hintWeight;
    public bool useLeftHand;
    public float shoulderInfluence;
    public float chestInfluence;
    public float maxArmReach;
    public float blendDuration;

    public static ThirdPersonWeaponHoldPose DefaultLongGun => FromDefaults(
        new Vector3(0.16f, 0.06f, 0.26f),
        Vector3.zero,
        new Vector3(0.32f, -0.04f, 0.06f),
        new Vector3(-0.16f, 0.02f, 0.18f),
        true);

    public static ThirdPersonWeaponHoldPose From(ThirdPersonWeaponHoldProfile profile)
    {
        if (profile == null)
            return DefaultLongGun;

        return new ThirdPersonWeaponHoldPose
        {
            weaponAnchorLocalPosition = profile.WeaponAnchorLocalPosition,
            weaponAnchorLocalEuler = profile.WeaponAnchorLocalEuler,
            rightElbowHintLocalPosition = profile.RightElbowHintLocalPosition,
            leftElbowHintLocalPosition = profile.LeftElbowHintLocalPosition,
            rightArmIkWeight = profile.RightArmIkWeight,
            leftArmIkWeight = profile.LeftArmIkWeight,
            hintWeight = profile.HintWeight,
            useLeftHand = profile.UseLeftHand,
            shoulderInfluence = profile.ShoulderInfluence,
            chestInfluence = profile.ChestInfluence,
            maxArmReach = profile.MaxArmReach,
            blendDuration = profile.BlendDuration
        };
    }

    public static ThirdPersonWeaponHoldPose FromDefaults(
        Vector3 weaponPos,
        Vector3 weaponEuler,
        Vector3 rightHint,
        Vector3 leftHint,
        bool leftHand)
    {
        return new ThirdPersonWeaponHoldPose
        {
            weaponAnchorLocalPosition = weaponPos,
            weaponAnchorLocalEuler = weaponEuler,
            rightElbowHintLocalPosition = rightHint,
            leftElbowHintLocalPosition = leftHint,
            rightArmIkWeight = 1f,
            leftArmIkWeight = leftHand ? 1f : 0f,
            hintWeight = 1f,
            useLeftHand = leftHand,
            shoulderInfluence = 0.12f,
            chestInfluence = 0.08f,
            maxArmReach = 0.72f,
            blendDuration = 0.14f
        };
    }

    public static ThirdPersonWeaponHoldPose Lerp(ThirdPersonWeaponHoldPose a, ThirdPersonWeaponHoldPose b, float t)
    {
        t = Mathf.Clamp01(t);
        return new ThirdPersonWeaponHoldPose
        {
            weaponAnchorLocalPosition = Vector3.Lerp(a.weaponAnchorLocalPosition, b.weaponAnchorLocalPosition, t),
            weaponAnchorLocalEuler = Quaternion.Lerp(
                Quaternion.Euler(a.weaponAnchorLocalEuler),
                Quaternion.Euler(b.weaponAnchorLocalEuler),
                t).eulerAngles,
            rightElbowHintLocalPosition = Vector3.Lerp(a.rightElbowHintLocalPosition, b.rightElbowHintLocalPosition, t),
            leftElbowHintLocalPosition = Vector3.Lerp(a.leftElbowHintLocalPosition, b.leftElbowHintLocalPosition, t),
            rightArmIkWeight = Mathf.Lerp(a.rightArmIkWeight, b.rightArmIkWeight, t),
            leftArmIkWeight = Mathf.Lerp(a.leftArmIkWeight, b.leftArmIkWeight, t),
            hintWeight = Mathf.Lerp(a.hintWeight, b.hintWeight, t),
            useLeftHand = t < 0.5f ? a.useLeftHand : b.useLeftHand,
            shoulderInfluence = Mathf.Lerp(a.shoulderInfluence, b.shoulderInfluence, t),
            chestInfluence = Mathf.Lerp(a.chestInfluence, b.chestInfluence, t),
            maxArmReach = Mathf.Lerp(a.maxArmReach, b.maxArmReach, t),
            blendDuration = Mathf.Lerp(a.blendDuration, b.blendDuration, t)
        };
    }
}
