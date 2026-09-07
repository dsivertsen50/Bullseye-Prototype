using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Replicated per-player match counters shown on the HUD.
/// Rich combat detail lives on CombatTelemetryManager; this component only
/// mirrors the scoreboard fields every client needs.
/// </summary>
public class PlayerStats : NetworkBehaviour
{
    private readonly NetworkVariable<int> eliminations = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> deaths = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> assists = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> detachedBullseyeEliminations = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int Eliminations => eliminations.Value;
    public int Deaths => deaths.Value;
    public int Assists => assists.Value;

    /// <summary>
    /// Match-session count of eliminations scored against a knocked-off bullseye.
    /// Kept for summaries; not shown on the in-match HUD.
    /// </summary>
    public int DetachedBullseyeEliminations => detachedBullseyeEliminations.Value;

    public event System.Action StatsChanged;

    public override void OnNetworkSpawn()
    {
        eliminations.OnValueChanged += OnStatChanged;
        deaths.OnValueChanged += OnStatChanged;
        assists.OnValueChanged += OnStatChanged;
        detachedBullseyeEliminations.OnValueChanged += OnStatChanged;
    }

    public override void OnNetworkDespawn()
    {
        eliminations.OnValueChanged -= OnStatChanged;
        deaths.OnValueChanged -= OnStatChanged;
        assists.OnValueChanged -= OnStatChanged;
        detachedBullseyeEliminations.OnValueChanged -= OnStatChanged;
    }

    public void AddElimination()
    {
        if (!IsServer || !IsSpawned)
            return;

        eliminations.Value = Mathf.Max(0, eliminations.Value + 1);
    }

    public void AddDeath()
    {
        if (!IsServer || !IsSpawned)
            return;

        deaths.Value = Mathf.Max(0, deaths.Value + 1);
    }

    public void AddAssist()
    {
        if (!IsServer || !IsSpawned)
            return;

        assists.Value = Mathf.Max(0, assists.Value + 1);
    }

    public void AddDetachedBullseyeElimination()
    {
        if (!IsServer || !IsSpawned)
            return;

        detachedBullseyeEliminations.Value = Mathf.Max(0, detachedBullseyeEliminations.Value + 1);
    }

    /// <summary>
    /// Resets match statistics. Call this for a new match/session only —
    /// never from death or respawn.
    /// </summary>
    public void ResetMatchStats()
    {
        if (!IsServer || !IsSpawned)
            return;

        eliminations.Value = 0;
        deaths.Value = 0;
        assists.Value = 0;
        detachedBullseyeEliminations.Value = 0;
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
