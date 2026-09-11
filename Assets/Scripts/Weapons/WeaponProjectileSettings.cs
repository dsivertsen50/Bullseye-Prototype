using UnityEngine;

/// <summary>
/// Per-weapon projectile and explosion profile. Tune these values on the
/// WeaponDefinition; do not hardcode rocket behavior in fire scripts.
/// </summary>
[System.Serializable]
public class WeaponProjectileSettings
{
    [Header("Prefab")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Flight")]
    [SerializeField] private ProjectileGuidanceMode guidanceMode = ProjectileGuidanceMode.Straight;
    [SerializeField] private float projectileSpeed = 35f;
    [SerializeField] private float projectileLifetime = 3.5f;
    [SerializeField] private float aimDistance = 500f;
    [SerializeField] private float spawnForwardOffset = 0.35f;
    [SerializeField] private float gravity = 0f;
    [SerializeField] private float collisionRadius = 0.12f;
    [SerializeField, Tooltip("When enabled, the projectile reflects from RicochetSurface colliders.")]
    private bool canRicochet = true;
    [SerializeField, Min(0)] private int maxRicochets = 1;

    [Header("Damage")]
    [SerializeField] private float directHitDamage;
    [SerializeField] private float explosionRadius = 4.5f;
    [SerializeField] private float maximumExplosionDamage = 6f;
    [SerializeField] private float minimumExplosionDamage = 1f;
    [SerializeField] private AnimationCurve explosionFalloff = CreateLinearFalloff();
    [SerializeField] private float explosionForce = 12f;
    [SerializeField] private float explosionUpwardModifier = 0.35f;
    [SerializeField, Tooltip("When enabled, anyone in the blast loses half of their max health. A full-health player survives; a player already at half health dies.")]
    private bool dealHalfMaxHealthExplosionDamage = true;
    [SerializeField, Tooltip("When enabled, a surviving player has their attached bullseye knocked off.")]
    private bool detachesBullseyes = true;

    [Header("Presentation")]
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private AudioClip[] fireSfx;
    [SerializeField] private AudioClip[] flightSfx;
    [SerializeField] private AudioClip[] explosionSfx;
    [SerializeField] private GameObject projectileTrail;
    [SerializeField, Range(0f, 1f)] private float flightSfxVolume = 0.55f;
    [SerializeField, Range(0f, 1f)] private float explosionSfxVolume = 0.95f;
    [SerializeField] private float explosionVfxLifetime = 2.5f;

    [Header("Debug")]
    [SerializeField] private bool debugProjectileTrajectory;
    [SerializeField] private bool debugExplosionRadius;
    [SerializeField] private bool debugAimTarget;

    public GameObject ProjectilePrefab => projectilePrefab;
    public ProjectileGuidanceMode GuidanceMode => guidanceMode;
    public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
    public float ProjectileLifetime => Mathf.Max(0.1f, projectileLifetime);
    public float AimDistance => Mathf.Max(10f, aimDistance);
    public float SpawnForwardOffset => Mathf.Max(0f, spawnForwardOffset);
    public float Gravity => Mathf.Max(0f, gravity);
    public float CollisionRadius => Mathf.Max(0.02f, collisionRadius);
    public bool CanRicochet => canRicochet;
    public int MaxRicochets => canRicochet ? Mathf.Max(0, maxRicochets) : 0;
    public float DirectHitDamage => Mathf.Max(0f, directHitDamage);
    public float ExplosionRadius => Mathf.Max(0.1f, explosionRadius);
    public float MaximumExplosionDamage => Mathf.Max(0f, maximumExplosionDamage);
    public float MinimumExplosionDamage => Mathf.Clamp(minimumExplosionDamage, 0f, MaximumExplosionDamage);
    public AnimationCurve ExplosionFalloff => explosionFalloff ??= CreateLinearFalloff();
    public float ExplosionForce => Mathf.Max(0f, explosionForce);
    public float ExplosionUpwardModifier => Mathf.Max(0f, explosionUpwardModifier);
    public bool DealHalfMaxHealthExplosionDamage => dealHalfMaxHealthExplosionDamage;
    public bool DetachesBullseyes => detachesBullseyes;

    public int ResolveExplosionDamage(PlayerHealth health, float distance, bool directHit)
    {
        if (health == null)
            return 0;

        int amount;
        if (dealHalfMaxHealthExplosionDamage)
        {
            amount = Mathf.Max(1, health.MaxHealth / 2);
        }
        else
        {
            amount = WeaponDamageCalculator.ToHealthUnits(EvaluateExplosionDamage(distance));
        }

        if (directHit)
            amount += WeaponDamageCalculator.ToHealthUnits(DirectHitDamage);

        return Mathf.Max(0, amount);
    }
    public GameObject ExplosionVfx => explosionVfx;
    public AudioClip[] FireSfx => fireSfx;
    public AudioClip[] FlightSfx => flightSfx;
    public AudioClip[] ExplosionSfx => explosionSfx;
    public GameObject ProjectileTrail => projectileTrail;
    public float FlightSfxVolume => Mathf.Clamp01(flightSfxVolume);
    public float ExplosionSfxVolume => Mathf.Clamp01(explosionSfxVolume);
    public float ExplosionVfxLifetime => Mathf.Max(0.1f, explosionVfxLifetime);
    public bool DebugProjectileTrajectory => debugProjectileTrajectory;
    public bool DebugExplosionRadius => debugExplosionRadius;
    public bool DebugAimTarget => debugAimTarget;

    public static WeaponProjectileSettings Fallback { get; } = new();

    public static AnimationCurve CreateLinearFalloff()
    {
        return AnimationCurve.Linear(0f, 1f, 1f, 0f);
    }

    public float EvaluateExplosionDamage(float distance)
    {
        float radius = ExplosionRadius;
        if (distance >= radius)
            return 0f;

        float normalized = Mathf.Clamp01(distance / radius);
        float curve = Mathf.Clamp01(ExplosionFalloff.Evaluate(normalized));
        return Mathf.Lerp(MinimumExplosionDamage, MaximumExplosionDamage, curve);
    }

    public void Validate()
    {
        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
        aimDistance = Mathf.Max(10f, aimDistance);
        spawnForwardOffset = Mathf.Max(0f, spawnForwardOffset);
        gravity = Mathf.Max(0f, gravity);
        collisionRadius = Mathf.Max(0.02f, collisionRadius);
        maxRicochets = Mathf.Max(0, maxRicochets);
        directHitDamage = Mathf.Max(0f, directHitDamage);
        explosionRadius = Mathf.Max(0.1f, explosionRadius);
        maximumExplosionDamage = Mathf.Max(0f, maximumExplosionDamage);
        minimumExplosionDamage = Mathf.Clamp(minimumExplosionDamage, 0f, maximumExplosionDamage);
        explosionForce = Mathf.Max(0f, explosionForce);
        explosionUpwardModifier = Mathf.Max(0f, explosionUpwardModifier);
        flightSfxVolume = Mathf.Clamp01(flightSfxVolume);
        explosionSfxVolume = Mathf.Clamp01(explosionSfxVolume);
        explosionVfxLifetime = Mathf.Max(0.1f, explosionVfxLifetime);
        if (explosionFalloff == null || explosionFalloff.length == 0)
            explosionFalloff = CreateLinearFalloff();
    }
}
