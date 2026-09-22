using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Startup main menu. One screen is active at a time. Settings write through
/// PlayerGameSettings so they stay in sync with the in-game pause menu.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    private enum MenuScreen
    {
        Main,
        Play,
        CustomMatch,
        Lobby,
        Join,
        Profile,
        Controls,
        Settings,
        ControlSettings,
        Credits
    }

    [SerializeField] private InputActionAsset playerActions;
    [SerializeField] private CreditsConfig creditsConfig;
    [SerializeField] private MenuAudioController menuAudio;
    [SerializeField] private WeaponCatalog weaponCatalog;
    [SerializeField] private MapCatalog mapCatalog;
    [SerializeField] private GameModeCatalog gameModeCatalog;
    [SerializeField] private string gameplaySceneName = GameSessionCoordinator.DefaultGameplaySceneName;
    [SerializeField] private float leftColumnPadding = 72f;
    [Tooltip("Optional still image shown on the right. If assigned, the 3D menu stage is hidden.")]
    [SerializeField] private Texture backgroundImage;
    [Tooltip("Optional looping video shown on the right. If assigned, it replaces the 3D stage and still image.")]
    [SerializeField] private VideoClip backgroundVideo;

    private const float MenuPanelAlpha = 0.78f;

    private GameObject mainPanel;
    private GameObject playPanel;
    private JoinCodeEntryUI joinEntry;
    private GameObject profilePanel;
    private GameObject controlsPanel;
    private GameObject settingsPanel;
    private GameObject controlSettingsPanel;
    private GameObject creditsPanel;
    private PlayerProfileUI profileUi;
    private MatchMenuUI matchMenu;

    private Selectable playButton;
    private Selectable profileButton;
    private Selectable controlsButton;
    private Selectable settingsButton;
    private Selectable creditsButton;
    private Selectable quitButton;
    private Selectable joinGameButton;
    private Selectable hostGameButton;
    private Selectable playBackButton;
    private Selectable settingsControlButton;
    private Selectable settingsBackButton;
    private Selectable controlSettingsBackButton;
    private Selectable controlsBackButton;
    private Selectable creditsBackButton;

    private Slider masterVolumeSlider;
    private Slider sfxVolumeSlider;
    private Slider musicVolumeSlider;
    private Slider brightnessSlider;
    private Slider mouseXSlider;
    private Slider mouseYSlider;
    private Slider controllerXSlider;
    private Slider controllerYSlider;
    private Slider aimSlider;
    private Toggle invertToggle;

    private bool joinInputOverridesActive;
    private Text creditsBody;
    private readonly List<Button> publicSessionButtons = new List<Button>();
    private int publicQuerySerial;
    private Coroutine publicQueryRoutine;

    private MenuScreen currentScreen = MenuScreen.Main;
    private string reservedJoinCode;
    private bool built;
    private bool suppressUiCallbacks;
    private bool suppressNavigateSound;
    private int lastBackFrame = -1;
    private GameObject lastSelected;
    private InputAction cancelAction;
    private InputAction navigateAction;
    private InputSystemUIInputModule uiInputModule;
    private GameSessionCoordinator sessionCoordinator;
    private Coroutine pendingSelect;
    private float joinListRefreshTimer;
    private List<GameSessionInfo> lastOnlineSessions;
    private string lastJoinListSignature = string.Empty;

    private void Awake()
    {
        if (menuAudio == null)
            menuAudio = GetComponent<MenuAudioController>();
        if (menuAudio == null)
            menuAudio = gameObject.AddComponent<MenuAudioController>();

        EnsureCoordinator();
        ConfigureEventSystem();
        EnsureUi();
        BindUiActions();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (sessionCoordinator != null)
        {
            if (!string.IsNullOrEmpty(sessionCoordinator.LastError))
            {
                string error = sessionCoordinator.LastError;
                bool hostFailed = sessionCoordinator.LastErrorKind == GameSessionCoordinator.PendingSessionKind.Host;
                sessionCoordinator.HideStatus();
                sessionCoordinator.ClearLastError();
                if (hostFailed)
                {
                    ShowScreen(MenuScreen.CustomMatch, false);
                    if (matchMenu != null)
                        matchMenu.SetCustomError(error);
                }
                else
                {
                    ShowScreen(MenuScreen.Join, false);
                    SetJoinError(error);
                }
                return;
            }

            sessionCoordinator.HideStatus();
        }

        ShowScreen(MenuScreen.Main, false);
    }

    private void OnEnable()
    {
        BindUiActions();
        PlayerGameSettings.Changed += HandleSettingsChanged;
        if (sessionCoordinator == null)
            EnsureCoordinator();
        if (sessionCoordinator != null)
        {
            sessionCoordinator.LobbyReady += HandleLobbyReady;
            sessionCoordinator.ConnectionFailed += HandleConnectionFailed;
        }
        MatchLobby.Ensure().Closed += HandleLobbyClosed;
    }

    private void OnDisable()
    {
        SetJoinInputOverrides(false);
        UnbindUiActions();
        PlayerGameSettings.Changed -= HandleSettingsChanged;
        if (sessionCoordinator != null)
        {
            sessionCoordinator.LobbyReady -= HandleLobbyReady;
            sessionCoordinator.ConnectionFailed -= HandleConnectionFailed;
        }
        if (MatchLobby.Instance != null)
            MatchLobby.Instance.Closed -= HandleLobbyClosed;
    }

    private void Update()
    {
        RestoreSelectionIfNeeded();
        TrackSelectionForNavigateSound();
        TickJoinListRefresh();
        if (currentScreen == MenuScreen.Join &&
            joinEntry != null &&
            joinEntry.IsBusy &&
            (sessionCoordinator == null || !sessionCoordinator.IsBusy))
        {
            joinEntry.SetBusy(false);
        }
    }

    private void TickJoinListRefresh()
    {
        if (currentScreen != MenuScreen.Join || publicQueryRoutine != null)
            return;

        joinListRefreshTimer += Time.unscaledDeltaTime;
        if (joinListRefreshTimer < 1.25f)
            return;

        joinListRefreshTimer = 0f;
        ApplyPublicSessionsIfChanged(MergeLocalPublicSessions(lastOnlineSessions));
    }

    /// <summary>
    /// NGO Single scene load can leave MainMenu loaded. Hide the overlay and
    /// leftover menu cameras so gameplay is visible.
    /// </summary>
    public static void HideForGameplay()
    {
        MainMenuController menu = FindAnyObjectByType<MainMenuController>();
        if (menu != null)
            menu.ApplyGameplayHide();

        MenuBackdrop backdrop = FindAnyObjectByType<MenuBackdrop>();
        if (backdrop != null)
            backdrop.gameObject.SetActive(false);

        DisableLeftoverMenuSceneRoots();
    }

    private void ApplyGameplayHide()
    {
        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
                canvases[i].gameObject.SetActive(false);
        }

        if (matchMenu != null)
            matchMenu.Hide();

        enabled = false;
        gameObject.SetActive(false);
    }

    private static void DisableLeftoverMenuSceneRoots()
    {
        Scene menuScene = SceneManager.GetSceneByName(GameSessionCoordinator.MainMenuSceneName);
        if (!menuScene.IsValid() || !menuScene.isLoaded)
            return;

        GameObject[] roots = menuScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
                roots[i].SetActive(false);
        }
    }

    private void EnsureCoordinator()
    {
        sessionCoordinator = GameSessionCoordinator.Instance;
        if (sessionCoordinator == null)
        {
            GameObject host = new GameObject("GameSessionCoordinator");
            sessionCoordinator = host.AddComponent<GameSessionCoordinator>();
        }

        sessionCoordinator.SetGameplaySceneName(gameplaySceneName);
    }

    private void BindUiActions()
    {
        if (cancelAction != null || playerActions == null)
            return;

        InputActionMap uiMap = playerActions.FindActionMap("UI");
        if (uiMap == null)
            return;

        uiMap.Enable();
        cancelAction = uiMap.FindAction("Cancel");
        navigateAction = uiMap.FindAction("Navigate");
        if (cancelAction != null)
            cancelAction.performed += OnCancelPerformed;
    }

    private void UnbindUiActions()
    {
        if (cancelAction != null)
            cancelAction.performed -= OnCancelPerformed;
        cancelAction = null;
        navigateAction = null;
    }

    private void OnCancelPerformed(InputAction.CallbackContext context)
    {
        bool fromKeyboard = context.control != null && context.control.device is Keyboard;
        if (currentScreen == MenuScreen.Join && !fromKeyboard && joinEntry != null && joinEntry.TryBackspace())
            return;
        HandleBack(true);
    }

    private void HandleBack(bool fromCancel)
    {
        if (Time.frameCount == lastBackFrame)
            return;
        lastBackFrame = Time.frameCount;

        if (sessionCoordinator != null && sessionCoordinator.IsBusy)
        {
            if (currentScreen == MenuScreen.Join)
            {
                sessionCoordinator.CancelConnection();
                if (joinEntry != null)
                    joinEntry.SetBusy(false);
                ShowScreen(MenuScreen.Play, fromCancel);
            }
            return;
        }

        switch (currentScreen)
        {
            case MenuScreen.Main:
                return;
            case MenuScreen.Play:
            case MenuScreen.Profile:
                ShowScreen(MenuScreen.Main, fromCancel);
                break;
            case MenuScreen.CustomMatch:
                if (MatchLobby.Ensure().IsInLobby)
                {
                    ShowScreen(MenuScreen.Lobby, fromCancel);
                    break;
                }
                ShowScreen(MenuScreen.Play, fromCancel);
                break;
            case MenuScreen.Lobby:
                break;
            case MenuScreen.Join:
                ShowScreen(MenuScreen.Play, fromCancel);
                break;
            case MenuScreen.ControlSettings:
                ShowScreen(MenuScreen.Settings, fromCancel);
                break;
            default:
                ShowScreen(MenuScreen.Main, fromCancel);
                break;
        }
    }

    private void ShowScreen(MenuScreen screen, bool playSound)
    {
        if (playSound && menuAudio != null)
        {
            if (screen == MenuScreen.Main || screen == MenuScreen.Play && currentScreen != MenuScreen.Main)
                menuAudio.PlayBack();
            else
                menuAudio.PlaySelect();
        }

        currentScreen = screen;
        SetActive(mainPanel, screen == MenuScreen.Main);
        SetActive(playPanel, screen == MenuScreen.Play);
        SetJoinInputOverrides(screen == MenuScreen.Join);
        if (joinEntry != null)
        {
            if (screen == MenuScreen.Join)
                joinEntry.Open(false);
            else
                joinEntry.Hide();
        }
        SetActive(profilePanel, screen == MenuScreen.Profile);
        SetActive(controlsPanel, screen == MenuScreen.Controls);
        SetActive(settingsPanel, screen == MenuScreen.Settings);
        SetActive(controlSettingsPanel, screen == MenuScreen.ControlSettings);
        SetActive(creditsPanel, screen == MenuScreen.Credits);
        if (matchMenu != null)
        {
            if (screen == MenuScreen.CustomMatch)
                matchMenu.OpenCustomMatch();
            else if (screen == MenuScreen.Lobby)
                matchMenu.OpenLobby(MatchLobby.Ensure().IsHost);
            else
                matchMenu.Hide();
        }

        if (screen == MenuScreen.CustomMatch)
            PrepareCustomMatchScreen();
        if (screen == MenuScreen.Join)
            PrepareJoinScreen();
        if (screen == MenuScreen.Profile && profileUi != null)
            profileUi.Open();
        if (screen == MenuScreen.Settings || screen == MenuScreen.ControlSettings)
            RefreshSettingsWidgets();
        if (screen == MenuScreen.Credits)
            RefreshCredits();

        SelectControl(DefaultSelectableForCurrentScreen());
    }

    private static void SetActive(GameObject panel, bool active)
    {
        if (panel != null)
            panel.SetActive(active);
    }

    private static bool LocalTestAvailable =>
