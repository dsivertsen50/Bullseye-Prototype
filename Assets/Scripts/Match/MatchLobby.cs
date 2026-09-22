using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Reusable match lobby state. Private join-code, public, and future
/// matchmade sessions should all present this same roster + configuration.
/// </summary>
[DefaultExecutionOrder(-170)]
public class MatchLobby : MonoBehaviour
{
    private readonly List<LobbyPlayerInfo> roster = new List<LobbyPlayerInfo>();
    private readonly Dictionary<ulong, LobbyPlayerInfo> namesByClient = new Dictionary<ulong, LobbyPlayerInfo>();

    private MapCatalog mapCatalog;
    private GameModeCatalog gameModeCatalog;
    private MatchConfiguration configuration = MatchConfiguration.CreateDefault(8);
    private MatchState state;
    private bool eventsBound;
    private bool networkCallbacksBound;
    private MatchLobbyNetwork networkState;
    private GameObject lobbyStatePrefab;

    public static MatchLobby Instance { get; private set; }

    public MatchConfiguration Configuration => configuration;
    public MatchState State => state;
    public IReadOnlyList<LobbyPlayerInfo> Players => roster;
    public MapCatalog Maps => mapCatalog;
    public GameModeCatalog Modes => gameModeCatalog;
    public bool IsHost { get; private set; }
    public bool IsInLobby => state == MatchState.Lobby || state == MatchState.Starting;
    public string StatusMessage { get; private set; }

    public event Action Changed;
    public event Action Closed;

    public static MatchLobby Ensure()
    {
        if (Instance != null)
            return Instance;

        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        if (coordinator != null)
        {
            Instance = coordinator.GetComponent<MatchLobby>();
            if (Instance == null)
                Instance = coordinator.gameObject.AddComponent<MatchLobby>();
            return Instance;
        }

        GameObject host = new GameObject("MatchLobby");
        Instance = host.AddComponent<MatchLobby>();
        DontDestroyOnLoad(host);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        UnbindSessionEvents();
        UnbindNetworkCallbacks();
        if (Instance == this)
            Instance = null;
    }

    public void Configure(MapCatalog maps, GameModeCatalog modes, GameObject lobbyNetworkPrefab)
    {
        if (maps != null)
            mapCatalog = maps;
        if (modes != null)
            gameModeCatalog = modes;
        if (lobbyNetworkPrefab != null)
            lobbyStatePrefab = lobbyNetworkPrefab;
        if (configuration == null)
            configuration = MatchConfiguration.CreateDefault(MultiplayerSessionManager.Ensure().MaxPlayers);
        configuration.MaxPlayers = MultiplayerSessionManager.Ensure().MaxPlayers;
    }

    public void SetDraftConfiguration(MatchConfiguration draft)
    {
        if (draft == null)
            return;
        configuration = draft.Clone();
        configuration.MaxPlayers = MultiplayerSessionManager.Ensure().MaxPlayers;
        Changed?.Invoke();
    }

    public void OpenLocalHost(MatchConfiguration hostConfiguration)
    {
        ApplyOpened(hostConfiguration, isHost: true);
        BindNetworkCallbacks();
        RefreshRoster();
    }

    public void OpenLocalClient(MatchConfiguration hostConfiguration)
    {
        ApplyOpened(hostConfiguration, isHost: false);
        BindNetworkCallbacks();
        RefreshRoster();
    }

    public void OpenSession(MatchConfiguration hostConfiguration, bool isHost)
    {
        ApplyOpened(hostConfiguration, isHost);
        BindSessionEvents();
        BindNetworkCallbacks();
        PullSessionConfiguration();
        RefreshRoster();
    }

    public void Close()
    {
        UnbindSessionEvents();
        UnbindNetworkCallbacks();
        roster.Clear();
        namesByClient.Clear();
        state = MatchState.None;
        IsHost = false;
        StatusMessage = null;
        networkState = null;
        Closed?.Invoke();
        Changed?.Invoke();
    }

    public bool TrySelectMap(string mapId, out string error)
    {
        error = null;
        if (!IsHost)
        {
            error = "Only the host can change the map.";
            return false;
        }

        MapDefinition map = mapCatalog != null ? mapCatalog.GetById(mapId) : null;
        if (map == null)
        {
            error = "Select a map.";
            return false;
        }

        if (!map.CanStartMatch)
        {
            error = map.IsAvailable ? "That map is not available yet." : map.DisplayName + " is coming soon.";
            return false;
        }

        configuration.MapId = map.MapId;
        PersistConfiguration();
        Changed?.Invoke();
        return true;
    }

