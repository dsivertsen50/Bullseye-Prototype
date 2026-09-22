using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared sampling helpers for research telemetry. Avoids per-sample scene
/// searches by taking already-resolved player references.
/// </summary>
public static class ResearchTelemetrySnapshot
{
    public const float StationarySpeedThreshold = 0.2f;

    public static float AngleBetween(Vector3 fromDirection, Vector3 toPoint, Vector3 origin)
    {
        Vector3 to = toPoint - origin;
        if (fromDirection.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f)
            return 180f;

        return Vector3.Angle(fromDirection, to);
    }

    public static Vector3 ScreenCenter(Camera camera)
    {
        if (camera == null)
            return new Vector3(0.5f, 0.5f, 0f);

        return new Vector3(0.5f, 0.5f, 0f);
    }

    public static ResearchInputMethod ResolveInputMethod(InputAction lookAction, InputAction fireAction, ResearchInputMethod lastKnown)
    {
        InputDevice device = fireAction?.activeControl?.device ?? lookAction?.activeControl?.device;
        if (device is Mouse || device is Keyboard)
            return ResearchInputMethod.MouseKeyboard;
        if (device is Gamepad)
            return ResearchInputMethod.Gamepad;
        return lastKnown;
    }

    public static ResearchWeaponClass ResolveWeaponClass(WeaponDefinition definition)
    {
        if (definition == null)
            return ResearchWeaponClass.Unknown;

        ResearchWeaponCatalogId catalog = ResolveWeaponCatalogId(definition.WeaponId);
        if (catalog == ResearchWeaponCatalogId.Bazooka)
            return ResearchWeaponClass.Heavy;

        return definition.WeaponPoseClass switch
        {
            ThirdPersonWeaponPoseClass.ShortGun => ResearchWeaponClass.Short,
            ThirdPersonWeaponPoseClass.LongGun => ResearchWeaponClass.Long,
            ThirdPersonWeaponPoseClass.HeavyGun => ResearchWeaponClass.Heavy,
            _ => ResearchWeaponClass.Unknown
        };
    }

    public static ResearchWeaponCatalogId ResolveWeaponCatalogId(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return ResearchWeaponCatalogId.Unknown;

        return weaponId.Trim().ToLowerInvariant() switch
        {
            "pistol" => ResearchWeaponCatalogId.Pistol,
            "ak" => ResearchWeaponCatalogId.AK,
            "dmr" => ResearchWeaponCatalogId.DMR,
            "shotgun" => ResearchWeaponCatalogId.Shotgun,
            "sniper" => ResearchWeaponCatalogId.Sniper,
            "bazooka" => ResearchWeaponCatalogId.Bazooka,
            _ => ResearchWeaponCatalogId.Unknown
        };
    }

    public static ResearchFireMode ResolveFireMode(WeaponDefinition definition)
    {
        if (definition == null)
            return ResearchFireMode.Unknown;
        return definition.IsProjectileWeapon ? ResearchFireMode.Projectile : ResearchFireMode.Hitscan;
    }

    public static ResearchMovementState ResolveMovementState(PlayerMovement movement, Rigidbody body)
    {
        if (movement == null)
            return ResearchMovementState.Other;

        if (movement.IsDolphinDiving)
            return ResearchMovementState.DolphinDiving;
        if (movement.IsClimbing)
            return ResearchMovementState.Climbing;
        if (movement.IsWallRunning)
            return ResearchMovementState.WallRunning;
        if (movement.IsSliding)
            return ResearchMovementState.Sliding;
        if (movement.IsProne)
            return ResearchMovementState.Prone;
        if (movement.IsCrouched)
            return ResearchMovementState.Crouching;
        if (!movement.Grounded)
        {
            float vertical = body != null ? body.linearVelocity.y : movement.VerticalVelocity;
            return vertical > 0.15f ? ResearchMovementState.Jumping : ResearchMovementState.Falling;
        }

        if (movement.IsSprinting)
            return ResearchMovementState.Sprinting;

        float speed = movement.HorizontalSpeed;
        return speed < StationarySpeedThreshold
            ? ResearchMovementState.Stationary
            : ResearchMovementState.Walking;
    }

