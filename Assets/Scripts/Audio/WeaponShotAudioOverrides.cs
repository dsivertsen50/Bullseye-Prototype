using UnityEngine;

/// <summary>
/// Optional per-weapon clip overrides. Empty arrays use the shared
/// WeaponShotAudioSettings library so new weapons work without extra audio.
/// </summary>
[System.Serializable]
public class WeaponShotAudioOverrides
{
    [SerializeField, Tooltip("When disabled, this weapon never triggers near-miss flyby audio.")]
    private bool nearMissEnabled = true;

    [SerializeField, Tooltip("Optional impact clips for this weapon. Leave empty to use the shared impact library.")]
    private AudioClip[] impactClips;

    [SerializeField, Tooltip("Optional flyby clips for this weapon. Leave empty to use the shared flyby library.")]
    private AudioClip[] flybyClips;

    public bool NearMissEnabled => nearMissEnabled;
    public AudioClip[] ImpactClips => impactClips;
    public AudioClip[] FlybyClips => flybyClips;

    public bool HasImpactOverride => HasAnyClip(impactClips);
    public bool HasFlybyOverride => HasAnyClip(flybyClips);

    public AudioClip[] ResolveImpactClips(AudioClip[] shared)
    {
        return HasImpactOverride ? impactClips : shared;
    }

    public AudioClip[] ResolveFlybyClips(AudioClip[] shared)
    {
        return HasFlybyOverride ? flybyClips : shared;
    }

    public static WeaponShotAudioOverrides Fallback { get; } = new();

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
}
