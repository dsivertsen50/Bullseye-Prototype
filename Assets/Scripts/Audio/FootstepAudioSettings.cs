using UnityEngine;

[CreateAssetMenu(
    fileName = "FootstepAudioSettings",
    menuName = "Bullseye/Audio/Footstep Audio Settings")]
public class FootstepAudioSettings : ScriptableObject
{
    public const string ResourcesName = "FootstepAudioSettings";

    [Header("Materials")]
    [SerializeField] private FootstepClipSet defaultSurface = new(FootstepSurfaceKind.Default);
    [SerializeField] private FootstepClipSet grass = new(FootstepSurfaceKind.Grass);
    [SerializeField] private FootstepClipSet dirt = new(FootstepSurfaceKind.Dirt);
    [SerializeField] private FootstepClipSet concrete = new(FootstepSurfaceKind.Concrete);
    [SerializeField] private FootstepClipSet metal = new(FootstepSurfaceKind.Metal);
    [SerializeField] private FootstepClipSet rock = new(FootstepSurfaceKind.Rock);
    [SerializeField] private FootstepClipSet stone = new(FootstepSurfaceKind.Stone);
    [SerializeField] private FootstepClipSet wood = new(FootstepSurfaceKind.Wood);
    [SerializeField] private FootstepClipSet sand = new(FootstepSurfaceKind.Sand);
    [SerializeField] private FootstepClipSet water = new(FootstepSurfaceKind.Water);

    [Header("Timing")]
    [SerializeField, Min(0.08f), Tooltip("Seconds between walk steps. Lower is a quicker pace.")]
    private float walkInterval = 0.3f;
    [SerializeField, Min(0.08f), Tooltip("Seconds between sprint steps. Should be faster than walk.")]
    private float sprintInterval = 0.21f;
    [SerializeField, Min(0.08f)] private float crouchInterval = 0.4f;
    [SerializeField, Min(0.05f)] private float minimumMoveSpeed = 0.85f;

    [Header("Volume")]
    [SerializeField, Range(0f, 2f)] private float walkVolume = 0.52f;
    [SerializeField, Range(0f, 2f)] private float sprintVolume = 0.9f;
    [SerializeField, Range(0f, 2f)] private float crouchVolume = 0.32f;
    [SerializeField, Range(0f, 0.35f)] private float volumeVariation = 0.12f;

    [Header("Pitch")]
    [SerializeField, Range(0.5f, 1.5f)] private float walkPitch = 1.04f;
    [SerializeField, Range(0.5f, 1.5f)] private float sprintPitch = 0.96f;
    [SerializeField, Range(0f, 0.35f), Tooltip("Per-step random pitch offset around the walk/sprint base.")]
    private float pitchVariation = 0.12f;

    [Header("Spatial")]
    [SerializeField, Min(0.05f)] private float minDistance = 1.2f;
    [SerializeField, Min(1f)] private float maxDistance = 22f;

    public float WalkInterval => Mathf.Max(0.08f, walkInterval);
    public float SprintInterval => Mathf.Max(0.08f, sprintInterval);
    public float CrouchInterval => Mathf.Max(0.08f, crouchInterval);
    public float MinimumMoveSpeed => Mathf.Max(0.05f, minimumMoveSpeed);
    public float WalkVolume => Mathf.Max(0f, walkVolume);
    public float SprintVolume => Mathf.Max(0f, sprintVolume);
    public float CrouchVolume => Mathf.Max(0f, crouchVolume);
    public float VolumeVariation => Mathf.Clamp(volumeVariation, 0f, 0.35f);
    public float WalkPitch => Mathf.Clamp(walkPitch, 0.5f, 1.5f);
    public float SprintPitch => Mathf.Clamp(sprintPitch, 0.5f, 1.5f);
    public float PitchVariation => Mathf.Clamp(pitchVariation, 0f, 0.35f);
    public float MinDistance => Mathf.Max(0.05f, minDistance);
    public float MaxDistance => Mathf.Max(MinDistance + 0.1f, maxDistance);

    public static FootstepAudioSettings Load()
    {
        return Resources.Load<FootstepAudioSettings>(ResourcesName);
    }

    public FootstepClipSet GetSet(FootstepSurfaceKind kind)
    {
        FootstepClipSet set = kind switch
        {
            FootstepSurfaceKind.Grass => grass,
            FootstepSurfaceKind.Dirt => dirt,
            FootstepSurfaceKind.Concrete => concrete,
            FootstepSurfaceKind.Metal => metal,
            FootstepSurfaceKind.Rock => rock,
            FootstepSurfaceKind.Stone => stone,
            FootstepSurfaceKind.Wood => wood,
            FootstepSurfaceKind.Sand => sand,
            FootstepSurfaceKind.Water => water,
            _ => defaultSurface
        };

        if (set != null && (FootstepClipSet.HasAny(set.WalkClips) || FootstepClipSet.HasAny(set.SprintClips)))
            return set;

        return defaultSurface;
    }

    public float ResolveInterval(bool sprinting, bool crouched)
    {
        if (sprinting)
            return SprintInterval;
        if (crouched)
            return CrouchInterval;
        return WalkInterval;
    }

    public float ResolveVolume(bool sprinting, bool crouched)
    {
        float volume = walkVolume;
        if (sprinting)
            volume = sprintVolume;
        else if (crouched)
            volume = crouchVolume;

        float variation = VolumeVariation;
        if (variation > 0f)
            volume *= 1f - Random.Range(0f, variation);

        return Mathf.Max(0f, volume);
    }

    public float ResolvePitch(bool sprinting)
    {
        float pitch = sprinting ? SprintPitch : WalkPitch;
        float variation = PitchVariation;
        if (variation > 0f)
            pitch += Random.Range(-variation, variation);

        return Mathf.Clamp(pitch, 0.5f, 1.6f);
    }

    private void OnValidate()
    {
        walkInterval = Mathf.Max(0.08f, walkInterval);
        sprintInterval = Mathf.Max(0.08f, sprintInterval);
        crouchInterval = Mathf.Max(0.08f, crouchInterval);
        minimumMoveSpeed = Mathf.Max(0.05f, minimumMoveSpeed);
        walkVolume = Mathf.Max(0f, walkVolume);
        sprintVolume = Mathf.Max(0f, sprintVolume);
        crouchVolume = Mathf.Max(0f, crouchVolume);
        volumeVariation = Mathf.Clamp(volumeVariation, 0f, 0.35f);
        walkPitch = Mathf.Clamp(walkPitch, 0.5f, 1.5f);
        sprintPitch = Mathf.Clamp(sprintPitch, 0.5f, 1.5f);
        pitchVariation = Mathf.Clamp(pitchVariation, 0f, 0.35f);
        minDistance = Mathf.Max(0.05f, minDistance);
        maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
        defaultSurface ??= new FootstepClipSet(FootstepSurfaceKind.Default);
        grass ??= new FootstepClipSet(FootstepSurfaceKind.Grass);
        dirt ??= new FootstepClipSet(FootstepSurfaceKind.Dirt);
        concrete ??= new FootstepClipSet(FootstepSurfaceKind.Concrete);
        metal ??= new FootstepClipSet(FootstepSurfaceKind.Metal);
        rock ??= new FootstepClipSet(FootstepSurfaceKind.Rock);
        stone ??= new FootstepClipSet(FootstepSurfaceKind.Stone);
        wood ??= new FootstepClipSet(FootstepSurfaceKind.Wood);
        sand ??= new FootstepClipSet(FootstepSurfaceKind.Sand);
        water ??= new FootstepClipSet(FootstepSurfaceKind.Water);
    }
}