    public static ResearchStance ResolveStance(PlayerMovement movement)
    {
        if (movement == null)
            return ResearchStance.Standing;
        if (movement.IsProne || movement.IsDolphinDiving)
            return ResearchStance.Prone;
        if (movement.IsCrouched)
            return ResearchStance.Crouching;
        if (movement.IsSliding || movement.IsWallRunning || movement.IsClimbing)
            return ResearchStance.Other;
        return ResearchStance.Standing;
    }

    public static Vector3 ResolveVelocity(Rigidbody body)
    {
        return body != null ? body.linearVelocity : Vector3.zero;
    }

    public static ResearchBodyRegion MapSurfaceRegion(BullseyeSurfaceRegionId region)
    {
        switch (region)
        {
            case BullseyeSurfaceRegionId.Head:
            case BullseyeSurfaceRegionId.Neck:
                return ResearchBodyRegion.Head;
            case BullseyeSurfaceRegionId.UpperChest:
            case BullseyeSurfaceRegionId.UpperBack:
            case BullseyeSurfaceRegionId.LeftShoulder:
            case BullseyeSurfaceRegionId.RightShoulder:
                return ResearchBodyRegion.UpperTorso;
            case BullseyeSurfaceRegionId.LowerChest:
            case BullseyeSurfaceRegionId.LowerBack:
                return ResearchBodyRegion.LowerTorso;
            case BullseyeSurfaceRegionId.LeftUpperArm:
            case BullseyeSurfaceRegionId.LeftForearm:
                return ResearchBodyRegion.LeftArm;
            case BullseyeSurfaceRegionId.RightUpperArm:
            case BullseyeSurfaceRegionId.RightForearm:
                return ResearchBodyRegion.RightArm;
            case BullseyeSurfaceRegionId.LeftThigh:
            case BullseyeSurfaceRegionId.LeftLowerLeg:
                return ResearchBodyRegion.LeftLeg;
            case BullseyeSurfaceRegionId.RightThigh:
            case BullseyeSurfaceRegionId.RightLowerLeg:
                return ResearchBodyRegion.RightLeg;
            default:
                return ResearchBodyRegion.Other;
        }
    }

    public static ResearchBodyRegion MapDamageZone(BullseyeBodyZone zone)
    {
        return zone switch
        {
            BullseyeBodyZone.Head => ResearchBodyRegion.Head,
            BullseyeBodyZone.Torso => ResearchBodyRegion.UpperTorso,
            BullseyeBodyZone.LowerBody => ResearchBodyRegion.LowerTorso,
            _ => ResearchBodyRegion.Other
        };
    }

    public static ResearchBodyRegion MapCombatHitbox(PlayerCombatHitbox hitbox)
    {
        return hitbox != null ? MapDamageZone(hitbox.Zone) : ResearchBodyRegion.Other;
    }

    public static ResearchBullseyeState ResolveBullseyeState(BullseyeDetachController detach)
    {
        if (detach == null)
            return ResearchBullseyeState.Attached;

        return detach.State switch
        {
            BullseyeAttachState.Attached => ResearchBullseyeState.Attached,
            BullseyeAttachState.Returning => ResearchBullseyeState.Returning,
            BullseyeAttachState.Detached => ResearchBullseyeState.Detached,
            _ => ResearchBullseyeState.TemporarilyUnavailable
        };
    }

