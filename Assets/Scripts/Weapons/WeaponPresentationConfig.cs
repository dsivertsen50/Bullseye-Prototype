using UnityEngine;

/// <summary>
/// Per-weapon first-person animation/presentation profile. Visual only.
/// </summary>
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

    [Header("Animator")]
    [SerializeField, Tooltip("Optional Animator Controller or Animator Override Controller. Assign weapon-specific clips here; missing clips fall back to procedural motion.")]
    private RuntimeAnimatorController animatorController;
    [SerializeField] private string idleAnimationState = "Idle";
    [SerializeField] private string walkAnimationState = "Walk";
    [SerializeField] private string sprintAnimationState = "Sprint";
    [SerializeField] private string fireAnimationState = "Fire";
    [SerializeField] private string emptyFireAnimationState = "EmptyFire";
    [SerializeField] private string reloadAnimationState = "Reload";
    [SerializeField] private string holsterAnimationState = "Holster";
    [SerializeField] private string unholsterAnimationState = "Unholster";
    [SerializeField] private string aimAnimationState = "Aim";
    [SerializeField] private float fireAnimationSpeed = 1f;
    [SerializeField, Tooltip("Known clip length for the reload state. Used to scale Animator speed to the gameplay reload duration. Leave 0 if unused.")]
    private float reloadAnimationClipLength;

    [Header("Procedural Fire Kick")]
    [SerializeField] private bool useProceduralFireKick = true;
    [SerializeField, Tooltip("Base local kick. Negative Z is backward. Keep Euler X small so the muzzle stays near screen center.")]
    private Vector3 fireKickLocalPosition = new(0f, 0.006f, -0.02f);
    [SerializeField] private Vector3 fireKickLocalEuler = new(-2f, 0f, 0f);
    [SerializeField, Tooltip("Per-shot random offset added to the base kick position.")]
    private Vector3 fireKickPositionVariance = new(0.008f, 0.004f, 0.006f);
    [SerializeField, Tooltip("Per-shot random offset added to the base kick rotation.")]
    private Vector3 fireKickEulerVariance = new(1.5f, 3f, 2.5f);
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
    [SerializeField, Tooltip("Camera pitch kick. The weapon itself should stay near center.")]
    private float recoilPitch;
    [SerializeField] private float recoilYaw;
    [SerializeField] private float recoilPitchVariance = 0.15f;
    [SerializeField] private float recoilYawVariance = 0.35f;

    [Header("Idle Motion")]
    [SerializeField] private float idleSwayAmount = 0.0024f;
    [SerializeField] private float idleSwayFrequency = 1.2f;

    [Header("Weapon Motion")]
    [SerializeField] private float lookSwayAmount = 0.03f;
    [SerializeField] private float lookSwaySmooth = 10f;
    [SerializeField] private float walkBobAmount = 0.006f;
    [SerializeField] private float walkBobFrequency = 8f;

    [Header("Procedural Reload")]
    [SerializeField] private bool useProceduralReload = true;
    [SerializeField, Tooltip("If greater than 0, overrides WeaponDefinition reload time for the visual reload. Leave 0 to match gameplay reload duration.")]
    private float reloadPresentationDuration;
    [SerializeField] private Vector3 reloadLowerLocalPosition = new(0.04f, -0.08f, -0.02f);
    [SerializeField] private Vector3 reloadLowerLocalEuler = new(-16f, 10f, 8f);
    [SerializeField] private Vector3 reloadActionLocalPosition = new(0.05f, -0.12f, -0.02f);
    [SerializeField] private Vector3 reloadActionLocalEuler = new(-8f, 16f, 12f);
    [SerializeField, Min(1)] private int reloadCycleCount = 1;

    [Header("Sprint Pose")]
    [SerializeField] private Vector3 sprintLocalPosition = new(0.08f, -0.16f, -0.06f);
    [SerializeField, Tooltip("Added to the hip rotation while sprinting. X is pitch, Y is yaw/left-right, Z is roll. Flip a component's sign if the gun faces the wrong way.")]
    private Vector3 sprintLocalEuler = new(24f, 22f, -14f);
    [SerializeField] private float sprintTransitionDuration = 0.18f;
    [SerializeField] private float sprintBobAmount = 0.016f;
    [SerializeField] private float sprintBobFrequency = 10f;

    [Header("Sprint Sway")]
    [SerializeField, Tooltip("Side-to-side carry motion while sprinting. Keep the sprint pose mild so this sway stays on-screen.")]
    private float sprintSwayAmount = 0.022f;
    [SerializeField, Tooltip("Sprint carry cycles per second. 1.4–2.5 feels like a running stride. Values above ~4 look frantic.")]
    private float sprintSwayFrequency = 1.9f;
    [SerializeField] private float sprintSwayVerticalAmount = 0.008f;
    [SerializeField] private float sprintSwayYaw = 7f;
    [SerializeField] private float sprintSwayRoll = 6f;
    [SerializeField] private float sprintSwayPitch = 2f;
    [SerializeField, Tooltip("How far sprint bob pulls the weapon backward. Keep this small so the weapon stays visible.")]
    private float sprintForwardBob = 0.004f;

    [Header("Equip / Holster")]
    [SerializeField] private Vector3 holsterLocalPosition = new(0.03f, -0.3f, -0.1f);
    [SerializeField] private Vector3 holsterLocalEuler = new(42f, 16f, -20f);
    [SerializeField] private float holsterDuration = 0.16f;
    [SerializeField] private float unholsterDuration = 0.2f;

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
    public RuntimeAnimatorController AnimatorController => animatorController;
    public string IdleAnimationState => idleAnimationState;
    public string WalkAnimationState => walkAnimationState;
    public string SprintAnimationState => sprintAnimationState;
    public string FireAnimationState => fireAnimationState;
    public string EmptyFireAnimationState => emptyFireAnimationState;
    public string ReloadAnimationState => reloadAnimationState;
    public string HolsterAnimationState => holsterAnimationState;
    public string UnholsterAnimationState => unholsterAnimationState;
    public string AimAnimationState => aimAnimationState;
    public float FireAnimationSpeed => fireAnimationSpeed;
    public float ReloadAnimationClipLength => reloadAnimationClipLength;
    public bool UseProceduralReload => useProceduralReload;
    public float ReloadPresentationDuration => reloadPresentationDuration;
    public Vector3 ReloadLowerLocalPosition => reloadLowerLocalPosition;
    public Vector3 ReloadLowerLocalEuler => reloadLowerLocalEuler;
    public Vector3 ReloadActionLocalPosition => reloadActionLocalPosition;
    public Vector3 ReloadActionLocalEuler => reloadActionLocalEuler;
    public int ReloadCycleCount => Mathf.Max(1, reloadCycleCount);
    public bool UseProceduralFireKick => useProceduralFireKick;
    public Vector3 FireKickLocalPosition => fireKickLocalPosition;
    public Vector3 FireKickLocalEuler => fireKickLocalEuler;
    public Vector3 FireKickPositionVariance => fireKickPositionVariance;
    public Vector3 FireKickEulerVariance => fireKickEulerVariance;
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
    public float RecoilPitchVariance => recoilPitchVariance;
    public float RecoilYawVariance => recoilYawVariance;
    public float LookSwayAmount => lookSwayAmount;
    public float LookSwaySmooth => lookSwaySmooth;
    public float IdleSwayAmount => idleSwayAmount;
    public float IdleSwayFrequency => idleSwayFrequency;
    public float WalkBobAmount => walkBobAmount;
    public float WalkBobFrequency => walkBobFrequency;
    public Vector3 SprintLocalPosition => sprintLocalPosition;
    public Vector3 SprintLocalEuler => sprintLocalEuler;
    public float SprintTransitionDuration => sprintTransitionDuration;
    public float SprintBobAmount => sprintBobAmount;
    public float SprintBobFrequency => sprintBobFrequency;
    public float SprintSwayAmount => sprintSwayAmount;
    public float SprintSwayFrequency => sprintSwayFrequency;
    public float SprintSwayVerticalAmount => sprintSwayVerticalAmount;
    public float SprintSwayYaw => sprintSwayYaw;
    public float SprintSwayRoll => sprintSwayRoll;
    public float SprintSwayPitch => sprintSwayPitch;
    public float SprintForwardBob => sprintForwardBob;
    public Vector3 HolsterLocalPosition => holsterLocalPosition;
    public Vector3 HolsterLocalEuler => holsterLocalEuler;
    public float HolsterDuration => holsterDuration;
    public float UnholsterDuration => unholsterDuration;
    public Vector3 WorldFireKickLocalPosition => worldFireKickLocalPosition;
    public Vector3 WorldFireKickLocalEuler => worldFireKickLocalEuler;
    public float WorldFireKickDuration => worldFireKickDuration;
    public float WorldFireRecoverDuration => worldFireRecoverDuration;
    public Vector3 WorldMuzzleVfxLocalScale => worldMuzzleVfxLocalScale;
    public float WorldFireSfxVolume => worldFireSfxVolume;
    public float WorldAudioMinDistance => worldAudioMinDistance;
    public float WorldAudioMaxDistance => worldAudioMaxDistance;

    public void SampleFireKick(out Vector3 position, out Vector3 euler)
    {
        position = fireKickLocalPosition + RandomRange(fireKickPositionVariance);
        euler = fireKickLocalEuler + RandomRange(fireKickEulerVariance);
    }

    public void SampleCameraRecoil(out float pitch, out float yaw)
    {
        pitch = Mathf.Max(0f, recoilPitch + Random.Range(-recoilPitchVariance, recoilPitchVariance));
        float yawRange = Mathf.Abs(recoilYaw) + recoilYawVariance;
        yaw = Random.Range(-yawRange, yawRange);
    }

    public float ResolveReloadDuration(float gameplayReloadTime)
    {
        if (reloadPresentationDuration > 0.01f)
            return reloadPresentationDuration;
        return Mathf.Max(0.05f, gameplayReloadTime);
    }

    public float ResolveReloadAnimatorSpeed(float duration)
    {
        if (reloadAnimationClipLength <= 0.01f || duration <= 0.01f)
            return 1f;
        return reloadAnimationClipLength / duration;
    }

    private static Vector3 RandomRange(Vector3 range)
    {
        return new Vector3(
            Random.Range(-range.x, range.x),
            Random.Range(-range.y, range.y),
            Random.Range(-range.z, range.z));
    }
}
