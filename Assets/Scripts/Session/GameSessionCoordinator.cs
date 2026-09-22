using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu-driven multiplayer session flow. Host/Join create a lobby first;
/// TryStartMatch() loads the selected map through NGO scene management.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameSessionCoordinator : MonoBehaviour
{
    public const string MainMenuSceneName = "MainMenu";
    public const string DefaultGameplaySceneName = "ArenaPrototype";
    public const ushort DefaultPort = 7777;
    private const ushort HostPortMin = 27100;
    private const int HostPortRetryCount = 8;

    [SerializeField] private string gameplaySceneName = DefaultGameplaySceneName;
    [SerializeField] private bool enterMatchImmediately;
    [SerializeField] private float clientConnectTimeout = 12f;
    [SerializeField] private GameObject persistentNetworkManagerPrefab;
    [SerializeField] private GameObject lobbyNetworkPrefab;
    [SerializeField] private MapCatalog mapCatalog;
    [SerializeField] private GameModeCatalog gameModeCatalog;

    private PendingSessionRequest pendingRequest;
    private Coroutine connectTimeoutRoutine;
    private Canvas statusCanvas;
    private Text statusLabel;
    private Button statusBackButton;
    private bool localClientConnected;
    private bool startingHost;
    private bool returningToMenu;
    private Coroutine sessionStartRoutine;
    private Coroutine returnToMenuRoutine;
    private int hostAttemptCount;
    private MultiplayerSessionManager sessionManager;
    private Text statusBackLabel;
    private MatchLobby matchLobby;
    private bool sceneEventsBound;
    private bool connectionApprovalBound;
    private bool showBusyOverlay;

    public static GameSessionCoordinator Instance { get; private set; }

    public bool IsBusy { get; private set; }
    public bool StartedFromMenu { get; private set; }
    public string StatusMessage { get; private set; }
    public string LastError { get; private set; }
    public PendingSessionKind LastErrorKind { get; private set; }
    public MultiplayerConnectionMode LastErrorMode { get; private set; }
    public GameSessionInfo ActiveSession { get; private set; }
    public string GameplaySceneName => string.IsNullOrEmpty(gameplaySceneName) ? DefaultGameplaySceneName : gameplaySceneName;
    public PendingSessionRequest PendingRequest => pendingRequest;
    public bool IsPendingHost => pendingRequest != null && pendingRequest.Kind == PendingSessionKind.Host;
    public string CurrentJoinCode
    {
        get
        {
            if (sessionManager != null && !string.IsNullOrEmpty(sessionManager.JoinCode))
                return sessionManager.JoinCode;
            if (ActiveSession != null && !string.IsNullOrEmpty(ActiveSession.JoinCode))
                return ActiveSession.JoinCode;
            if (pendingRequest != null && pendingRequest.Session != null && !string.IsNullOrEmpty(pendingRequest.Session.JoinCode))
                return pendingRequest.Session.JoinCode;
            return null;
        }
    }

    public static bool HasMenuDrivenSession =>
        Instance != null && (Instance.StartedFromMenu || Instance.pendingRequest != null || Instance.IsBusy);

    public event Action<string> StatusChanged;
    public event Action<string> ConnectionFailed;
    public event Action ConnectionSucceeded;
    public event Action LobbyReady;

    public enum PendingSessionKind
    {
        None,
        Host,
        Join
    }

    public class PendingSessionRequest
    {
        public PendingSessionKind Kind;
        public GameVisibility Visibility;
        public MultiplayerConnectionMode ConnectionMode = MultiplayerConnectionMode.Local;
        public GameSessionInfo Session;
        public MatchConfiguration Configuration;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionManager = MultiplayerSessionManager.Ensure();
        sessionManager.HostDisconnected += HandleRelayHostDisconnected;
        matchLobby = MatchLobby.Ensure();
        matchLobby.Configure(mapCatalog, gameModeCatalog, lobbyNetworkPrefab);
        EnsureStatusUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            if (sessionManager != null)
                sessionManager.HostDisconnected -= HandleRelayHostDisconnected;
            UnbindNetworkCallbacks();
            LocalSessionRegistry.UnregisterCurrentProcess();
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        PlayerProfileMatchBridge.TryFinalizeLocalMatch();
        LocalSessionRegistry.UnregisterCurrentProcess();
        ShutdownNetwork();
    }

    public bool TryHost(GameVisibility visibility, string reservedJoinCode, out string error)
    {
        error = null;
        if (IsBusy)
        {
            error = "Already connecting.";
            return false;
        }

        GameSessionInfo session = CreateHostSession(visibility, reservedJoinCode);
        MatchConfiguration configuration = CurrentMatchConfiguration(visibility);
        session.MapId = configuration.MapId;
        session.GameModeId = configuration.GameModeId;
        session.MaxPlayers = configuration.MaxPlayers;
        pendingRequest = new PendingSessionRequest
        {
            Kind = PendingSessionKind.Host,
            Visibility = visibility,
            Session = session,
            ConnectionMode = MultiplayerConnectionMode.Local,
            Configuration = configuration
        };
        StartedFromMenu = true;
        hostAttemptCount = 0;
        MultiplayerSessionManager.Ensure().SetLocalMode();
        SetStatus("Creating lobby...");
        BeginBusy(showOverlay: false);
        EnterLobby();
        return true;
    }

    public bool TryJoinByCode(string joinCode, out string error)
    {
        error = null;
        if (IsBusy)
        {
            error = "Already connecting.";
            return false;
        }

        if (TryParseDirectAddress(joinCode, out string address, out ushort port))
            return TryJoinSession(CreateDirectSession(address, port), out error);

        string normalized = LocalSessionRegistry.NormalizeCode(joinCode);
        if (normalized.Length < LocalSessionRegistry.MinJoinCodeLength)
        {
            error = "Enter a valid join code.";
            return false;
        }

        GameSessionInfo session = LocalSessionRegistry.FindByJoinCode(normalized);
        if (session == null)
        {
            error = "Game could not be found.";
            return false;
        }

        return TryJoinSession(session, out error);
    }

    public bool TryJoinSession(GameSessionInfo session, out string error)
    {
        error = null;
        if (IsBusy)
        {
            error = "Already connecting.";
            return false;
        }

        if (session == null || string.IsNullOrEmpty(session.Address))
        {
            error = "Game could not be found.";
            return false;
        }

        pendingRequest = new PendingSessionRequest
        {
            Kind = PendingSessionKind.Join,
            Visibility = session.Visibility,
            Session = session,
            ConnectionMode = session.ConnectionMode,
            Configuration = CurrentMatchConfiguration(session.Visibility)
        };
        StartedFromMenu = true;
        if (session.ConnectionMode == MultiplayerConnectionMode.Local)
            MultiplayerSessionManager.Ensure().SetLocalMode();
        SetStatus(pendingRequest.Kind == PendingSessionKind.Host ? "Creating lobby..." : "Joining lobby...");
        BeginBusy(showOverlay: pendingRequest.Kind != PendingSessionKind.Host);
        EnterLobby();
        return true;
    }

    public bool TryHostOnline(GameVisibility visibility, out string error)
    {
        error = null;
        if (IsBusy)
        {
            error = "Already connecting.";
            return false;
        }

        pendingRequest = new PendingSessionRequest
        {
            Kind = PendingSessionKind.Host,
            Visibility = visibility,
            ConnectionMode = MultiplayerConnectionMode.Relay,
            Session = new GameSessionInfo
            {
                Visibility = visibility,
                ConnectionMode = MultiplayerConnectionMode.Relay,
                CreatedUtcTicks = DateTime.UtcNow.Ticks,
                MapId = CurrentMatchConfiguration(visibility).MapId,
                GameModeId = CurrentMatchConfiguration(visibility).GameModeId,
                MaxPlayers = MultiplayerSessionManager.Ensure().MaxPlayers
            },
            Configuration = CurrentMatchConfiguration(visibility)
        };
        StartedFromMenu = true;
        hostAttemptCount = 0;
        SetStatus("Creating lobby...");
        BeginBusy(showOverlay: false);
        EnterLobby();
        return true;
    }

    public bool TryJoinOnline(string joinCode, out string error)
    {
        error = null;
        if (IsBusy)
        {
            error = "Already connecting.";
            return false;
        }

        string normalized = LocalSessionRegistry.NormalizeCode(joinCode);
        if (normalized.Length < LocalSessionRegistry.MinJoinCodeLength)
        {
            error = "Enter a valid join code.";
            return false;
        }

        pendingRequest = new PendingSessionRequest
        {
            Kind = PendingSessionKind.Join,
            Visibility = GameVisibility.Private,
            ConnectionMode = MultiplayerConnectionMode.Relay,
            Session = new GameSessionInfo
            {
                JoinCode = normalized,
                Visibility = GameVisibility.Private,
                ConnectionMode = MultiplayerConnectionMode.Relay,
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            },
            Configuration = MatchConfiguration.CreateDefault(MultiplayerSessionManager.Ensure().MaxPlayers)
        };
        StartedFromMenu = true;
        SetStatus("Joining lobby...");
        BeginBusy(showOverlay: true);
        EnterLobby();
        return true;
    }

    public bool TryJoinOnlineSession(GameSessionInfo session, out string error)
    {
        error = null;
        if (IsBusy)
        {
            error = "Already connecting.";
            return false;
        }

        if (session == null || string.IsNullOrEmpty(session.SessionId))
        {
            error = "Game could not be found.";
            return false;
        }

        pendingRequest = new PendingSessionRequest
        {
            Kind = PendingSessionKind.Join,
            Visibility = GameVisibility.Public,
            ConnectionMode = MultiplayerConnectionMode.Relay,
            Session = session,
            Configuration = MatchConfiguration.CreateDefault(MultiplayerSessionManager.Ensure().MaxPlayers)
        };
        StartedFromMenu = true;
        SetStatus("Joining lobby...");
        BeginBusy(showOverlay: true);
        EnterLobby();
        return true;
    }

    /// <summary>
    /// Creates the multiplayer session on the current menu scene and opens the lobby.
    /// </summary>
    public void EnterLobby()
    {
        if (pendingRequest == null)
            return;

        if (enterMatchImmediately)
        {
            SetStatus(pendingRequest.Kind == PendingSessionKind.Host ? "Creating Game..." : "Connecting...");
            SceneManager.LoadScene(ResolveGameplaySceneName(), LoadSceneMode.Single);
            return;
        }

        matchLobby = MatchLobby.Ensure();
        matchLobby.Configure(mapCatalog, gameModeCatalog, lobbyNetworkPrefab);
        if (pendingRequest.Configuration != null)
            matchLobby.SetDraftConfiguration(pendingRequest.Configuration);

        MenuDisplayPawn.NeutralizeScenePawns();
        SetStatus(pendingRequest.Kind == PendingSessionKind.Host ? "Creating lobby..." : "Joining lobby...");
        NetworkManager networkManager = EnsurePersistentNetworkManager();
        if (networkManager == null)
        {
            FailAndReturnToMenu(pendingRequest.Kind == PendingSessionKind.Host
                ? "Unable to create game."
                : "Unable to connect to game.");
            return;
        }

        ExecutePendingRequest(networkManager);
    }

    public bool TryStartMatch(out string error)
    {
        error = null;
        matchLobby = MatchLobby.Ensure();
        if (matchLobby.TryGetStartError(out error))
            return false;

        MapDefinition map = matchLobby.SelectedMap;
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.SceneManager == null)
        {
            error = "The session is not ready yet.";
            return false;
        }

        matchLobby.SetState(MatchState.Starting);
        BindSceneEvents(networkManager);
        SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(map.SceneName, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started)
        {
            matchLobby.SetState(MatchState.Lobby);
            error = "Unable to start the match.";
            return false;
        }

        HideMenuForGameplay();
        SetStatus("Starting match...");
        return true;
    }

    public void ExecutePendingRequest(NetworkManager networkManager)
    {
        if (pendingRequest == null || networkManager == null)
            return;

        if (sessionStartRoutine != null)
            StopCoroutine(sessionStartRoutine);
        sessionStartRoutine = StartCoroutine(ExecutePendingRequestRoutine(networkManager));
    }

    public void CancelConnection()
    {
        if (!IsBusy && pendingRequest == null)
            return;

        if (sessionManager != null)
            sessionManager.CancelInFlight();
        LeaveToMenu(null);
    }

    public void LeaveToMenu(string message)
    {
        StartReturnToMenu(message, silent: string.IsNullOrEmpty(message), allowWhileConnected: true);
    }

    public void ClearLastError()
    {
        LastError = null;
        LastErrorKind = PendingSessionKind.None;
        LastErrorMode = MultiplayerConnectionMode.Local;
    }

    public void HideStatus()
    {
        showBusyOverlay = false;
        if (statusCanvas != null)
            statusCanvas.gameObject.SetActive(false);
    }

    private GameSessionInfo CreateHostSession(GameVisibility visibility, string reservedJoinCode)
    {
        string code = LocalSessionRegistry.NormalizeCode(reservedJoinCode);
        if (string.IsNullOrEmpty(code))
            code = LocalSessionRegistry.GenerateJoinCode();

        return new GameSessionInfo
        {
            JoinCode = code,
            Visibility = visibility,
            ConnectionMode = MultiplayerConnectionMode.Local,
            Address = "127.0.0.1",
            Port = DefaultPort,
            ListenAddress = "127.0.0.1",
            HostProcessId = 0,
            CreatedUtcTicks = DateTime.UtcNow.Ticks
        };
    }

    private static GameSessionInfo CreateDirectSession(string address, ushort port)
    {
        return new GameSessionInfo
        {
            JoinCode = LocalSessionRegistry.NormalizeCode(address),
            Visibility = GameVisibility.Private,
            Address = address,
            Port = port,
            ListenAddress = address,
            CreatedUtcTicks = DateTime.UtcNow.Ticks
        };
    }

    private static bool TryParseDirectAddress(string value, out string address, out ushort port)
    {
        address = null;
        port = DefaultPort;
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("."))
            return false;

        string trimmed = value.Trim();
        string[] parts = trimmed.Split(':');
        address = parts[0].Trim();
        if (parts.Length > 1 && ushort.TryParse(parts[1], out ushort parsedPort))
            port = parsedPort;

        return !string.IsNullOrEmpty(address);
    }

    public void SetGameplaySceneName(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
            gameplaySceneName = sceneName;
    }

    public void SetMatchAssets(MapCatalog maps, GameModeCatalog modes, GameObject networkManagerPrefab, GameObject lobbyStatePrefab)
    {
        mapCatalog = maps;
        gameModeCatalog = modes;
        persistentNetworkManagerPrefab = networkManagerPrefab;
        lobbyNetworkPrefab = lobbyStatePrefab;
        MatchLobby.Ensure().Configure(maps, modes, lobbyStatePrefab);
    }

    private void ConfigureTransport(NetworkManager networkManager, PendingSessionRequest request)
    {
        if (request == null || request.Session == null)
            return;

        GameSessionInfo session = request.Session;
        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
            return;

        ushort port = session.Port == 0 ? DefaultPort : session.Port;
        string address = string.IsNullOrEmpty(session.Address) ? "127.0.0.1" : session.Address;
        string listen = string.IsNullOrEmpty(session.ListenAddress) ? address : session.ListenAddress;
        transport.SetConnectionData(true, address, port, listen);
    }

    private void BindNetworkCallbacks(NetworkManager networkManager)
    {
        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnect;
        networkManager.OnTransportFailure += HandleTransportFailure;
    }

    private void UnbindNetworkCallbacks()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
        networkManager.OnTransportFailure -= HandleTransportFailure;
    }

    private void HandleClientConnected(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        if (clientId != networkManager.LocalClientId)
            return;

        localClientConnected = true;
        IsBusy = false;
        if (sessionManager != null)
        {
            if ((pendingRequest != null && pendingRequest.ConnectionMode == MultiplayerConnectionMode.Relay) ||
                sessionManager.ConnectionMode == MultiplayerConnectionMode.Relay)
            {
                sessionManager.MarkConnected();
                if (ActiveSession == null)
                {
                    ActiveSession = pendingRequest != null ? pendingRequest.Session : new GameSessionInfo();
                    ActiveSession.JoinCode = sessionManager.JoinCode;
                    ActiveSession.SessionId = sessionManager.SessionId;
                    ActiveSession.ConnectionMode = MultiplayerConnectionMode.Relay;
                    ActiveSession.Visibility = pendingRequest != null ? pendingRequest.Visibility : GameVisibility.Private;
                }

                OnlineDebugOverlay.Ensure();
            }
            else
            {
                sessionManager.MarkLocalModeConnected();
            }
        }

        OpenLobbyAfterConnect(networkManager.IsServer || networkManager.IsHost);
        pendingRequest = null;
        SetStatus(null);
        HideStatus();
        DespawnPlayersIfStillInLobby();
        ConnectionSucceeded?.Invoke();
        LobbyReady?.Invoke();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (startingHost || returningToMenu)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || clientId != networkManager.LocalClientId)
            return;

        if (!localClientConnected)
        {
            string reason = networkManager.DisconnectReason;
            FailAndReturnToMenu(MapDisconnectReason(reason));
            return;
        }

        bool isHost = networkManager.IsHost || networkManager.IsServer;
        LeaveToMenu(isHost ? null : "Host disconnected.");
    }

    private void HandleTransportFailure()
    {
        if (startingHost || returningToMenu)
            return;

        if (localClientConnected)
        {
            LeaveToMenu("Connection lost.");
            return;
        }

        FailAndReturnToMenu("Connection failed.");
    }

    private void HandleRelayHostDisconnected()
    {
        if (returningToMenu || startingHost)
            return;

        LeaveToMenu("Host disconnected.");
    }

    private IEnumerator ClientConnectTimeout()
    {
        float timeout = Mathf.Max(3f, clientConnectTimeout);
        float elapsed = 0f;
        while (elapsed < timeout && !localClientConnected && IsBusy)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        connectTimeoutRoutine = null;
        if (!localClientConnected && IsBusy)
            FailAndReturnToMenu("Unable to connect to game.");
    }

    private void FailAndReturnToMenu(string error, bool silent = false)
    {
        StartReturnToMenu(error, silent, allowWhileConnected: false);
    }

    private void StartReturnToMenu(string error, bool silent, bool allowWhileConnected)
    {
        if (returningToMenu)
            return;

        NetworkManager live = NetworkManager.Singleton;
        if (!allowWhileConnected && localClientConnected && live != null && live.IsListening)
            return;

        if (returnToMenuRoutine != null)
            StopCoroutine(returnToMenuRoutine);
        returnToMenuRoutine = StartCoroutine(ReturnToMenuRoutine(error, silent));
    }

    private IEnumerator ReturnToMenuRoutine(string error, bool silent)
    {
        returningToMenu = true;
        PendingSessionKind errorKind = pendingRequest != null ? pendingRequest.Kind : LastErrorKind;
        MultiplayerConnectionMode errorMode = pendingRequest != null
            ? pendingRequest.ConnectionMode
            : LastErrorMode;
        bool deleteHostSession = sessionManager != null && sessionManager.IsHostSession;

        if (connectTimeoutRoutine != null)
        {
            StopCoroutine(connectTimeoutRoutine);
            connectTimeoutRoutine = null;
        }

        if (sessionStartRoutine != null)
        {
            StopCoroutine(sessionStartRoutine);
            sessionStartRoutine = null;
        }

        UnbindNetworkCallbacks();
        LocalSessionRegistry.UnregisterCurrentProcess();
        if (localClientConnected)
            PlayerProfileMatchBridge.TryFinalizeLocalMatch();

        OnlineMatchHud hud = FindAnyObjectByType<OnlineMatchHud>();
        if (hud != null)
            hud.Hide();

        if (matchLobby != null)
            matchLobby.Close();
        UnbindSceneEvents();
        UnbindConnectionApproval();

        if (sessionManager != null)
        {
            Task leave = sessionManager.LeaveAsync(deleteHostSession);
            float elapsed = 0f;
            while (leave != null && !leave.IsCompleted && elapsed < 3f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        ShutdownNetwork();
        pendingRequest = null;
        ActiveSession = null;
        IsBusy = false;
        StartedFromMenu = false;
        localClientConnected = false;

        if (!silent && !string.IsNullOrEmpty(error))
        {
            LastError = error;
            LastErrorKind = errorKind;
            LastErrorMode = errorMode;
            ConnectionFailed?.Invoke(error);
            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            {
                showBusyOverlay = true;
                SetStatus(error, showBack: true);
                SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            }
            else
            {
                HideStatus();
            }
        }
        else
        {
            SetStatus(null);
            HideStatus();
            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
                SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        }

        returningToMenu = false;
        returnToMenuRoutine = null;
    }

    private IEnumerator ExecutePendingRequestRoutine(NetworkManager networkManager)
    {
        yield return WaitForNetworkIdle(networkManager);

        if (pendingRequest == null)
        {
            sessionStartRoutine = null;
            yield break;
        }

        if (networkManager == null)
            networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            FailAndReturnToMenu(pendingRequest.Kind == PendingSessionKind.Host
                ? "Unable to create game."
                : "Unable to connect to game.");
            sessionStartRoutine = null;
            yield break;
        }

        UnbindNetworkCallbacks();
        BindNetworkCallbacks(networkManager);
        BindConnectionApproval(networkManager);
        BindSceneEvents(networkManager);
        MatchConnectionPayload.ApplyTo(networkManager);
        localClientConnected = false;
        returningToMenu = false;
        MenuDisplayPawn.NeutralizeScenePawns();

        if (pendingRequest.ConnectionMode == MultiplayerConnectionMode.Relay)
        {
            yield return ExecuteRelayRequest(networkManager);
            sessionStartRoutine = null;
            yield break;
        }

        if (pendingRequest.Kind == PendingSessionKind.Host)
        {
            yield return StartHostWithFreePort(networkManager);
            sessionStartRoutine = null;
            yield break;
        }

        ConfigureTransport(networkManager, pendingRequest);
        PlayerProfileManager.Ensure();
        CombatTelemetryManager.Ensure();
        ResearchTelemetryManager.Ensure();
        bool clientStarted = networkManager.StartClient();
        if (!clientStarted)
        {
            FailAndReturnToMenu("Unable to connect to game.");
            sessionStartRoutine = null;
            yield break;
        }

        if (connectTimeoutRoutine != null)
            StopCoroutine(connectTimeoutRoutine);
        connectTimeoutRoutine = StartCoroutine(ClientConnectTimeout());
        sessionStartRoutine = null;
    }

    private IEnumerator ExecuteRelayRequest(NetworkManager networkManager)
    {
        PlayerProfileManager.Ensure();
        CombatTelemetryManager.Ensure();
        ResearchTelemetryManager.Ensure();
        OnlineDebugOverlay.Ensure();
        sessionManager = MultiplayerSessionManager.Ensure();

        bool hosting = pendingRequest != null && pendingRequest.Kind == PendingSessionKind.Host;
        string joinCode = pendingRequest != null && pendingRequest.Session != null
            ? pendingRequest.Session.JoinCode
            : null;

        startingHost = hosting;
        bool isPrivate = pendingRequest == null || pendingRequest.Visibility != GameVisibility.Public;
        string sessionId = pendingRequest != null && pendingRequest.Session != null
            ? pendingRequest.Session.SessionId
            : null;
        Task<OnlineSessionOperationResult> task;
        if (hosting)
            task = sessionManager.HostRelaySessionAsync(isPrivate, pendingRequest != null ? pendingRequest.Configuration : null);
        else if (!string.IsNullOrEmpty(sessionId) && string.IsNullOrEmpty(joinCode))
            task = sessionManager.JoinRelaySessionByIdAsync(sessionId);
        else
            task = sessionManager.JoinRelaySessionAsync(joinCode);

        while (task != null && !task.IsCompleted)
        {
            if (!localClientConnected && !string.IsNullOrEmpty(sessionManager.StatusMessage))
                SetStatus(sessionManager.StatusMessage, showBack: true);
            yield return null;
        }

        startingHost = false;

        if (task != null && task.IsFaulted)
        {
            Exception exception = task.Exception != null ? task.Exception.GetBaseException() : null;
            MultiplayerLog.Error("Relay session task failed.", exception);
            FailAndReturnToMenu(OnlineSessionErrorMapper.ToPlayerMessage(exception));
            yield break;
        }

        OnlineSessionOperationResult result = task != null
            ? task.Result
            : OnlineSessionOperationResult.Fail("Unable to connect to this match.");

        if (result.Cancelled)
            yield break;

        if (!result.Succeeded)
        {
            FailAndReturnToMenu(string.IsNullOrEmpty(result.Error)
                ? "Unable to join lobby.\nCheck the join code and try again."
                : result.Error);
            yield break;
        }

        if (pendingRequest != null && pendingRequest.Session != null)
        {
            pendingRequest.Session.JoinCode = sessionManager.JoinCode;
            pendingRequest.Session.SessionId = sessionManager.SessionId;
            pendingRequest.Session.ConnectionMode = MultiplayerConnectionMode.Relay;
            ActiveSession = pendingRequest.Session;
        }
        else if (ActiveSession == null && sessionManager != null)
        {
            ActiveSession = new GameSessionInfo
            {
                JoinCode = sessionManager.JoinCode,
                SessionId = sessionManager.SessionId,
                ConnectionMode = MultiplayerConnectionMode.Relay,
                Visibility = pendingRequest != null ? pendingRequest.Visibility : GameVisibility.Private,
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            };
        }
        if (hosting)
            MultiplayerLog.Relay("Host started successfully.");
        else
            MultiplayerLog.Relay("Client connected.");
        MultiplayerLog.Info("Relay network connected.");

        if (networkManager != null &&
            (networkManager.IsHost || networkManager.IsClient) &&
            !localClientConnected)
        {
            HandleClientConnected(networkManager.LocalClientId);
        }

        if (localClientConnected)
            HideStatus();

        if (!localClientConnected)
        {
            if (connectTimeoutRoutine != null)
                StopCoroutine(connectTimeoutRoutine);
            connectTimeoutRoutine = StartCoroutine(ClientConnectTimeout());
        }
    }

    private IEnumerator StartHostWithFreePort(NetworkManager networkManager)
    {
        GameSessionInfo session = pendingRequest.Session;
        ushort port = FindAvailableUdpPort(HostPortMin, 256);
        session.Port = port;
        session.Address = "127.0.0.1";
        session.ListenAddress = "127.0.0.1";
        ConfigureTransport(networkManager, pendingRequest);
        Debug.Log($"Bullseye: starting host on 127.0.0.1:{port}");

        startingHost = true;
        PlayerProfileManager.Ensure();
        CombatTelemetryManager.Ensure();
        ResearchTelemetryManager.Ensure();
        networkManager.StartHost();
        bool hostIsUp = localClientConnected ||
                        (networkManager != null && networkManager.IsServer && networkManager.IsListening);
        if (!hostIsUp)
        {
            yield return null;
            yield return null;
            yield return null;
            hostIsUp = localClientConnected ||
                       (networkManager != null &&
                        networkManager.IsServer &&
                        networkManager.IsListening &&
                        !networkManager.ShutdownInProgress);
        }

        startingHost = false;

        if (hostIsUp)
        {
            LocalSessionRegistry.Register(session);
            ActiveSession = session;
            if (!localClientConnected)
                HandleClientConnected(networkManager.LocalClientId);
            yield break;
        }

        hostAttemptCount++;
        if (hostAttemptCount < HostPortRetryCount && pendingRequest != null)
        {
            Debug.LogWarning($"Bullseye: host bind failed on port {port}, retrying with a fresh NetworkManager ({hostAttemptCount}/{HostPortRetryCount}).");
            DestroyNetworkManager();
            yield return null;
            NetworkManager replacement = EnsurePersistentNetworkManager();
            if (replacement != null)
            {
                sessionStartRoutine = StartCoroutine(ExecutePendingRequestRoutine(replacement));
                yield break;
            }
        }

        LocalSessionRegistry.UnregisterCurrentProcess();
        FailAndReturnToMenu("Unable to create game.");
    }

    private static IEnumerator WaitForNetworkIdle(NetworkManager networkManager)
    {
        if (networkManager == null)
            yield break;

        if (networkManager.IsListening)
            networkManager.Shutdown();

        float elapsed = 0f;
        const float timeout = 2f;
        while (networkManager != null &&
               (networkManager.ShutdownInProgress || networkManager.IsListening) &&
               elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ShutdownNetwork()
    {
        DestroyNetworkManager();
    }

    private static void DestroyNetworkManager()
    {
        if (Instance != null)
            Instance.UnbindSceneEvents();
        else
            UnbindSceneEventsFrom(NetworkManager.Singleton);

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            return;

        if (networkManager.IsListening || networkManager.ShutdownInProgress)
            networkManager.Shutdown();

        UnityEngine.Object.Destroy(networkManager.gameObject);
    }

    private MatchConfiguration CurrentMatchConfiguration(GameVisibility visibility)
    {
        MatchLobby lobby = MatchLobby.Ensure();
        lobby.Configure(mapCatalog, gameModeCatalog, lobbyNetworkPrefab);
        MatchConfiguration configuration = lobby.Configuration != null
            ? lobby.Configuration.Clone()
            : MatchConfiguration.CreateDefault(MultiplayerSessionManager.Ensure().MaxPlayers);
        configuration.Visibility = MatchCatalogs.ToMatchVisibility(visibility);
        configuration.MaxPlayers = MultiplayerSessionManager.Ensure().MaxPlayers;
        return configuration;
    }

    private string ResolveGameplaySceneName()
    {
        MapDefinition map = matchLobby != null ? matchLobby.SelectedMap : null;
        if (map != null && map.HasLoadableScene)
            return map.SceneName;
        return GameplaySceneName;
    }

    private void OpenLobbyAfterConnect(bool isHost)
    {
        matchLobby = MatchLobby.Ensure();
        matchLobby.Configure(mapCatalog, gameModeCatalog, lobbyNetworkPrefab);
        MatchConfiguration configuration = pendingRequest != null && pendingRequest.Configuration != null
            ? pendingRequest.Configuration
            : matchLobby.Configuration;

        if (sessionManager != null && sessionManager.ActiveMultiplayerSession != null)
            matchLobby.OpenSession(configuration, sessionManager.IsHostSession);
        else if (isHost)
            matchLobby.OpenLocalHost(configuration);
        else
            matchLobby.OpenLocalClient(configuration);

        if (isHost)
            matchLobby.SpawnNetworkStateIfNeeded();
    }

    private NetworkManager EnsurePersistentNetworkManager()
    {
        NetworkManager existing = NetworkManager.Singleton;
        if (existing != null)
        {
            existing.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
            existing.NetworkConfig.ConnectionApproval = true;
            BindConnectionApproval(existing);
            return existing;
        }

        if (persistentNetworkManagerPrefab == null)
        {
            Debug.LogError("Bullseye: Persistent NetworkManager prefab is missing.");
            return null;
        }

        GameObject instance = Instantiate(persistentNetworkManagerPrefab);
        instance.name = "NetworkManager";
        DontDestroyOnLoad(instance);
        NetworkManager networkManager = instance.GetComponent<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("Bullseye: Persistent NetworkManager prefab has no NetworkManager.");
            return null;
        }

        networkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;
        networkManager.NetworkConfig.ConnectionApproval = true;
        BindConnectionApproval(networkManager);
        return networkManager;
    }

    private void BindConnectionApproval(NetworkManager networkManager)
    {
        if (networkManager == null)
            return;

        networkManager.NetworkConfig.ConnectionApproval = true;
        if (connectionApprovalBound)
            return;

        networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
        connectionApprovalBound = true;
    }

    private void UnbindConnectionApproval()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (connectionApprovalBound && networkManager != null)
            networkManager.ConnectionApprovalCallback = null;
        connectionApprovalBound = false;
    }

    private void HandleConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        MatchLobby lobby = MatchLobby.Ensure();
        if (lobby.State == MatchState.Starting || lobby.State == MatchState.InMatch)
        {
            response.Approved = false;
            response.Reason = "This match has already started.";
            MultiplayerLog.Relay("Rejected late join. Match already started.");
            return;
        }

        NetworkManager networkManager = NetworkManager.Singleton;
        int connected = networkManager != null && networkManager.ConnectedClientsIds != null
            ? networkManager.ConnectedClientsIds.Count
            : 0;
        if (connected >= lobby.MaxPlayers)
        {
            response.Approved = false;
            response.Reason = "This lobby is full.";
            MultiplayerLog.Relay("Rejected join. Lobby is full.");
            return;
        }

        response.Approved = true;
        lobby.RegisterConnectionPayload(request.ClientNetworkId, request.Payload);
        bool inMenu = SceneManager.GetActiveScene().name == MainMenuSceneName;
        response.CreatePlayerObject = !inMenu && lobby.State == MatchState.InMatch;
    }

    private void BindSceneEvents(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.SceneManager == null || sceneEventsBound)
            return;

        networkManager.SceneManager.OnLoad += HandleSceneLoadStarted;
        networkManager.SceneManager.OnLoadComplete += HandleSceneLoadComplete;
        networkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
        sceneEventsBound = true;
    }

    private void UnbindSceneEvents()
    {
        UnbindSceneEventsFrom(NetworkManager.Singleton);
        sceneEventsBound = false;
    }

    private static void UnbindSceneEventsFrom(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.SceneManager == null)
            return;

        networkManager.SceneManager.OnLoad -= HandleSceneLoadStarted;
        networkManager.SceneManager.OnLoadComplete -= HandleSceneLoadComplete;
        networkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
    }

    private static void HandleSceneLoadStarted(ulong clientId, string sceneName, LoadSceneMode loadSceneMode, AsyncOperation asyncOperation)
    {
        GameSessionCoordinator coordinator = Instance;
        if (coordinator == null || sceneName == MainMenuSceneName)
            return;
        coordinator.HideMenuForGameplay();
    }

    private static void HandleSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        GameSessionCoordinator coordinator = Instance;
        NetworkManager networkManager = NetworkManager.Singleton;
        if (coordinator == null || networkManager == null || clientId != networkManager.LocalClientId)
            return;
        if (sceneName == MainMenuSceneName)
            return;

        coordinator.HideMenuForGameplay();
        coordinator.TryUnloadLeftoverMenuScene();
    }

    private static void HandleLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        GameSessionCoordinator coordinator = Instance;
        if (coordinator == null)
            return;
        coordinator.HandleMatchSceneLoaded(sceneName);
    }

    private void HandleMatchSceneLoaded(string sceneName)
    {
        if (sceneName != MainMenuSceneName)
        {
            HideMenuForGameplay();
            TryUnloadLeftoverMenuScene();
        }

        matchLobby = MatchLobby.Ensure();
        MapDefinition map = matchLobby.SelectedMap;
        if (map == null || sceneName != map.SceneName)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsServer)
            SpawnConnectedPlayers(networkManager);

        matchLobby.SetState(MatchState.InMatch);
        SetStatus(null);
        HideStatus();

        if (networkManager != null && networkManager.IsHost)
            OnlineMatchHud.Ensure();
    }

    private void HideMenuForGameplay()
    {
        HideStatus();
        MainMenuController.HideForGameplay();
    }

    private void TryUnloadLeftoverMenuScene()
    {
        Scene menu = SceneManager.GetSceneByName(MainMenuSceneName);
        if (!menu.IsValid() || !menu.isLoaded)
            return;

        string mapName = matchLobby != null && matchLobby.SelectedMap != null
            ? matchLobby.SelectedMap.SceneName
            : GameplaySceneName;
        Scene map = SceneManager.GetSceneByName(mapName);
        if (map.IsValid() && map.isLoaded && SceneManager.GetActiveScene() == menu)
            SceneManager.SetActiveScene(map);

        if (SceneManager.GetActiveScene() == menu)
            return;

        SceneManager.UnloadSceneAsync(menu);
    }

    private static void SpawnConnectedPlayers(NetworkManager networkManager)
    {
        if (networkManager == null || networkManager.SpawnManager == null || networkManager.NetworkConfig.PlayerPrefab == null)
            return;

        NetworkObject prefab = networkManager.NetworkConfig.PlayerPrefab.GetComponent<NetworkObject>();
        if (prefab == null)
            return;

        IReadOnlyList<ulong> clients = networkManager.ConnectedClientsIds;
        for (int i = 0; i < clients.Count; i++)
        {
            ulong clientId = clients[i];
            if (networkManager.SpawnManager.GetPlayerNetworkObject(clientId) != null)
                continue;

            networkManager.SpawnManager.InstantiateAndSpawn(prefab, clientId, true, true);
        }
    }

    private static void DespawnPlayersIfStillInLobby()
    {
        if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            return;

        MenuDisplayPawn.NeutralizeScenePawns();

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || networkManager.SpawnManager == null)
            return;

        IReadOnlyList<ulong> clients = networkManager.ConnectedClientsIds;
        for (int i = 0; i < clients.Count; i++)
        {
            NetworkObject player = networkManager.SpawnManager.GetPlayerNetworkObject(clients[i]);
            if (player == null || !player.IsSpawned)
                continue;
            if (player.GetComponent<MenuDisplayPawn>() == null
                && player.gameObject.scene.name != MainMenuSceneName)
                continue;
            player.Despawn(true);
        }
    }

    private void BeginBusy(bool showOverlay = true)
    {
        IsBusy = true;
        showBusyOverlay = showOverlay;
        LastError = null;
        LastErrorKind = PendingSessionKind.None;
        LastErrorMode = MultiplayerConnectionMode.Local;
        EnsureStatusUi();
        if (statusCanvas != null)
            statusCanvas.gameObject.SetActive(showOverlay);
        if (statusBackButton != null)
            statusBackButton.gameObject.SetActive(showOverlay);
        if (statusBackLabel != null)
            statusBackLabel.text = "Cancel";
    }

    private static string MapDisconnectReason(string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return "Unable to connect.\nPlease check your connection and try again.";
        if (ContainsIgnoreCase(reason, "already started"))
            return "This match has already started.";
        if (ContainsIgnoreCase(reason, "full"))
            return "This lobby is full.";
        if (ContainsIgnoreCase(reason, "no longer"))
            return "This lobby is no longer available.";
        return "Unable to join lobby.\nCheck the join code and try again.";
    }

    private static bool ContainsIgnoreCase(string value, string token)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SetStatus(string message, bool showBack = false)
    {
        StatusMessage = message;
        EnsureStatusUi();
        bool overlayAllowed = showBusyOverlay && (!localClientConnected || returningToMenu);
        bool visible = overlayAllowed && !string.IsNullOrEmpty(message);
        if (statusCanvas != null)
            statusCanvas.gameObject.SetActive(visible);
        if (statusLabel != null)
            statusLabel.text = message ?? string.Empty;
        if (statusBackButton != null)
            statusBackButton.gameObject.SetActive((showBack || IsBusy) && visible);
        if (statusBackLabel != null)
            statusBackLabel.text = IsBusy ? "Cancel" : "Back";
        StatusChanged?.Invoke(message);
    }

    private static ushort FindAvailableUdpPort(ushort preferred, int searchCount)
    {
        int start = Mathf.Clamp(preferred, HostPortMin, 65535);
        int count = Mathf.Max(1, searchCount);
        for (int i = 0; i < count; i++)
        {
            int candidate = start + i;
            if (candidate > 65535)
                break;
            if (IsUdpPortAvailable((ushort)candidate))
                return (ushort)candidate;
        }

        return preferred == 0 ? DefaultPort : preferred;
    }

    private static bool IsUdpPortAvailable(ushort port)
    {
        Socket socket = null;
        try
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
            {
                ExclusiveAddressUse = true
            };
            socket.Bind(new IPEndPoint(IPAddress.Any, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            if (socket != null)
            {
                socket.Close();
                socket.Dispose();
            }
        }
    }

    private void EnsureStatusUi()
    {
        if (statusCanvas != null)
            return;

        GameObject canvasObject = new GameObject("SessionStatusCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        statusCanvas = canvasObject.GetComponent<Canvas>();
        statusCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        statusCanvas.sortingOrder = 200;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image dim = MenuUiFactory.CreateImage(canvasObject.transform, "Dimmer", new Color(0.02f, 0.02f, 0.04f, 0.82f));
        MenuUiFactory.Stretch(dim.rectTransform);

        statusLabel = MenuUiFactory.CreateLabel(canvasObject.transform, "Status", "Connecting...", 36, new Vector2(0f, 40f), new Vector2(900f, 80f));
        statusBackButton = MenuUiFactory.CreateButton(canvasObject.transform, "Back", "Cancel", new Vector2(0f, -60f), () =>
        {
            if (IsBusy)
            {
                CancelConnection();
                return;
            }

            HideStatus();
            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
                SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        });
        statusBackLabel = statusBackButton.GetComponentInChildren<Text>();
        statusBackButton.gameObject.SetActive(false);
        statusCanvas.gameObject.SetActive(false);
    }
}
