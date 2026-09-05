using UnityEngine;

/// <summary>
/// Resolves the active procedural hold profile from WeaponDefinition,
/// shared class defaults, and gameplay state. Never selects locomotion clips.
/// </summary>
public static class ThirdPersonWeaponHoldResolver
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
        if (crouching)
            return ThirdPersonWeaponPoseKind.Crouch;
        return ThirdPersonWeaponPoseKind.Hold;
    }

    public static ThirdPersonWeaponHoldProfile Resolve(
        WeaponDefinition definition,
        ThirdPersonWeaponPoseKind kind)
    {
        if (definition != null && definition.HoldProfileOverride != null)
        {
            if (definition.HoldProfileOverride.HoldKind == kind || kind == ThirdPersonWeaponPoseKind.Hold)
                return definition.HoldProfileOverride;
        }

        ThirdPersonWeaponPoseClass holdClass = definition != null
            ? definition.ThirdPersonHoldClass
            : ThirdPersonWeaponPoseClass.LongGun;

        if (kind == ThirdPersonWeaponPoseKind.Crouch)
            kind = ThirdPersonWeaponPoseKind.Hold;

        ThirdPersonWeaponHoldProfile profile = ThirdPersonWeaponHoldLibraryCache.Get(holdClass, kind);
        if (profile != null)
            return profile;

        return ThirdPersonWeaponHoldLibraryCache.GetClassHold(holdClass);
    }

    public static ThirdPersonWeaponHoldPose ResolvePose(
        WeaponDefinition definition,
        ThirdPersonWeaponPoseKind kind)
    {
        ThirdPersonWeaponHoldProfile profile = Resolve(definition, kind);
        ThirdPersonWeaponHoldPose pose = ThirdPersonWeaponHoldPose.From(profile);
        if (definition == null)
            return pose;

        pose.weaponAnchorLocalPosition += definition.ThirdPersonAnchorPositionOffset;
        pose.weaponAnchorLocalEuler += definition.ThirdPersonAnchorRotationOffset;
        pose.useLeftHand = definition.UseLeftHandGrip && pose.useLeftHand;
        if (!pose.useLeftHand)
            pose.leftArmIkWeight = 0f;

        return pose;
    }

    public static string Describe(WeaponDefinition definition, ThirdPersonWeaponPoseKind kind)
    {
        ThirdPersonWeaponHoldProfile profile = Resolve(definition, kind);
        if (definition != null && definition.HoldProfileOverride != null && definition.HoldProfileOverride == profile)
            return profile.name + " (weapon override)";
        return profile != null ? profile.name : "Missing hold profile";
    }
}