#if UNITY_EDITOR
        true;
#else
        Debug.isDebugBuild;
#endif

    private void PrepareCustomMatchScreen()
    {
        EnsureLocalJoinCode();
        if (matchMenu != null)
        {
            matchMenu.SetCustomError(null);
            matchMenu.SetLocalTestHint(LocalTestAvailable ? reservedJoinCode : null);
        }
        MatchLobby.Ensure().Configure(mapCatalog, gameModeCatalog, null);
    }

    private void PrepareJoinScreen()
    {
        if (joinEntry != null)
            joinEntry.Prepare(LocalTestAvailable);
        RebuildPublicSessionList();
    }

    private void SetJoinError(string message)
    {
        if (joinEntry != null)
        {
            joinEntry.SetBusy(false);
            joinEntry.SetError(message);
        }
    }

    private void EnsureLocalJoinCode()
    {
        if (!string.IsNullOrEmpty(reservedJoinCode))
            return;
        reservedJoinCode = LocalSessionRegistry.GenerateJoinCode();
    }

    private void RebuildPublicSessionList()
    {
        lastJoinListSignature = string.Empty;
        ClearPublicSessionButtons();
        if (joinEntry != null)
            joinEntry.SetPublicListMessage(LocalTestAvailable
                ? "Looking for local test and public games..."
                : "Looking for public games...");
        WireJoinNavigation();

        if (publicQueryRoutine != null)
            StopCoroutine(publicQueryRoutine);
        publicQueryRoutine = StartCoroutine(QueryPublicSessionsRoutine());
    }

    private IEnumerator QueryPublicSessionsRoutine()
    {
        int serial = ++publicQuerySerial;
        Task<List<GameSessionInfo>> task = MultiplayerSessionManager.Ensure().QueryPublicSessionsAsync();
        while (task != null && !task.IsCompleted)
            yield return null;

        publicQueryRoutine = null;
        if (serial != publicQuerySerial || currentScreen != MenuScreen.Join)
            yield break;

        if (task == null || task.IsFaulted || task.Result == null)
        {
            lastOnlineSessions = null;
            List<GameSessionInfo> localOnly = MergeLocalPublicSessions(null);
            if (localOnly.Count > 0)
            {
                ApplyPublicSessionsIfChanged(localOnly);
                yield break;
            }

            if (joinEntry != null)
                joinEntry.SetPublicListMessage(LocalTestAvailable
                    ? "No local test games yet.\nHost Local Test on Player 1, or enter a join code."
                    : "Unable to list public games.\nYou can still join with a join code.");
            WireJoinNavigation();
            yield break;
        }

        lastOnlineSessions = task.Result;
        ApplyPublicSessionsIfChanged(MergeLocalPublicSessions(task.Result));
    }

    private void ApplyPublicSessionsIfChanged(List<GameSessionInfo> sessions)
    {
        string signature = BuildJoinListSignature(sessions);
        if (signature == lastJoinListSignature && publicSessionButtons.Count > 0)
            return;
        lastJoinListSignature = signature;
        ApplyPublicSessions(sessions);
    }

    private static string BuildJoinListSignature(List<GameSessionInfo> sessions)
    {
        if (sessions == null || sessions.Count == 0)
            return "empty";

        var parts = new System.Text.StringBuilder();
        int count = Mathf.Min(sessions.Count, 3);
        for (int i = 0; i < count; i++)
        {
            GameSessionInfo session = sessions[i];
            if (session == null)
                continue;
            parts.Append(session.JoinCode).Append('|')
                .Append(session.SessionId).Append('|')
                .Append(session.ConnectionMode).Append(';');
        }

        return parts.ToString();
    }

    private void ApplyPublicSessions(List<GameSessionInfo> sessions)
    {
        ClearPublicSessionButtons();
        if (sessions == null || sessions.Count == 0)
        {
            if (joinEntry != null)
                joinEntry.SetPublicListMessage(LocalTestAvailable
                    ? "No local test games found.\nOn Player 1: Host Custom Match → Local Test → Create Lobby."
                    : "No public games found.\nAsk a host for a join code, or host a public game.");
            WireJoinNavigation();
            return;
        }

        if (joinEntry != null)
            joinEntry.SetPublicListMessage(string.Empty);
        Transform parent = joinEntry != null ? joinEntry.PublicListRoot : null;
        if (parent == null)
            return;

        float y = -330f;
        int count = Mathf.Min(sessions.Count, 3);
        for (int i = 0; i < count; i++)
        {
            GameSessionInfo session = sessions[i];
            string label = session.ConnectionMode == MultiplayerConnectionMode.Local && !string.IsNullOrEmpty(session.JoinCode)
                ? (session.IsPublic ? "Join Local " : "Join Local Test ") + session.JoinCode
                : "Join Game " + (i + 1);
            Button button = MenuUiFactory.CreateButton(parent, "PublicSession" + i, label, new Vector2(0f, y), () => JoinListedSession(session), new Vector2(420f, 44f));
            publicSessionButtons.Add(button);
            y -= 48f;
        }

        WireJoinNavigation();
    }

    private void ClearPublicSessionButtons()
    {
        for (int i = 0; i < publicSessionButtons.Count; i++)
        {
            if (publicSessionButtons[i] != null)
                Destroy(publicSessionButtons[i].gameObject);
        }

        publicSessionButtons.Clear();
    }

    private static List<GameSessionInfo> MergeLocalPublicSessions(List<GameSessionInfo> online)
    {
        List<GameSessionInfo> merged = new List<GameSessionInfo>();
        List<GameSessionInfo> local = LocalSessionRegistry.ListLocalSessions();
        int selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        if (local != null)
        {
            for (int i = 0; i < local.Count; i++)
            {
                GameSessionInfo session = local[i];
                if (session == null || session.HostProcessId == selfPid)
                    continue;
                merged.Add(session);
            }
        }
        if (online != null)
            merged.AddRange(online);
        return merged;
    }

    private void JoinListedSession(GameSessionInfo session)
    {
        if (!CanStartConnection())
            return;

        PlaySelect();
        if (session.ConnectionMode == MultiplayerConnectionMode.Relay)
        {
            if (sessionCoordinator.TryJoinOnlineSession(session, out string onlineError))
                return;
            SetJoinError(onlineError);
            return;
        }

        if (sessionCoordinator.TryJoinSession(session, out string error))
            return;

        SetJoinError(error);
    }

    private void HostFromMenu()
    {
        if (!CanStartConnection())
            return;

        PlaySelect();
        MatchLobby lobby = MatchLobby.Ensure();
        lobby.Configure(mapCatalog, gameModeCatalog, null);
        if (!MatchCatalogs.TryValidate(lobby.Configuration, mapCatalog, gameModeCatalog, out string validationError, out _, out _))
        {
            if (matchMenu != null)
                matchMenu.SetCustomError(validationError);
            return;
        }

        GameVisibility visibility = MatchCatalogs.ToGameVisibility(lobby.Configuration.Visibility);
        if (MainMenuHostOptions.LocalTest && LocalTestAvailable)
        {
            EnsureLocalJoinCode();
            if (sessionCoordinator.TryHost(GameVisibility.Private, reservedJoinCode, out string localError))
            {
                OpenHostLobby();
                return;
            }
            if (matchMenu != null)
                matchMenu.SetCustomError(localError);
            return;
        }

        if (sessionCoordinator.TryHostOnline(visibility, out string error))
        {
            OpenHostLobby();
            return;
        }

        if (matchMenu != null)
            matchMenu.SetCustomError(error);
    }

    private void HandleLobbyReady()
    {
        ShowScreen(MenuScreen.Lobby, false);
        if (matchMenu != null)
            matchMenu.SetLobbyError(null);
    }

    private void HandleConnectionFailed(string error)
    {
        if (sessionCoordinator != null)
            sessionCoordinator.HideStatus();

        if (sessionCoordinator != null && sessionCoordinator.LastErrorKind == GameSessionCoordinator.PendingSessionKind.Host)
        {
            ShowScreen(MenuScreen.CustomMatch, false);
            if (matchMenu != null)
                matchMenu.SetCustomError(error);
            if (sessionCoordinator != null)
                sessionCoordinator.ClearLastError();
            return;
        }

        ShowScreen(MenuScreen.Join, false);
        SetJoinError(error);
        if (sessionCoordinator != null)
            sessionCoordinator.ClearLastError();
    }

    private void OpenHostLobby()
    {
        ShowScreen(MenuScreen.Lobby, false);
        if (matchMenu != null)
            matchMenu.OpenLobby(true);
    }

    private void HandleLobbyClosed()
    {
        if (currentScreen == MenuScreen.Lobby)
            ShowScreen(MenuScreen.Play, false);
    }

    private void StartMatchFromLobby()
    {
        if (sessionCoordinator == null)
            EnsureCoordinator();
        if (sessionCoordinator == null)
            return;

        PlaySelect();
        if (sessionCoordinator.TryStartMatch(out string error))
            return;

        if (matchMenu != null)
            matchMenu.SetLobbyError(error);
    }

    private void LeaveLobbyFromMenu()
    {
        PlaySelect();
        if (sessionCoordinator != null)
            sessionCoordinator.LeaveToMenu(null);
        ShowScreen(MenuScreen.Play, false);
    }

    private void CancelLobbyFromMenu()
    {
        PlaySelect();
        if (sessionCoordinator != null)
            sessionCoordinator.LeaveToMenu(null);
        ShowScreen(MenuScreen.Main, false);
    }

    private void CancelJoinAndBack()
    {
        if (sessionCoordinator != null && sessionCoordinator.IsBusy)
            sessionCoordinator.CancelConnection();
        if (joinEntry != null)
            joinEntry.SetBusy(false);
        ShowScreen(MenuScreen.Play, true);
    }

    private void OpenJoin()
    {
        if (joinEntry != null)
            joinEntry.ClearCode();
        ShowScreen(MenuScreen.Join, true);
    }

    private void JoinFromMenu()
    {
        if (!CanStartConnection())
            return;

        PlaySelect();
        if (joinEntry != null)
            joinEntry.SetBusy(true);
        string code = joinEntry != null ? joinEntry.CurrentCode : string.Empty;
        string normalized = LocalSessionRegistry.NormalizeCode(code);
        if (!string.IsNullOrEmpty(normalized) && LocalSessionRegistry.FindByJoinCode(normalized) != null)
        {
            if (sessionCoordinator.TryJoinByCode(code, out string localError))
                return;
            SetJoinError(localError);
            return;
        }

        if (sessionCoordinator.TryJoinOnline(code, out string error))
            return;

        SetJoinError(error);
    }

    private bool CanStartConnection()
    {
        if (sessionCoordinator == null)
            EnsureCoordinator();
        return sessionCoordinator != null && !sessionCoordinator.IsBusy;
    }

    private void QuitGame()
    {
        PlaySelect();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void PlaySelect()
    {
        if (menuAudio != null)
            menuAudio.PlaySelect();
    }

    private void HandleSettingsChanged()
    {
        if (currentScreen == MenuScreen.Settings || currentScreen == MenuScreen.ControlSettings)
            RefreshSettingsWidgets();
    }

    private void RefreshSettingsWidgets()
    {
        suppressUiCallbacks = true;
        SetSlider(masterVolumeSlider, PlayerGameSettings.MasterVolume);
        SetSlider(sfxVolumeSlider, PlayerGameSettings.SfxVolume);
        SetSlider(musicVolumeSlider, PlayerGameSettings.MusicVolume);
        SetSlider(brightnessSlider, PlayerGameSettings.Brightness);
        SetSlider(mouseXSlider, PlayerGameSettings.MouseSensitivityX);
        SetSlider(mouseYSlider, PlayerGameSettings.MouseSensitivityY);
        SetSlider(controllerXSlider, PlayerGameSettings.ControllerSensitivityX);
        SetSlider(controllerYSlider, PlayerGameSettings.ControllerSensitivityY);
        SetSlider(aimSlider, PlayerGameSettings.AimSensitivityMultiplier);
        if (invertToggle != null)
            invertToggle.isOn = PlayerGameSettings.InvertY;
        suppressUiCallbacks = false;
    }

    private static void SetSlider(Slider slider, float value)
    {
        if (slider != null)
            slider.value = value;
    }

    private void RefreshCredits()
    {
        if (creditsBody == null)
            return;

        CreditsConfig config = creditsConfig;
        string title = config != null && !string.IsNullOrEmpty(config.gameTitle) ? config.gameTitle : "BULLSEYE";
        var lines = new List<string> { title, string.Empty };

        if (config == null)
        {
            lines.Add("Game Directors");
            lines.Add("[Director Name]");
            lines.Add("[Director Name]");
        }
        else
        {
            foreach (CreditsConfig.CreditCategory category in config.GetCategories())
            {
                lines.Add(category.heading);
                if (category.names == null || category.names.Length == 0)
                    lines.Add("[Director Name]");
                else
                {
                    for (int i = 0; i < category.names.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(category.names[i]))
                            lines.Add(category.names[i]);
                    }
                }

                lines.Add(string.Empty);
            }
        }

        creditsBody.text = string.Join("\n", lines);
    }

    private void SelectControl(Selectable selectable)
    {
        if (pendingSelect != null)
            StopCoroutine(pendingSelect);
        pendingSelect = StartCoroutine(SelectControlNextFrame(selectable));
    }

    private IEnumerator SelectControlNextFrame(Selectable selectable)
    {
        yield return null;

        Selectable target = UsableSelectable(selectable) ?? UsableSelectable(DefaultSelectableForCurrentScreen());
        suppressNavigateSound = true;
        lastSelected = target != null ? target.gameObject : null;
        if (target != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        pendingSelect = null;
    }

    private static Selectable UsableSelectable(Selectable selectable)
    {
        if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.IsInteractable())
            return null;
        return selectable;
    }

    private void RestoreSelectionIfNeeded()
    {
        if (EventSystem.current == null || pendingSelect != null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
            return;

        SelectControl(DefaultSelectableForCurrentScreen());
    }

    private Selectable DefaultSelectableForCurrentScreen()
    {
        switch (currentScreen)
        {
            case MenuScreen.Play: return joinGameButton;
            case MenuScreen.CustomMatch: return matchMenu != null ? matchMenu.CustomDefaultSelectable : playButton;
            case MenuScreen.Lobby: return matchMenu != null ? matchMenu.LobbyDefaultSelectable : playButton;
            case MenuScreen.Join:
                return joinEntry != null ? joinEntry.DefaultSelectable : joinGameButton;
            case MenuScreen.Profile: return profileUi != null ? profileUi.DefaultSelectable : playButton;
            case MenuScreen.Controls: return controlsBackButton;
            case MenuScreen.Settings: return masterVolumeSlider;
            case MenuScreen.ControlSettings: return aimSlider;
            case MenuScreen.Credits: return creditsBackButton;
            default: return playButton;
        }
    }

    private void TrackSelectionForNavigateSound()
    {
        if (EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == lastSelected)
            return;

        GameObject previous = lastSelected;
        lastSelected = selected;
        if (suppressNavigateSound)
        {
            suppressNavigateSound = false;
            return;
        }

        if (previous == null || selected == null || !selected.activeInHierarchy)
            return;

        if (menuAudio != null)
            menuAudio.PlayNavigate();
    }

    private void PlaySliderSound()
    {
        if (menuAudio != null)
            menuAudio.PlaySliderAdjust();
    }

    private void EnsureUi()
    {
        if (built)
            return;

        GameObject canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas menuCanvas = canvasObject.GetComponent<Canvas>();
        menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        menuCanvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        SetupVisualBackdrop(canvasObject.transform);

        BuildMainPanel(canvasObject.transform);
        BuildPlayPanel(canvasObject.transform);
        BuildMatchMenu(canvasObject.transform);
        BuildJoinPanel(canvasObject.transform);
        BuildProfilePanel(canvasObject.transform);
        BuildControlsPanel(canvasObject.transform);
        BuildSettingsPanel(canvasObject.transform);
        BuildControlSettingsPanel(canvasObject.transform);
        BuildCreditsPanel(canvasObject.transform);
        WireStaticNavigation();
        built = true;
    }

    private void SetupVisualBackdrop(Transform canvasParent)
    {
        bool usingVideo = backgroundVideo != null;
        bool usingImage = !usingVideo && backgroundImage != null;
        MenuBackdrop sceneBackdrop = FindAnyObjectByType<MenuBackdrop>();

        if (usingVideo || usingImage)
        {
            if (sceneBackdrop != null)
                sceneBackdrop.gameObject.SetActive(false);

            GameObject mediaObject = new GameObject("BackdropMedia", typeof(RectTransform), typeof(RawImage));
            mediaObject.transform.SetParent(canvasParent, false);
            RectTransform rect = mediaObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.34f, 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            RawImage rawImage = mediaObject.GetComponent<RawImage>();
            rawImage.raycastTarget = false;

            if (usingVideo)
            {
                RenderTexture target = new RenderTexture(1920, 1080, 0)
                {
                    name = "MenuBackgroundVideo",
                    hideFlags = HideFlags.HideAndDontSave
                };
                VideoPlayer player = mediaObject.AddComponent<VideoPlayer>();
                player.clip = backgroundVideo;
                player.isLooping = true;
                player.playOnAwake = true;
                player.renderMode = VideoRenderMode.RenderTexture;
                player.targetTexture = target;
                player.audioOutputMode = VideoAudioOutputMode.None;
                rawImage.texture = target;
                player.Play();
            }
            else
            {
                rawImage.texture = backgroundImage;
            }
        }
        else if (sceneBackdrop != null)
        {
            sceneBackdrop.gameObject.SetActive(true);
        }

        MenuUiFactory.CreateLeftScrim(canvasParent, 0.5f);
    }

    private GameObject CreateMenuPanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = MenuUiFactory.CreatePanel(parent, name, size, new Color(0.07f, 0.08f, 0.11f, MenuPanelAlpha));
        MenuUiFactory.DockLeft(panel, leftColumnPadding);
        return panel;
    }

    private void BuildMainPanel(Transform parent)
    {
        mainPanel = CreateMenuPanel(parent, "MainPanel", new Vector2(520f, 700f));
        MenuUiFactory.CreateLabel(mainPanel.transform, "Title", "BULLSEYE", 64, new Vector2(0f, 250f), new Vector2(560f, 80f));
        playButton = MenuUiFactory.CreateButton(mainPanel.transform, "Play", "Play", new Vector2(0f, 150f), () => ShowScreen(MenuScreen.Play, true));
        profileButton = MenuUiFactory.CreateButton(mainPanel.transform, "Profile", "Profile", new Vector2(0f, 80f), () => ShowScreen(MenuScreen.Profile, true));
        controlsButton = MenuUiFactory.CreateButton(mainPanel.transform, "Controls", "Controls", new Vector2(0f, 10f), () => ShowScreen(MenuScreen.Controls, true));
        settingsButton = MenuUiFactory.CreateButton(mainPanel.transform, "Settings", "Settings", new Vector2(0f, -60f), () => ShowScreen(MenuScreen.Settings, true));
        creditsButton = MenuUiFactory.CreateButton(mainPanel.transform, "Credits", "Credits", new Vector2(0f, -130f), () => ShowScreen(MenuScreen.Credits, true));
        quitButton = MenuUiFactory.CreateButton(mainPanel.transform, "Quit", "Quit", new Vector2(0f, -200f), QuitGame);
    }

    private void BuildProfilePanel(Transform parent)
    {
        profileUi = PlayerProfileUI.Create(
            parent,
            leftColumnPadding,
            weaponCatalog,
            menuAudio,
            () => ShowScreen(MenuScreen.Main, true));
        profilePanel = profileUi != null ? profileUi.gameObject : null;
    }

    private void BuildPlayPanel(Transform parent)
    {
        playPanel = CreateMenuPanel(parent, "PlayPanel", new Vector2(520f, 620f));
        playPanel.SetActive(false);
        MenuUiFactory.CreateLabel(playPanel.transform, "Title", "PLAY", 48, new Vector2(0f, 200f), new Vector2(520f, 64f));
        joinGameButton = MenuUiFactory.CreateButton(playPanel.transform, "JoinGame", "Join Game", new Vector2(0f, 90f), OpenJoin);
        hostGameButton = MenuUiFactory.CreateButton(playPanel.transform, "HostGame", "Custom Match", new Vector2(0f, 20f), () => ShowScreen(MenuScreen.CustomMatch, true));
        playBackButton = MenuUiFactory.CreateButton(playPanel.transform, "Back", "Back", new Vector2(0f, -90f), () => ShowScreen(MenuScreen.Main, true));
    }

    private void BuildMatchMenu(Transform parent)
    {
        matchMenu = MatchMenuUI.Create(
            parent,
            leftColumnPadding,
            mapCatalog,
            gameModeCatalog,
            menuAudio,
            HostFromMenu,
            () => ShowScreen(MenuScreen.Play, true),
            StartMatchFromLobby,
            LeaveLobbyFromMenu,
            CancelLobbyFromMenu,
            () => ShowScreen(MenuScreen.CustomMatch, true),
            () => ShowScreen(MenuScreen.Lobby, true));
    }

    private void BuildJoinPanel(Transform parent)
    {
        joinEntry = JoinCodeEntryUI.Create(
            parent,
            leftColumnPadding,
            menuAudio,
            JoinFromMenu,
            CancelJoinAndBack);
    }

    private void BuildControlsPanel(Transform parent)
    {
        controlsPanel = CreateMenuPanel(parent, "ControlsPanel", new Vector2(980f, 820f));
        controlsPanel.SetActive(false);
        MenuUiFactory.CreateLabel(controlsPanel.transform, "Title", "CONTROLS", 44, new Vector2(0f, 360f), new Vector2(1000f, 56f));
        MenuUiFactory.CreateLabel(controlsPanel.transform, "KeyboardTitle", "Keyboard & Mouse", 26, new Vector2(-220f, 300f), new Vector2(420f, 36f));
        MenuUiFactory.CreateLabel(controlsPanel.transform, "GamepadTitle", "Gamepad", 26, new Vector2(220f, 300f), new Vector2(420f, 36f));

        ControlsGuideFormatter.Build(playerActions, out string keyboard, out string gamepad);
        Text keyboardBody = MenuUiFactory.CreateLabel(controlsPanel.transform, "KeyboardBody", keyboard, 18, new Vector2(-220f, 20f), new Vector2(440f, 520f), TextAnchor.UpperLeft);
        Text gamepadBody = MenuUiFactory.CreateLabel(controlsPanel.transform, "GamepadBody", gamepad, 18, new Vector2(220f, 20f), new Vector2(440f, 520f), TextAnchor.UpperLeft);
        keyboardBody.lineSpacing = 1.1f;
        gamepadBody.lineSpacing = 1.1f;
        controlsBackButton = MenuUiFactory.CreateButton(controlsPanel.transform, "Back", "Back", new Vector2(0f, -350f), () => ShowScreen(MenuScreen.Main, true));
    }

    private void BuildSettingsPanel(Transform parent)
    {
        settingsPanel = CreateMenuPanel(parent, "SettingsPanel", new Vector2(560f, 620f));
        settingsPanel.SetActive(false);
        MenuUiFactory.CreateLabel(settingsPanel.transform, "Title", "SETTINGS", 40, new Vector2(0f, 250f), new Vector2(640f, 56f));
        masterVolumeSlider = MenuUiFactory.CreateLabeledSlider(settingsPanel.transform, "Master Volume", 170f, 0f, 1f, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetMasterVolume(value);
            PlaySliderSound();
        });
        sfxVolumeSlider = MenuUiFactory.CreateLabeledSlider(settingsPanel.transform, "Sound Effects", 100f, 0f, 1f, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetSfxVolume(value);
            PlaySliderSound();
        });
        musicVolumeSlider = MenuUiFactory.CreateLabeledSlider(settingsPanel.transform, "Music Volume", 30f, 0f, 1f, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetMusicVolume(value);
            PlaySliderSound();
        });
        brightnessSlider = MenuUiFactory.CreateLabeledSlider(settingsPanel.transform, "Brightness", -40f, PlayerGameSettings.MinBrightness, PlayerGameSettings.MaxBrightness, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetBrightness(value);
            PlaySliderSound();
        });
        settingsControlButton = MenuUiFactory.CreateButton(settingsPanel.transform, "ControlSettings", "Control Settings", new Vector2(0f, -130f), () => ShowScreen(MenuScreen.ControlSettings, true), new Vector2(320f, 54f));
        settingsBackButton = MenuUiFactory.CreateButton(settingsPanel.transform, "Back", "Back", new Vector2(0f, -200f), () => ShowScreen(MenuScreen.Main, true));
    }

    private void BuildControlSettingsPanel(Transform parent)
    {
        controlSettingsPanel = CreateMenuPanel(parent, "ControlSettingsPanel", new Vector2(560f, 760f));
        controlSettingsPanel.SetActive(false);
        MenuUiFactory.CreateLabel(controlSettingsPanel.transform, "Title", "CONTROL SETTINGS", 36, new Vector2(0f, 330f), new Vector2(640f, 56f));
        aimSlider = MenuUiFactory.CreateLabeledSlider(controlSettingsPanel.transform, "Aim Sensitivity", 250f, 0.1f, 1f, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetAimSensitivity(value);
            PlaySliderSound();
        });
        mouseXSlider = MenuUiFactory.CreateLabeledSlider(controlSettingsPanel.transform, "Mouse X Sensitivity", 180f, 0.2f, 12f, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetMouseSensitivity(value, PlayerGameSettings.MouseSensitivityY);
            PlaySliderSound();
        });
        mouseYSlider = MenuUiFactory.CreateLabeledSlider(controlSettingsPanel.transform, "Mouse Y Sensitivity", 110f, 0.2f, 12f, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetMouseSensitivity(PlayerGameSettings.MouseSensitivityX, value);
            PlaySliderSound();
        });
        controllerXSlider = MenuUiFactory.CreateLabeledSlider(controlSettingsPanel.transform, "Controller X Sensitivity", 40f, PlayerGameSettings.MinControllerSensitivity, PlayerGameSettings.MaxControllerSensitivity, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetControllerSensitivity(value, PlayerGameSettings.ControllerSensitivityY);
            PlaySliderSound();
        });
        controllerYSlider = MenuUiFactory.CreateLabeledSlider(controlSettingsPanel.transform, "Controller Y Sensitivity", -30f, PlayerGameSettings.MinControllerSensitivity, PlayerGameSettings.MaxControllerSensitivity, value =>
        {
            if (suppressUiCallbacks) return;
            PlayerGameSettings.SetControllerSensitivity(PlayerGameSettings.ControllerSensitivityX, value);
            PlaySliderSound();
        });
        invertToggle = MenuUiFactory.CreateToggle(controlSettingsPanel.transform, "Invert Y", -100f, value =>
        {
            if (!suppressUiCallbacks)
                PlayerGameSettings.SetInvertY(value);
        });
        controlSettingsBackButton = MenuUiFactory.CreateButton(controlSettingsPanel.transform, "Back", "Back", new Vector2(0f, -180f), () => ShowScreen(MenuScreen.Settings, true));
    }

    private void BuildCreditsPanel(Transform parent)
    {
        creditsPanel = CreateMenuPanel(parent, "CreditsPanel", new Vector2(560f, 560f));
        creditsPanel.SetActive(false);
        creditsBody = MenuUiFactory.CreateLabel(creditsPanel.transform, "Body", "BULLSEYE", 28, new Vector2(0f, 60f), new Vector2(640f, 360f));
        creditsBackButton = MenuUiFactory.CreateButton(creditsPanel.transform, "Back", "Back", new Vector2(0f, -200f), () => ShowScreen(MenuScreen.Main, true));
    }

    private void WireStaticNavigation()
    {
        MenuUiFactory.SetVerticalNav(playButton, quitButton, profileButton);
        MenuUiFactory.SetVerticalNav(profileButton, playButton, controlsButton);
        MenuUiFactory.SetVerticalNav(controlsButton, profileButton, settingsButton);
        MenuUiFactory.SetVerticalNav(settingsButton, controlsButton, creditsButton);
        MenuUiFactory.SetVerticalNav(creditsButton, settingsButton, quitButton);
        MenuUiFactory.SetVerticalNav(quitButton, creditsButton, playButton);

        MenuUiFactory.SetVerticalNav(joinGameButton, playBackButton, hostGameButton);
        MenuUiFactory.SetVerticalNav(hostGameButton, joinGameButton, playBackButton);
        MenuUiFactory.SetVerticalNav(playBackButton, hostGameButton, joinGameButton);

        MenuUiFactory.SetVerticalNav(masterVolumeSlider, settingsBackButton, sfxVolumeSlider);
        MenuUiFactory.SetVerticalNav(sfxVolumeSlider, masterVolumeSlider, musicVolumeSlider);
        MenuUiFactory.SetVerticalNav(musicVolumeSlider, sfxVolumeSlider, brightnessSlider);
        MenuUiFactory.SetVerticalNav(brightnessSlider, musicVolumeSlider, settingsControlButton);
        MenuUiFactory.SetVerticalNav(settingsControlButton, brightnessSlider, settingsBackButton);
        MenuUiFactory.SetVerticalNav(settingsBackButton, settingsControlButton, masterVolumeSlider);

        MenuUiFactory.SetVerticalNav(aimSlider, controlSettingsBackButton, mouseXSlider);
        MenuUiFactory.SetVerticalNav(mouseXSlider, aimSlider, mouseYSlider);
        MenuUiFactory.SetVerticalNav(mouseYSlider, mouseXSlider, controllerXSlider);
        MenuUiFactory.SetVerticalNav(controllerXSlider, mouseYSlider, controllerYSlider);
        MenuUiFactory.SetVerticalNav(controllerYSlider, controllerXSlider, invertToggle);
        MenuUiFactory.SetVerticalNav(invertToggle, controllerYSlider, controlSettingsBackButton);
        MenuUiFactory.SetVerticalNav(controlSettingsBackButton, invertToggle, aimSlider);

        MenuUiFactory.SetVerticalNav(controlsBackButton, controlsBackButton, controlsBackButton);
        MenuUiFactory.SetVerticalNav(creditsBackButton, creditsBackButton, creditsBackButton);

        WireJoinNavigation();
    }

    private void WireJoinNavigation()
    {
        if (joinEntry != null)
            joinEntry.WirePublicNavigation(publicSessionButtons);
    }

    private void SetJoinInputOverrides(bool joinMode)
    {
        if (playerActions == null || joinMode == joinInputOverridesActive)
            return;

        InputActionMap uiMap = playerActions.FindActionMap("UI");
        if (uiMap == null)
            return;

        InputAction navigate = uiMap.FindAction("Navigate");
        InputAction submit = uiMap.FindAction("Submit");
        if (navigate != null)
            navigate.RemoveAllBindingOverrides();
        if (submit != null)
            submit.RemoveAllBindingOverrides();

        joinInputOverridesActive = joinMode;
        if (!joinMode)
            return;

        DisableBinding(navigate, "<Keyboard>/w");
        DisableBinding(navigate, "<Keyboard>/a");
        DisableBinding(navigate, "<Keyboard>/s");
        DisableBinding(navigate, "<Keyboard>/d");
        DisableBinding(submit, "<Keyboard>/enter");
        DisableBinding(submit, "<Keyboard>/numpadEnter");
    }

    private static void DisableBinding(InputAction action, string path)
    {
        if (action == null || string.IsNullOrEmpty(path))
            return;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].path == path)
                action.ApplyBindingOverride(i, new InputBinding { overridePath = "" });
        }
    }

    private void ConfigureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            uiInputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            uiInputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiInputModule == null)
            {
                StandaloneInputModule legacy = eventSystem.GetComponent<StandaloneInputModule>();
                if (legacy != null)
                    Destroy(legacy);
                uiInputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }
        }

        if (playerActions != null)
        {
            playerActions.devices = null;
            uiInputModule.actionsAsset = playerActions;
            InputActionMap uiMap = playerActions.FindActionMap("UI");
            if (uiMap != null)
                uiMap.Enable();
        }

        uiInputModule.deselectOnBackgroundClick = false;
        uiInputModule.moveRepeatDelay = 0.35f;
        uiInputModule.moveRepeatRate = 0.08f;
        uiInputModule.pointerBehavior = UnityEngine.InputSystem.UI.UIPointerBehavior.SingleMouseOrPenButMultiTouchAndTrack;
        uiInputModule.enabled = true;
    }
}
