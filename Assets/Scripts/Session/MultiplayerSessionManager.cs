using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

/// <summary>
/// Relay/UGS session lifecycle. Local NGO hosting stays on GameSessionCoordinator.
/// </summary>
[DefaultExecutionOrder(-180)]
public class MultiplayerSessionManager : MonoBehaviour
{
    public const string DisplayNamePropertyKey = "DisplayName";
    public const string BuildVersionPropertyKey = "BuildVersion";

    [SerializeField] private int maxPlayers = 8;
    [SerializeField] private float sessionOperationTimeout = 35f;

    private ISession activeSession;
    private int operationSerial;
    private bool eventsBound;

    public static MultiplayerSessionManager Instance { get; private set; }

    public MultiplayerConnectionMode ConnectionMode { get; private set; } = MultiplayerConnectionMode.Local;
    public MultiplayerConnectionState ConnectionState { get; private set; } = MultiplayerConnectionState.Offline;
    public string JoinCode { get; private set; }
    public string SessionId { get; private set; }
    public string LastError { get; private set; }
    public string StatusMessage { get; private set; }
    public bool IsBusy { get; private set; }
    public int MaxPlayers => Mathf.Clamp(maxPlayers, 2, 16);
    public string UnityPlayerId => OnlineServicesBootstrap.UnityPlayerId;
    public bool IsRelaySession => ConnectionMode == MultiplayerConnectionMode.Relay && activeSession != null;
    public bool IsHostSession => activeSession != null && activeSession.IsHost;

    public event Action StateChanged;
    public event Action HostDisconnected;

    public static MultiplayerSessionManager Ensure()
    {
        if (Instance != null)
            return Instance;

        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        if (coordinator != null)
        {
            Instance = coordinator.GetComponent<MultiplayerSessionManager>();
            if (Instance == null)
                Instance = coordinator.gameObject.AddComponent<MultiplayerSessionManager>();
            return Instance;
        }

        GameObject host = new GameObject("MultiplayerSessionManager");
        Instance = host.AddComponent<MultiplayerSessionManager>();
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
        if (Instance == this)
            Instance = null;
        UnbindSessionEvents();
    }

    public void SetLocalMode()
    {
        ConnectionMode = MultiplayerConnectionMode.Local;
        SetState(MultiplayerConnectionState.Offline, null);
    }

    public async Task<OnlineSessionOperationResult> HostRelaySessionAsync(bool isPrivate)
    {
        int serial = BeginOperation();
        ConnectionMode = MultiplayerConnectionMode.Relay;
        try
        {
            SetState(MultiplayerConnectionState.InitializingServices, "Initializing online services...");
            OnlineServicesResult services = await OnlineServicesBootstrap.EnsureReadyAsync();
            if (WasCancelled(serial))
                return OnlineSessionOperationResult.CancelledResult();
            if (!services.Succeeded)
            {
                Fail(services.PlayerMessage);
                return OnlineSessionOperationResult.Fail(services.PlayerMessage);
            }

            SetState(MultiplayerConnectionState.Authenticating, "Signing in...");
            if (!OnlineServicesBootstrap.IsAuthenticated)
            {
                Fail("Unable to sign in to online services.\nCheck your Internet connection and try again.");
                return OnlineSessionOperationResult.Fail(LastError);
            }

            SetState(MultiplayerConnectionState.CreatingSession, "Creating online match...");
            MultiplayerLog.Info("Creating Relay session. Private=" + isPrivate + " Build " + Application.version);

            SessionOptions options = new SessionOptions
            {
                Name = "Bullseye",
                MaxPlayers = MaxPlayers,
                IsPrivate = isPrivate,
                PlayerProperties = BuildPlayerProperties()
            }.WithRelayNetwork();

            IHostSession created = await AwaitWithTimeout(
                MultiplayerService.Instance.CreateSessionAsync(options),
                sessionOperationTimeout,
                "Creating the online match timed out.\nCheck your Internet connection and try again.");
            ISession session = created;

            if (WasCancelled(serial))
            {
                await SafeLeaveAsync(session, deleteIfHost: true);
                return OnlineSessionOperationResult.CancelledResult();
            }

            BindSession(session);
            SetState(MultiplayerConnectionState.WaitingForPlayers, "Waiting for players...");
            MultiplayerLog.Info("Online session created.");
            MultiplayerLog.Info("Join code: " + JoinCode);
            MultiplayerLog.Info("Session ID: " + SessionId);
            return OnlineSessionOperationResult.Ok(session);
        }
        catch (TimeoutException timeout)
        {
            Fail(timeout.Message);
            return OnlineSessionOperationResult.Fail(LastError);
        }
        catch (Exception exception)
        {
            if (WasCancelled(serial))
                return OnlineSessionOperationResult.CancelledResult();
            MultiplayerLog.Error("Creating Relay session failed.", exception);
            Fail(OnlineSessionErrorMapper.ToPlayerMessage(exception));
            return OnlineSessionOperationResult.Fail(LastError);
        }
        finally
        {
            EndOperation(serial);
        }
    }

