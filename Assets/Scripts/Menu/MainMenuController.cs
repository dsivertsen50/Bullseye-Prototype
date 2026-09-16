using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        Host,
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
    [SerializeField] private string gameplaySceneName = GameSessionCoordinator.DefaultGameplaySceneName;
    [SerializeField] private float leftColumnPadding = 72f;
    [Tooltip("Optional still image shown on the right. If assigned, the 3D menu stage is hidden.")]
    [SerializeField] private Texture backgroundImage;
    [Tooltip("Optional looping video shown on the right. If assigned, it replaces the 3D stage and still image.")]
    [SerializeField] private VideoClip backgroundVideo;

    private const float MenuPanelAlpha = 0.78f;

    private GameObject mainPanel;
    private GameObject playPanel;
    private GameObject hostPanel;
    private GameObject joinPanel;
    private GameObject profilePanel;
    private GameObject controlsPanel;
    private GameObject settingsPanel;
    private GameObject controlSettingsPanel;
    private GameObject creditsPanel;
    private PlayerProfileUI profileUi;

    private Selectable playButton;
    private Selectable profileButton;
    private Selectable controlsButton;
    private Selectable settingsButton;
    private Selectable creditsButton;
    private Selectable quitButton;
    private Selectable joinGameButton;
    private Selectable hostGameButton;
    private Selectable playBackButton;
    private Button publicButton;
    private Button privateButton;
    private Button localButton;
    private Selectable startGameButton;
    private Selectable hostBackButton;
    private InputField joinCodeField;
    private Selectable joinConfirmButton;
    private Selectable joinBackButton;
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

    private Text hostJoinCodeLabel;
    private Text hostVisibilityNote;
    private Text hostErrorLabel;
    private Text joinPublicListLabel;
    private Text joinErrorLabel;
    private Text joinTitleLabel;
    private Text creditsBody;
    private readonly List<Button> publicSessionButtons = new List<Button>();
    private int publicQuerySerial;
    private Coroutine publicQueryRoutine;

    private MenuScreen currentScreen = MenuScreen.Main;
    private GameVisibility hostVisibility = GameVisibility.Private;
    private bool hostLocalTest;
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
                    ShowScreen(MenuScreen.Host, false);
                    SetHostError(error);
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
    }

    private void OnDisable()
    {
        UnbindUiActions();
        PlayerGameSettings.Changed -= HandleSettingsChanged;
    }

    private void Update()
    {
        RestoreSelectionIfNeeded();
        TrackSelectionForNavigateSound();
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
        HandleBack(true);
    }

    private void HandleBack(bool fromCancel)
    {
        if (Time.frameCount == lastBackFrame)
            return;
        lastBackFrame = Time.frameCount;

        if (sessionCoordinator != null && sessionCoordinator.IsBusy)
            return;

        switch (currentScreen)
        {
            case MenuScreen.Main:
                return;
            case MenuScreen.Play:
            case MenuScreen.Profile:
                ShowScreen(MenuScreen.Main, fromCancel);
                break;
            case MenuScreen.Host:
                ShowScreen(MenuScreen.Play, fromCancel);
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
        SetActive(hostPanel, screen == MenuScreen.Host);
        SetActive(joinPanel, screen == MenuScreen.Join);
        SetActive(profilePanel, screen == MenuScreen.Profile);
        SetActive(controlsPanel, screen == MenuScreen.Controls);
        SetActive(settingsPanel, screen == MenuScreen.Settings);
        SetActive(controlSettingsPanel, screen == MenuScreen.ControlSettings);
        SetActive(creditsPanel, screen == MenuScreen.Credits);

        if (screen == MenuScreen.Host)
            PrepareHostScreen();
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

    private static bool LocalTestAvailable => Application.isEditor;

    private void PrepareHostScreen()
    {
        if (LocalTestAvailable)
            hostLocalTest = true;
        EnsureLocalJoinCode();
        SetHostError(null);
        RefreshVisibilityButtons();
    }

    private void PrepareJoinScreen()
    {
        if (joinTitleLabel != null)
            joinTitleLabel.text = "JOIN GAME";
        if (joinConfirmButton != null)
        {
            Text confirmLabel = joinConfirmButton.GetComponentInChildren<Text>();
            if (confirmLabel != null)
                confirmLabel.text = "Join Game";
        }

        Transform publicTitle = joinPanel != null ? joinPanel.transform.Find("PublicTitle") : null;
        if (publicTitle != null)
        {
            Text publicTitleText = publicTitle.GetComponent<Text>();
            if (publicTitleText != null)
                publicTitleText.text = "Public Games";
        }

        Transform privateTitle = joinPanel != null ? joinPanel.transform.Find("PrivateTitle") : null;
        if (privateTitle != null)
            privateTitle.gameObject.SetActive(true);

        RebuildPublicSessionList();
        if (joinErrorLabel != null && string.IsNullOrEmpty(joinErrorLabel.text))
            joinErrorLabel.text = string.Empty;
    }

    private void SetJoinError(string message)
    {
        if (joinErrorLabel != null)
            joinErrorLabel.text = message ?? string.Empty;
    }

    private void SetHostError(string message)
    {
        if (hostErrorLabel != null)
            hostErrorLabel.text = message ?? string.Empty;
    }

    private void RefreshVisibilityButtons()
    {
        MenuUiFactory.SetButtonSelectedVisual(publicButton, !hostLocalTest && hostVisibility == GameVisibility.Public);
        MenuUiFactory.SetButtonSelectedVisual(privateButton, !hostLocalTest && hostVisibility == GameVisibility.Private);
        if (localButton != null)
        {
            localButton.gameObject.SetActive(LocalTestAvailable);
            MenuUiFactory.SetButtonSelectedVisual(localButton, hostLocalTest);
        }

        if (hostJoinCodeLabel != null)
        {
            if (hostLocalTest)
                hostJoinCodeLabel.text = "Join Code  " + reservedJoinCode;
            else if (hostVisibility == GameVisibility.Private)
                hostJoinCodeLabel.text = "You'll get a join code after the match starts.";
            else
                hostJoinCodeLabel.text = "Public games appear under Join Game.";
        }

        if (hostVisibilityNote != null)
        {
            if (hostLocalTest)
                hostVisibilityNote.text = "Local test stays on this computer. Use this for Multiplayer Play Mode.";
            else if (hostVisibility == GameVisibility.Public)
                hostVisibilityNote.text = "Public games can be found by other players in Join Game.";
            else
                hostVisibilityNote.text = "Private games are joined with a code. Share it with invited players.";
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
        ClearPublicSessionButtons();
        if (joinPublicListLabel != null)
            joinPublicListLabel.text = "Looking for public games...";
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
            List<GameSessionInfo> localOnly = MergeLocalPublicSessions(null);
            if (localOnly.Count > 0)
            {
                ApplyPublicSessions(localOnly);
                yield break;
            }

            if (joinPublicListLabel != null)
                joinPublicListLabel.text = "Unable to list public games.\nYou can still join with a join code.";
            WireJoinNavigation();
            yield break;
        }

        ApplyPublicSessions(MergeLocalPublicSessions(task.Result));
    }

    private void ApplyPublicSessions(List<GameSessionInfo> sessions)
    {
        ClearPublicSessionButtons();
        if (sessions == null || sessions.Count == 0)
        {
            if (joinPublicListLabel != null)
                joinPublicListLabel.text = "No public games found.\nAsk a host for a join code, or host a public game.";
            WireJoinNavigation();
            return;
        }

        if (joinPublicListLabel != null)
            joinPublicListLabel.text = "Public games";
        float y = 70f;
        int count = Mathf.Min(sessions.Count, 3);
        for (int i = 0; i < count; i++)
        {
            GameSessionInfo session = sessions[i];
            string label = session.ConnectionMode == MultiplayerConnectionMode.Local && !string.IsNullOrEmpty(session.JoinCode)
                ? "Join " + session.JoinCode
                : "Join Game " + (i + 1);
            Button button = MenuUiFactory.CreateButton(joinPanel.transform, "PublicSession" + i, label, new Vector2(0f, y), () => JoinListedSession(session), new Vector2(420f, 48f));
            publicSessionButtons.Add(button);
            y -= 56f;
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
        List<GameSessionInfo> local = LocalSessionRegistry.ListPublicSessions();
        if (local != null)
            merged.AddRange(local);
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
        if (hostLocalTest && LocalTestAvailable)
        {
            EnsureLocalJoinCode();
            if (sessionCoordinator.TryHost(GameVisibility.Private, reservedJoinCode, out string localError))
                return;
            SetHostError(localError);
            return;
        }

        if (sessionCoordinator.TryHostOnline(hostVisibility, out string error))
            return;

        SetHostError(error);
    }

    private void OpenJoin()
    {
        ShowScreen(MenuScreen.Join, true);
    }

    private void JoinFromMenu()
    {
        if (!CanStartConnection())
            return;

        PlaySelect();
        string code = joinCodeField != null ? joinCodeField.text : string.Empty;
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
        suppressNavigateSound = true;
        lastSelected = selectable != null ? selectable.gameObject : null;
        if (selectable == null || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
    }

    private void RestoreSelectionIfNeeded()
    {
        if (EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
            return;

        bool wantsSelection = navigateAction != null &&
                              navigateAction.ReadValue<Vector2>().sqrMagnitude > 0.25f;
        if (!wantsSelection)
            return;

        SelectControl(DefaultSelectableForCurrentScreen());
    }

    private Selectable DefaultSelectableForCurrentScreen()
    {
        switch (currentScreen)
        {
            case MenuScreen.Play: return joinGameButton;
            case MenuScreen.Host: return publicButton;
            case MenuScreen.Join:
                return publicSessionButtons.Count > 0 ? publicSessionButtons[0] : joinCodeField;
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
        BuildHostPanel(canvasObject.transform);
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
        hostGameButton = MenuUiFactory.CreateButton(playPanel.transform, "HostGame", "Host Game", new Vector2(0f, 20f), () => ShowScreen(MenuScreen.Host, true));
        playBackButton = MenuUiFactory.CreateButton(playPanel.transform, "Back", "Back", new Vector2(0f, -90f), () => ShowScreen(MenuScreen.Main, true));
    }

    private void BuildHostPanel(Transform parent)
    {
        hostPanel = CreateMenuPanel(parent, "HostPanel", new Vector2(640f, 620f));
        hostPanel.SetActive(false);
        MenuUiFactory.CreateLabel(hostPanel.transform, "Title", "HOST GAME", 44, new Vector2(0f, 240f), new Vector2(640f, 56f));
        MenuUiFactory.CreateLabel(hostPanel.transform, "VisibilityLabel", "Visibility", 22, new Vector2(0f, 175f), new Vector2(520f, 28f));
        publicButton = MenuUiFactory.CreateButton(hostPanel.transform, "Public", "Public", new Vector2(-150f, 120f), () =>
        {
            hostLocalTest = false;
            hostVisibility = GameVisibility.Public;
            RefreshVisibilityButtons();
            PlaySelect();
        }, new Vector2(220f, 54f));
        privateButton = MenuUiFactory.CreateButton(hostPanel.transform, "Private", "Private", new Vector2(150f, 120f), () =>
        {
            hostLocalTest = false;
            hostVisibility = GameVisibility.Private;
            RefreshVisibilityButtons();
            PlaySelect();
        }, new Vector2(220f, 54f));
        localButton = MenuUiFactory.CreateButton(hostPanel.transform, "Local", "Local Test", new Vector2(0f, 55f), () =>
        {
            hostLocalTest = true;
            EnsureLocalJoinCode();
            RefreshVisibilityButtons();
            PlaySelect();
        }, new Vector2(240f, 54f));
        localButton.gameObject.SetActive(LocalTestAvailable);
        hostJoinCodeLabel = MenuUiFactory.CreateLabel(hostPanel.transform, "JoinCode", "Join Code", 28, new Vector2(0f, -5f), new Vector2(640f, 40f));
        hostVisibilityNote = MenuUiFactory.CreateLabel(hostPanel.transform, "VisibilityNote", string.Empty, 18, new Vector2(0f, -60f), new Vector2(640f, 80f));
        startGameButton = MenuUiFactory.CreateButton(hostPanel.transform, "StartGame", "Start Game", new Vector2(0f, -120f), HostFromMenu);
        hostErrorLabel = MenuUiFactory.CreateLabel(hostPanel.transform, "Error", string.Empty, 20, new Vector2(0f, -155f), new Vector2(640f, 40f));
        hostErrorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);
        hostBackButton = MenuUiFactory.CreateButton(hostPanel.transform, "Back", "Back", new Vector2(0f, -210f), () => ShowScreen(MenuScreen.Play, true));
    }

    private void BuildJoinPanel(Transform parent)
    {
        joinPanel = CreateMenuPanel(parent, "JoinPanel", new Vector2(680f, 720f));
        joinPanel.SetActive(false);
        joinTitleLabel = MenuUiFactory.CreateLabel(joinPanel.transform, "Title", "JOIN GAME", 44, new Vector2(0f, 310f), new Vector2(680f, 56f));
        MenuUiFactory.CreateLabel(joinPanel.transform, "PublicTitle", "Public Games", 24, new Vector2(0f, 250f), new Vector2(640f, 32f));
        joinPublicListLabel = MenuUiFactory.CreateLabel(joinPanel.transform, "PublicPlaceholder", string.Empty, 18, new Vector2(0f, 175f), new Vector2(680f, 90f));
        MenuUiFactory.CreateLabel(joinPanel.transform, "PrivateTitle", "Private Game", 24, new Vector2(0f, 20f), new Vector2(640f, 32f));
        MenuUiFactory.CreateLabel(joinPanel.transform, "JoinCodeLabel", "Enter Join Code", 18, new Vector2(0f, -20f), new Vector2(520f, 24f));
        joinCodeField = MenuUiFactory.CreateInputField(joinPanel.transform, "JoinCode", "Join Code", new Vector2(0f, -60f), new Vector2(420f, 54f));
        joinCodeField.characterLimit = 12;
        joinCodeField.contentType = InputField.ContentType.Alphanumeric;
        joinCodeField.onEndEdit.AddListener(text =>
        {
            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                JoinFromMenu();
            }
        });
        joinConfirmButton = MenuUiFactory.CreateButton(joinPanel.transform, "Join", "Join Game", new Vector2(0f, -130f), JoinFromMenu);
        joinBackButton = MenuUiFactory.CreateButton(joinPanel.transform, "Back", "Back", new Vector2(0f, -200f), () => ShowScreen(MenuScreen.Play, true));
        joinErrorLabel = MenuUiFactory.CreateLabel(joinPanel.transform, "Error", string.Empty, 20, new Vector2(0f, -260f), new Vector2(680f, 48f));
        joinErrorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);
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

        MenuUiFactory.SetNav(publicButton, hostBackButton, localButton != null ? localButton : startGameButton, privateButton, privateButton);
        MenuUiFactory.SetNav(privateButton, hostBackButton, localButton != null ? localButton : startGameButton, publicButton, publicButton);
        if (localButton != null)
        {
            MenuUiFactory.SetVerticalNav(localButton, publicButton, startGameButton);
            MenuUiFactory.SetVerticalNav(startGameButton, localButton, hostBackButton);
        }
        else
        {
            MenuUiFactory.SetVerticalNav(startGameButton, publicButton, hostBackButton);
        }
        MenuUiFactory.SetVerticalNav(hostBackButton, startGameButton, publicButton);

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
        Selectable firstPublic = publicSessionButtons.Count > 0 ? publicSessionButtons[0] : joinCodeField;
        Selectable lastPublic = publicSessionButtons.Count > 0 ? publicSessionButtons[publicSessionButtons.Count - 1] : joinBackButton;

        for (int i = 0; i < publicSessionButtons.Count; i++)
        {
            Selectable up = i == 0 ? joinBackButton : publicSessionButtons[i - 1];
            Selectable down = i == publicSessionButtons.Count - 1 ? joinCodeField : publicSessionButtons[i + 1];
            MenuUiFactory.SetVerticalNav(publicSessionButtons[i], up, down);
        }

        MenuUiFactory.SetVerticalNav(joinCodeField, lastPublic, joinConfirmButton);
        MenuUiFactory.SetVerticalNav(joinConfirmButton, joinCodeField, joinBackButton);
        MenuUiFactory.SetVerticalNav(joinBackButton, joinConfirmButton, firstPublic);
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