    public bool TrySelectGameMode(string gameModeId, out string error)
    {
        error = null;
        if (!IsHost)
        {
            error = "Only the host can change the game mode.";
            return false;
        }

        GameModeDefinition mode = gameModeCatalog != null ? gameModeCatalog.GetById(gameModeId) : null;
        if (mode == null)
        {
            error = "Select a game mode.";
            return false;
        }

        if (!mode.IsAvailable)
        {
            error = mode.DisplayName + " is coming soon.";
            return false;
        }

        configuration.GameModeId = mode.GameModeId;
        PersistConfiguration();
        Changed?.Invoke();
        return true;
    }

    public bool TryGetStartError(out string error)
    {
        if (state == MatchState.Starting || state == MatchState.InMatch)
        {
            error = "Match is already starting.";
            return true;
        }

        if (!IsHost)
        {
            error = "Only the host can start the match.";
            return true;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsServer)
        {
            error = "The session is not ready yet.";
            return true;
        }

        return !MatchCatalogs.TryValidate(configuration, mapCatalog, gameModeCatalog, out error, out _, out _);
    }

    public void SetState(MatchState next)
    {
        state = next;
        StatusMessage = next == MatchState.Starting ? "Starting match..." : null;
        PersistConfiguration();
        Changed?.Invoke();
    }

    public void RegisterConnectionPayload(ulong clientId, byte[] payload)
    {
        if (!MatchConnectionPayload.TryDecode(payload, out string displayName, out string publicTag))
        {
            displayName = PublicPlayerTagUtility.FallbackDisplayName(PublicPlayerTagUtility.FromProfileId(clientId.ToString("X")));
            publicTag = PublicPlayerTagUtility.FromProfileId(clientId.ToString("X"));
        }

        namesByClient[clientId] = new LobbyPlayerInfo
        {
            NetworkClientId = clientId,
            DisplayName = displayName,
            PublicTag = publicTag
        };
    }

    public void HandleNetworkStateSpawned(MatchLobbyNetwork spawned)
    {
        networkState = spawned;
        if (spawned == null)
            return;

        spawned.MapId.OnValueChanged += HandleNetworkConfigChanged;
        spawned.GameModeId.OnValueChanged += HandleNetworkConfigChanged;
        spawned.State.OnValueChanged += HandleNetworkStateChanged;
        if (spawned.Players != null)
            spawned.Players.OnListChanged += HandleNetworkPlayersChanged;

        if (IsHost)
            PushNetworkState();
        else
            PullNetworkConfiguration();

        RefreshRoster();
    }