    public async Task<OnlineSessionOperationResult> JoinRelaySessionAsync(string joinCode)
    {
        string normalized = LocalSessionRegistry.NormalizeCode(joinCode);
        if (string.IsNullOrEmpty(normalized))
        {
            Fail("Enter a join code.");
            return OnlineSessionOperationResult.Fail(LastError);
        }

        int serial = BeginOperation();
        ConnectionMode = MultiplayerConnectionMode.Relay;
        try
        {
            SetState(MultiplayerConnectionState.InitializingServices, "Initializing online services...");
            OnlineServicesResult services = await OnlineServicesBootstrap.EnsureReadyAsync();
            if (WasCancelled(serial))
                return OnlineSessionOperationResult.CancelledResult();
            if (!services.Succeeded)
            {
                Fail(services.PlayerMessage);
                return OnlineSessionOperationResult.Fail(services.PlayerMessage);
            }

            SetState(MultiplayerConnectionState.JoiningSession, "Joining match...");
            MultiplayerLog.Info("Joining session by code.");

            JoinSessionOptions options = new JoinSessionOptions
            {
                PlayerProperties = BuildPlayerProperties()
            };

            ISession session = await AwaitWithTimeout(
                MultiplayerService.Instance.JoinSessionByCodeAsync(normalized, options),
                sessionOperationTimeout,
                "Joining the match timed out.\nCheck the join code and try again.");

            if (WasCancelled(serial))
            {
                await SafeLeaveAsync(session, deleteIfHost: false);
                return OnlineSessionOperationResult.CancelledResult();
            }

            BindSession(session);
            SetState(MultiplayerConnectionState.Connecting, "Connecting...");
            MultiplayerLog.Info("Relay network connected.");
            MultiplayerLog.Info("Join code: " + JoinCode);
            return OnlineSessionOperationResult.Ok(session);
        }
        catch (TimeoutException timeout)
        {
            Fail(timeout.Message);
            return OnlineSessionOperationResult.Fail(LastError);
        }
        catch (Exception exception)
        {
            if (WasCancelled(serial))
                return OnlineSessionOperationResult.CancelledResult();
            MultiplayerLog.Error("Joining Relay session failed.", exception);
            Fail(OnlineSessionErrorMapper.ToPlayerMessage(exception));
            return OnlineSessionOperationResult.Fail(LastError);
        }
        finally
        {
            EndOperation(serial);
        }
    }

