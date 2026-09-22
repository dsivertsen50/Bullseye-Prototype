using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Custom Match configuration and reusable Match Lobby screens.
/// </summary>
public class MatchMenuUI : MonoBehaviour
{
    private const float PanelAlpha = 0.78f;
    private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, PanelAlpha);

    private MapCatalog mapCatalog;
    private GameModeCatalog modeCatalog;
    private MenuAudioController menuAudio;
    private Action onCreateLobby;
    private Action onBackFromCustom;
    private Action onLeaveLobby;
    private Action onStartMatch;
    private Action onCancelLobby;
    private Action onEditSetup;
    private Action onShowLobby;

    private GameObject customPanel;
    private GameObject lobbyPanel;
    private readonly List<Button> modeButtons = new List<Button>();
    private readonly List<MapCardView> mapCards = new List<MapCardView>();
    private readonly List<LobbyPlayerRow> playerRows = new List<LobbyPlayerRow>();
    private Button publicButton;
    private Button privateButton;
    private Button localButton;
    private Button createLobbyButton;
    private Button customBackButton;
    private Button customCancelLobbyButton;
    private Button startMatchButton;
    private Button changeSetupButton;
    private Button leaveLobbyButton;
    private Button cancelLobbyButton;
    private Text modeDescriptionLabel;
    private Text previewNameLabel;
    private Text previewDescriptionLabel;
    private Image previewImage;
    private Text customErrorLabel;
    private Text localTestHintLabel;
    private string reservedLocalJoinCode;
    private Text lobbyTitleLabel;
    private Text lobbyModeLabel;
    private Text lobbyModeDescriptionLabel;
    private Text lobbyMapNameLabel;
    private Text lobbyMapDescriptionLabel;
    private Image lobbyPreviewImage;
    private Text lobbyJoinCodeHeading;
    private Text lobbyJoinCodeLabel;
    private Text lobbyJoinCodeCopiedLabel;
    private Button copyJoinCodeButton;
    private Text lobbyPlayerCountLabel;
    private Text lobbyErrorLabel;
    private Text lobbyHostHintLabel;
    private float joinCodeCopiedUntil;
    private ScrollRect playerScroll;
    private Transform playerContent;
    private string focusedMapId;
    private bool hostControlsVisible = true;

    public GameObject CustomPanel => customPanel;
    public GameObject LobbyPanel => lobbyPanel;
    public Selectable CustomDefaultSelectable => publicButton != null ? publicButton : (modeButtons.Count > 0 ? modeButtons[0] : createLobbyButton);
    public Selectable LobbyDefaultSelectable
    {
        get
        {
            if (cancelLobbyButton != null && cancelLobbyButton.gameObject.activeSelf)
                return cancelLobbyButton;
            if (leaveLobbyButton != null && leaveLobbyButton.gameObject.activeSelf)
                return leaveLobbyButton;
            return startMatchButton;
        }
    }
    public Selectable CustomBackButton => customBackButton;
    public Selectable LobbyBackButton => cancelLobbyButton != null && cancelLobbyButton.gameObject.activeSelf
        ? cancelLobbyButton
        : leaveLobbyButton;

    public static MatchMenuUI Create(
        Transform canvasParent,
        float leftPadding,
        MapCatalog maps,
        GameModeCatalog modes,
        MenuAudioController audio,
        Action createLobby,
        Action backFromCustom,
        Action startMatch,
        Action leaveLobby,
        Action cancelLobby,
        Action editSetup,
        Action showLobby)
    {
        GameObject host = new GameObject("MatchMenuUI", typeof(RectTransform));
        host.transform.SetParent(canvasParent, false);
        MenuUiFactory.Stretch(host.GetComponent<RectTransform>());
        MatchMenuUI ui = host.AddComponent<MatchMenuUI>();
        ui.mapCatalog = maps;
        ui.modeCatalog = modes;
        ui.menuAudio = audio;
        ui.onCreateLobby = createLobby;
        ui.onBackFromCustom = backFromCustom;
        ui.onStartMatch = startMatch;
        ui.onLeaveLobby = leaveLobby;
        ui.onCancelLobby = cancelLobby;
        ui.onEditSetup = editSetup;
        ui.onShowLobby = showLobby;
        ui.Build(leftPadding);
        return ui;
    }

    private void OnEnable()
    {
        MatchLobby lobby = MatchLobby.Ensure();
        lobby.Changed += HandleLobbyChanged;
        MultiplayerSessionManager manager = MultiplayerSessionManager.Ensure();
        manager.StateChanged += HandleSessionStateChanged;
    }

    private void OnDisable()
    {
        MatchLobby lobby = MatchLobby.Instance;
        if (lobby != null)
            lobby.Changed -= HandleLobbyChanged;
        MultiplayerSessionManager manager = MultiplayerSessionManager.Instance;
        if (manager != null)
            manager.StateChanged -= HandleSessionStateChanged;
    }

    private void Update()
    {
        if (lobbyPanel == null || !lobbyPanel.activeSelf)
            return;

        RefreshJoinCode();
        if (lobbyJoinCodeCopiedLabel != null)
            lobbyJoinCodeCopiedLabel.enabled = Time.unscaledTime < joinCodeCopiedUntil;
    }

    public void OpenCustomMatch()
    {
        MatchLobby lobby = MatchLobby.Ensure();
        if (!lobby.IsInLobby)
            ApplyFreshCustomDefaults();
        else
            EnsureDraft();
        focusedMapId = MatchLobby.Ensure().Configuration.MapId;
        customPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        RefreshCustomMatch();
    }

    public void OpenLobby(bool isHost)
    {
        hostControlsVisible = isHost;
        customPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        RefreshLobby();
    }

    public void Hide()
    {
        if (customPanel != null)
            customPanel.SetActive(false);
        if (lobbyPanel != null)
            lobbyPanel.SetActive(false);
    }

    public void SetCustomError(string message)
    {
        if (customErrorLabel != null)
            customErrorLabel.text = message ?? string.Empty;
    }

    public void SetLocalTestHint(string joinCode)
    {
        reservedLocalJoinCode = joinCode;
        if (localTestHintLabel == null)
            return;
        if (!MainMenuHostOptions.LocalTest || string.IsNullOrEmpty(joinCode))
        {
            localTestHintLabel.text = string.Empty;
            return;
        }

        localTestHintLabel.gameObject.SetActive(true);
        localTestHintLabel.text = "Player 2: Join Game → Local Test " + joinCode;
    }

    private static bool LocalTestUiAvailable =>
