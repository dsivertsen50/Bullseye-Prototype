using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One completed match ready to merge into lifetime totals.
/// NetworkClientId is informational only and is never used as PlayerProfileId.
/// </summary>
[Serializable]
public class FinalizedMatchStats
{
    public ulong NetworkClientId;
    public bool Completed = true;
    public bool Won;
    public bool HasPlacementResult;

    public int Eliminations;
    public int Deaths;
    public int Assists;
    public int ShotsFired;
    public int ShotsHit;
    public float DamageDealt;
    public float DamageReceived;
    public float PlayTimeSeconds;

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

    public List<WeaponMatchStats> WeaponStats = new();

    public WeaponMatchStats GetOrCreateWeapon(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return null;

        WeaponStats ??= new List<WeaponMatchStats>();
        for (int i = 0; i < WeaponStats.Count; i++)
        {
            if (WeaponStats[i] != null && WeaponStats[i].WeaponId == weaponId)
                return WeaponStats[i];
        }

        var created = new WeaponMatchStats { WeaponId = weaponId };
        WeaponStats.Add(created);
        return created;
    }
}

[Serializable]
public class WeaponMatchStats
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
}

/// <summary>
/// Builds a finalized match snapshot from REQ-055 combat telemetry when the
/// local client is the host, or from replicated scoreboard fields otherwise.
/// Does not invent research-only metrics such as aim samples.
/// </summary>
public static class FinalizedMatchStatsFactory
{
    public static FinalizedMatchStats FromPlayerMatchStats(
        PlayerMatchStats match,
        IReadOnlyList<CombatEvent> events,
        float playTimeSeconds,
        bool completed = true)
    {
        if (match == null)
            return null;

        var result = new FinalizedMatchStats
        {
            NetworkClientId = match.ClientId,
            Completed = completed,
            Eliminations = match.Eliminations,
            Deaths = match.Deaths,
            Assists = match.Assists,
            ShotsFired = match.ShotsFired,
            ShotsHit = match.BullseyeHits,
            PlayTimeSeconds = Mathf.Max(0f, playTimeSeconds),
            AttachedBullseyeEliminations = match.AttachedBullseyeEliminations,
            DetachedBullseyeEliminations = match.DetachedBullseyeEliminations,
            BullseyeHits = match.BullseyeHits,
            GrenadeBullseyeDetachments =
                match.GetDetachments(BullseyeDetachMethod.CombustionGrenade) +
                match.GetDetachments(BullseyeDetachMethod.MagnetismGrenade),
            BodySlamEliminations = match.BodySlamEliminations,
            RicochetEliminations = match.RicochetEliminations,
            LongestEliminationDistance = match.LongestFirearmEliminationDistance
        };

        if (match.FirearmEliminationDistances != null)
        {
            for (int i = 0; i < match.FirearmEliminationDistances.Count; i++)
            {
                float distance = Mathf.Max(0f, match.FirearmEliminationDistances[i]);
                result.TotalEliminationDistance += distance;
                result.EliminationDistanceSamples++;
            }
        }

        if (match.EliminationsByWeapon != null)
        {
            foreach (KeyValuePair<string, int> pair in match.EliminationsByWeapon)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value <= 0)
                    continue;

                WeaponMatchStats weapon = result.GetOrCreateWeapon(pair.Key);
                weapon.Eliminations = pair.Value;
            }
        }

        AccumulateFromEvents(result, match.ClientId, events);
        return result;
    }

    public static FinalizedMatchStats FromScoreboard(PlayerStats stats, float playTimeSeconds, bool completed = true)
    {
        if (stats == null)
            return null;

        return new FinalizedMatchStats
        {
            NetworkClientId = stats.OwnerClientId,
            Completed = completed,
            Eliminations = stats.Eliminations,
            Deaths = stats.Deaths,
            Assists = stats.Assists,
            DetachedBullseyeEliminations = stats.DetachedBullseyeEliminations,
            PlayTimeSeconds = Mathf.Max(0f, playTimeSeconds)
        };
    }

    public static FinalizedMatchStats FromLocalSession()
    {
        ulong localClientId = 0;
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
            localClientId = networkManager.LocalClientId;

        CombatTelemetryManager telemetry = CombatTelemetryManager.Instance;
        if (telemetry != null && telemetry.TryGetStats(localClientId, out PlayerMatchStats match))
            return FromPlayerMatchStats(match, telemetry.Events, telemetry.MatchTimeSeconds);

        PlayerStats scoreboard = PlayerStats.FindOwnedByClient(localClientId);
        if (scoreboard != null)
        {
            float playTime = telemetry != null ? telemetry.MatchTimeSeconds : 0f;
            return FromScoreboard(scoreboard, playTime);
        }

        return null;
    }

    private static void AccumulateFromEvents(
        FinalizedMatchStats result,
        ulong clientId,
        IReadOnlyList<CombatEvent> events)
    {
        if (result == null || events == null)
            return;

        for (int i = 0; i < events.Count; i++)
        {
            CombatEvent combatEvent = events[i];
            if (combatEvent == null)
                continue;

            bool isActor = combatEvent.ActorClientId == clientId;
            bool isTarget = combatEvent.TargetClientId == clientId;
            string weaponId = combatEvent.WeaponId;
            bool hasWeapon = !string.IsNullOrWhiteSpace(weaponId);

            switch (combatEvent.Type)
            {
                case CombatEventType.ShotFired:
                    if (!isActor || !hasWeapon)
                        break;
                    result.GetOrCreateWeapon(weaponId).ShotsFired++;
                    break;

                case CombatEventType.BullseyeHit:
                    if (!isActor)
                        break;
                    if (hasWeapon)
                    {
                        WeaponMatchStats weapon = result.GetOrCreateWeapon(weaponId);
                        weapon.ShotsHit++;
                        weapon.BullseyeHits++;
                    }

                    break;

                case CombatEventType.BullseyeDamaged:
                    if (isActor)
                    {
                        result.DamageDealt += Mathf.Max(0, combatEvent.Amount);
                        if (hasWeapon)
                            result.GetOrCreateWeapon(weaponId).DamageDealt += Mathf.Max(0, combatEvent.Amount);
                    }

                    if (isTarget)
                        result.DamageReceived += Mathf.Max(0, combatEvent.Amount);
                    break;

                case CombatEventType.Elimination:
                    if (!isActor || !hasWeapon || combatEvent.SourceType != EliminationSourceType.Firearm)
                        break;

                    WeaponMatchStats elimWeapon = result.GetOrCreateWeapon(weaponId);
                    float distance = Mathf.Max(0f, combatEvent.Distance);
                    elimWeapon.LongestEliminationDistance = Mathf.Max(
                        elimWeapon.LongestEliminationDistance,
                        distance);
                    elimWeapon.TotalEliminationDistance += distance;
                    elimWeapon.EliminationDistanceSamples++;
                    break;
            }
        }
    }
}
