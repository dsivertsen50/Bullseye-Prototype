using System;
using System.Collections.Generic;

/// <summary>
/// Local persistent player record. Extra career categories (progression,
/// cosmetics, ranked, settings) can be added later without changing identity.
/// </summary>
[Serializable]
public class PlayerProfile
{
    public int SchemaVersion = PlayerProfileConstants.CurrentSchemaVersion;
    public PlayerIdentity Identity = new();
    public LifetimeStats LifetimeStats = new();
    public List<WeaponLifetimeStats> WeaponStats = new();

    public string PlayerProfileId => Identity != null ? Identity.PlayerProfileId : "";
    public string DisplayName => Identity != null ? Identity.DisplayName : PlayerProfileConstants.DefaultDisplayName;

    public static PlayerProfile CreateDefault()
    {
        return new PlayerProfile
        {
            SchemaVersion = PlayerProfileConstants.CurrentSchemaVersion,
            Identity = PlayerIdentity.CreateNew(),
            LifetimeStats = new LifetimeStats(),
            WeaponStats = new List<WeaponLifetimeStats>()
        };
    }

    public WeaponLifetimeStats GetWeaponStats(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId) || WeaponStats == null)
            return null;

        for (int i = 0; i < WeaponStats.Count; i++)
        {
            WeaponLifetimeStats stats = WeaponStats[i];
            if (stats != null && stats.WeaponId == weaponId)
                return stats;
        }

        return null;
    }

    public WeaponLifetimeStats GetOrCreateWeaponStats(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return null;

        WeaponStats ??= new List<WeaponLifetimeStats>();
        WeaponLifetimeStats existing = GetWeaponStats(weaponId);
        if (existing != null)
            return existing;

        var created = new WeaponLifetimeStats { WeaponId = weaponId };
        WeaponStats.Add(created);
        return created;
    }

    public string FavoriteWeaponId
    {
        get
        {
            if (WeaponStats == null || WeaponStats.Count == 0)
                return "";

            string bestId = "";
            int bestEliminations = 0;
            for (int i = 0; i < WeaponStats.Count; i++)
            {
                WeaponLifetimeStats stats = WeaponStats[i];
                if (stats == null || stats.Eliminations <= bestEliminations)
                    continue;

                bestEliminations = stats.Eliminations;
                bestId = stats.WeaponId;
            }

            return bestEliminations > 0 ? bestId : "";
        }
    }

    public void ApplyMatch(FinalizedMatchStats match)
    {
        LifetimeStats ??= new LifetimeStats();
        LifetimeStats.Apply(match);
        if (match == null || match.WeaponStats == null)
            return;

        for (int i = 0; i < match.WeaponStats.Count; i++)
        {
            WeaponMatchStats weaponMatch = match.WeaponStats[i];
            if (weaponMatch == null || string.IsNullOrWhiteSpace(weaponMatch.WeaponId))
                continue;

            GetOrCreateWeaponStats(weaponMatch.WeaponId)?.Apply(weaponMatch);
        }

        Identity?.TouchLastPlayed();
    }

    public void EnsureCollections()
    {
        Identity ??= new PlayerIdentity();
        LifetimeStats ??= new LifetimeStats();
        WeaponStats ??= new List<WeaponLifetimeStats>();
        if (SchemaVersion <= 0)
            SchemaVersion = PlayerProfileConstants.CurrentSchemaVersion;
        if (string.IsNullOrWhiteSpace(Identity.DisplayName))
            Identity.DisplayName = PlayerProfileConstants.DefaultDisplayName;
    }
}
