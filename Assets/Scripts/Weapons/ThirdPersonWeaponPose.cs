using System;
using UnityEngine;

public enum ThirdPersonWeaponClass
{
    Pistol = 0,
    Rifle = 1,
    Shotgun = 2
}

/// <summary>
/// Inspector-tunable third-person hold and aim offsets.
/// Positions are meters in upper-chest space unless noted as right-hand local.
/// </summary>
[Serializable]
public class ThirdPersonWeaponPose
{
    [Header("Right-Hand Socket")]
    [Tooltip("Weapon local position on RightHandWeaponSocket.")]
    public Vector3 rightHandLocalPosition;
    [Tooltip("Weapon local euler on RightHandWeaponSocket.")]
    public Vector3 rightHandLocalEuler;
    [Tooltip("Desired world scale of the third-person weapon.")]
    public Vector3 rightHandLocalScale = Vector3.one;

    [Header("Hip-Fire Hold (meters from upper chest)")]
    [Tooltip("Where the right hand should sit while not aiming. Z is forward reach.")]
    public Vector3 holdLocalPosition = new(0.20f, 0.04f, 0.50f);
    public Vector3 holdLocalEuler;

    [Header("Aim Hold (meters from upper chest)")]
    [Tooltip("Where the right hand should sit while aiming.")]
    public Vector3 aimHoldLocalPosition = new(0.18f, 0.10f, 0.54f);
    public Vector3 aimHoldLocalEuler;

    [Header("Aim Socket Offset")]
    public Vector3 aimRightHandLocalPosition;
    public Vector3 aimRightHandLocalEuler;

    [Header("Weights")]
    [Range(0f, 1f)] public float defaultWeight = 1f;
    [Range(0f, 1f)] public float sprintWeight = 0.28f;
    [Range(0f, 1f)] public float crouchWeight = 1f;
    [Range(0f, 1f)] public float proneWeight = 0.55f;
    [Range(0f, 1f)] public float diveWeight = 0.12f;
    [Range(0f, 1f)] public float jumpWeight = 0.85f;

    [Header("Arm Reach")]
    [Tooltip("Right-arm length used by IK. Lower than 1 keeps a more bent elbow.")]
    [Range(0.55f, 0.98f)] public float rightArmReach = 0.90f;
    [Tooltip("Left-arm length used by IK. Higher values reach farther along the weapon.")]
    [Range(0.55f, 0.98f)] public float leftArmReach = 0.74f;

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
                rightHandLocalPosition = new Vector3(0.01f, 0.03f, 0.05f),
                rightHandLocalEuler = new Vector3(6f, 0f, -8f),
                holdLocalPosition = new Vector3(0.20f, -0.02f, 0.24f),
                holdLocalEuler = new Vector3(8f, 8f, 0f),
                aimHoldLocalPosition = new Vector3(0.16f, 0.12f, 0.28f),
                aimHoldLocalEuler = new Vector3(-6f, 4f, 0f),
                rightArmReach = 0.76f,
                leftArmReach = 0.93f,
                aimRaisePitch = 16f,
                recoilPitch = 2f,
                recoilRightRoll = 1.2f,
                recoilOutTime = 0.08f,
                sprintWeight = 0.22f,
                proneWeight = 0.5f
            },
            ThirdPersonWeaponClass.Shotgun => new ThirdPersonWeaponPose
            {
                rightHandLocalPosition = new Vector3(0.01f, 0.04f, 0.04f),
                rightHandLocalEuler = new Vector3(10f, 0f, -6f),
                holdLocalPosition = new Vector3(0.20f, -0.12f, 0.20f),
                holdLocalEuler = new Vector3(14f, 8f, 0f),
                aimHoldLocalPosition = new Vector3(0.16f, 0.08f, 0.26f),
                aimHoldLocalEuler = new Vector3(-4f, 4f, 0f),
                rightArmReach = 0.66f,
                leftArmReach = 0.90f,
                aimRaisePitch = 15f,
                recoilPitch = 3.2f,
                recoilRightRoll = 1.8f,
                recoilOutTime = 0.10f,
                sprintWeight = 0.22f,
                proneWeight = 0.5f
            },
            _ => new ThirdPersonWeaponPose
            {
                rightHandLocalPosition = new Vector3(0.01f, 0.04f, 0.03f),
                rightHandLocalEuler = new Vector3(8f, 4f, -4f),
                holdLocalPosition = new Vector3(0.12f, 0.10f, 0.48f),
                holdLocalEuler = new Vector3(-2f, 2f, 0f),
                aimHoldLocalPosition = new Vector3(0.10f, 0.22f, 0.52f),
                aimHoldLocalEuler = new Vector3(-10f, 1f, 0f),
                rightArmReach = 0.92f,
                leftArmReach = 0.84f,
                aimRaisePitch = 18f,
                recoilPitch = 2.4f,
                recoilRightRoll = 1.4f,
                sprintWeight = 0.3f,
                proneWeight = 0.6f
            }
        };
    }
}
