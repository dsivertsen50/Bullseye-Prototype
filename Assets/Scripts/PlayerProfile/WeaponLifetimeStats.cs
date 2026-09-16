using System;
using UnityEngine;

/// <summary>
/// Persistent per-weapon career totals keyed by WeaponDefinition.WeaponId.
/// </summary>
[Serializable]
public class WeaponLifetimeStats
{
    public string WeaponId;
    public int Eliminations;
    public int ShotsFired;
    public int ShotsHit;
    public float DamageDealt;
    public int HeadHits;
    public int BodyHits;
    public int BullseyeHits;
    public float LongestEliminationDistance;
    public float TotalEliminationDistance;
    public int EliminationDistanceSamples;

    public float Accuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;
    public float AverageEliminationDistance =>
        EliminationDistanceSamples > 0
            ? TotalEliminationDistance / EliminationDistanceSamples
            : 0f;

    public void Apply(WeaponMatchStats match)
    {
        if (match == null || string.IsNullOrWhiteSpace(match.WeaponId))
            return;

        if (string.IsNullOrWhiteSpace(WeaponId))
            WeaponId = match.WeaponId;

        Eliminations += match.Eliminations;
        ShotsFired += match.ShotsFired;
        ShotsHit += match.ShotsHit;
        DamageDealt += Mathf.Max(0f, match.DamageDealt);
        HeadHits += match.HeadHits;
        BodyHits += match.BodyHits;
        BullseyeHits += match.BullseyeHits;
        LongestEliminationDistance = Mathf.Max(
            LongestEliminationDistance,
            Mathf.Max(0f, match.LongestEliminationDistance));
        TotalEliminationDistance += Mathf.Max(0f, match.TotalEliminationDistance);
        EliminationDistanceSamples += Mathf.Max(0, match.EliminationDistanceSamples);
    }
}
