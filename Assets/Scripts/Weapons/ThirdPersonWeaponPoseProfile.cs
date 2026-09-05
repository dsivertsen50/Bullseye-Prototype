using UnityEngine;

/// <summary>
/// Authored third-person weapon presentation. Missing clips fall back to
/// the class defaults, then to Hold, then to no upper-body override.
/// </summary>
[CreateAssetMenu(
    fileName = "ThirdPersonWeaponPoseProfile",
    menuName = "Bullseye/Weapons/Third-Person Weapon Pose Profile")]
public class ThirdPersonWeaponPoseProfile : ScriptableObject
{
    [SerializeField] private ThirdPersonWeaponPoseClass weaponPoseClass = ThirdPersonWeaponPoseClass.LongGun;
    [SerializeField] private ThirdPersonWeaponPoseProfile classDefaults;
    [SerializeField] private AnimationClip defaultHoldPose;
    [SerializeField] private AnimationClip sprintPose;
    [SerializeField] private AnimationClip adsOrAimPose;
    [SerializeField] private AnimationClip pronePose;
    [SerializeField] private AnimationClip optionalCrouchPose;
    [SerializeField] private bool supportHandIkEnabled = true;
    [SerializeField, Min(0.01f)] private float weaponPoseBlendDuration = 0.14f;
    [SerializeField, Min(0.01f)] private float ikBlendDuration = 0.12f;
    [SerializeField, Range(0.15f, 1f)] private float overrideLayerWeight = 0.78f;
    [SerializeField, Range(0f, 1f)] private float sprintSupportIkWeight = 0.55f;

    public ThirdPersonWeaponPoseClass WeaponPoseClass => weaponPoseClass;
    public ThirdPersonWeaponPoseProfile ClassDefaults => classDefaults == this ? null : classDefaults;
    public AnimationClip DefaultHoldPose => defaultHoldPose;
    public AnimationClip SprintPose => sprintPose;
    public AnimationClip AdsOrAimPose => adsOrAimPose;
    public AnimationClip PronePose => pronePose;
    public AnimationClip OptionalCrouchPose => optionalCrouchPose;
    public bool SupportHandIkEnabled => supportHandIkEnabled;
    public float WeaponPoseBlendDuration => Mathf.Max(0.01f, weaponPoseBlendDuration);
    public float IkBlendDuration => Mathf.Max(0.01f, ikBlendDuration);
    public float OverrideLayerWeight => Mathf.Clamp(overrideLayerWeight, 0.15f, 1f);
    public float SprintSupportIkWeight => Mathf.Clamp01(sprintSupportIkWeight);

    public AnimationClip GetOwnClip(ThirdPersonWeaponPoseKind kind)
    {
        return kind switch
        {
            ThirdPersonWeaponPoseKind.Sprint => sprintPose,
            ThirdPersonWeaponPoseKind.Prone => pronePose,
            ThirdPersonWeaponPoseKind.Aim => adsOrAimPose,
            ThirdPersonWeaponPoseKind.Crouch => optionalCrouchPose,
            _ => defaultHoldPose
        };
    }

    public bool HasDedicatedCrouchPose()
    {
        return optionalCrouchPose != null;
    }
}
