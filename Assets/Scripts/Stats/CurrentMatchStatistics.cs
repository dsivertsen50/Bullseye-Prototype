using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One current-match scoreboard row. Additional match fields can be added later
/// without introducing lifetime/career statistics.
/// </summary>
public readonly struct MatchScoreboardRow
{
    public readonly ulong ClientId;
    public readonly string DisplayName;
    public readonly int Placement;
    public readonly int Eliminations;
    public readonly int Assists;
    public readonly int Deaths;
    public readonly bool IsLocalPlayer;

    public MatchScoreboardRow(
        ulong clientId,
        string displayName,
        int placement,
        int eliminations,
        int assists,
        int deaths,
        bool isLocalPlayer)
    {
        ClientId = clientId;
        DisplayName = displayName;
        Placement = placement;
        Eliminations = eliminations;
        Assists = assists;
        Deaths = deaths;
        IsLocalPlayer = isLocalPlayer;
    }
}

/// <summary>
/// Reads the replicated REQ-055 match counters for the in-game scoreboard.
/// CombatTelemetryManager keeps richer server-only detail; PlayerStats is the
/// synchronized current-match surface every client can display.
/// </summary>
public static class CurrentMatchStatistics
{
    private static readonly List<PlayerStats> ScratchPlayers = new(16);

    public static string GetDisplayName(ulong clientId)
    {
        return "Player " + (clientId + 1);
    }

    public static void CollectSortedRows(List<MatchScoreboardRow> destination, ulong localClientId)
    {
        if (destination == null)
            return;

        destination.Clear();
        ScratchPlayers.Clear();

        PlayerStats[] players = Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerStats stats = players[i];
            if (stats == null || !stats.IsSpawned)
                continue;

            ScratchPlayers.Add(stats);
        }

        ScratchPlayers.Sort(CompareForScoreboard);

        for (int i = 0; i < ScratchPlayers.Count; i++)
        {
            PlayerStats stats = ScratchPlayers[i];
            ulong clientId = stats.OwnerClientId;
            destination.Add(new MatchScoreboardRow(
                clientId,
                GetDisplayName(clientId),
                i + 1,
                stats.Eliminations,
                stats.Assists,
                stats.Deaths,
                clientId == localClientId));
        }
    }

    public static int CountConnectedPlayers()
    {
        int count = 0;
        PlayerStats[] players = Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].IsSpawned)
                count++;
        }

        return count;
    }

    private static int CompareForScoreboard(PlayerStats a, PlayerStats b)
    {
        int eliminationCompare = b.Eliminations.CompareTo(a.Eliminations);
        if (eliminationCompare != 0)
            return eliminationCompare;

        int assistCompare = b.Assists.CompareTo(a.Assists);
        if (assistCompare != 0)
            return assistCompare;

        int deathCompare = a.Deaths.CompareTo(b.Deaths);
        if (deathCompare != 0)
            return deathCompare;

        return a.OwnerClientId.CompareTo(b.OwnerClientId);
    }
}
