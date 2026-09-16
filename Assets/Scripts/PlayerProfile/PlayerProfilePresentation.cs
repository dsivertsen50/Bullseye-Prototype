using System.Collections.Generic;

/// <summary>
/// Read-only presentation helpers for the Profile screen.
/// Does not own or mutate persistent statistics.
/// </summary>
public static class PlayerProfilePresentation
{
    public static List<WeaponLifetimeStats> GetSortedWeaponStats(PlayerProfile profile)
    {
        var list = new List<WeaponLifetimeStats>();
        if (profile == null || profile.WeaponStats == null)
            return list;

        for (int i = 0; i < profile.WeaponStats.Count; i++)
        {
            WeaponLifetimeStats stats = profile.WeaponStats[i];
            if (stats == null || string.IsNullOrWhiteSpace(stats.WeaponId))
                continue;
            if (!HasVisibleStats(stats))
                continue;

            list.Add(stats);
        }

        list.Sort(CompareWeapons);
        return list;
    }

    public static bool HasVisibleStats(WeaponLifetimeStats stats)
    {
        if (stats == null)
            return false;

        return stats.Eliminations > 0
            || stats.ShotsFired > 0
            || stats.ShotsHit > 0
            || stats.DamageDealt > 0f
            || stats.BullseyeHits > 0
            || stats.HeadHits > 0
            || stats.BodyHits > 0
            || stats.LongestEliminationDistance > 0f;
    }

    public static int BullseyeEliminations(LifetimeStats life)
    {
        if (life == null)
            return 0;

        return life.AttachedBullseyeEliminations + life.DetachedBullseyeEliminations;
    }

    public static bool ShowMatchesCompleted(LifetimeStats life)
    {
        if (life == null)
            return false;

        return life.MatchesCompleted != life.MatchesPlayed;
    }

    private static int CompareWeapons(WeaponLifetimeStats a, WeaponLifetimeStats b)
    {
        int byEliminations = b.Eliminations.CompareTo(a.Eliminations);
        if (byEliminations != 0)
            return byEliminations;

        int byShots = b.ShotsFired.CompareTo(a.ShotsFired);
        if (byShots != 0)
            return byShots;

        return string.CompareOrdinal(a.WeaponId, b.WeaponId);
    }
}