    public async Task<OnlineSessionOperationResult> JoinRelaySessionByIdAsync(string sessionId)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            Fail("Game could not be found.");
            return OnlineSessionOperationResult.Fail(LastError);
        }

        int serial = BeginOperation();
        ConnectionMode = MultiplayerConnectionMode.Relay;
        try
        {
            SetState(MultiplayerConnectionState.InitializingServices, "Initializing online services...");
            OnlineServicesResult services = await OnlineServicesBootstrap.EnsureReadyAsync();
            if (WasCancelled(serial))
                return OnlineSessionOperationResult.CancelledResult();
            if (!services.Succeeded)
            {
                Fail(services.PlayerMessage);
                return OnlineSessionOperationResult.Fail(services.PlayerMessage);
            }

            SetState(MultiplayerConnectionState.JoiningSession, "Joining match...");
            MultiplayerLog.Info("Joining session by id.");

            JoinSessionOptions options = new JoinSessionOptions
            {
                PlayerProperties = BuildPlayerProperties()
            };

            ISession session = await AwaitWithTimeout(
                MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, options),
                sessionOperationTimeout,
                "Joining the match timed out.\nTry again in a moment.");

            if (WasCancelled(serial))
            {
                await SafeLeaveAsync(session, deleteIfHost: false);
                return OnlineSessionOperationResult.CancelledResult();
            }

            BindSession(session);
            SetState(MultiplayerConnectionState.Connecting, "Connecting...");
            MultiplayerLog.Info("Relay network connected.");
            MultiplayerLog.Info("Join code: " + JoinCode);
            return OnlineSessionOperationResult.Ok(session);
        }
        catch (TimeoutException timeout)
        {
            Fail(timeout.Message);
            return OnlineSessionOperationResult.Fail(LastError);
        }
        catch (Exception exception)
        {
            if (WasCancelled(serial))
                return OnlineSessionOperationResult.CancelledResult();
            MultiplayerLog.Error("Joining Relay session by id failed.", exception);
            Fail(OnlineSessionErrorMapper.ToPlayerMessage(exception));
            return OnlineSessionOperationResult.Fail(LastError);
        }
        finally
        {
            EndOperation(serial);
        }
    }

    public async Task<List<GameSessionInfo>> QueryPublicSessionsAsync()
    {
        try
        {
            OnlineServicesResult services = await OnlineServicesBootstrap.EnsureReadyAsync();
            if (!services.Succeeded || MultiplayerService.Instance == null)
                return null;

            QuerySessionsOptions options = new QuerySessionsOptions
            {
                Count = 8,
                FilterOptions = new List<FilterOption>
                {
                    new FilterOption(FilterField.Name, "Bullseye", FilterOperation.Equal),
                    new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
                }
            };

            QuerySessionsResults results = await AwaitWithTimeout(
                MultiplayerService.Instance.QuerySessionsAsync(options),
                Mathf.Min(12f, sessionOperationTimeout),
                "Looking for public games timed out.");

            List<GameSessionInfo> sessions = new List<GameSessionInfo>();
            if (results == null || results.Sessions == null)
                return sessions;

            for (int i = 0; i < results.Sessions.Count; i++)
            {
                ISessionInfo info = results.Sessions[i];
                if (info == null || info.IsLocked || info.AvailableSlots <= 0)
                    continue;

                sessions.Add(new GameSessionInfo
                {
                    SessionId = info.Id,
                    Visibility = GameVisibility.Public,
                    ConnectionMode = MultiplayerConnectionMode.Relay,
                    CreatedUtcTicks = info.Created.Ticks
                });
            }

            return sessions;
        }
        catch (Exception exception)
        {
            MultiplayerLog.Error("Public session query failed.", exception);
            return null;
        }
    }

    public void CancelInFlight()
    {
        operationSerial++;
        IsBusy = false;
    }

    public async Task LeaveAsync(bool deleteIfHost)
    {
        operationSerial++;
        IsBusy = false;
        SetState(MultiplayerConnectionState.Disconnecting, "Leaving match...");
        ISession session = activeSession;
        UnbindSessionEvents();
        activeSession = null;
        await SafeLeaveAsync(session, deleteIfHost);
        ClearSessionIdentity();
        ConnectionMode = MultiplayerConnectionMode.Local;
        SetState(MultiplayerConnectionState.Offline, null);
        MultiplayerLog.Info("Session left.");
    }

    public void MarkConnected()
    {
        if (ConnectionMode != MultiplayerConnectionMode.Relay)
        {
            SetState(MultiplayerConnectionState.Connected, "Connected.");
            return;
        }

        SetState(IsHostSession ? MultiplayerConnectionState.WaitingForPlayers : MultiplayerConnectionState.Connected,
            null);
        MultiplayerLog.Info("Client connected.");
    }

    public void MarkLocalModeConnected()
    {
        ConnectionMode = MultiplayerConnectionMode.Local;
        SetState(MultiplayerConnectionState.Connected, null);
    }

    private void BindSession(ISession session)
    {
        UnbindSessionEvents();
        activeSession = session;
        JoinCode = session != null ? session.Code : null;
        SessionId = session != null ? session.Id : null;
        eventsBound = true;
        if (session == null)
            return;

        session.Deleted += HandleSessionDeleted;
        session.RemovedFromSession += HandleRemovedFromSession;
        session.PlayerJoined += HandlePlayerJoined;
        session.PlayerHasLeft += HandlePlayerHasLeft;
    }

    private void UnbindSessionEvents()
    {
        if (!eventsBound || activeSession == null)
        {
            eventsBound = false;
            return;
        }

        activeSession.Deleted -= HandleSessionDeleted;
        activeSession.RemovedFromSession -= HandleRemovedFromSession;
        activeSession.PlayerJoined -= HandlePlayerJoined;
        activeSession.PlayerHasLeft -= HandlePlayerHasLeft;
        eventsBound = false;
    }

    private void HandleSessionDeleted()
    {
        MultiplayerLog.Info("Session deleted.");
        HandleRemoteSessionEnded();
    }

    private void HandleRemovedFromSession()
    {
        MultiplayerLog.Info("Removed from session.");
        HandleRemoteSessionEnded();
    }

    private void HandlePlayerJoined(string playerId)
    {
        MultiplayerLog.Info("Player joined session: " + playerId);
    }

    private void HandlePlayerHasLeft(string playerId)
    {
        MultiplayerLog.Info("Player left session: " + playerId);
    }

    private void HandleRemoteSessionEnded()
    {
        if (activeSession != null && activeSession.IsHost)
            return;

        UnbindSessionEvents();
        activeSession = null;
        ClearSessionIdentity();
        HostDisconnected?.Invoke();
    }

    private void ClearSessionIdentity()
    {
        JoinCode = null;
        SessionId = null;
        LastError = null;
        StatusMessage = null;
    }

    private Dictionary<string, PlayerProperty> BuildPlayerProperties()
    {
        string displayName = PlayerProfileManager.Ensure().DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = PlayerProfileConstants.DefaultDisplayName;

        return new Dictionary<string, PlayerProperty>
        {
            { DisplayNamePropertyKey, new PlayerProperty(displayName, VisibilityPropertyOptions.Member) },
            { BuildVersionPropertyKey, new PlayerProperty(Application.version ?? "0", VisibilityPropertyOptions.Member) }
        };
    }

    private int BeginOperation()
    {
        IsBusy = true;
        LastError = null;
        operationSerial++;
        return operationSerial;
    }

    private void EndOperation(int serial)
    {
        if (serial == operationSerial)
            IsBusy = false;
    }

    private bool WasCancelled(int serial)
    {
        return serial != operationSerial;
    }

    private void Fail(string playerMessage)
    {
        LastError = playerMessage;
        StatusMessage = playerMessage;
        SetState(MultiplayerConnectionState.Error, playerMessage);
    }

    private void SetState(MultiplayerConnectionState state, string status)
    {
        ConnectionState = state;
        StatusMessage = status;
        StateChanged?.Invoke();
    }

    private static async Task<T> AwaitWithTimeout<T>(Task<T> task, float timeoutSeconds, string timeoutMessage)
    {
        Task timeout = Task.Delay(TimeSpan.FromSeconds(Mathf.Max(5f, timeoutSeconds)));
        Task completed = await Task.WhenAny(task, timeout);
        if (completed != task)
            throw new TimeoutException(timeoutMessage);

        return await task;
    }

    private static async Task SafeLeaveAsync(ISession session, bool deleteIfHost)
    {
        if (session == null)
            return;

        try
        {
            if (deleteIfHost && session.IsHost)
            {
                MultiplayerLog.Info("Deleting host session.");
                await session.AsHost().DeleteAsync();
            }
            else
            {
                await session.LeaveAsync();
            }
        }
        catch (Exception exception)
        {
            MultiplayerLog.Error("Leaving session failed.", exception);
        }
    }

    public string BuildDebugText()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        int connected = networkManager != null && networkManager.ConnectedClientsIds != null
            ? networkManager.ConnectedClientsIds.Count
            : 0;

        return
            "ONLINE DEBUG\n" +
            "Mode: " + ConnectionMode + "\n" +
            "State: " + ConnectionState + "\n" +
            "Role: " + (networkManager != null && networkManager.IsHost ? "Host" : networkManager != null && networkManager.IsClient ? "Client" : "—") + "\n" +
            "Players: " + connected + " / " + MaxPlayers + "\n" +
            "Join Code: " + (string.IsNullOrEmpty(JoinCode) ? "—" : JoinCode) + "\n" +
            "Session: " + (string.IsNullOrEmpty(SessionId) ? "—" : SessionId) + "\n" +
            "UGS: " + (OnlineServicesBootstrap.IsInitialized ? "Ready" : "No") + "\n" +
            "Auth: " + (OnlineServicesBootstrap.IsAuthenticated ? "Yes" : "No") + "\n" +
            "UnityPlayerId: " + (string.IsNullOrEmpty(UnityPlayerId) ? "—" : UnityPlayerId) + "\n" +
            "ProfileId: " + PlayerProfileManager.Ensure().PlayerProfileId + "\n" +
            "NGO Host: " + (networkManager != null && networkManager.IsHost) + "\n" +
            "NGO Client: " + (networkManager != null && networkManager.IsClient) + "\n" +
            "Local Client ID: " + (networkManager != null ? networkManager.LocalClientId.ToString() : "—") + "\n" +
            "Build: " + Application.version;
    }
}

public readonly struct OnlineSessionOperationResult
{
    public readonly bool Succeeded;
    public readonly bool Cancelled;
    public readonly string Error;
    public readonly ISession Session;

    private OnlineSessionOperationResult(bool succeeded, bool cancelled, string error, ISession session)
    {
        Succeeded = succeeded;
        Cancelled = cancelled;
        Error = error;
        Session = session;
    }

    public static OnlineSessionOperationResult Ok(ISession session)
    {
        return new OnlineSessionOperationResult(true, false, null, session);
    }

    public static OnlineSessionOperationResult Fail(string error)
    {
        return new OnlineSessionOperationResult(false, false, error, null);
    }

    public static OnlineSessionOperationResult CancelledResult()
    {
        return new OnlineSessionOperationResult(false, true, null, null);
    }
}
