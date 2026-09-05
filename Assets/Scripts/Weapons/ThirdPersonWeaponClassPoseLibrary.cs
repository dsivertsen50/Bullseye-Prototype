using UnityEngine;

/// <summary>
/// Shared LongGun / ShortGun / HeavyGun pose profiles used when a weapon
/// does not supply its own clip for a given pose.
/// </summary>
[CreateAssetMenu(
    fileName = "ThirdPersonWeaponClassPoseLibrary",
    menuName = "Bullseye/Weapons/Third-Person Class Pose Library")]
public class ThirdPersonWeaponClassPoseLibrary : ScriptableObject
{
    [SerializeField] private ThirdPersonWeaponPoseProfile longGun;
    [SerializeField] private ThirdPersonWeaponPoseProfile shortGun;
    [SerializeField] private ThirdPersonWeaponPoseProfile heavyGun;

    public ThirdPersonWeaponPoseProfile LongGun => longGun;
    public ThirdPersonWeaponPoseProfile ShortGun => shortGun;
    public ThirdPersonWeaponPoseProfile HeavyGun => heavyGun;

    public ThirdPersonWeaponPoseProfile GetClassProfile(ThirdPersonWeaponPoseClass poseClass)
    {
        return poseClass switch
        {
            ThirdPersonWeaponPoseClass.ShortGun => shortGun,
            ThirdPersonWeaponPoseClass.HeavyGun => heavyGun,
            _ => longGun
        };
    }
}
