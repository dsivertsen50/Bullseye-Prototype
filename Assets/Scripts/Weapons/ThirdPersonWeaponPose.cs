using System;
using UnityEngine;

public enum ThirdPersonWeaponClass
{
    Pistol = 0,
    Rifle = 1,
    Shotgun = 2
}

/// <summary>
/// Legacy per-bone hold data from REQ-047. REQ-048 no longer poses arms from
/// these fields. Kept so existing WeaponDefinition assets deserialize cleanly.
/// Socket scale and a small extra gun tilt are still read where useful.
/// </summary>
[Serializable]
public class ThirdPersonWeaponPose
{
    [Header("Gun")]
    [Tooltip("Extra local tilt applied on top of the socket attachment. The gun follows the right hand.")]
    public Vector3 gunEuler;
    [Tooltip("Desired world scale of the third-person weapon.")]
    public Vector3 gunScale = Vector3.one;

    [Header("Right Arm")]
    [Tooltip("Right-hand location from the upper chest in player-facing space.")]
    public Vector3 rightHandPosition = new(0.18f, 0.06f, 0.50f);
    [Tooltip("Right wrist tilt. The parented weapon rotates with this wrist.")]
    public Vector3 rightWristEuler;
    [Tooltip("How straight the right elbow is. 1 is almost locked. Lower keeps a bent arm.")]
    [Range(0.55f, 0.98f)] public float rightArmReach = 0.90f;
    [Tooltip("Swings the right elbow around the shoulder-to-hand line. 0 is out from the body.")]
    [Range(-180f, 180f)] public float rightElbowYaw;
    public Vector3 aimRightHandPosition = new(0.16f, 0.12f, 0.54f);
    public Vector3 aimRightWristEuler;
    public Vector3 sprintRightHandPosition;
    public Vector3 sprintRightWristEuler;
    public Vector3 proneRightHandPosition;
    public Vector3 proneRightWristEuler;
    public Vector3 crouchRightHandPosition;
    public Vector3 crouchRightWristEuler;
    public Vector3 aimGunPosition;
    public Vector3 aimGunEuler;
    public Vector3 sprintGunPosition;
    public Vector3 sprintGunEuler;
    public Vector3 crouchGunPosition;
    public Vector3 crouchGunEuler;
    public Vector3 proneGunPosition;
    public Vector3 proneGunEuler;

    [Header("Left Arm")]
    [Tooltip("When enabled, the left hand sticks to the weapon LeftHandGrip and moves with the gun.")]
    public bool leftHandFollowGrip;
    [Tooltip("Left-hand location from the upper chest in player-facing space. Used when the hand does not follow the grip.")]
    public Vector3 leftHandPosition;
    [Tooltip("Left wrist tilt in player-facing space.")]
    public Vector3 leftWristEuler;
    [Tooltip("How straight the left elbow is. 1 is almost locked. Lower keeps a bent arm.")]
    [Range(0.55f, 0.98f)] public float leftArmReach = 0.86f;
    [Tooltip("Swings the left elbow around the shoulder-to-hand line. 0 is out from the body.")]
    [Range(-180f, 180f)] public float leftElbowYaw;
    public Vector3 aimLeftHandPosition;
    public Vector3 aimLeftWristEuler;
    public Vector3 sprintLeftHandPosition;
    public Vector3 sprintLeftWristEuler;
    public Vector3 proneLeftHandPosition;
    public Vector3 proneLeftWristEuler;
    public Vector3 crouchLeftHandPosition;
    public Vector3 crouchLeftWristEuler;
    [Range(0f, 1f)] public float sprintLeftIkWeight = 0.45f;

    [Header("Weights")]
    [Range(0f, 1f)] public float defaultWeight = 1f;
    [Range(0f, 1f)] public float sprintWeight = 0.28f;
    [Range(0f, 1f)] public float crouchWeight = 1f;
    [Range(0f, 1f)] public float proneWeight = 0.85f;
    [Range(0f, 1f)] public float diveWeight = 0.12f;
    [Range(0f, 1f)] public float jumpWeight = 0.85f;

    [Header("Aim Follow")]
    [Tooltip("Maximum upward spine/arm pitch applied from networked look pitch.")]
    public float maxAimPitch = 50f;
    [Tooltip("Maximum downward spine/arm pitch applied from networked look pitch.")]
    public float maxAimPitchDown = 50f;
    [Range(0f, 1f)] public float spineAimWeight = 0.62f;
    [Range(0f, 1f)] public float upperChestAimShare = 0.45f;
    [Tooltip("Extra upward upper-body pitch while ADS/zoom is active.")]
    public float aimRaisePitch = 8f;
    [Tooltip("Pitch applied to the hold while prone so the weapon stays along the body.")]
    public float proneBodyPitch = -68f;

    [Header("Upper-Body Recoil")]
    [Tooltip("Light upward muzzle kick. Positive values kick up. Does not stack.")]
    public float recoilPitch = 2.5f;
    [Tooltip("Extra right-side roll so the kicking shoulder is readable.")]
    public float recoilRightRoll = 5f;
    public float recoilYaw = 1.5f;
    public float recoilInTime = 0.035f;
    public float recoilOutTime = 0.11f;

