using System;
using UnityEngine;

public enum ThirdPersonWeaponClass
{
    Pistol = 0,
    Rifle = 1,
    Shotgun = 2
}

/// <summary>
/// Independent third-person gun, arm, wrist, and elbow controls.
/// Every position is meters from the upper chest in player-facing space.
/// Moving the gun does not move the hands. Moving a hand does not move the gun.
/// </summary>
[Serializable]
public class ThirdPersonWeaponPose
{
    [Header("Gun")]
    [Tooltip("Gun location from the upper chest. Does not move either hand.")]
    public Vector3 gunPosition = new(0.18f, 0.06f, 0.50f);
    [Tooltip("Gun tilt in the same chest space. Does not rotate either wrist.")]
    public Vector3 gunEuler;
    [Tooltip("Desired world scale of the third-person weapon.")]
    public Vector3 gunScale = Vector3.one;
    public Vector3 aimGunPosition = new(0.16f, 0.12f, 0.54f);
    public Vector3 aimGunEuler;

    [Header("Right Arm")]
    [Tooltip("Right-hand location from the upper chest. Does not move the gun or left hand.")]
    public Vector3 rightHandPosition = new(0.18f, 0.06f, 0.50f);
    [Tooltip("Right wrist tilt. Does not rotate the gun.")]
    public Vector3 rightWristEuler;
    [Tooltip("How straight the right elbow is. 1 is almost locked. Lower keeps a bent arm.")]
    [Range(0.55f, 0.98f)] public float rightArmReach = 0.90f;
    [Tooltip("Swings the right elbow around the shoulder-to-hand line. 0 is out from the body.")]
    [Range(-180f, 180f)] public float rightElbowYaw;
    public Vector3 aimRightHandPosition = new(0.16f, 0.12f, 0.54f);
    public Vector3 aimRightWristEuler;

    [Header("Left Arm")]
    [Tooltip("Left-hand location from the upper chest. Does not move the gun or right hand.")]
    public Vector3 leftHandPosition = new(0.08f, 0.04f, 0.46f);
    [Tooltip("Left wrist tilt. Does not rotate the gun.")]
    public Vector3 leftWristEuler;
    [Tooltip("How straight the left elbow is. 1 is almost locked. Lower keeps a bent arm.")]
    [Range(0.55f, 0.98f)] public float leftArmReach = 0.86f;
    [Tooltip("Swings the left elbow around the shoulder-to-hand line. 0 is out from the body.")]
    [Range(-180f, 180f)] public float leftElbowYaw;
    public Vector3 aimLeftHandPosition = new(0.08f, 0.10f, 0.50f);
    public Vector3 aimLeftWristEuler;

    [Header("Weights")]
    [Range(0f, 1f)] public float defaultWeight = 1f;
    [Range(0f, 1f)] public float sprintWeight = 0.28f;
    [Range(0f, 1f)] public float crouchWeight = 1f;
    [Range(0f, 1f)] public float proneWeight = 0.55f;
    [Range(0f, 1f)] public float diveWeight = 0.12f;
    [Range(0f, 1f)] public float jumpWeight = 0.85f;

    [Header("Aim Follow")]
    [Tooltip("Maximum spine/arm pitch applied from networked look pitch.")]
    public float maxAimPitch = 50f;
    [Range(0f, 1f)] public float spineAimWeight = 0.62f;
    [Range(0f, 1f)] public float upperChestAimShare = 0.45f;
    [Tooltip("Extra upward upper-body pitch while ADS/zoom is active.")]
    public float aimRaisePitch = 14f;

    [Header("Upper-Body Recoil")]
    [Tooltip("Light upward muzzle kick. Positive values kick up. Does not stack.")]
    public float recoilPitch = 2.5f;
    [Tooltip("Extra right-side roll so the kicking shoulder is readable.")]
    public float recoilRightRoll = 5f;
    public float recoilYaw = 1.5f;
    public float recoilInTime = 0.035f;
    public float recoilOutTime = 0.11f;

    public static ThirdPersonWeaponPose CreateDefault(ThirdPersonWeaponClass weaponClass)
    {
        return weaponClass switch
        {
            ThirdPersonWeaponClass.Rifle => new ThirdPersonWeaponPose
            {
                gunPosition = new Vector3(0.22f, -0.04f, 0.48f),
                gunEuler = new Vector3(0f, 0f, -8f),
                aimGunPosition = new Vector3(0.20f, 0.04f, 0.52f),
                aimGunEuler = new Vector3(-4f, 0f, -8f),
                rightHandPosition = new Vector3(0.22f, -0.04f, 0.48f),
                rightWristEuler = new Vector3(-4f, 6f, 0f),
                rightArmReach = 0.90f,
                aimRightHandPosition = new Vector3(0.20f, 0.04f, 0.52f),
                aimRightWristEuler = new Vector3(-8f, 4f, 0f),
                leftHandPosition = new Vector3(0.24f, -0.06f, 0.70f),
                leftArmReach = 0.86f,
                aimLeftHandPosition = new Vector3(0.22f, 0.02f, 0.74f),
                aimRaisePitch = 0f,
                recoilPitch = 5f,
                recoilRightRoll = 4f,
                recoilOutTime = 0.09f,
                sprintWeight = 0.22f,
                proneWeight = 0.5f
            },
            ThirdPersonWeaponClass.Shotgun => new ThirdPersonWeaponPose
            {
                gunPosition = new Vector3(0.23f, -0.05f, 0.47f),
                gunEuler = new Vector3(0f, 0f, -6f),
                aimGunPosition = new Vector3(0.20f, 0.03f, 0.51f),
                aimGunEuler = new Vector3(-4f, 0f, -6f),
                rightHandPosition = new Vector3(0.23f, -0.05f, 0.47f),
                rightWristEuler = new Vector3(-3f, 6f, 0f),
                rightArmReach = 0.89f,
                aimRightHandPosition = new Vector3(0.20f, 0.03f, 0.51f),
                aimRightWristEuler = new Vector3(-7f, 4f, 0f),
                leftHandPosition = new Vector3(0.25f, -0.07f, 0.65f),
                leftArmReach = 0.84f,
                aimLeftHandPosition = new Vector3(0.22f, 0.01f, 0.69f),
                aimRaisePitch = 0f,
                recoilPitch = 9f,
                recoilRightRoll = 6f,
                recoilOutTime = 0.12f,
                sprintWeight = 0.22f,
                proneWeight = 0.5f
            },
            _ => new ThirdPersonWeaponPose
            {
                gunPosition = new Vector3(0.18f, 0.06f, 0.50f),
                aimGunPosition = new Vector3(0.16f, 0.12f, 0.54f),
                rightHandPosition = new Vector3(0.18f, 0.06f, 0.50f),
                rightWristEuler = new Vector3(-2f, 4f, 0f),
                rightArmReach = 0.91f,
                aimRightHandPosition = new Vector3(0.16f, 0.12f, 0.54f),
                aimRightWristEuler = new Vector3(-6f, 3f, 0f),
                leftHandPosition = new Vector3(0.22f, 0.03f, 0.50f),
                leftArmReach = 0.78f,
                aimLeftHandPosition = new Vector3(0.20f, 0.09f, 0.54f),
                aimRaisePitch = 0f,
                recoilPitch = 7f,
                recoilRightRoll = 5f,
                sprintWeight = 0.3f,
                proneWeight = 0.6f
            }
        };
    }
}