    public static bool TryGetBullseyePose(
        PlayerHealth health,
        out Vector3 worldPosition,
        out Vector3 localPosition,
        out Vector3 surfaceNormal,
        out ResearchBodyRegion region,
        out ResearchBullseyeState state,
        out bool attached)
    {
        worldPosition = health != null ? health.transform.position : Vector3.zero;
        localPosition = Vector3.zero;
        surfaceNormal = Vector3.forward;
        region = ResearchBodyRegion.Other;
        state = ResearchBullseyeState.TemporarilyUnavailable;
        attached = false;

        if (health == null)
            return false;

        BullseyeDetachController detach = health.GetComponent<BullseyeDetachController>();
        state = ResolveBullseyeState(detach);
        attached = state == ResearchBullseyeState.Attached;

        BullseyeMover mover = health.GetComponent<BullseyeMover>();
        if (mover != null && attached && mover.TryGetSurfacePose(out worldPosition, out surfaceNormal, out _))
        {
            localPosition = health.transform.InverseTransformPoint(worldPosition);
            region = MapSurfaceRegion((BullseyeSurfaceRegionId)mover.CurrentRegionIndex);
            return true;
        }

        if (detach != null)
        {
            worldPosition = detach.ActiveWorldPosition;
            localPosition = health.transform.InverseTransformPoint(worldPosition);
        }

        return true;
    }

    public static Vector3 ResolveHeadPosition(PlayerHealth health)
    {
        if (health == null)
            return Vector3.zero;

        PlayerVisualRig rig = health.GetComponent<PlayerVisualRig>();
        if (rig != null && rig.BullseyeHeadAnchor != null)
            return rig.BullseyeHeadAnchor.position;

        Animator animator = health.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
                return head.position;
        }

        Collider body = health.GetComponent<Collider>();
        if (body != null)
        {
            Bounds bounds = body.bounds;
            return new Vector3(bounds.center.x, bounds.max.y - 0.12f, bounds.center.z);
        }

        return health.transform.position + Vector3.up * 1.6f;
    }

    public static Vector3 ResolveCenterMassPosition(PlayerHealth health)
    {
        if (health == null)
            return Vector3.zero;

        PlayerVisualRig rig = health.GetComponent<PlayerVisualRig>();
        if (rig != null && rig.BullseyeUpperTorsoAnchor != null)
            return rig.BullseyeUpperTorsoAnchor.position;

        Animator animator = health.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest != null)
                return chest.position;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
                return hips.position;
        }

        Collider body = health.GetComponent<Collider>();
        if (body != null)
            return body.bounds.center;

        return health.transform.position + Vector3.up * 0.95f;
    }

    public static bool HasLineOfSight(Camera camera, PlayerHealth target, float maxDistance)
    {
        if (camera == null || target == null)
            return false;

        Vector3 origin = camera.transform.position;
        Vector3 aim = ResolveCenterMassPosition(target);
        Vector3 delta = aim - origin;
        float distance = delta.magnitude;
        if (distance < 0.05f)
            return true;
        if (distance > maxDistance)
            return false;

        if (!Physics.Raycast(
                origin,
                delta / distance,
                out RaycastHit hit,
                Mathf.Min(distance, maxDistance),
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        return hit.collider != null && hit.collider.transform.IsChildOf(target.transform);
    }

    public static float CurrentZoom(PlayerAimZoom aimZoom, WeaponDefinition definition)
    {
        if (aimZoom != null && aimZoom.IsAiming)
            return aimZoom.CurrentMagnification;
        return definition != null && definition.UsesMagnifiedAds ? 1f : 1f;
    }

    public static void FillAimAssist(
        ResearchInputMethod input,
        out bool enabled,
        out float strength,
        out bool magnetism,
        out bool friction,
        out float sensitivityX,
        out float sensitivityY,
        out float adsModifier,
        out float deadzone)
    {
        enabled = false;
        strength = 0f;
        magnetism = false;
        friction = false;
        adsModifier = PlayerGameSettings.AimSensitivityMultiplier;
        deadzone = 0f;

        if (input == ResearchInputMethod.Gamepad)
        {
            sensitivityX = PlayerGameSettings.ControllerSensitivityX;
            sensitivityY = PlayerGameSettings.ControllerSensitivityY;
        }
        else
        {
            sensitivityX = PlayerGameSettings.MouseSensitivityX;
            sensitivityY = PlayerGameSettings.MouseSensitivityY;
        }
    }
}
