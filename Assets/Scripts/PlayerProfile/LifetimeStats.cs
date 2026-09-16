using System;
using UnityEngine;

/// <summary>
/// Persistent career totals. Raw counters are stored; ratios are derived.
/// Aggregation rules: most fields SUM, longest distances MAX.
/// </summary>
[Serializable]
public class LifetimeStats
{
    public int MatchesPlayed;
    public int MatchesCompleted;
    public int Wins;
    public int Losses;

    public int Eliminations;
    public int Deaths;
    public int Assists;

    public int ShotsFired;
    public int ShotsHit;

    public float TotalDamageDealt;
    public float TotalDamageReceived;
    public float TotalPlayTimeSeconds;

    public int AttachedBullseyeEliminations;
    public int DetachedBullseyeEliminations;
    public int BullseyeHits;
    public int BodyHits;
    public int HeadHits;
    public int GrenadeBullseyeDetachments;
    public int BodySlamEliminations;
    public int RicochetEliminations;

    public float LongestEliminationDistance;
    public float TotalEliminationDistance;
    public int EliminationDistanceSamples;

    public float KDRatio => Deaths > 0 ? (float)Eliminations / Deaths : Eliminations;
    public float Accuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;
    public float WinRate => MatchesCompleted > 0 ? (float)Wins / MatchesCompleted : 0f;
    public float AverageEliminationDistance =>
        EliminationDistanceSamples > 0
            ? TotalEliminationDistance / EliminationDistanceSamples
            : 0f;
    public float BullseyeHitPercentage =>
        ShotsFired > 0 ? (float)BullseyeHits / ShotsFired : 0f;

    public void Apply(FinalizedMatchStats match)
    {
        if (match == null)
            return;

        MatchesPlayed += 1;
        if (match.Completed)
            MatchesCompleted += 1;
        if (match.Won)
            Wins += 1;
        else if (match.Completed && match.HasPlacementResult)
            Losses += 1;

        Eliminations += match.Eliminations;
        Deaths += match.Deaths;
        Assists += match.Assists;
        ShotsFired += match.ShotsFired;
        ShotsHit += match.ShotsHit;
        TotalDamageDealt += Mathf.Max(0f, match.DamageDealt);
        TotalDamageReceived += Mathf.Max(0f, match.DamageReceived);
        TotalPlayTimeSeconds += Mathf.Max(0f, match.PlayTimeSeconds);

        AttachedBullseyeEliminations += match.AttachedBullseyeEliminations;
        DetachedBullseyeEliminations += match.DetachedBullseyeEliminations;
        BullseyeHits += match.BullseyeHits;
        BodyHits += match.BodyHits;
        HeadHits += match.HeadHits;
        GrenadeBullseyeDetachments += match.GrenadeBullseyeDetachments;
        BodySlamEliminations += match.BodySlamEliminations;
        RicochetEliminations += match.RicochetEliminations;

        LongestEliminationDistance = Mathf.Max(
            LongestEliminationDistance,
            Mathf.Max(0f, match.LongestEliminationDistance));
        TotalEliminationDistance += Mathf.Max(0f, match.TotalEliminationDistance);
        EliminationDistanceSamples += Mathf.Max(0, match.EliminationDistanceSamples);
    }
}
