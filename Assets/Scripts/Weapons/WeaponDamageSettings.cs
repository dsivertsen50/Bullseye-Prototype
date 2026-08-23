using UnityEngine;

public enum WeaponHitscanMode
{
    Single = 0,
    Pellet = 1
}

public enum BeyondMaxRangeDamage
{
    MinimumDamage = 0,
    NoDamage = 1
}

/// <summary>
/// Per-weapon hitscan damage and range profile. New weapons should configure
/// this data rather than adding named cases to the combat scripts.
/// </summary>
[System.Serializable]
public class WeaponDamageSettings
{
    public const int MaxProjectiles = 16;

    [Header("Hitscan")]
    [SerializeField] private WeaponHitscanMode hitscanMode = WeaponHitscanMode.Single;
    [SerializeField, Tooltip("Independent pellet rays. Ignored for single-hitscan weapons.")]
    private int pelletCount = 8;

    [Header("Damage")]
    [SerializeField, Tooltip("Damage of one bullet or, if pellet damage is unused, one pellet.")]
    private float baseDamage = 1f;
    [SerializeField, Tooltip("Per-pellet damage. Used when Hitscan Mode is Pellet and this value is above zero.")]
    private float damagePerPellet = 0.375f;
    [SerializeField, Tooltip("If enabled, a Bullseye hit while the target is on the head still deals at least lethal damage.")]
    private bool guaranteeLethalHeadshot;

    [Header("Range")]
    [SerializeField, Tooltip("Full damage until this distance, in meters.")]
    private float falloffStart = 12f;
    [SerializeField, Tooltip("Damage reaches the minimum multiplier at this distance, in meters.")]
    private float falloffEnd = 30f;
    [SerializeField, Tooltip("Hitscan length and the distance where beyond-max-range behavior begins.")]
    private float maximumRange = 80f;
    [SerializeField] private BeyondMaxRangeDamage beyondMaxRange = BeyondMaxRangeDamage.MinimumDamage;

    [Header("Falloff")]
    [SerializeField, Tooltip("X is normalized distance from falloff start to end. Y is 1 at full damage and 0 at minimum.")]
    private AnimationCurve falloffCurve = CreateLinearFalloff();
    [SerializeField, Range(0f, 1f)] private float minimumDamageMultiplier = 0.5f;

    [Header("Pellet Spread")]
    [SerializeField, Tooltip("Pellet cone in 1080p pixels. 0 uses the current reticle spread. Pellets stay inside the reticle.")]
    private float pelletSpread;

    [Header("Debug")]
    [SerializeField, Tooltip("Logs a per-shot damage breakdown. Leave off for normal play.")]
    private bool logDamage;

    public WeaponHitscanMode HitscanMode => hitscanMode;
    public bool IsPelletHitscan => hitscanMode == WeaponHitscanMode.Pellet;
    public int PelletCount => Mathf.Clamp(pelletCount, 1, MaxProjectiles);
    public int ProjectileCount => IsPelletHitscan ? PelletCount : 1;
    public float BaseDamage => Mathf.Max(0f, baseDamage);
    public float DamagePerPellet => damagePerPellet > 0f ? damagePerPellet : BaseDamage;
    public float ProjectileDamage => IsPelletHitscan ? DamagePerPellet : BaseDamage;
    public float MaximumCloseRangeDamage => ProjectileDamage * ProjectileCount;
    public bool GuaranteeLethalHeadshot => guaranteeLethalHeadshot;
    public float FalloffStart => Mathf.Max(0f, falloffStart);
    public float FalloffEnd => Mathf.Max(FalloffStart, falloffEnd);
    public float MaximumRange => Mathf.Max(0.1f, maximumRange);
    public BeyondMaxRangeDamage BeyondMaxRange => beyondMaxRange;
    public AnimationCurve FalloffCurve => falloffCurve ??= CreateLinearFalloff();
    public float MinimumDamageMultiplier => Mathf.Clamp01(minimumDamageMultiplier);
    public float PelletSpread => Mathf.Max(0f, pelletSpread);
    public bool LogDamage => logDamage;

    public static WeaponDamageSettings Fallback { get; } = new();

    public static AnimationCurve CreateLinearFalloff()
    {
        return AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }

    public void Validate()
    {
        pelletCount = Mathf.Clamp(pelletCount, 1, MaxProjectiles);
        baseDamage = Mathf.Max(0f, baseDamage);
        damagePerPellet = Mathf.Max(0f, damagePerPellet);
        falloffStart = Mathf.Max(0f, falloffStart);
        falloffEnd = Mathf.Max(falloffStart, falloffEnd);
        maximumRange = Mathf.Max(0.1f, maximumRange);
        minimumDamageMultiplier = Mathf.Clamp01(minimumDamageMultiplier);
        pelletSpread = Mathf.Max(0f, pelletSpread);
        if (falloffCurve == null || falloffCurve.length == 0)
            falloffCurve = CreateLinearFalloff();
    }
}
