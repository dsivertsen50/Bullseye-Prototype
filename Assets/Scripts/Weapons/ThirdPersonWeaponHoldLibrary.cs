using UnityEngine;

/// <summary>
/// Shared LongGun / ShortGun / HeavyGun procedural hold profiles.
/// State variants adjust weapon placement and IK, not locomotion clips.
/// </summary>
[CreateAssetMenu(
    fileName = "ThirdPersonWeaponHoldLibrary",
    menuName = "Bullseye/Weapons/Third-Person Weapon Hold Library")]
public class ThirdPersonWeaponHoldLibrary : ScriptableObject
{
    [Header("LongGun")]
    [SerializeField] private ThirdPersonWeaponHoldProfile longGunHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile longGunSprintHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile longGunProneHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile longGunAimHold;

    [Header("ShortGun")]
    [SerializeField] private ThirdPersonWeaponHoldProfile shortGunHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile shortGunSprintHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile shortGunProneHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile shortGunAimHold;

    [Header("HeavyGun")]
    [SerializeField] private ThirdPersonWeaponHoldProfile heavyGunHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile heavyGunSprintHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile heavyGunProneHold;
    [SerializeField] private ThirdPersonWeaponHoldProfile heavyGunAimHold;

    public ThirdPersonWeaponHoldProfile LongGunHold => longGunHold;
    public ThirdPersonWeaponHoldProfile ShortGunHold => shortGunHold;
    public ThirdPersonWeaponHoldProfile HeavyGunHold => heavyGunHold;

    public void Assign(
        ThirdPersonWeaponHoldProfile longHold,
        ThirdPersonWeaponHoldProfile shortHold,
        ThirdPersonWeaponHoldProfile heavyHold,
        ThirdPersonWeaponHoldProfile longSprint,
        ThirdPersonWeaponHoldProfile longProne,
        ThirdPersonWeaponHoldProfile longAim,
        ThirdPersonWeaponHoldProfile shortSprint,
        ThirdPersonWeaponHoldProfile shortProne,
        ThirdPersonWeaponHoldProfile shortAim,
        ThirdPersonWeaponHoldProfile heavySprint,
        ThirdPersonWeaponHoldProfile heavyProne,
        ThirdPersonWeaponHoldProfile heavyAim)
    {
        longGunHold = longHold;
        shortGunHold = shortHold;
        heavyGunHold = heavyHold;
        longGunSprintHold = longSprint;
        longGunProneHold = longProne;
        longGunAimHold = longAim;
        shortGunSprintHold = shortSprint;
        shortGunProneHold = shortProne;
        shortGunAimHold = shortAim;
        heavyGunSprintHold = heavySprint;
        heavyGunProneHold = heavyProne;
        heavyGunAimHold = heavyAim;
    }

    public ThirdPersonWeaponHoldProfile GetClassHold(ThirdPersonWeaponPoseClass holdClass)
    {
        return holdClass switch
        {
            ThirdPersonWeaponPoseClass.ShortGun => shortGunHold,
            ThirdPersonWeaponPoseClass.HeavyGun => heavyGunHold,
            _ => longGunHold
        };
    }

    public ThirdPersonWeaponHoldProfile GetProfile(
        ThirdPersonWeaponPoseClass holdClass,
        ThirdPersonWeaponPoseKind kind)
    {
        ThirdPersonWeaponHoldProfile hold = GetClassHold(holdClass);
        ThirdPersonWeaponHoldProfile variant = kind switch
        {
            ThirdPersonWeaponPoseKind.Sprint => GetSprint(holdClass),
            ThirdPersonWeaponPoseKind.Prone => GetProne(holdClass),
            ThirdPersonWeaponPoseKind.Aim => GetAim(holdClass),
            _ => hold
        };

        return variant != null ? variant : hold;
    }

    private ThirdPersonWeaponHoldProfile GetSprint(ThirdPersonWeaponPoseClass holdClass)
    {
        return holdClass switch
        {
            ThirdPersonWeaponPoseClass.ShortGun => shortGunSprintHold,
            ThirdPersonWeaponPoseClass.HeavyGun => heavyGunSprintHold,
            _ => longGunSprintHold
        };
    }

    private ThirdPersonWeaponHoldProfile GetProne(ThirdPersonWeaponPoseClass holdClass)
    {
        return holdClass switch
        {
            ThirdPersonWeaponPoseClass.ShortGun => shortGunProneHold,
            ThirdPersonWeaponPoseClass.HeavyGun => heavyGunProneHold,
            _ => longGunProneHold
        };
    }

    private ThirdPersonWeaponHoldProfile GetAim(ThirdPersonWeaponPoseClass holdClass)
    {
        return holdClass switch
        {
            ThirdPersonWeaponPoseClass.ShortGun => shortGunAimHold,
            ThirdPersonWeaponPoseClass.HeavyGun => heavyGunAimHold,
            _ => longGunAimHold
        };
    }
}

public static class ThirdPersonWeaponHoldLibraryCache
{
    public const string ResourceName = "ThirdPersonWeaponHoldLibrary";
    public const string AssetPath =
        "Assets/Animations/ThirdPersonWeapons/Holds/ThirdPersonWeaponHoldLibrary.asset";

    private static ThirdPersonWeaponHoldLibrary cached;

    public static ThirdPersonWeaponHoldLibrary Library
    {
        get
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<ThirdPersonWeaponHoldLibrary>(ResourceName);
            return cached;
        }
    }

    public static ThirdPersonWeaponHoldProfile Get(
        ThirdPersonWeaponPoseClass holdClass,
        ThirdPersonWeaponPoseKind kind)
    {
        ThirdPersonWeaponHoldLibrary library = Library;
        return library != null ? library.GetProfile(holdClass, kind) : null;
    }

    public static ThirdPersonWeaponHoldProfile GetClassHold(ThirdPersonWeaponPoseClass holdClass)
    {
        ThirdPersonWeaponHoldLibrary library = Library;
        return library != null ? library.GetClassHold(holdClass) : null;
    }

    public static void SetEditorLibrary(ThirdPersonWeaponHoldLibrary library)
    {
        cached = library;
    }
}
