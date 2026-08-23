using UnityEngine;

/// <summary>
/// Per-weapon reticle and hitscan accuracy profile.
/// Spread values are pixels at 1080p height; they scale with screen height.
/// </summary>
[System.Serializable]
public class WeaponAccuracySettings
{
    public const float ReferenceScreenHeight = 1080f;
    public const float MinimumVisualGap = 2f;

    [Header("Spread")]
    [SerializeField, Tooltip("Reticle gap and hitscan radius at rest, in pixels at 1080p.")]
    private float baseSpread = 10f;
    [SerializeField, Tooltip("Hard cap for reticle gap and hitscan radius, in pixels at 1080p.")]
    private float maxSpread = 48f;

    [Header("Firing Bloom")]
    [SerializeField, Tooltip("Added to current spread after each shot, in pixels at 1080p.")]
    private float bloomPerShot = 6f;
    [SerializeField, Tooltip("Seconds after the last shot before bloom recovery starts.")]
    private float bloomRecoveryDelay = 0.1f;
    [SerializeField, Tooltip("How quickly shot bloom returns to zero, in 1080p pixels per second.")]
    private float bloomRecoverySpeed = 50f;

    [Header("Sprint")]
    [SerializeField, Tooltip("Target spread while sprinting, before firing bloom, in pixels at 1080p.")]
    private float sprintSpread = 32f;
    [SerializeField, Tooltip("How quickly spread grows toward the sprint target, in 1080p pixels per second.")]
    private float sprintSpreadIncreaseSpeed = 80f;
    [SerializeField, Tooltip("How quickly sprint bloom recovers after sprinting stops, in 1080p pixels per second.")]
    private float sprintSpreadRecoverySpeed = 65f;

    [Header("Reticle Visual")]
    [SerializeField, Tooltip("Optional sprite drawn on each of the four reticle arms. Leave empty for a simple bar.")]
    private Sprite reticleSprite;
    [SerializeField, Tooltip("Length of each reticle arm, in pixels at 1080p.")]
    private float reticleElementLength = 10f;
    [SerializeField, Tooltip("Thickness of each reticle arm, in pixels at 1080p.")]
    private float reticleElementThickness = 2f;

    public float BaseSpread => Mathf.Max(MinimumVisualGap, baseSpread);
    public float MaxSpread => Mathf.Max(BaseSpread, maxSpread);
    public float BloomPerShot => Mathf.Max(0f, bloomPerShot);
    public float BloomRecoveryDelay => Mathf.Max(0f, bloomRecoveryDelay);
    public float BloomRecoverySpeed => Mathf.Max(0f, bloomRecoverySpeed);
    public float SprintSpread => Mathf.Clamp(sprintSpread, BaseSpread, MaxSpread);
    public float SprintSpreadIncreaseSpeed => Mathf.Max(0.01f, sprintSpreadIncreaseSpeed);
    public float SprintSpreadRecoverySpeed => Mathf.Max(0.01f, sprintSpreadRecoverySpeed);
    public Sprite ReticleSprite => reticleSprite;
    public float ReticleElementLength => Mathf.Max(1f, reticleElementLength);
    public float ReticleElementThickness => Mathf.Max(1f, reticleElementThickness);

    public float SprintBloomTarget => Mathf.Max(0f, SprintSpread - BaseSpread);

    public void Validate()
    {
        baseSpread = Mathf.Max(MinimumVisualGap, baseSpread);
        maxSpread = Mathf.Max(baseSpread, maxSpread);
        bloomPerShot = Mathf.Max(0f, bloomPerShot);
        bloomRecoveryDelay = Mathf.Max(0f, bloomRecoveryDelay);
        bloomRecoverySpeed = Mathf.Max(0f, bloomRecoverySpeed);
        sprintSpread = Mathf.Clamp(sprintSpread, baseSpread, maxSpread);
        sprintSpreadIncreaseSpeed = Mathf.Max(0.01f, sprintSpreadIncreaseSpeed);
        sprintSpreadRecoverySpeed = Mathf.Max(0.01f, sprintSpreadRecoverySpeed);
        reticleElementLength = Mathf.Max(1f, reticleElementLength);
        reticleElementThickness = Mathf.Max(1f, reticleElementThickness);
    }
}
