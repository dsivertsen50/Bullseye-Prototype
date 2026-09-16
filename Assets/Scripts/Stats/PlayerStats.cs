using Unity.Collections;
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

    private readonly NetworkVariable<FixedString64Bytes> displayName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public int Eliminations => eliminations.Value;
    public int Deaths => deaths.Value;
    public int Assists => assists.Value;
    public string DisplayName
    {
        get
        {
            string value = displayName.Value.ToString();
            return string.IsNullOrWhiteSpace(value)
                ? CurrentMatchStatistics.FallbackDisplayName(OwnerClientId)
                : value;
        }
    }

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
        displayName.OnValueChanged += OnDisplayNameChanged;
        if (IsOwner)
            ApplyLocalDisplayName();
    }

    public override void OnNetworkDespawn()
    {
        eliminations.OnValueChanged -= OnStatChanged;
        deaths.OnValueChanged -= OnStatChanged;
        assists.OnValueChanged -= OnStatChanged;
        detachedBullseyeEliminations.OnValueChanged -= OnStatChanged;
        displayName.OnValueChanged -= OnDisplayNameChanged;
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

    private void OnDisplayNameChanged(FixedString64Bytes previous, FixedString64Bytes next)
    {
        StatsChanged?.Invoke();
    }

    private void ApplyLocalDisplayName()
    {
        string name = PlayerProfileManager.Ensure().DisplayName;
        if (string.IsNullOrWhiteSpace(name))
            name = PlayerProfileConstants.DefaultDisplayName;
        if (name.Length > 24)
            name = name.Substring(0, 24);
        displayName.Value = name;
    }
}
