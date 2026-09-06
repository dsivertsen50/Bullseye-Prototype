using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponShotAudioSettings",
    menuName = "Bullseye/Audio/Weapon Shot Audio Settings")]
public class WeaponShotAudioSettings : ScriptableObject
{
    public const string ResourcesName = "WeaponShotAudioSettings";

    [Header("Bullet Impact Audio")]
    [SerializeField] private bool impactEnabled = true;
    [SerializeField] private AudioClip[] impactClips;
    [SerializeField, Range(0f, 2f)] private float impactVolume = 1f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Pitch is randomized ± this amount around 1.")]
    private float impactPitchVariation = 0.05f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Volume is randomized down from Impact Volume by this fraction.")]
    private float impactVolumeVariation = 0.1f;
    [SerializeField, Min(1), Tooltip("Hard cap on impact sounds from one trigger pull. Limits shotgun pellet spam.")]
    private int maxImpactSoundsPerShot = 3;
    [SerializeField, Min(0f), Tooltip("Skip extra pellet impact sounds closer than this, in meters.")]
    private float impactSoundSeparation = 0.6f;
    [SerializeField, Min(0.1f)] private float impactMinDistance = 1.2f;
    [SerializeField, Min(1f)] private float impactMaxDistance = 42f;

    [Header("Near Miss Audio")]
    [SerializeField] private bool nearMissEnabled = true;
    [SerializeField] private AudioClip[] flybyClips;
    [SerializeField, Min(0.05f), Tooltip("Maximum distance from the shot line that still counts as a near miss.")]
    private float nearMissRadius = 1.5f;
    [SerializeField, Min(0f), Tooltip("Inside this radius the flyby is treated as very close. 0 disables the inner zone.")]
    private float innerNearMissRadius = 0.75f;
    [SerializeField, Range(0f, 2f)] private float nearMissVolume = 0.4f;
    [SerializeField, Min(1f), Tooltip("Volume multiplier applied when the shot is inside the inner radius.")]
    private float innerNearMissVolumeMultiplier = 1.25f;
    [SerializeField, Range(0f, 0.25f)] private float nearMissPitchVariation = 0.05f;
    [SerializeField, Min(0f), Tooltip("Local cooldown after a flyby so automatic fire does not stack dozens of sounds.")]
    private float nearMissCooldown = 0.12f;
    [SerializeField, Min(0.05f)] private float flybyMinDistance = 0.35f;
    [SerializeField, Min(1f)] private float flybyMaxDistance = 10f;

    [Header("Ricochet Audio")]
    [SerializeField] private bool ricochetEnabled = true;
    [SerializeField, Tooltip("Drop several ricochet clips here, or into Assets/Audio/Resources/Ricochets.")]
    private AudioClip[] ricochetClips;
    [SerializeField, Range(0f, 2f)] private float ricochetVolume = 0.85f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Pitch is randomized ± this amount around 1.")]
    private float ricochetPitchVariation = 0.08f;
    [SerializeField, Range(0f, 0.25f), Tooltip("Volume is randomized down from Ricochet Volume by this fraction.")]
    private float ricochetVolumeVariation = 0.12f;
    [SerializeField, Min(0.1f)] private float ricochetMinDistance = 1.2f;
    [SerializeField, Min(1f)] private float ricochetMaxDistance = 42f;

    [Header("Debug")]
    [SerializeField, Tooltip("Draws shot lines and logs near-miss decisions. Editor / development builds only unless enabled.")]
    private bool debugNearMiss;

    public bool ImpactEnabled => impactEnabled;
    public AudioClip[] ImpactClips => impactClips;
    public float ImpactVolume => Mathf.Max(0f, impactVolume);
    public float ImpactPitchVariation => Mathf.Max(0f, impactPitchVariation);
    public float ImpactVolumeVariation => Mathf.Clamp01(impactVolumeVariation);
    public int MaxImpactSoundsPerShot => Mathf.Max(1, maxImpactSoundsPerShot);
    public float ImpactSoundSeparation => Mathf.Max(0f, impactSoundSeparation);
    public float ImpactMinDistance => Mathf.Max(0.1f, impactMinDistance);
    public float ImpactMaxDistance => Mathf.Max(ImpactMinDistance + 0.1f, impactMaxDistance);

