using System;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu-driven multiplayer session flow. Host/Join create a session first;
/// EnterMatch() is the seam where a future lobby can delay loading gameplay.
/// </summary>
[DefaultExecutionOrder(-200)]
public class GameSessionCoordinator : MonoBehaviour
{
    public const string MainMenuSceneName = "MainMenu";
    public const string DefaultGameplaySceneName = "ArenaPrototype";
    public const ushort DefaultPort = 7777;

    [SerializeField] private string gameplaySceneName = DefaultGameplaySceneName;
    [SerializeField] private bool enterMatchImmediately = true;
    [SerializeField] private float clientConnectTimeout = 12f;

    private PendingSessionRequest pendingRequest;
    private Coroutine connectTimeoutRoutine;
    private Canvas statusCanvas;
    private Text statusLabel;
    private Button statusBackButton;
    private bool localClientConnected;
    private bool shuttingDown;

    public static GameSessionCoordinator Instance { get; private set; }

    public bool IsBusy { get; private set; }
    public bool StartedFromMenu { get; private set; }
    public string StatusMessage { get; private set; }
    public string LastError { get; private set; }
    public GameSessionInfo ActiveSession { get; private set; }
    public string GameplaySceneName => string.IsNullOrEmpty(gameplaySceneName) ? DefaultGameplaySceneName : gameplaySceneName;
    public PendingSessionRequest PendingRequest => pendingRequest;

    public static bool HasMenuDrivenSession =>
        Instance != null && (Instance.StartedFromMenu || Instance.pendingRequest != null || Instance.IsBusy);

    public event Action<string> StatusChanged;
    public event Action<string> ConnectionFailed;
    public event Action ConnectionSucceeded;

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
        public GameSessionInfo Session;
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
        EnsureStatusUi();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            UnbindNetworkCallbacks();
            LocalSessionRegistry.UnregisterCurrentProcess();
            Instance = null;
        }
    }

    private void OnApplicationQuit()
    {
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
        pendingRequest = new PendingSessionRequest
        {
            Kind = PendingSessionKind.Host,
            Visibility = visibility,
            Session = session
        };
        StartedFromMenu = true;
        SetStatus(visibility == GameVisibility.Public ? "Creating Game..." : "Creating Game...");
        BeginBusy();

        if (enterMatchImmediately)
            EnterMatch();
        else
            SetStatus("Session created. Waiting for lobby.");

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
        if (string.IsNullOrEmpty(normalized))
        {
            error = "Enter a join code.";
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
            Session = session
        };
        StartedFromMenu = true;
        SetStatus("Joining Game...");
        BeginBusy();
        EnterMatch();
        return true;
    }

    /// <summary>
    /// Loads the gameplay scene. A future lobby can call this after ready-up
    /// instead of hosting directly into a match.
    /// </summary>
    public void EnterMatch()
    {
        if (pendingRequest == null)
            return;

        SetStatus(pendingRequest.Kind == PendingSessionKind.Host ? "Creating Game..." : "Connecting...");
        SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
    }

    public void ExecutePendingRequest(NetworkManager networkManager)
    {
        if (pendingRequest == null || networkManager == null)
            return;

        UnbindNetworkCallbacks();
        BindNetworkCallbacks(networkManager);
        ConfigureTransport(networkManager, pendingRequest);
        localClientConnected = false;
        shuttingDown = false;

        bool started;
        if (pendingRequest.Kind == PendingSessionKind.Host)
        {
            LocalSessionRegistry.Register(pendingRequest.Session);
            ActiveSession = pendingRequest.Session;
            started = networkManager.StartHost();
            if (!started)
            {
                LocalSessionRegistry.UnregisterCurrentProcess();
                FailAndReturnToMenu("Unable to create game.");
            }

            return;
        }

        started = networkManager.StartClient();
        if (!started)
        {
            FailAndReturnToMenu("Unable to connect to game.");
            return;
        }

        if (connectTimeoutRoutine != null)
            StopCoroutine(connectTimeoutRoutine);
        connectTimeoutRoutine = StartCoroutine(ClientConnectTimeout());
    }

    public void CancelConnection()
    {
        if (!IsBusy && pendingRequest == null)
            return;

        FailAndReturnToMenu(null, silent: true);
    }

    public void ClearLastError()
    {
        LastError = null;
    }

    public void HideStatus()
    {
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
            Address = "127.0.0.1",
            Port = DefaultPort,
            ListenAddress = "0.0.0.0",
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
        if (request.Kind == PendingSessionKind.Host)
        {
            string listen = string.IsNullOrEmpty(session.ListenAddress) ? "0.0.0.0" : session.ListenAddress;
            transport.SetConnectionData(address, port, listen);
            return;
        }

        transport.SetConnectionData(address, port);
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
        pendingRequest = null;
        SetStatus(null);
        HideStatus();
        ConnectionSucceeded?.Invoke();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || clientId != networkManager.LocalClientId)
            return;

        if (localClientConnected)
            return;

        FailAndReturnToMenu("Unable to connect to game.");
    }

    private void HandleTransportFailure()
    {
        if (localClientConnected)
            return;

        FailAndReturnToMenu("Connection failed.");
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
        if (shuttingDown)
            return;

        shuttingDown = true;
        if (connectTimeoutRoutine != null)
        {
            StopCoroutine(connectTimeoutRoutine);
            connectTimeoutRoutine = null;
        }

        UnbindNetworkCallbacks();
        LocalSessionRegistry.UnregisterCurrentProcess();
        ShutdownNetwork();
        pendingRequest = null;
        ActiveSession = null;
        IsBusy = false;
        StartedFromMenu = false;
        localClientConnected = false;

        if (!silent && !string.IsNullOrEmpty(error))
        {
            LastError = error;
            SetStatus(error, showBack: true);
            ConnectionFailed?.Invoke(error);
        }
        else
        {
            SetStatus(null);
            HideStatus();
        }

        if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);

        shuttingDown = false;
    }

    private void ShutdownNetwork()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();
    }

    private void BeginBusy()
    {
        IsBusy = true;
        LastError = null;
        EnsureStatusUi();
        if (statusCanvas != null)
            statusCanvas.gameObject.SetActive(true);
        if (statusBackButton != null)
            statusBackButton.gameObject.SetActive(false);
    }

    private void SetStatus(string message, bool showBack = false)
    {
        StatusMessage = message;
        EnsureStatusUi();
        bool visible = !string.IsNullOrEmpty(message);
        if (statusCanvas != null)
            statusCanvas.gameObject.SetActive(visible);
        if (statusLabel != null)
            statusLabel.text = message ?? string.Empty;
        if (statusBackButton != null)
            statusBackButton.gameObject.SetActive(showBack && visible);
        StatusChanged?.Invoke(message);
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
        statusBackButton = MenuUiFactory.CreateButton(canvasObject.transform, "Back", "Back", new Vector2(0f, -60f), () =>
        {
            HideStatus();
            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
                SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        });
        statusBackButton.gameObject.SetActive(false);
        statusCanvas.gameObject.SetActive(false);
    }
}
