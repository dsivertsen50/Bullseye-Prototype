using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponPresentationConfig",
    menuName = "Bullseye/Weapons/Weapon Presentation Config")]
public class WeaponPresentationConfig : ScriptableObject
{
    [SerializeField] private string weaponName = "Ruger 22";

    [Header("Audio")]
    [SerializeField] private AudioClip fireSfx;
    [SerializeField] private AudioClip reloadSfx;
    [SerializeField] private AudioClip holsterSfx;
    [SerializeField] private AudioClip unholsterSfx;
    [SerializeField, Range(0f, 1f)] private float fireSfxVolume = 0.7f;

    [Header("Muzzle VFX")]
    [SerializeField] private GameObject muzzleVfxPrefab;
    [SerializeField] private Vector3 muzzleVfxLocalScale = new(0.35f, 0.35f, 0.35f);
    [SerializeField] private float muzzleVfxLifetime = 0.6f;

    [Header("Animator States")]
    [SerializeField] private string idleAnimationState = "Idle";
    [SerializeField] private string fireAnimationState = "Fire";
    [SerializeField] private string reloadAnimationState = "Reload";
    [SerializeField] private string holsterAnimationState = "Holster";
    [SerializeField] private string unholsterAnimationState = "Unholster";
    [SerializeField] private string aimAnimationState = "Aim";
    [SerializeField] private float fireAnimationSpeed = 1f;

    [Header("Procedural Fire Kick")]
    [SerializeField] private bool useProceduralFireKick = true;
    [SerializeField] private Vector3 fireKickLocalPosition = new(0f, 0.006f, -0.02f);
    [SerializeField] private Vector3 fireKickLocalEuler = new(-8f, 1.8f, 1.2f);
    [SerializeField] private float fireKickDuration = 0.055f;
    [SerializeField] private float fireRecoverDuration = 0.12f;

    [Header("Hip Pose")]
    [SerializeField] private bool useConfiguredHipPose = true;
    [SerializeField] private Vector3 hipLocalPosition = new(0.15f, -0.15f, 0.4f);
    [SerializeField] private Vector3 hipLocalEuler = new(0f, 1f, 0f);

    [Header("ADS Pose")]
    [SerializeField] private bool useAimPoint = true;
    [SerializeField] private float aimDistance = 0.28f;
    [SerializeField] private Vector3 adsLocalPosition;
    [SerializeField] private Vector3 adsLocalEuler;
    [SerializeField] private float adsBlendDuration = 0.12f;
    [SerializeField] private float aimInSpeed = 8.5f;
    [SerializeField] private float aimOutSpeed = 7f;
    [SerializeField, Range(0f, 1f)] private float adsSwayMultiplier = 0.15f;
    [SerializeField, Range(0f, 1f)] private float adsBobMultiplier = 0.2f;

    [Header("Recoil Foundation")]
    [SerializeField] private float recoilPitch;
    [SerializeField] private float recoilYaw;

    [Header("Weapon Motion")]
    [SerializeField] private float lookSwayAmount = 0.03f;
    [SerializeField] private float lookSwaySmooth = 10f;
    [SerializeField] private float walkBobAmount = 0.006f;
    [SerializeField] private float walkBobFrequency = 8f;

    [Header("World Presentation")]
    [SerializeField] private Vector3 worldFireKickLocalPosition = new(0f, 0.02f, -0.05f);
    [SerializeField] private Vector3 worldFireKickLocalEuler = new(-14f, 3.5f, 2.5f);
    [SerializeField] private float worldFireKickDuration = 0.06f;
    [SerializeField] private float worldFireRecoverDuration = 0.16f;
    [SerializeField] private Vector3 worldMuzzleVfxLocalScale = new(0.55f, 0.55f, 0.55f);
    [SerializeField, Range(0f, 1f)] private float worldFireSfxVolume = 1f;
    [SerializeField] private float worldAudioMinDistance = 1.5f;
    [SerializeField] private float worldAudioMaxDistance = 45f;

    public string WeaponName => weaponName;
    public AudioClip FireSfx => fireSfx;
    public AudioClip ReloadSfx => reloadSfx;
    public AudioClip HolsterSfx => holsterSfx;
    public AudioClip UnholsterSfx => unholsterSfx;
    public float FireSfxVolume => fireSfxVolume;
    public GameObject MuzzleVfxPrefab => muzzleVfxPrefab;
    public Vector3 MuzzleVfxLocalScale => muzzleVfxLocalScale;
    public float MuzzleVfxLifetime => muzzleVfxLifetime;
    public string IdleAnimationState => idleAnimationState;
    public string FireAnimationState => fireAnimationState;
    public string ReloadAnimationState => reloadAnimationState;
    public string HolsterAnimationState => holsterAnimationState;
    public string UnholsterAnimationState => unholsterAnimationState;
    public string AimAnimationState => aimAnimationState;
    public float FireAnimationSpeed => fireAnimationSpeed;
    public bool UseProceduralFireKick => useProceduralFireKick;
    public Vector3 FireKickLocalPosition => fireKickLocalPosition;
    public Vector3 FireKickLocalEuler => fireKickLocalEuler;
    public float FireKickDuration => fireKickDuration;
    public float FireRecoverDuration => fireRecoverDuration;
    public bool UseConfiguredHipPose => useConfiguredHipPose;
    public Vector3 HipLocalPosition => hipLocalPosition;
    public Vector3 HipLocalEuler => hipLocalEuler;
    public bool UseAimPoint => useAimPoint;
    public float AimDistance => aimDistance;
    public Vector3 AdsLocalPosition => adsLocalPosition;
    public Vector3 AdsLocalEuler => adsLocalEuler;
    public float AdsBlendDuration => adsBlendDuration;
    public float AimInSpeed => aimInSpeed;
    public float AimOutSpeed => aimOutSpeed;
    public float AdsSwayMultiplier => adsSwayMultiplier;
    public float AdsBobMultiplier => adsBobMultiplier;
    public float RecoilPitch => recoilPitch;
    public float RecoilYaw => recoilYaw;
    public float LookSwayAmount => lookSwayAmount;
    public float LookSwaySmooth => lookSwaySmooth;
    public float WalkBobAmount => walkBobAmount;
    public float WalkBobFrequency => walkBobFrequency;
    public Vector3 WorldFireKickLocalPosition => worldFireKickLocalPosition;
    public Vector3 WorldFireKickLocalEuler => worldFireKickLocalEuler;
    public float WorldFireKickDuration => worldFireKickDuration;
    public float WorldFireRecoverDuration => worldFireRecoverDuration;
    public Vector3 WorldMuzzleVfxLocalScale => worldMuzzleVfxLocalScale;
    public float WorldFireSfxVolume => worldFireSfxVolume;
    public float WorldAudioMinDistance => worldAudioMinDistance;
    public float WorldAudioMaxDistance => worldAudioMaxDistance;
}