#if UNITY_EDITOR
        true;
#else
        Debug.isDebugBuild;
#endif

    public void SetLobbyError(string message)
    {
        if (lobbyErrorLabel != null)
            lobbyErrorLabel.text = message ?? string.Empty;
    }

    private void Build(float leftPadding)
    {
        customPanel = CreatePanel("CustomMatchPanel", new Vector2(1180f, 920f), leftPadding);
        lobbyPanel = CreatePanel("MatchLobbyPanel", new Vector2(1180f, 920f), leftPadding);
        customPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        BuildCustomMatch();
        BuildLobby();
        WireNavigation();
    }

    private GameObject CreatePanel(string name, Vector2 size, float leftPadding)
    {
        GameObject panel = MenuUiFactory.CreatePanel(transform, name, size, PanelColor);
        MenuUiFactory.DockLeft(panel, leftPadding);
        return panel;
    }

    private void BuildCustomMatch()
    {
        Transform root = customPanel.transform;
        MenuUiFactory.CreateLabel(root, "Title", "CUSTOM MATCH", 40, new Vector2(0f, 410f), new Vector2(1100f, 52f));

        MenuUiFactory.CreateLabel(root, "VisibilityLabel", "VISIBILITY", 20, new Vector2(-360f, 355f), new Vector2(280f, 28f), TextAnchor.MiddleLeft);
        publicButton = MenuUiFactory.CreateButton(root, "Public", "Public", new Vector2(-390f, 310f), () => SetVisibility(MatchVisibility.Public, false), new Vector2(180f, 48f));
        privateButton = MenuUiFactory.CreateButton(root, "Private", "Private", new Vector2(-200f, 310f), () => SetVisibility(MatchVisibility.Private, false), new Vector2(180f, 48f));
        localButton = MenuUiFactory.CreateButton(root, "Local", "Local Test", new Vector2(-10f, 310f), () => SetVisibility(MatchVisibility.Private, true), new Vector2(180f, 48f));
        localButton.gameObject.SetActive(LocalTestUiAvailable);
        localTestHintLabel = MenuUiFactory.CreateLabel(root, "LocalTestHint", string.Empty, 16, new Vector2(200f, 310f), new Vector2(420f, 48f), TextAnchor.MiddleLeft);
        localTestHintLabel.color = ProfileUiFactory.MutedColor;

        MenuUiFactory.CreateLabel(root, "ModeLabel", "GAME MODE", 20, new Vector2(-360f, 250f), new Vector2(280f, 28f), TextAnchor.MiddleLeft);
        BuildModeButtons(root, 200f);
        modeDescriptionLabel = MenuUiFactory.CreateLabel(root, "ModeDescription", string.Empty, 18, new Vector2(-160f, 140f), new Vector2(720f, 48f), TextAnchor.MiddleLeft);
        modeDescriptionLabel.color = ProfileUiFactory.MutedColor;

        MenuUiFactory.CreateLabel(root, "MapLabel", "MAPS", 20, new Vector2(-360f, 90f), new Vector2(280f, 28f), TextAnchor.MiddleLeft);
        BuildMapCards(root, new Vector2(-360f, -40f));

        previewImage = MenuUiFactory.CreateImage(root, "Preview", new Color(0.12f, 0.14f, 0.18f, 1f));
        previewImage.rectTransform.sizeDelta = new Vector2(420f, 220f);
        previewImage.rectTransform.anchoredPosition = new Vector2(330f, 20f);
        previewImage.preserveAspect = true;
        previewNameLabel = MenuUiFactory.CreateLabel(root, "PreviewName", string.Empty, 24, new Vector2(330f, -110f), new Vector2(420f, 36f));
        previewDescriptionLabel = MenuUiFactory.CreateLabel(root, "PreviewDescription", string.Empty, 16, new Vector2(330f, -165f), new Vector2(420f, 70f));
        previewDescriptionLabel.color = ProfileUiFactory.MutedColor;

        createLobbyButton = MenuUiFactory.CreateButton(root, "CreateLobby", "Create Lobby", new Vector2(-180f, -380f), HandleCreateOrReturn, new Vector2(280f, 54f));
        customCancelLobbyButton = MenuUiFactory.CreateButton(root, "CancelLobbyFromSetup", "Cancel Lobby", new Vector2(0f, -380f), HandleCancelLobbyFromSetup, new Vector2(240f, 54f));
        customCancelLobbyButton.gameObject.SetActive(false);
        customBackButton = MenuUiFactory.CreateButton(root, "Back", "Back", new Vector2(140f, -380f), HandleCustomBack, new Vector2(220f, 54f));
        customErrorLabel = MenuUiFactory.CreateLabel(root, "Error", string.Empty, 20, new Vector2(0f, -430f), new Vector2(1000f, 36f));
        customErrorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);
        MenuUiFactory.CreateLabel(root, "Hint", "Hover or focus a map to preview. Select to choose it.", 16, new Vector2(0f, -330f), new Vector2(1000f, 24f)).color = ProfileUiFactory.MutedColor;
    }

    private void BuildLobby()
    {
        Transform root = lobbyPanel.transform;
        lobbyTitleLabel = MenuUiFactory.CreateLabel(root, "Title", "MATCH LOBBY", 40, new Vector2(0f, 410f), new Vector2(1100f, 52f));
        lobbyModeLabel = MenuUiFactory.CreateLabel(root, "Mode", "Mode", 26, new Vector2(-280f, 345f), new Vector2(520f, 36f), TextAnchor.MiddleLeft);
        lobbyModeDescriptionLabel = MenuUiFactory.CreateLabel(root, "ModeDescription", string.Empty, 16, new Vector2(-280f, 305f), new Vector2(520f, 40f), TextAnchor.MiddleLeft);
        lobbyModeDescriptionLabel.color = ProfileUiFactory.MutedColor;

        lobbyPreviewImage = MenuUiFactory.CreateImage(root, "Preview", new Color(0.12f, 0.14f, 0.18f, 1f));
        lobbyPreviewImage.rectTransform.sizeDelta = new Vector2(360f, 190f);
        lobbyPreviewImage.rectTransform.anchoredPosition = new Vector2(340f, 300f);
        lobbyPreviewImage.preserveAspect = true;
        lobbyMapNameLabel = MenuUiFactory.CreateLabel(root, "MapName", "Map", 24, new Vector2(340f, 185f), new Vector2(380f, 32f));
        lobbyMapDescriptionLabel = MenuUiFactory.CreateLabel(root, "MapDescription", string.Empty, 16, new Vector2(340f, 140f), new Vector2(380f, 56f));
        lobbyMapDescriptionLabel.color = ProfileUiFactory.MutedColor;

        lobbyPlayerCountLabel = MenuUiFactory.CreateLabel(root, "PlayerCount", "PLAYERS", 20, new Vector2(-280f, 250f), new Vector2(520f, 28f), TextAnchor.MiddleLeft);
        playerScroll = ProfileUiFactory.CreateScrollArea(root, "Players", new Vector2(-200f, 40f), new Vector2(680f, 280f));
        playerContent = playerScroll.content;

        lobbyJoinCodeHeading = MenuUiFactory.CreateLabel(root, "JoinCodeHeading", "JOIN CODE", 18, new Vector2(0f, -125f), new Vector2(900f, 24f));
        lobbyJoinCodeLabel = MenuUiFactory.CreateLabel(root, "JoinCode", string.Empty, 42, new Vector2(-80f, -165f), new Vector2(620f, 52f));
        copyJoinCodeButton = MenuUiFactory.CreateButton(root, "CopyCode", "Copy Code", new Vector2(320f, -165f), CopyJoinCode, new Vector2(180f, 44f));
        Text copyLabel = copyJoinCodeButton.GetComponentInChildren<Text>();
        if (copyLabel != null)
            copyLabel.fontSize = 18;
        lobbyJoinCodeCopiedLabel = MenuUiFactory.CreateLabel(root, "Copied", "Copied", 16, new Vector2(320f, -200f), new Vector2(180f, 22f));
        lobbyJoinCodeCopiedLabel.enabled = false;
        lobbyHostHintLabel = MenuUiFactory.CreateLabel(root, "HostHint", string.Empty, 16, new Vector2(0f, -210f), new Vector2(900f, 28f));
        lobbyHostHintLabel.color = ProfileUiFactory.MutedColor;

        startMatchButton = MenuUiFactory.CreateButton(root, "StartMatch", "Start Match", new Vector2(220f, -270f), () => onStartMatch?.Invoke(), new Vector2(260f, 54f));
        changeSetupButton = MenuUiFactory.CreateButton(root, "ChangeSetup", "Change Map / Mode", new Vector2(220f, -340f), HandleEditSetup, new Vector2(260f, 54f));
        leaveLobbyButton = MenuUiFactory.CreateButton(root, "LeaveLobby", "Leave Lobby", new Vector2(-220f, -340f), () => onLeaveLobby?.Invoke(), new Vector2(260f, 54f));
        cancelLobbyButton = MenuUiFactory.CreateButton(root, "CancelLobby", "Cancel Lobby", new Vector2(-220f, -270f), () => onCancelLobby?.Invoke(), new Vector2(260f, 54f));
        lobbyErrorLabel = MenuUiFactory.CreateLabel(root, "Error", string.Empty, 20, new Vector2(0f, -400f), new Vector2(1000f, 40f));
        lobbyErrorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);
    }

    private void BuildModeButtons(Transform parent, float y)
    {
        modeButtons.Clear();
        int count = modeCatalog != null ? modeCatalog.Count : 0;
        float startX = -390f;
        for (int i = 0; i < count; i++)
        {
            GameModeDefinition mode = modeCatalog.Get(i);
            if (mode == null)
                continue;
            int index = i;
            Button button = MenuUiFactory.CreateButton(
                parent,
                mode.GameModeId,
                ModeButtonLabel(mode),
                new Vector2(startX + i * 200f, y),
                () => SelectMode(mode.GameModeId),
                new Vector2(190f, 48f));
            modeButtons.Add(button);
        }
    }

    private void BuildMapCards(Transform parent, Vector2 origin)
    {
        mapCards.Clear();
        int count = mapCatalog != null ? mapCatalog.Count : 0;
        const float width = 170f;
        const float height = 150f;
        const float gapX = 186f;
        const float gapY = 164f;
        for (int i = 0; i < count; i++)
        {
            MapDefinition map = mapCatalog.Get(i);
            if (map == null)
                continue;
            int column = i % 3;
            int row = i / 3;
            Vector2 position = origin + new Vector2(column * gapX, -row * gapY);
            MapCardView card = MapCardView.Create(parent, map, position, new Vector2(width, height), HandleMapFocused, HandleMapSelected);
            mapCards.Add(card);
        }
    }

    private void EnsureDraft()
    {
        MatchLobby lobby = MatchLobby.Ensure();
        if (lobby.Configuration == null)
            lobby.SetDraftConfiguration(MatchConfiguration.CreateDefault(MultiplayerSessionManager.Ensure().MaxPlayers));
        if (string.IsNullOrEmpty(lobby.Configuration.MapId) && mapCatalog != null && mapCatalog.GetDefaultAvailable() != null)
        {
            MatchConfiguration draft = lobby.Configuration.Clone();
            draft.MapId = mapCatalog.GetDefaultAvailable().MapId;
            lobby.SetDraftConfiguration(draft);
        }

        if (string.IsNullOrEmpty(lobby.Configuration.GameModeId) && modeCatalog != null && modeCatalog.GetDefaultAvailable() != null)
        {
            MatchConfiguration draft = lobby.Configuration.Clone();
            draft.GameModeId = modeCatalog.GetDefaultAvailable().GameModeId;
            lobby.SetDraftConfiguration(draft);
        }
    }

    private void ApplyFreshCustomDefaults()
    {
        MainMenuHostOptions.LocalTest = false;
        MatchLobby.Ensure().SetDraftConfiguration(
            MatchConfiguration.CreateDefault(MultiplayerSessionManager.Ensure().MaxPlayers));
        EnsureDraft();
    }

    private void SetVisibility(MatchVisibility visibility, bool localTest)
    {
        MatchLobby lobby = MatchLobby.Ensure();
        MatchConfiguration draft = lobby.Configuration.Clone();
        draft.Visibility = visibility;
        lobby.SetDraftConfiguration(draft);
        MainMenuHostOptions.LocalTest = localTest;
        PlaySelect();
        RefreshCustomMatch();
    }

    private void SelectMode(string gameModeId)
    {
        MatchLobby lobby = MatchLobby.Ensure();
        if (lobby.IsInLobby)
        {
            if (!lobby.TrySelectGameMode(gameModeId, out string error))
            {
                SetLobbyError(error);
                return;
            }
        }
        else
        {
            GameModeDefinition mode = modeCatalog != null ? modeCatalog.GetById(gameModeId) : null;
            if (mode == null || !mode.IsAvailable)
            {
                SetCustomError(mode != null ? mode.DisplayName + " is coming soon." : "Select a game mode.");
                return;
            }

            MatchConfiguration draft = lobby.Configuration.Clone();
            draft.GameModeId = gameModeId;
            lobby.SetDraftConfiguration(draft);
        }

        PlaySelect();
        RefreshCustomMatch();
        RefreshLobby();
    }

    private void HandleMapFocused(MapCardView card)
    {
        if (card == null || card.Definition == null)
            return;
        focusedMapId = card.Definition.MapId;
        RefreshPreview(card.Definition);
    }

    private void HandleMapSelected(MapCardView card)
    {
        if (card == null || card.Definition == null)
            return;

        MatchLobby lobby = MatchLobby.Ensure();
        if (lobby.IsInLobby)
        {
            if (!lobby.TrySelectMap(card.Definition.MapId, out string error))
            {
                SetLobbyError(error);
                return;
            }
        }
        else if (!card.Definition.CanStartMatch)
        {
            SetCustomError(card.Definition.IsAvailable ? "That map is not available yet." : card.Definition.DisplayName + " is coming soon.");
            return;
        }
        else
        {
            MatchConfiguration draft = lobby.Configuration.Clone();
            draft.MapId = card.Definition.MapId;
            lobby.SetDraftConfiguration(draft);
        }

        PlaySelect();
        RefreshCustomMatch();
        RefreshLobby();
    }

    private void HandleLobbyChanged()
    {
        if (lobbyPanel != null && lobbyPanel.activeSelf)
            RefreshLobby();
        if (customPanel != null && customPanel.activeSelf)
            RefreshCustomMatch();
    }

    private void HandleSessionStateChanged()
    {
        if (lobbyPanel != null && lobbyPanel.activeSelf)
            RefreshJoinCode();
    }

    private void RefreshCustomMatch()
    {
        MatchLobby lobby = MatchLobby.Ensure();
        MatchConfiguration config = lobby.Configuration;
        bool inExistingLobby = lobby.IsInLobby;
        bool localTest = MainMenuHostOptions.LocalTest && LocalTestUiAvailable;
        MenuUiFactory.SetButtonSelectedVisual(publicButton, !localTest && config.Visibility == MatchVisibility.Public);
        MenuUiFactory.SetButtonSelectedVisual(privateButton, !localTest && config.Visibility == MatchVisibility.Private);
        if (localButton != null)
        {
            localButton.gameObject.SetActive(LocalTestUiAvailable);
            MenuUiFactory.SetButtonSelectedVisual(localButton, localTest);
        }

        if (localTestHintLabel != null)
        {
            localTestHintLabel.gameObject.SetActive(localTest);
            localTestHintLabel.text = localTest && !string.IsNullOrEmpty(reservedLocalJoinCode)
                ? "Player 2: Join Game → Local Test " + reservedLocalJoinCode
                : string.Empty;
        }

        for (int i = 0; i < modeButtons.Count; i++)
        {
            GameModeDefinition mode = modeCatalog != null ? modeCatalog.Get(i) : null;
            bool selected = mode != null && mode.GameModeId == config.GameModeId;
            MenuUiFactory.SetButtonSelectedVisual(modeButtons[i], selected);
            Text label = modeButtons[i].GetComponentInChildren<Text>();
            if (label != null && mode != null)
                label.text = ModeButtonLabel(mode);
        }

        GameModeDefinition selectedMode = modeCatalog != null ? modeCatalog.GetById(config.GameModeId) : null;
        if (modeDescriptionLabel != null)
            modeDescriptionLabel.text = selectedMode != null ? selectedMode.ShortDescription : string.Empty;

        if (createLobbyButton != null)
        {
            Text createLabel = createLobbyButton.GetComponentInChildren<Text>();
            if (createLabel != null)
                createLabel.text = inExistingLobby ? "Back to Lobby" : "Create Lobby";
        }

        bool showCancel = inExistingLobby && lobby.IsHost;
        if (customCancelLobbyButton != null)
            customCancelLobbyButton.gameObject.SetActive(showCancel);
        if (customBackButton != null)
            customBackButton.gameObject.SetActive(!showCancel);

        if (showCancel)
        {
            PlaceButton(createLobbyButton, new Vector2(-180f, -380f), new Vector2(280f, 54f));
            PlaceButton(customCancelLobbyButton, new Vector2(140f, -380f), new Vector2(240f, 54f));
        }
        else
        {
            PlaceButton(createLobbyButton, new Vector2(-180f, -380f), new Vector2(280f, 54f));
            PlaceButton(customBackButton, new Vector2(140f, -380f), new Vector2(220f, 54f));
        }

        for (int i = 0; i < mapCards.Count; i++)
            mapCards[i].RefreshVisual(mapCards[i].Definition != null && mapCards[i].Definition.MapId == config.MapId);

        MapDefinition previewMap = mapCatalog != null ? mapCatalog.GetById(focusedMapId) : null;
        if (previewMap == null)
            previewMap = lobby.SelectedMap;
        RefreshPreview(previewMap);
        WireNavigation();
    }

    private void RefreshLobby()
    {
        MatchLobby lobby = MatchLobby.Ensure();
        MatchConfiguration config = lobby.Configuration;
        hostControlsVisible = IsActingHost(lobby);
        GameModeDefinition mode = lobby.SelectedMode;
        MapDefinition map = lobby.SelectedMap;

        if (lobbyTitleLabel != null)
            lobbyTitleLabel.text = config.Visibility == MatchVisibility.Public ? "PUBLIC MATCH" : "CUSTOM MATCH LOBBY";
        if (lobbyModeLabel != null)
            lobbyModeLabel.text = mode != null ? mode.DisplayName : config.GameModeId;
        if (lobbyModeDescriptionLabel != null)
            lobbyModeDescriptionLabel.text = mode != null ? mode.ShortDescription : string.Empty;
        if (lobbyMapNameLabel != null)
            lobbyMapNameLabel.text = map != null ? map.DisplayName : config.MapId;
        if (lobbyMapDescriptionLabel != null)
            lobbyMapDescriptionLabel.text = map != null ? map.Description : string.Empty;
        if (lobbyPreviewImage != null)
        {
            lobbyPreviewImage.sprite = map != null ? map.PreviewImage : null;
            lobbyPreviewImage.color = Color.white;
        }

        if (lobbyPlayerCountLabel != null)
            lobbyPlayerCountLabel.text = "PLAYERS  " + lobby.PlayerCount + " / " + lobby.MaxPlayers;

        RebuildPlayerRows(lobby);
        RefreshJoinCode();
        if (lobbyHostHintLabel != null)
            lobbyHostHintLabel.text = IsActingHost(lobby)
                ? "Share the join code with invited players."
                : "Waiting for the host to start.";

        bool actingHost = IsActingHost(lobby);
        if (startMatchButton != null)
            startMatchButton.gameObject.SetActive(actingHost);
        if (changeSetupButton != null)
            changeSetupButton.gameObject.SetActive(actingHost);
        if (cancelLobbyButton != null)
            cancelLobbyButton.gameObject.SetActive(actingHost);
        if (leaveLobbyButton != null)
        {
            Text leaveLabel = leaveLobbyButton.GetComponentInChildren<Text>();
            if (leaveLabel != null)
                leaveLabel.text = actingHost ? "Cancel Lobby" : "Leave Lobby";
            leaveLobbyButton.gameObject.SetActive(!actingHost);
        }

        bool canStart = lobby.IsHost && !lobby.TryGetStartError(out _);
        if (startMatchButton != null)
            startMatchButton.interactable = canStart;

        WireLobbyNavigation();
    }

    private void RebuildPlayerRows(MatchLobby lobby)
    {
        IReadOnlyList<LobbyPlayerInfo> players = lobby.Players;
        int slots = Mathf.Max(lobby.MaxPlayers, players.Count);
        while (playerRows.Count < slots)
            playerRows.Add(LobbyPlayerRow.Create(playerContent));

        for (int i = 0; i < playerRows.Count; i++)
        {
            if (i < players.Count)
            {
                playerRows[i].gameObject.SetActive(true);
                playerRows[i].Bind(players[i]);
            }
            else if (i < slots)
            {
                playerRows[i].gameObject.SetActive(true);
                playerRows[i].BindWaiting();
            }
            else
            {
                playerRows[i].gameObject.SetActive(false);
            }
        }
    }

    private void RefreshPreview(MapDefinition map)
    {
        if (previewNameLabel != null)
            previewNameLabel.text = map != null ? map.DisplayName : string.Empty;
        if (previewDescriptionLabel != null)
            previewDescriptionLabel.text = map != null ? map.Description : string.Empty;
        if (previewImage != null)
        {
            previewImage.sprite = map != null ? map.PreviewImage : null;
            previewImage.color = Color.white;
        }
    }

    private void WireNavigation()
    {
        Selectable firstMode = modeButtons.Count > 0 ? modeButtons[0] : createLobbyButton;
        Selectable lastMode = modeButtons.Count > 0 ? modeButtons[modeButtons.Count - 1] : firstMode;
        for (int i = 0; i < modeButtons.Count; i++)
        {
            Selectable left = i > 0 ? modeButtons[i - 1] : modeButtons[modeButtons.Count - 1];
            Selectable right = i < modeButtons.Count - 1 ? modeButtons[i + 1] : modeButtons[0];
            Selectable up = publicButton;
            Selectable down = mapCards.Count > 0 ? mapCards[0].Button : createLobbyButton;
            MenuUiFactory.SetNav(modeButtons[i], up, down, left, right);
        }

        for (int i = 0; i < mapCards.Count; i++)
        {
            int column = i % 3;
            int row = i / 3;
            Selectable up = row == 0 ? lastMode : mapCards[i - 3].Button;
            Selectable down;
            if (i + 3 < mapCards.Count)
                down = mapCards[i + 3].Button;
            else
                down = createLobbyButton;
            Selectable left = column == 0 ? mapCards[row * 3 + Math.Min(2, mapCards.Count - row * 3 - 1)].Button : mapCards[i - 1].Button;
            Selectable right = column == 2 || i + 1 >= mapCards.Count ? mapCards[row * 3].Button : mapCards[i + 1].Button;
            MenuUiFactory.SetNav(mapCards[i].Button, up, down, left, right);
        }

        Selectable lastMap = mapCards.Count > 0 ? mapCards[mapCards.Count - 1].Button : lastMode;
        Selectable bottomRight = customCancelLobbyButton != null && customCancelLobbyButton.gameObject.activeSelf
            ? customCancelLobbyButton
            : customBackButton;
        MenuUiFactory.SetNav(publicButton, bottomRight, firstMode, localButton, privateButton);
        MenuUiFactory.SetNav(privateButton, bottomRight, firstMode, publicButton, localButton != null && localButton.gameObject.activeSelf ? localButton : publicButton);
        if (localButton != null)
            MenuUiFactory.SetNav(localButton, bottomRight, firstMode, privateButton, publicButton);
        MenuUiFactory.SetNav(createLobbyButton, lastMap, publicButton, bottomRight, bottomRight);
        if (bottomRight != null)
            MenuUiFactory.SetNav(bottomRight, lastMap, publicButton, createLobbyButton, createLobbyButton);
    }

    private void WireLobbyNavigation()
    {
        Selectable start = startMatchButton != null && startMatchButton.gameObject.activeSelf ? startMatchButton : null;
        Selectable change = changeSetupButton != null && changeSetupButton.gameObject.activeSelf ? changeSetupButton : null;
        Selectable cancel = cancelLobbyButton != null && cancelLobbyButton.gameObject.activeSelf ? cancelLobbyButton : null;
        Selectable leave = leaveLobbyButton != null && leaveLobbyButton.gameObject.activeSelf ? leaveLobbyButton : null;
        Selectable copy = copyJoinCodeButton != null && copyJoinCodeButton.gameObject.activeSelf ? copyJoinCodeButton : null;

        if (start != null && change != null && cancel != null)
        {
            Selectable upFromButtons = copy != null ? copy : change;
            MenuUiFactory.SetNav(start, upFromButtons, change, cancel, copy != null ? copy : cancel);
            MenuUiFactory.SetNav(change, start, start, cancel, copy != null ? copy : cancel);
            MenuUiFactory.SetNav(cancel, upFromButtons, change, start, start);
            if (copy != null)
                MenuUiFactory.SetNav(copy, start, start, start, cancel);
            return;
        }

        if (leave != null)
            MenuUiFactory.SetNav(leave, copy != null ? copy : leave, leave, leave, copy != null ? copy : leave);
        if (copy != null && leave != null)
            MenuUiFactory.SetNav(copy, leave, leave, leave, leave);
    }

    private void HandleCustomBack()
    {
        if (MatchLobby.Ensure().IsInLobby)
        {
            if (onShowLobby != null)
                onShowLobby.Invoke();
            else
                OpenLobby(MatchLobby.Ensure().IsHost);
            return;
        }

        onBackFromCustom?.Invoke();
    }

    private void HandleCreateOrReturn()
    {
        if (MatchLobby.Ensure().IsInLobby)
        {
            if (onShowLobby != null)
                onShowLobby.Invoke();
            else
                OpenLobby(MatchLobby.Ensure().IsHost);
            return;
        }

        onCreateLobby?.Invoke();
    }

    private void HandleEditSetup()
    {
        if (!MatchLobby.Ensure().IsHost)
            return;
        if (onEditSetup != null)
            onEditSetup.Invoke();
        else
            OpenCustomMatch();
    }

    private void HandleCancelLobbyFromSetup()
    {
        if (!MatchLobby.Ensure().IsHost)
            return;
        onCancelLobby?.Invoke();
    }

    private static void PlaceButton(Button button, Vector2 position, Vector2 size)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;

        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static string ModeButtonLabel(GameModeDefinition mode)
    {
        if (mode == null)
            return "Mode";
        return mode.IsAvailable ? mode.DisplayName : mode.DisplayName + " — Soon";
    }

    private void RefreshJoinCode()
    {
        string joinCode = ResolveJoinCode();
        bool generating = string.IsNullOrEmpty(joinCode) && IsActingHost(MatchLobby.Ensure());
        if (lobbyJoinCodeHeading != null)
            lobbyJoinCodeHeading.text = "JOIN CODE";
        if (lobbyJoinCodeLabel != null)
        {
            if (!string.IsNullOrEmpty(joinCode))
                lobbyJoinCodeLabel.text = joinCode;
            else if (generating)
                lobbyJoinCodeLabel.text = "Generating join code...";
            else
                lobbyJoinCodeLabel.text = string.Empty;
        }

        if (copyJoinCodeButton != null)
            copyJoinCodeButton.gameObject.SetActive(!string.IsNullOrEmpty(joinCode));
    }

    private void CopyJoinCode()
    {
        string joinCode = ResolveJoinCode();
        if (string.IsNullOrEmpty(joinCode))
            return;

        GUIUtility.systemCopyBuffer = joinCode;
        joinCodeCopiedUntil = Time.unscaledTime + 1.6f;
        if (lobbyJoinCodeCopiedLabel != null)
            lobbyJoinCodeCopiedLabel.enabled = true;
        MultiplayerLog.Info("Join code copied.");
        PlaySelect();
    }

    private static bool IsActingHost(MatchLobby lobby)
    {
        if (lobby != null && lobby.IsHost)
            return true;
        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        return coordinator != null && coordinator.IsPendingHost;
    }

    private static string ResolveJoinCode()
    {
        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        if (coordinator != null && !string.IsNullOrEmpty(coordinator.CurrentJoinCode))
            return coordinator.CurrentJoinCode;
        MultiplayerSessionManager manager = MultiplayerSessionManager.Instance;
        if (manager != null && !string.IsNullOrEmpty(manager.JoinCode))
            return manager.JoinCode;
        return null;
    }

    private void PlaySelect()
    {
        if (menuAudio != null)
            menuAudio.PlaySelect();
    }
}

/// <summary>
/// Local-test flag kept beside the custom-match draft so Host Game can still
/// start a same-machine session without changing MatchVisibility.
/// </summary>
public static class MainMenuHostOptions
{
    public static bool LocalTest;
}
