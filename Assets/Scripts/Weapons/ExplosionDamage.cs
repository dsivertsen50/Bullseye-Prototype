using UnityEngine;

/// <summary>
/// Shared radial explosion resolution for projectile weapons. Applies
/// existing PlayerHealth / knockback / telemetry paths rather than a
/// weapon-specific damage manager.
/// </summary>
public static class ExplosionDamage
{
    public static void Apply(
        Vector3 origin,
        WeaponProjectileSettings settings,
        WeaponDefinition weapon,
        ulong shooterClientId,
        PlayerHealth directHitVictim)
    {
        settings ??= WeaponProjectileSettings.Fallback;
        string weaponId = weapon != null ? weapon.WeaponId : "unknown";
        float radius = settings.ExplosionRadius;

        if (settings.DebugExplosionRadius)
            Debug.DrawLine(origin, origin + Vector3.up * radius, Color.red, 1.5f);

        PlayerHealth[] healths = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Exclude);
        for (int i = 0; i < healths.Length; i++)
        {
            PlayerHealth health = healths[i];
            if (health == null || !health.IsSpawned || health.IsDead)
                continue;

            Vector3 target = health.transform.position + Vector3.up * 1f;
            float distance = Vector3.Distance(origin, target);
            if (distance > radius)
                continue;

            bool direct = health == directHitVictim;
            int amount = settings.ResolveExplosionDamage(health, distance, direct);
            if (amount <= 0 && !direct)
                continue;

            BullseyeDetachController detach = health.GetComponent<BullseyeDetachController>();
            bool wouldSurvive = health.CurrentHealth > amount;
            if (settings.DetachesBullseyes && detach != null && (wouldSurvive || detach.IsDetached))
            {
                float detachRadius = settings.ExplosionRadius + 1.25f;
                detach.NotifyExplosion(
                    origin,
                    settings.ExplosionForce,
                    detachRadius,
                    detachRadius,
                    settings.ExplosionUpwardModifier,
                    shooterClientId,
                    BullseyeDetachMethod.RocketExplosion);
            }

            if (direct)
            {
                CombatTelemetryManager.Ensure().RecordBullseyeHit(
                    shooterClientId,
                    health.OwnerClientId,
                    weaponId,
                    distance,
                    CombatTelemetryManager.ResolveBullseyeState(detach),
                    amount);
            }
            else
            {
                CombatTelemetryManager.Ensure().RecordWeaponDamage(
                    shooterClientId,
                    health.OwnerClientId,
                    weaponId,
                    distance,
                    amount);
            }

            if (amount > 0)
            {
                health.ApplyDamage(DamageContext.FromFirearm(
                    shooterClientId,
                    health.OwnerClientId,
                    amount,
                    weaponId,
                    distance));
            }
        }

        ApplyKnockback(origin, settings);
    }

    private static void ApplyKnockback(Vector3 origin, WeaponProjectileSettings settings)
    {
        if (settings.ExplosionForce <= 0f)
            return;

        PlayerMovement[] movers = Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude);
        for (int i = 0; i < movers.Length; i++)
        {
            PlayerMovement movement = movers[i];
            if (movement == null || !movement.IsSpawned)
                continue;

            PlayerHealth health = movement.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead)
                continue;

            Vector3 target = movement.transform.position + Vector3.up * 1f;
            if (Vector3.Distance(origin, target) > settings.ExplosionRadius)
                continue;

            movement.ApplyExplosionKnockbackOwnerRpc(
                origin,
                settings.ExplosionForce,
                settings.ExplosionRadius,
                settings.ExplosionUpwardModifier);
        }
    }
}