    public Vector3 ResolvedSprintRightHand()
    {
        if (sprintRightHandPosition.sqrMagnitude < 0.0001f)
            return rightHandPosition + new Vector3(0.05f, -0.10f, -0.08f);
        return sprintRightHandPosition;
    }

    public Vector3 ResolvedSprintRightWrist()
    {
        if (sprintRightWristEuler.sqrMagnitude < 0.0001f)
            return rightWristEuler + new Vector3(16f, 10f, -8f);
        return sprintRightWristEuler;
    }

    public Vector3 ResolvedLeftHand()
    {
        if (leftHandPosition.sqrMagnitude < 0.0001f)
            return rightHandPosition + new Vector3(-0.22f, 0.02f, 0.02f);
        return leftHandPosition;
    }

    public Vector3 ResolvedAimLeftHand()
    {
        if (aimLeftHandPosition.sqrMagnitude < 0.0001f)
            return ResolvedLeftHand() + new Vector3(0f, 0.04f, 0.04f);
        return aimLeftHandPosition;
    }

    public Vector3 ResolvedSprintLeftHand()
    {
        if (sprintLeftHandPosition.sqrMagnitude < 0.0001f)
            return ResolvedLeftHand() + new Vector3(0.04f, -0.08f, -0.10f);
        return sprintLeftHandPosition;
    }

    public Vector3 ResolvedProneLeftHand()
    {
        if (proneLeftHandPosition.sqrMagnitude < 0.0001f)
            return ResolvedLeftHand() + new Vector3(0.02f, -0.04f, 0.04f);
        return proneLeftHandPosition;
    }

    public Vector3 ResolvedSprintLeftWrist()
    {
        if (sprintLeftWristEuler.sqrMagnitude < 0.0001f)
            return leftWristEuler + new Vector3(10f, -8f, 6f);
        return sprintLeftWristEuler;
    }

    public Vector3 ResolvedProneLeftWrist()
    {
        if (proneLeftWristEuler.sqrMagnitude < 0.0001f)
            return leftWristEuler;
        return proneLeftWristEuler;
    }

    public Vector3 ResolvedProneRightHand()
    {
        if (proneRightHandPosition.sqrMagnitude < 0.0001f)
            return rightHandPosition + new Vector3(0.02f, -0.05f, 0.06f);
        return proneRightHandPosition;
    }

    public Vector3 ResolvedProneRightWrist()
    {
        if (proneRightWristEuler.sqrMagnitude < 0.0001f)
            return rightWristEuler;
        return proneRightWristEuler;
    }

    public Vector3 ResolvedCrouchRightHand()
    {
        return HasOverride(crouchRightHandPosition, crouchRightWristEuler)
            ? crouchRightHandPosition
            : rightHandPosition;
    }

    public Vector3 ResolvedCrouchRightWrist()
    {
        return HasOverride(crouchRightHandPosition, crouchRightWristEuler)
            ? crouchRightWristEuler
            : rightWristEuler;
    }

    public Vector3 ResolvedCrouchLeftHand()
    {
        return HasOverride(crouchLeftHandPosition, crouchLeftWristEuler)
            ? crouchLeftHandPosition
            : ResolvedLeftHand();
    }

    public Vector3 ResolvedCrouchLeftWrist()
    {
        return HasOverride(crouchLeftHandPosition, crouchLeftWristEuler)
            ? crouchLeftWristEuler
            : leftWristEuler;
    }

    public void ResolveSocket(
        Vector3 fallbackPosition,
        Vector3 fallbackEuler,
        float aim,
        float sprint,
        float crouch,
        float prone,
        out Vector3 position,
        out Vector3 euler)
    {
        position = fallbackPosition;
        euler = fallbackEuler;
        if (HasOverride(crouchGunPosition, crouchGunEuler))
        {
            position = Vector3.Lerp(position, crouchGunPosition, crouch);
            euler = Quaternion.Slerp(Quaternion.Euler(euler), Quaternion.Euler(crouchGunEuler), crouch).eulerAngles;
        }

        if (HasOverride(sprintGunPosition, sprintGunEuler))
        {
            position = Vector3.Lerp(position, sprintGunPosition, sprint);
            euler = Quaternion.Slerp(Quaternion.Euler(euler), Quaternion.Euler(sprintGunEuler), sprint).eulerAngles;
        }

        if (HasOverride(proneGunPosition, proneGunEuler))
        {
            position = Vector3.Lerp(position, proneGunPosition, prone);
            euler = Quaternion.Slerp(Quaternion.Euler(euler), Quaternion.Euler(proneGunEuler), prone).eulerAngles;
        }

        if (HasOverride(aimGunPosition, aimGunEuler))
        {
            position = Vector3.Lerp(position, aimGunPosition, aim);
            euler = Quaternion.Slerp(Quaternion.Euler(euler), Quaternion.Euler(aimGunEuler), aim).eulerAngles;
        }
    }

