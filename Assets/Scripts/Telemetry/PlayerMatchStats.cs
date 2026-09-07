using System.Collections.Generic;

/// <summary>
/// Server-side per-player match aggregates derived from combat events.
/// Individual firearm elimination distances are kept so later systems can
/// compute averages, medians, and weapon-specific ranges.
/// </summary>
public sealed class PlayerMatchStats
{
    public ulong ClientId;

    public int Eliminations;
    public int Deaths;
    public int Assists;
    public int AttachedBullseyeEliminations;
    public int DetachedBullseyeEliminations;
    public int BodySlamEliminations;
    public int BullseyesDetached;
    public int ShotsFired;
    public int BullseyeHits;

    public readonly Dictionary<string, int> EliminationsByWeapon = new();
    public readonly Dictionary<EliminationSourceType, int> EliminationsBySource = new();
    public readonly Dictionary<BullseyeDetachMethod, int> DetachmentsByMethod = new();
    public readonly List<float> FirearmEliminationDistances = new();

    public float AverageFirearmEliminationDistance
    {
        get
        {
            if (FirearmEliminationDistances.Count == 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < FirearmEliminationDistances.Count; i++)
                total += FirearmEliminationDistances[i];
            return total / FirearmEliminationDistances.Count;
        }
    }

    public float LongestFirearmEliminationDistance
    {
        get
        {
            float longest = 0f;
            for (int i = 0; i < FirearmEliminationDistances.Count; i++)
            {
                if (FirearmEliminationDistances[i] > longest)
                    longest = FirearmEliminationDistances[i];
            }

            return longest;
        }
    }

    public float BullseyeHitPercentage =>
        ShotsFired > 0 ? (float)BullseyeHits / ShotsFired : 0f;

    public int GetWeaponEliminations(string weaponId)
    {
        if (string.IsNullOrEmpty(weaponId))
            return 0;

        return EliminationsByWeapon.TryGetValue(weaponId, out int count) ? count : 0;
    }

    public int GetDetachments(BullseyeDetachMethod method)
    {
        return DetachmentsByMethod.TryGetValue(method, out int count) ? count : 0;
    }

    public void Reset()
    {
        Eliminations = 0;
        Deaths = 0;
        Assists = 0;
        AttachedBullseyeEliminations = 0;
        DetachedBullseyeEliminations = 0;
        BodySlamEliminations = 0;
        BullseyesDetached = 0;
        ShotsFired = 0;
        BullseyeHits = 0;
        EliminationsByWeapon.Clear();
        EliminationsBySource.Clear();
        DetachmentsByMethod.Clear();
        FirearmEliminationDistances.Clear();
    }
}
