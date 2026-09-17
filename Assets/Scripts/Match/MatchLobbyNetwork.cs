using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Host-authoritative lobby configuration mirrored over NGO for local sessions
/// and as a fallback once a Relay session has started networking.
/// </summary>
public class MatchLobbyNetwork : NetworkBehaviour
{
    public readonly NetworkVariable<FixedString64Bytes> MapId = new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes(MatchIds.DefaultMapId),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<FixedString64Bytes> GameModeId = new NetworkVariable<FixedString64Bytes>(
        new FixedString64Bytes(MatchIds.DefaultGameModeId),
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<int> Visibility = new NetworkVariable<int>(
        (int)MatchVisibility.Public,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<int> State = new NetworkVariable<int>(
        (int)MatchState.Lobby,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkList<LobbyPlayerNetworkState> Players;

    public static MatchLobbyNetwork Instance { get; private set; }

    private void Awake()
    {
        Players = new NetworkList<LobbyPlayerNetworkState>();
    }

    public override void OnNetworkSpawn()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (MatchLobby.Instance != null)
            MatchLobby.Instance.HandleNetworkStateSpawned(this);
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ApplyConfiguration(MatchConfiguration configuration, MatchState matchState)
    {
        if (!IsServer || configuration == null)
            return;

        MapId.Value = configuration.MapId ?? string.Empty;
        GameModeId.Value = configuration.GameModeId ?? string.Empty;
        Visibility.Value = (int)configuration.Visibility;
        State.Value = (int)matchState;
    }

    public void ReplacePlayers(LobbyPlayerInfo[] roster)
    {
        if (!IsServer || Players == null)
            return;

        Players.Clear();
        if (roster == null)
            return;

        for (int i = 0; i < roster.Length; i++)
        {
            LobbyPlayerInfo info = roster[i];
            if (info == null)
                continue;

            Players.Add(new LobbyPlayerNetworkState
            {
                ClientId = info.NetworkClientId,
                DisplayName = Truncate32(info.DisplayName),
                PublicTag = Truncate32(info.PublicTag),
                IsHost = info.IsHost,
                IsReady = info.IsReady
            });
        }
    }

    private static FixedString32Bytes Truncate32(string value)
    {
        if (string.IsNullOrEmpty(value))
            return default;
        return value.Length <= 32 ? value : value.Substring(0, 32);
    }
}
