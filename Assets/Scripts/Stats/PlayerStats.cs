using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative per-player match statistics.
/// Kills, deaths, and detached-bullseye kills are the first implemented fields.
/// Additional Bullseye metrics (hits by region, accuracy, weighted score, etc.)
/// can be added here later without replacing this component or treating score
/// as kill count.
/// </summary>
public class PlayerStats : NetworkBehaviour
{
    private readonly NetworkVariable<int> kills = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> deaths = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> detachedBullseyeKills = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int Kills => kills.Value;
    public int Deaths => deaths.Value;

    /// <summary>
    /// Match-session count of kills scored against a knocked-off bullseye.
    /// Kept for an end-of-game summary; not shown on the in-match HUD.
    /// </summary>
    public int DetachedBullseyeKills => detachedBullseyeKills.Value;

    public event System.Action StatsChanged;

    public override void OnNetworkSpawn()
    {
        kills.OnValueChanged += OnStatChanged;
        deaths.OnValueChanged += OnStatChanged;
        detachedBullseyeKills.OnValueChanged += OnStatChanged;
    }

    public override void OnNetworkDespawn()
    {
        kills.OnValueChanged -= OnStatChanged;
        deaths.OnValueChanged -= OnStatChanged;
        detachedBullseyeKills.OnValueChanged -= OnStatChanged;
    }

    public void AddKill()
    {
        if (!IsServer || !IsSpawned)
            return;

        kills.Value = Mathf.Max(0, kills.Value + 1);
    }

    public void AddDeath()
    {
        if (!IsServer || !IsSpawned)
            return;

        deaths.Value = Mathf.Max(0, deaths.Value + 1);
    }

    public void AddDetachedBullseyeKill()
    {
        if (!IsServer || !IsSpawned)
            return;

        detachedBullseyeKills.Value = Mathf.Max(0, detachedBullseyeKills.Value + 1);
    }

    /// <summary>
    /// Resets match statistics. Call this for a new match/session only —
    /// never from death or respawn.
    /// </summary>
    public void ResetMatchStats()
    {
        if (!IsServer || !IsSpawned)
            return;

        kills.Value = 0;
        deaths.Value = 0;
        detachedBullseyeKills.Value = 0;
    }

    public static PlayerStats FindOwnedByClient(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.SpawnManager == null)
            return null;

        NetworkObject playerObject = networkManager.SpawnManager.GetPlayerNetworkObject(clientId);
        if (playerObject == null)
            return null;

        return playerObject.GetComponent<PlayerStats>();
    }

    private void OnStatChanged(int previous, int next)
    {
        StatsChanged?.Invoke();
    }
}