    public void SpawnNetworkStateIfNeeded()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || lobbyStatePrefab == null)
            return;
        if (MatchLobbyNetwork.Instance != null)
        {
            HandleNetworkStateSpawned(MatchLobbyNetwork.Instance);
            return;
        }

        GameObject instance = Instantiate(lobbyStatePrefab);
        instance.name = "MatchLobbyNetwork";
        DontDestroyOnLoad(instance);
        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
            networkObject = instance.AddComponent<NetworkObject>();
        networkObject.Spawn(true);
    }

    public MapDefinition SelectedMap => mapCatalog != null ? mapCatalog.GetById(configuration.MapId) : null;
    public GameModeDefinition SelectedMode => gameModeCatalog != null ? gameModeCatalog.GetById(configuration.GameModeId) : null;

    public int PlayerCount => roster.Count;
    public int MaxPlayers => configuration != null ? configuration.MaxPlayers : MultiplayerSessionManager.Ensure().MaxPlayers;

    private void ApplyOpened(MatchConfiguration hostConfiguration, bool isHost)
    {
        IsHost = isHost;
        state = MatchState.Lobby;
        if (hostConfiguration != null)
            configuration = hostConfiguration.Clone();
        configuration.MaxPlayers = MultiplayerSessionManager.Ensure().MaxPlayers;
        StatusMessage = null;
    }

    private void BindSessionEvents()
    {
        UnbindSessionEvents();
        ISession session = MultiplayerSessionManager.Ensure().ActiveMultiplayerSession;
        if (session == null)
            return;

        eventsBound = true;
        session.Changed += HandleSessionChanged;
        session.PlayerJoined += HandleSessionPlayerJoined;
        session.PlayerHasLeft += HandleSessionPlayerChanged;
        session.SessionPropertiesChanged += HandleSessionChanged;
        session.PlayerPropertiesChanged += HandleSessionChanged;
    }

    private void UnbindSessionEvents()
    {
        ISession session = MultiplayerSessionManager.Instance != null
            ? MultiplayerSessionManager.Instance.ActiveMultiplayerSession
            : null;
        if (!eventsBound || session == null)
        {
            eventsBound = false;
            return;
        }

        session.Changed -= HandleSessionChanged;
        session.PlayerJoined -= HandleSessionPlayerJoined;
        session.PlayerHasLeft -= HandleSessionPlayerChanged;
        session.SessionPropertiesChanged -= HandleSessionChanged;
        session.PlayerPropertiesChanged -= HandleSessionChanged;
        eventsBound = false;
    }

    private void BindNetworkCallbacks()
    {
        UnbindNetworkCallbacks();
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        networkCallbacksBound = true;
        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void UnbindNetworkCallbacks()
    {
        if (!networkCallbacksBound)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= HandleClientConnected;
            networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        }

        networkCallbacksBound = false;
    }

    private void HandleSessionChanged()
    {
        PullSessionConfiguration();
        RefreshRoster();
    }

    private void HandleSessionPlayerChanged(string playerId)
    {
        RefreshRoster();
    }

    private void HandleSessionPlayerJoined(string playerId)
    {
        RefreshRoster();
        LobbyPlayerInfo info = FindPlayerBySessionId(playerId);
        MultiplayerLog.Lobby("Player joined: " + (info != null && !string.IsNullOrEmpty(info.DisplayName)
            ? info.DisplayName
            : playerId));
    }

    private LobbyPlayerInfo FindPlayerBySessionId(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return null;
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i] != null && roster[i].SessionPlayerId == playerId)
                return roster[i];
        }

        return null;
    }

    private void HandleClientConnected(ulong clientId)
    {
        RefreshRoster();
        if (IsHost)
            SpawnNetworkStateIfNeeded();

        if (!UsesSessionRoster())
        {
            namesByClient.TryGetValue(clientId, out LobbyPlayerInfo stored);
            string name = stored != null && !string.IsNullOrEmpty(stored.DisplayName)
                ? stored.DisplayName
                : "Client " + clientId;
            MultiplayerLog.Lobby("Player joined: " + name);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        namesByClient.Remove(clientId);
        RefreshRoster();
    }

    private void HandleNetworkConfigChanged(Unity.Collections.FixedString64Bytes previous, Unity.Collections.FixedString64Bytes next)
    {
        if (IsHost)
            return;
        PullNetworkConfiguration();
        Changed?.Invoke();
    }

    private void HandleNetworkStateChanged(int previous, int next)
    {
        if (IsHost)
            return;
        state = (MatchState)next;
        Changed?.Invoke();
    }

    private void HandleNetworkPlayersChanged(NetworkListEvent<LobbyPlayerNetworkState> changeEvent)
    {
        if (UsesSessionRoster())
            return;
        RefreshRoster();
    }

    private bool UsesSessionRoster()
    {
        MultiplayerSessionManager manager = MultiplayerSessionManager.Instance;
        return manager != null && manager.ActiveMultiplayerSession != null;
    }

    private void PullSessionConfiguration()
    {
        ISession session = MultiplayerSessionManager.Ensure().ActiveMultiplayerSession;
        if (session == null || session.Properties == null)
            return;

        if (TryReadProperty(session, MatchSessionKeys.MapId, out string mapId) && !string.IsNullOrEmpty(mapId))
            configuration.MapId = mapId;
        if (TryReadProperty(session, MatchSessionKeys.GameModeId, out string modeId) && !string.IsNullOrEmpty(modeId))
            configuration.GameModeId = modeId;
        if (TryReadProperty(session, MatchSessionKeys.MatchState, out string matchState) &&
            Enum.TryParse(matchState, out MatchState parsedState))
            state = parsedState;
        if (TryReadProperty(session, MatchSessionKeys.Visibility, out string visibility) &&
            Enum.TryParse(visibility, out MatchVisibility parsedVisibility))
            configuration.Visibility = parsedVisibility;

        configuration.MaxPlayers = session.MaxPlayers > 0
            ? session.MaxPlayers
            : MultiplayerSessionManager.Ensure().MaxPlayers;
        IsHost = session.IsHost;
    }

    private void PullNetworkConfiguration()
    {
        if (networkState == null)
            networkState = MatchLobbyNetwork.Instance;
        if (networkState == null)
            return;

        string mapId = networkState.MapId.Value.ToString();
        string modeId = networkState.GameModeId.Value.ToString();
        if (!string.IsNullOrEmpty(mapId))
            configuration.MapId = mapId;
        if (!string.IsNullOrEmpty(modeId))
            configuration.GameModeId = modeId;
        configuration.Visibility = (MatchVisibility)networkState.Visibility.Value;
        state = (MatchState)networkState.State.Value;
    }

    private void PersistConfiguration()
    {
        if (!IsHost)
            return;

        MultiplayerSessionManager.Ensure().PublishMatchConfiguration(configuration, state);
        PushNetworkState();

        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        if (coordinator != null && coordinator.ActiveSession != null)
        {
            coordinator.ActiveSession.MapId = configuration.MapId;
            coordinator.ActiveSession.GameModeId = configuration.GameModeId;
            if (coordinator.ActiveSession.ConnectionMode == MultiplayerConnectionMode.Local)
                LocalSessionRegistry.Register(coordinator.ActiveSession);
        }
    }

    private void PushNetworkState()
    {
        if (networkState == null)
            networkState = MatchLobbyNetwork.Instance;
        if (networkState == null || !networkState.IsSpawned)
            return;

        networkState.ApplyConfiguration(configuration, state);
        if (!UsesSessionRoster())
            networkState.ReplacePlayers(roster.ToArray());
    }

    private void RefreshRoster()
    {
        roster.Clear();
        if (UsesSessionRoster())
            BuildSessionRoster();
        else
            BuildNetworkRoster();

        if (IsHost)
            PushNetworkState();

        Changed?.Invoke();
    }

    private void BuildSessionRoster()
    {
        ISession session = MultiplayerSessionManager.Ensure().ActiveMultiplayerSession;
        if (session == null || session.Players == null)
            return;

        string localId = session.CurrentPlayer != null ? session.CurrentPlayer.Id : OnlineServicesBootstrap.UnityPlayerId;
        string hostId = session.Host;
        NetworkManager networkManager = NetworkManager.Singleton;

        for (int i = 0; i < session.Players.Count; i++)
        {
            IReadOnlyPlayer player = session.Players[i];
            if (player == null)
                continue;

            string displayName = ReadPlayerProperty(player, MultiplayerSessionManager.DisplayNamePropertyKey);
            string publicTag = ReadPlayerProperty(player, MatchSessionKeys.PublicTag);
            if (string.IsNullOrWhiteSpace(publicTag))
                publicTag = PublicPlayerTagUtility.FromProfileId(player.Id);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = PublicPlayerTagUtility.FallbackDisplayName(publicTag);

            roster.Add(new LobbyPlayerInfo
            {
                SessionPlayerId = player.Id,
                NetworkClientId = networkManager != null ? networkManager.LocalClientId : 0,
                DisplayName = displayName,
                PublicTag = publicTag,
                IsHost = !string.IsNullOrEmpty(hostId) && player.Id == hostId,
                IsLocal = !string.IsNullOrEmpty(localId) && player.Id == localId
            });
        }
    }

    private void BuildNetworkRoster()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            AddLocalFallbackRow();
            return;
        }

        if (networkState != null && networkState.IsSpawned && networkState.Players != null && !networkManager.IsServer)
        {
            for (int i = 0; i < networkState.Players.Count; i++)
            {
                LobbyPlayerNetworkState row = networkState.Players[i];
                roster.Add(new LobbyPlayerInfo
                {
                    SessionPlayerId = "client-" + row.ClientId,
                    NetworkClientId = row.ClientId,
                    DisplayName = row.DisplayName.ToString(),
                    PublicTag = row.PublicTag.ToString(),
                    IsHost = row.IsHost,
                    IsLocal = row.ClientId == networkManager.LocalClientId,
                    IsReady = row.IsReady
                });
            }

            return;
        }

        IReadOnlyList<ulong> clients = networkManager.ConnectedClientsIds;
        if (clients == null)
            return;

        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i];
            namesByClient.TryGetValue(clientId, out LobbyPlayerInfo stored);
            string tag = stored != null ? stored.PublicTag : PublicPlayerTagUtility.FromProfileId(clientId.ToString("X"));
            string displayName = stored != null && !string.IsNullOrEmpty(stored.DisplayName)
                ? stored.DisplayName
                : PublicPlayerTagUtility.FallbackDisplayName(tag);

            roster.Add(new LobbyPlayerInfo
            {
                SessionPlayerId = "client-" + clientId,
                NetworkClientId = clientId,
                DisplayName = displayName,
                PublicTag = tag,
                IsHost = clientId == NetworkManager.ServerClientId,
                IsLocal = clientId == networkManager.LocalClientId
            });
        }

        if (roster.Count == 0)
            AddLocalFallbackRow();
    }

    private void AddLocalFallbackRow()
    {
        roster.Add(new LobbyPlayerInfo
        {
            SessionPlayerId = "local",
            DisplayName = PublicPlayerTagUtility.ResolveLocalDisplayName(),
            PublicTag = PublicPlayerTagUtility.ResolveLocalTag(),
            IsHost = IsHost,
            IsLocal = true
        });
    }

    private static bool TryReadProperty(ISession session, string key, out string value)
    {
        value = null;
        if (session.Properties == null || !session.Properties.TryGetValue(key, out SessionProperty property) || property == null)
            return false;
        value = property.Value;
        return true;
    }

    private static string ReadPlayerProperty(IReadOnlyPlayer player, string key)
    {
        if (player == null || player.Properties == null)
            return null;
        if (!player.Properties.TryGetValue(key, out PlayerProperty property) || property == null)
            return null;
        return property.Value;
    }
}