    public static bool HasOverride(Vector3 position, Vector3 euler)
    {
        return position.sqrMagnitude > 0.0000001f || euler.sqrMagnitude > 0.0000001f;
    }

    public float ResolvedSprintLeftIkWeight()
    {
        return sprintLeftIkWeight > 0.01f ? sprintLeftIkWeight : 0.45f;
    }

    public float ResolvedProneBodyPitch()
    {
        return Mathf.Abs(proneBodyPitch) > 0.01f ? proneBodyPitch : -68f;
    }

    public float ResolvedMaxAimPitchDown()
    {
        return maxAimPitchDown > 0.01f ? maxAimPitchDown : maxAimPitch;
    }

    public static ThirdPersonWeaponPose CreateDefault(ThirdPersonWeaponClass weaponClass)
    {
        return weaponClass switch
        {
            ThirdPersonWeaponClass.Rifle => new ThirdPersonWeaponPose
            {
                gunEuler = new Vector3(0f, 0f, -8f),
                gunScale = new Vector3(1.15f, 1.15f, 1.15f),
                rightHandPosition = new Vector3(0.22f, -0.04f, 0.48f),
                rightWristEuler = new Vector3(-4f, 6f, 0f),
                rightArmReach = 0.90f,
                aimRightHandPosition = new Vector3(0.20f, 0.04f, 0.52f),
                aimRightWristEuler = new Vector3(-8f, 4f, 0f),
                sprintRightHandPosition = new Vector3(0.26f, -0.14f, 0.38f),
                sprintRightWristEuler = new Vector3(12f, 16f, -8f),
                proneRightHandPosition = new Vector3(0.20f, -0.08f, 0.52f),
                leftHandPosition = new Vector3(0.00f, -0.04f, 0.50f),
                aimLeftHandPosition = new Vector3(-0.02f, 0.02f, 0.54f),
                sprintLeftHandPosition = new Vector3(0.04f, -0.12f, 0.40f),
                proneLeftHandPosition = new Vector3(-0.02f, -0.08f, 0.52f),
                leftArmReach = 0.86f,
                aimRaisePitch = 6f,
                recoilPitch = 5f,
                recoilRightRoll = 4f,
                recoilOutTime = 0.09f,
                sprintWeight = 0.22f,
                proneWeight = 0.85f,
                sprintLeftIkWeight = 0.4f
            },
            ThirdPersonWeaponClass.Shotgun => new ThirdPersonWeaponPose
            {
                gunEuler = new Vector3(0f, 0f, -6f),
                rightHandPosition = new Vector3(0.23f, -0.05f, 0.47f),
                rightWristEuler = new Vector3(-3f, 6f, 0f),
                rightArmReach = 0.89f,
                aimRightHandPosition = new Vector3(0.20f, 0.03f, 0.51f),
                aimRightWristEuler = new Vector3(-7f, 4f, 0f),
                sprintRightHandPosition = new Vector3(0.27f, -0.14f, 0.36f),
                sprintRightWristEuler = new Vector3(14f, 16f, -8f),
                proneRightHandPosition = new Vector3(0.21f, -0.08f, 0.50f),
                leftHandPosition = new Vector3(0.01f, -0.05f, 0.49f),
                aimLeftHandPosition = new Vector3(-0.02f, 0.01f, 0.53f),
                sprintLeftHandPosition = new Vector3(0.05f, -0.12f, 0.38f),
                proneLeftHandPosition = new Vector3(-0.01f, -0.08f, 0.50f),
                leftArmReach = 0.84f,
                aimRaisePitch = 6f,
                recoilPitch = 9f,
                recoilRightRoll = 6f,
                recoilOutTime = 0.12f,
                sprintWeight = 0.22f,
                proneWeight = 0.85f,
                sprintLeftIkWeight = 0.4f
            },
            _ => new ThirdPersonWeaponPose
            {
                rightHandPosition = new Vector3(0.18f, 0.06f, 0.50f),
                rightWristEuler = new Vector3(-2f, 4f, 0f),
                rightArmReach = 0.91f,
                aimRightHandPosition = new Vector3(0.16f, 0.12f, 0.54f),
                aimRightWristEuler = new Vector3(-6f, 3f, 0f),
                sprintRightHandPosition = new Vector3(0.22f, -0.08f, 0.40f),
                sprintRightWristEuler = new Vector3(10f, 12f, -6f),
                proneRightHandPosition = new Vector3(0.16f, 0.02f, 0.52f),
                leftHandPosition = new Vector3(-0.04f, 0.06f, 0.48f),
                aimLeftHandPosition = new Vector3(-0.06f, 0.10f, 0.52f),
                sprintLeftHandPosition = new Vector3(0.00f, -0.06f, 0.38f),
                proneLeftHandPosition = new Vector3(-0.06f, 0.02f, 0.50f),
                leftArmReach = 0.78f,
                aimRaisePitch = 8f,
                recoilPitch = 7f,
                recoilRightRoll = 5f,
                sprintWeight = 0.3f,
                proneWeight = 0.85f,
                sprintLeftIkWeight = 0.5f
            }
        };
    }
}
