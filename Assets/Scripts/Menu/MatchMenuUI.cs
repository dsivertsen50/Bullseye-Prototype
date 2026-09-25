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
    private Text copyJoinCodeLabel;
    private Button copyJoinCodeButton;
    private Text lobbyPlayerCountLabel;
    private Text lobbyErrorLabel;
    private Text lobbyHostHintLabel;
    private float joinCodeCopiedUntil;
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
        UpdateCopyFeedback();
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
        lobbyPanel = CreatePanel("MatchLobbyPanel", new Vector2(1160f, 920f), leftPadding);
        StretchLobbyPanel(lobbyPanel, leftPadding);
        customPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        BuildCustomMatch();
        BuildLobby();
        WireNavigation();
        WireLobbyNavigation();
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

    private void StretchLobbyPanel(GameObject panel, float leftPadding)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = new Vector2(leftPadding, 24f);
        rect.offsetMax = new Vector2(leftPadding + 1160f, -24f);
    }

    private void BuildLobby()
    {
        RectTransform columns = CreateStretchChild(lobbyPanel.transform, "Columns", 28f);
        HorizontalLayoutGroup split = columns.gameObject.AddComponent<HorizontalLayoutGroup>();
        split.spacing = 32f;
        split.childAlignment = TextAnchor.UpperLeft;
        split.childControlWidth = true;
        split.childControlHeight = true;
        split.childForceExpandWidth = true;
        split.childForceExpandHeight = true;

        RectTransform left = CreateLayoutColumn(columns, "LeftColumn", 1.2f, 520f);
        RectTransform right = CreateLayoutColumn(columns, "RightColumn", 0.7f, 400f);
        BuildLobbyLeftColumn(left);
        BuildLobbyRightColumn(right);
    }

    private void BuildLobbyLeftColumn(RectTransform column)
    {
        VerticalLayoutGroup layout = AddVerticalStack(column, 8f);
        layout.childAlignment = TextAnchor.UpperLeft;

        lobbyTitleLabel = CreateStackLabel(column, "Visibility", "PRIVATE LOBBY", 18, 28f, TextAnchor.MiddleLeft);
        lobbyTitleLabel.color = ProfileUiFactory.MutedColor;

        lobbyPreviewImage = MenuUiFactory.CreateImage(column, "Preview", new Color(0.12f, 0.14f, 0.18f, 1f));
        lobbyPreviewImage.preserveAspect = true;
        SetLayoutHeight(lobbyPreviewImage.rectTransform, 200f, 200f);

        lobbyMapNameLabel = CreateStackLabel(column, "MapName", "Map", 26, 36f, TextAnchor.MiddleLeft);
        lobbyMapDescriptionLabel = CreateStackLabel(column, "MapDescription", string.Empty, 16, 72f, TextAnchor.UpperLeft);
        lobbyMapDescriptionLabel.color = ProfileUiFactory.MutedColor;
        lobbyMapDescriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

        CreateStackLabel(column, "ModeHeading", "GAME MODE", 16, 24f, TextAnchor.MiddleLeft).color = ProfileUiFactory.MutedColor;
        lobbyModeLabel = CreateStackLabel(column, "Mode", "Mode", 26, 36f, TextAnchor.MiddleLeft);
        lobbyModeDescriptionLabel = CreateStackLabel(column, "ModeDescription", string.Empty, 16, 64f, TextAnchor.UpperLeft);
        lobbyModeDescriptionLabel.color = ProfileUiFactory.MutedColor;
        lobbyModeDescriptionLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

        RectTransform joinRow = CreateLayoutColumn(column, "JoinRow", 1f, 400f);
        HorizontalLayoutGroup joinLayout = joinRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        joinLayout.spacing = 14f;
        joinLayout.padding = new RectOffset(0, 0, 0, 0);
        joinLayout.childAlignment = TextAnchor.MiddleLeft;
        joinLayout.childControlWidth = true;
        joinLayout.childControlHeight = true;
        joinLayout.childForceExpandWidth = false;
        joinLayout.childForceExpandHeight = false;
        SetLayoutHeight(joinRow, 32f, 400f);

        lobbyJoinCodeHeading = CreateStackLabel(joinRow, "JoinCodeHeading", "JOIN CODE:", 16, 32f, TextAnchor.MiddleLeft);
        lobbyJoinCodeHeading.color = ProfileUiFactory.MutedColor;
        LayoutElement headingLayout = lobbyJoinCodeHeading.GetComponent<LayoutElement>();
        headingLayout.minWidth = 108f;
        headingLayout.preferredWidth = 118f;
        headingLayout.flexibleWidth = 0f;

        lobbyJoinCodeLabel = CreateStackLabel(joinRow, "JoinCode", string.Empty, 22, 32f, TextAnchor.MiddleLeft);
        lobbyJoinCodeLabel.fontStyle = FontStyle.Bold;
        LayoutElement codeLayout = lobbyJoinCodeLabel.GetComponent<LayoutElement>();
        codeLayout.minWidth = 96f;
        codeLayout.preferredWidth = 140f;
        codeLayout.flexibleWidth = 1f;

        copyJoinCodeButton = MenuUiFactory.CreateButton(joinRow, "CopyCode", "Copy Code", Vector2.zero, CopyJoinCode, new Vector2(148f, 32f));
        SetLayoutHeight(copyJoinCodeButton.GetComponent<RectTransform>(), 32f, 148f);
        LayoutElement copyLayout = copyJoinCodeButton.GetComponent<LayoutElement>();
        copyLayout.minWidth = 148f;
        copyLayout.preferredWidth = 148f;
        copyLayout.flexibleWidth = 0f;
        copyJoinCodeLabel = copyJoinCodeButton.GetComponentInChildren<Text>();
        if (copyJoinCodeLabel != null)
            copyJoinCodeLabel.fontSize = 16;
        lobbyHostHintLabel = CreateStackLabel(column, "HostHint", string.Empty, 14, 22f, TextAnchor.MiddleLeft);
        lobbyHostHintLabel.color = ProfileUiFactory.MutedColor;

        cancelLobbyButton = MenuUiFactory.CreateButton(column, "CancelLobby", "Cancel Lobby", Vector2.zero, () => onCancelLobby?.Invoke(), new Vector2(280f, 54f));
        SetLayoutHeight(cancelLobbyButton.GetComponent<RectTransform>(), 54f, 280f);
        leaveLobbyButton = MenuUiFactory.CreateButton(column, "LeaveLobby", "Leave Lobby", Vector2.zero, () => onLeaveLobby?.Invoke(), new Vector2(280f, 54f));
        SetLayoutHeight(leaveLobbyButton.GetComponent<RectTransform>(), 54f, 280f);

        lobbyErrorLabel = CreateStackLabel(column, "Error", string.Empty, 18, 40f, TextAnchor.UpperLeft);
        lobbyErrorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);
        lobbyErrorLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    private void BuildLobbyRightColumn(RectTransform column)
    {
        AddVerticalStack(column, 10f);

        lobbyPlayerCountLabel = CreateStackLabel(column, "PlayerCount", "PLAYERS", 20, 28f, TextAnchor.MiddleLeft);
        playerContent = CreateRosterArea(column);

        changeSetupButton = MenuUiFactory.CreateButton(column, "ChangeSetup", "Change Map / Mode", Vector2.zero, HandleEditSetup, new Vector2(320f, 48f));
        SetLayoutHeight(changeSetupButton.GetComponent<RectTransform>(), 48f, 320f);

        startMatchButton = MenuUiFactory.CreateButton(column, "StartMatch", "Start Match", Vector2.zero, () => onStartMatch?.Invoke(), new Vector2(320f, 88f));
        SetLayoutHeight(startMatchButton.GetComponent<RectTransform>(), 88f, 320f);
        Text startLabel = startMatchButton.GetComponentInChildren<Text>();
        if (startLabel != null)
            startLabel.fontSize = 28;
        ColorBlock colors = startMatchButton.colors;
        colors.normalColor = new Color(0.16f, 0.55f, 0.26f, 1f);
        colors.highlightedColor = new Color(0.24f, 0.78f, 0.36f, 1f);
        colors.selectedColor = new Color(0.24f, 0.78f, 0.36f, 1f);
        colors.pressedColor = new Color(0.12f, 0.42f, 0.2f, 1f);
        startMatchButton.colors = colors;
    }

    private static RectTransform CreateStretchChild(Transform parent, string name, float padding)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
        return rect;
    }

    private static RectTransform CreateLayoutColumn(Transform parent, string name, float flexibleWidth, float preferredWidth)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = flexibleWidth;
        layout.preferredWidth = preferredWidth;
        layout.minWidth = preferredWidth > 160f ? preferredWidth * 0.75f : preferredWidth;
        return rect;
    }

    private static VerticalLayoutGroup AddVerticalStack(RectTransform column, float spacing)
    {
        VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    private static Text CreateStackLabel(Transform parent, string name, string text, int size, float height, TextAnchor alignment)
    {
        Text label = MenuUiFactory.CreateLabel(parent, name, text, size, Vector2.zero, new Vector2(100f, height), alignment);
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        SetLayoutHeight(label.rectTransform, height, 40f);
        return label;
    }

    private static void SetLayoutHeight(RectTransform rect, float height, float preferredWidth)
    {
        LayoutElement layout = rect.GetComponent<LayoutElement>();
        if (layout == null)
            layout = rect.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        if (preferredWidth > 0f)
        {
            layout.minWidth = Mathf.Min(preferredWidth, 120f);
            layout.preferredWidth = preferredWidth;
        }
    }

    private static Transform CreateRosterArea(Transform parent)
    {
        const float rowHeight = 36f;
        const float rowSpacing = 4f;
        const float headerHeight = 26f;
        const int visibleSlots = 8;
        float rowsHeight = visibleSlots * rowHeight + (visibleSlots - 1) * rowSpacing;
        float height = headerHeight + rowsHeight + 20f;

        Image root = MenuUiFactory.CreateImage(parent, "Players", new Color(0.04f, 0.05f, 0.07f, 0.4f));
        SetLayoutHeight(root.rectTransform, height, 360f);
        LayoutElement rootLayout = root.GetComponent<LayoutElement>();
        rootLayout.flexibleHeight = 0f;
        rootLayout.flexibleWidth = 1f;

        VerticalLayoutGroup stack = root.gameObject.AddComponent<VerticalLayoutGroup>();
        stack.spacing = 4f;
        stack.padding = new RectOffset(12, 12, 8, 8);
        stack.childAlignment = TextAnchor.UpperLeft;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;

        RectTransform header = new GameObject("Header", typeof(RectTransform)).GetComponent<RectTransform>();
        header.SetParent(root.transform, false);
        HorizontalLayoutGroup headerRow = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerRow.childAlignment = TextAnchor.MiddleLeft;
        headerRow.childControlWidth = true;
        headerRow.childControlHeight = true;
        headerRow.childForceExpandWidth = false;
        headerRow.childForceExpandHeight = true;
        LayoutElement headerLayout = header.gameObject.AddComponent<LayoutElement>();
        headerLayout.minHeight = headerHeight;
        headerLayout.preferredHeight = headerHeight;
        headerLayout.flexibleWidth = 1f;

        Text playerHeader = MenuUiFactory.CreateLabel(header, "PlayerHeader", "Player", 14, Vector2.zero, new Vector2(160f, headerHeight), TextAnchor.MiddleLeft);
        playerHeader.color = ProfileUiFactory.MutedColor;
        playerHeader.horizontalOverflow = HorizontalWrapMode.Overflow;
        LayoutElement playerHeaderLayout = playerHeader.gameObject.AddComponent<LayoutElement>();
        playerHeaderLayout.minWidth = 80f;
        playerHeaderLayout.flexibleWidth = 1f;
        playerHeaderLayout.preferredHeight = headerHeight;

        Text statusHeader = MenuUiFactory.CreateLabel(header, "StatusHeader", "Status", 14, Vector2.zero, new Vector2(88f, headerHeight), TextAnchor.MiddleRight);
        statusHeader.color = ProfileUiFactory.MutedColor;
        statusHeader.horizontalOverflow = HorizontalWrapMode.Overflow;
        LayoutElement statusHeaderLayout = statusHeader.gameObject.AddComponent<LayoutElement>();
        statusHeaderLayout.minWidth = 72f;
        statusHeaderLayout.preferredWidth = 88f;
        statusHeaderLayout.preferredHeight = headerHeight;

        RectTransform content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(root.transform, false);
        VerticalLayoutGroup rows = content.gameObject.AddComponent<VerticalLayoutGroup>();
        rows.spacing = rowSpacing;
        rows.padding = new RectOffset(0, 0, 0, 0);
        rows.childAlignment = TextAnchor.UpperLeft;
        rows.childControlWidth = true;
        rows.childControlHeight = true;
        rows.childForceExpandWidth = true;
        rows.childForceExpandHeight = false;
        LayoutElement contentLayout = content.gameObject.AddComponent<LayoutElement>();
        contentLayout.minHeight = rowsHeight;
        contentLayout.preferredHeight = rowsHeight;
        contentLayout.flexibleWidth = 1f;
        contentLayout.flexibleHeight = 0f;
        return content;
    }

    private void BuildModeButtons(Transform parent, float y)
    {
        modeButtons.Clear();
        int count = modeCatalog != null ? modeCatalog.Count : 0;
        const float buttonWidth = 210f;
        const float step = 220f;
        float startX = count > 0 ? -((count - 1) * step) * 0.5f : 0f;
        for (int i = 0; i < count; i++)
        {
            GameModeDefinition mode = modeCatalog.Get(i);
            if (mode == null)
                continue;
            Button button = MenuUiFactory.CreateButton(
                parent,
                mode.GameModeId,
                ModeButtonLabel(mode),
                new Vector2(startX + i * step, y),
                () => SelectMode(mode.GameModeId),
                new Vector2(buttonWidth, 56f));
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = 16;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 12;
                label.resizeTextMaxSize = 16;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }

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
            lobbyTitleLabel.text = config.Visibility == MatchVisibility.Public ? "PUBLIC LOBBY" : "PRIVATE LOBBY";
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
        Selectable firstMap = mapCards.Count > 0 ? mapCards[0].Button : createLobbyButton;
        for (int i = 0; i < modeButtons.Count; i++)
        {
            Selectable left = i > 0 ? modeButtons[i - 1] : modeButtons[modeButtons.Count - 1];
            Selectable right = i < modeButtons.Count - 1 ? modeButtons[i + 1] : modeButtons[0];
            MenuUiFactory.SetNav(modeButtons[i], publicButton, firstMap, left, right);
        }

        const int mapColumns = 3;
        for (int i = 0; i < mapCards.Count; i++)
        {
            int column = i % mapColumns;
            int row = i / mapColumns;
            int rowStart = row * mapColumns;
            int nextRowStart = rowStart + mapColumns;
            Selectable up = firstMode;
            Selectable down = nextRowStart < mapCards.Count ? mapCards[nextRowStart].Button : createLobbyButton;
            int rowCount = Math.Min(mapColumns, mapCards.Count - rowStart);
            Selectable left = column == 0 ? mapCards[rowStart + rowCount - 1].Button : mapCards[i - 1].Button;
            Selectable right = column == rowCount - 1 ? mapCards[rowStart].Button : mapCards[i + 1].Button;
            MenuUiFactory.SetNav(mapCards[i].Button, up, down, left, right);
        }

        Selectable lastMap = mapCards.Count > 0 ? mapCards[((mapCards.Count - 1) / 3) * 3].Button : firstMode;
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
        Selectable start = UsableLobbySelectable(startMatchButton);
        Selectable change = UsableLobbySelectable(changeSetupButton);
        Selectable cancel = UsableLobbySelectable(cancelLobbyButton);
        Selectable leave = UsableLobbySelectable(leaveLobbyButton);
        Selectable copy = UsableLobbySelectable(copyJoinCodeButton);
        Selectable leftEdge = cancel != null ? cancel : leave;
        Selectable rightPrimary = change != null ? change : start;

        if (copy != null && leftEdge != null)
            MenuUiFactory.SetNav(copy, rightPrimary != null ? rightPrimary : leftEdge, leftEdge, leftEdge, rightPrimary != null ? rightPrimary : leftEdge);

        if (start != null && change != null && cancel != null)
        {
            MenuUiFactory.SetNav(change, start, start, copy != null ? copy : cancel, start);
            MenuUiFactory.SetNav(start, change, change, cancel, change);
            MenuUiFactory.SetNav(cancel, copy != null ? copy : start, start, start, change);
            return;
        }

        if (leave != null)
            MenuUiFactory.SetNav(leave, copy != null ? copy : leave, leave, leave, copy != null ? copy : leave);
    }

    private static Selectable UsableLobbySelectable(Selectable selectable)
    {
        if (selectable == null || !selectable.gameObject.activeSelf)
            return null;
        return selectable;
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
            lobbyJoinCodeHeading.text = "JOIN CODE:";
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

        if (lobbyPanel != null && lobbyPanel.activeSelf)
            WireLobbyNavigation();
    }

    private void CopyJoinCode()
    {
        string joinCode = ResolveJoinCode();
        if (string.IsNullOrEmpty(joinCode))
            return;

        GUIUtility.systemCopyBuffer = joinCode;
        joinCodeCopiedUntil = Time.unscaledTime + 1.6f;
        UpdateCopyFeedback();
        MultiplayerLog.Info("Join code copied.");
        PlaySelect();
    }

    private void UpdateCopyFeedback()
    {
        if (copyJoinCodeLabel == null)
            return;

        bool copied = Time.unscaledTime < joinCodeCopiedUntil;
        string next = copied ? "Copied!" : "Copy Code";
        if (copyJoinCodeLabel.text == next)
            return;

        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool keepSelection = copyJoinCodeButton != null && selected == copyJoinCodeButton.gameObject;
        copyJoinCodeLabel.text = next;
        if (keepSelection && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(copyJoinCodeButton.gameObject);
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
