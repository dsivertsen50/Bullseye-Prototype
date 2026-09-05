using UnityEngine;

/// <summary>
/// Predictable clip fallback for third-person weapon poses.
/// Weapon-specific → class → Hold → no override.
/// </summary>
public static class ThirdPersonWeaponPoseResolver
{
    public static ThirdPersonWeaponPoseKind ResolveKind(
        WeaponDefinition definition,
        bool prone,
        bool sprinting,
        bool aiming,
        bool crouching)
    {
        if (prone)
            return ThirdPersonWeaponPoseKind.Prone;
        if (sprinting)
            return ThirdPersonWeaponPoseKind.Sprint;
        if (aiming)
            return ThirdPersonWeaponPoseKind.Aim;
        if (crouching && HasDedicatedCrouch(definition))
            return ThirdPersonWeaponPoseKind.Crouch;
        return ThirdPersonWeaponPoseKind.Hold;
    }

    public static AnimationClip ResolveClip(WeaponDefinition definition, ThirdPersonWeaponPoseKind kind)
    {
        ThirdPersonWeaponPoseProfile profile = definition != null ? definition.PoseProfile : null;
        ThirdPersonWeaponPoseProfile classProfile = ResolveClassProfile(definition, profile);

        AnimationClip clip = profile != null ? profile.GetOwnClip(kind) : null;
        if (clip != null)
            return clip;

        clip = classProfile != null ? classProfile.GetOwnClip(kind) : null;
        if (clip != null)
            return clip;

        if (kind != ThirdPersonWeaponPoseKind.Hold)
        {
            clip = profile != null ? profile.GetOwnClip(ThirdPersonWeaponPoseKind.Hold) : null;
            if (clip != null)
                return clip;

            clip = classProfile != null ? classProfile.GetOwnClip(ThirdPersonWeaponPoseKind.Hold) : null;
            if (clip != null)
                return clip;
        }

        return null;
    }

    public static ThirdPersonWeaponPoseProfile ResolveClassProfile(WeaponDefinition definition)
    {
        return ResolveClassProfile(definition, definition != null ? definition.PoseProfile : null);
    }

    public static string DescribeFallback(
        WeaponDefinition definition,
        ThirdPersonWeaponPoseKind kind,
        out bool usingFallback)
    {
        usingFallback = false;
        if (definition == null)
            return "No weapon";

        ThirdPersonWeaponPoseProfile profile = definition.PoseProfile;
        AnimationClip own = profile != null ? profile.GetOwnClip(kind) : null;
        if (own != null)
            return own.name;

        ThirdPersonWeaponPoseProfile classProfile = ResolveClassProfile(definition, profile);
        AnimationClip classClip = classProfile != null ? classProfile.GetOwnClip(kind) : null;
        if (classClip != null)
        {
            usingFallback = true;
            return classClip.name;
        }

        if (kind != ThirdPersonWeaponPoseKind.Hold)
        {
            AnimationClip hold = ResolveClip(definition, ThirdPersonWeaponPoseKind.Hold);
            if (hold != null)
            {
                usingFallback = true;
                return hold.name + " (Hold fallback)";
            }
        }

        usingFallback = true;
        return "No upper-body override";
    }

    public static bool HasDedicatedCrouch(WeaponDefinition definition)
    {
        if (definition == null)
            return false;
        if (definition.PoseProfile != null && definition.PoseProfile.HasDedicatedCrouchPose())
            return true;

        ThirdPersonWeaponPoseProfile classProfile = ResolveClassProfile(definition);
        return classProfile != null && classProfile.HasDedicatedCrouchPose();
    }

    private static ThirdPersonWeaponPoseProfile ResolveClassProfile(
        WeaponDefinition definition,
        ThirdPersonWeaponPoseProfile profile)
    {
        if (profile != null && profile.ClassDefaults != null && profile.ClassDefaults != profile)
            return profile.ClassDefaults;

        ThirdPersonWeaponPoseClass poseClass = definition != null
            ? definition.WeaponPoseClass
            : ThirdPersonWeaponPoseClass.LongGun;
        if (profile != null && profile.ClassDefaults == null && IsClassProfile(profile, poseClass))
            return null;

        return ThirdPersonWeaponClassPoseLibraryCache.Get(poseClass);
    }

    private static bool IsClassProfile(ThirdPersonWeaponPoseProfile profile, ThirdPersonWeaponPoseClass poseClass)
    {
        ThirdPersonWeaponPoseProfile classProfile = ThirdPersonWeaponClassPoseLibraryCache.Get(poseClass);
        return classProfile != null && classProfile == profile;
    }
}

/// <summary>
/// Editor and runtime lookup for the shared class pose library.
/// The library asset lives at the well-known authoring path.
/// </summary>
public static class ThirdPersonWeaponClassPoseLibraryCache
{
    public const string ResourceName = "ThirdPersonWeaponClassPoseLibrary";
    public const string AssetPath =
        "Assets/Animations/ThirdPersonWeapons/Shared/ThirdPersonWeaponClassPoseLibrary.asset";

    private static ThirdPersonWeaponClassPoseLibrary cached;

    public static ThirdPersonWeaponClassPoseLibrary Library
    {
        get
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<ThirdPersonWeaponClassPoseLibrary>(ResourceName);
            return cached;
        }
    }

    public static ThirdPersonWeaponPoseProfile Get(ThirdPersonWeaponPoseClass poseClass)
    {
        ThirdPersonWeaponClassPoseLibrary library = Library;
        return library != null ? library.GetClassProfile(poseClass) : null;
    }

    public static void SetEditorLibrary(ThirdPersonWeaponClassPoseLibrary library)
    {
        cached = library;
    }
}