    public bool NearMissEnabled => nearMissEnabled;
    public AudioClip[] FlybyClips => flybyClips;
    public float NearMissRadius => Mathf.Max(0.05f, nearMissRadius);
    public float InnerNearMissRadius => Mathf.Clamp(innerNearMissRadius, 0f, NearMissRadius);
    public float NearMissVolume => Mathf.Max(0f, nearMissVolume);
    public float InnerNearMissVolumeMultiplier => Mathf.Max(1f, innerNearMissVolumeMultiplier);
    public float NearMissPitchVariation => Mathf.Max(0f, nearMissPitchVariation);
    public float NearMissCooldown => Mathf.Max(0f, nearMissCooldown);
    public float FlybyMinDistance => Mathf.Max(0.05f, flybyMinDistance);
    public float FlybyMaxDistance => Mathf.Max(FlybyMinDistance + 0.1f, flybyMaxDistance);
    public bool DebugNearMiss => debugNearMiss;

    public bool RicochetEnabled => ricochetEnabled;
    public AudioClip[] RicochetClips => ResolveRicochetClips();
    public float RicochetVolume => Mathf.Max(0f, ricochetVolume);
    public float RicochetPitchVariation => Mathf.Max(0f, ricochetPitchVariation);
    public float RicochetVolumeVariation => Mathf.Clamp01(ricochetVolumeVariation);
    public float RicochetMinDistance => Mathf.Max(0.1f, ricochetMinDistance);
    public float RicochetMaxDistance => Mathf.Max(RicochetMinDistance + 0.1f, ricochetMaxDistance);

    public static WeaponShotAudioSettings Load()
    {
        return Resources.Load<WeaponShotAudioSettings>(ResourcesName);
    }

    private AudioClip[] ResolveRicochetClips()
    {
        if (HasAnyClip(ricochetClips))
            return ricochetClips;

        AudioClip[] loaded = Resources.LoadAll<AudioClip>("Ricochets");
        if (HasAnyClip(loaded))
            return loaded;

        return impactClips;
    }

    private static bool HasAnyClip(AudioClip[] clips)
    {
        if (clips == null)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        impactVolume = Mathf.Max(0f, impactVolume);
        impactPitchVariation = Mathf.Max(0f, impactPitchVariation);
        impactVolumeVariation = Mathf.Clamp01(impactVolumeVariation);
        maxImpactSoundsPerShot = Mathf.Max(1, maxImpactSoundsPerShot);
        impactSoundSeparation = Mathf.Max(0f, impactSoundSeparation);
        impactMinDistance = Mathf.Max(0.1f, impactMinDistance);
        impactMaxDistance = Mathf.Max(impactMinDistance + 0.1f, impactMaxDistance);
        nearMissRadius = Mathf.Max(0.05f, nearMissRadius);
        innerNearMissRadius = Mathf.Max(0f, innerNearMissRadius);
        nearMissVolume = Mathf.Max(0f, nearMissVolume);
        innerNearMissVolumeMultiplier = Mathf.Max(1f, innerNearMissVolumeMultiplier);
        nearMissPitchVariation = Mathf.Max(0f, nearMissPitchVariation);
        nearMissCooldown = Mathf.Max(0f, nearMissCooldown);
        flybyMinDistance = Mathf.Max(0.05f, flybyMinDistance);
        flybyMaxDistance = Mathf.Max(flybyMinDistance + 0.1f, flybyMaxDistance);
        ricochetVolume = Mathf.Max(0f, ricochetVolume);
        ricochetPitchVariation = Mathf.Max(0f, ricochetPitchVariation);
        ricochetVolumeVariation = Mathf.Clamp01(ricochetVolumeVariation);
        ricochetMinDistance = Mathf.Max(0.1f, ricochetMinDistance);
        ricochetMaxDistance = Mathf.Max(ricochetMinDistance + 0.1f, ricochetMaxDistance);
    }
}
