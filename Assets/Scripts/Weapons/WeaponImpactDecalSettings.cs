using UnityEngine;

public enum WeaponImpactPattern
{
    Single = 0,
    PelletSpread = 1
}

/// <summary>
/// Per-weapon cosmetic surface-impact settings. New weapons participate by
/// configuring this data rather than adding named cases to combat scripts.
/// </summary>
[System.Serializable]
public class WeaponImpactDecalSettings
{
    [SerializeField, Tooltip("When disabled, this weapon never leaves surface bullet marks.")]
    private bool enabled = true;

    [SerializeField, Min(0.01f), Tooltip("Relative bullet-mark size. 1 is the shared base size.")]
    private float decalScale = 1f;

    [SerializeField, Min(0f), Tooltip("Maximum distance from the fire origin that still creates a mark. Hitscan/damage range is separate.")]
    private float maximumDecalDistance = 50f;

    [SerializeField, Tooltip("Optional override of the shared bullet-hole set. Leave empty to use the global default.")]
    private BulletImpactDecalSet variantSet;

    [SerializeField, Tooltip("Single creates one mark per shot. Pellet Spread creates one mark per nearby pellet hit.")]
    private WeaponImpactPattern impactPattern = WeaponImpactPattern.Single;

    [SerializeField, Min(1), Tooltip("Hard cap on marks created by one trigger pull. Used by pellet-spread weapons.")]
    private int maxDecalsPerShot = 1;

    public bool Enabled => enabled;
    public float DecalScale => Mathf.Max(0.01f, decalScale);
    public float MaximumDecalDistance => Mathf.Max(0f, maximumDecalDistance);
    public BulletImpactDecalSet VariantSet => variantSet;
    public WeaponImpactPattern ImpactPattern => impactPattern;
    public int MaxDecalsPerShot => Mathf.Max(1, maxDecalsPerShot);

    public int ResolveMaxDecalsForShot()
    {
        if (!enabled)
            return 0;

        return impactPattern == WeaponImpactPattern.PelletSpread ? MaxDecalsPerShot : 1;
    }

    public static WeaponImpactDecalSettings Fallback { get; } = new();

    public void Validate()
    {
        decalScale = Mathf.Max(0.01f, decalScale);
        maximumDecalDistance = Mathf.Max(0f, maximumDecalDistance);
        maxDecalsPerShot = Mathf.Max(1, maxDecalsPerShot);
    }
}
