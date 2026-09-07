using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Owner-only hold-to-view current-match scoreboard. Does not pause gameplay,
/// change Time.timeScale, or alter cursor lock.
/// </summary>
public class MatchScoreboardController : NetworkBehaviour
{
    private const int CanvasSortOrder = 80;
    private const float RosterPollInterval = 0.25f;

    [SerializeField] private LocalPlayerMenuState menuState;
    [SerializeField] private InputActionReference scoreboardAction;

    private readonly List<MatchScoreboardRow> rows = new(16);
    private readonly List<PlayerStats> subscribedStats = new(16);
    private readonly List<ScoreboardRowWidgets> rowWidgets = new(16);

    private LocalPlayerInputBinding inputBinding;
    private InputAction resolvedScoreboardAction;
    private Canvas canvas;
    private Transform rowParent;
    private bool ownerEnabled;
    private bool built;
    private bool visible;
    private bool networkCallbacksBound;
    private int lastPlayerCount = -1;
    private float nextRosterCheck;

    public bool IsVisible => visible;

    public override void OnNetworkSpawn()
    {
        if (menuState == null)
            menuState = GetComponent<LocalPlayerMenuState>();
        TryGetComponent(out inputBinding);

        ownerEnabled = IsOwner;
        if (!ownerEnabled)
        {
            enabled = false;
            return;
        }

        EnsureUi();
        BindScoreboardAction();
        Hide();
    }

    public override void OnNetworkDespawn()
    {
        Hide();
        UnbindScoreboardAction();
        UnbindNetworkCallbacks();
        ownerEnabled = false;
    }

    private void OnEnable()
    {
        if (ownerEnabled)
            BindScoreboardAction();
    }

    private void OnDisable()
    {
        UnbindScoreboardAction();
        if (visible)
            Hide();
    }

    private void Update()
    {
        if (!ownerEnabled)
            return;

        if (visible && IsPauseMenuOpen())
        {
            Hide();
            return;
        }

        if (!visible)
            return;

        if (Time.unscaledTime < nextRosterCheck)
            return;

        nextRosterCheck = Time.unscaledTime + RosterPollInterval;
        int playerCount = CurrentMatchStatistics.CountConnectedPlayers();
        if (playerCount == lastPlayerCount)
            return;

        BindStatsListeners();
        RefreshRows();
    }

    public void ForceHide()
    {
        Hide();
    }

    private void Show()
    {
        if (!ownerEnabled || visible || IsPauseMenuOpen())
            return;

        EnsureUi();
        BindNetworkCallbacks();
        BindStatsListeners();
        RefreshRows();

        if (canvas != null)
            canvas.gameObject.SetActive(true);

        visible = true;
        nextRosterCheck = Time.unscaledTime + RosterPollInterval;
    }

    private void Hide()
    {
        visible = false;
        UnbindNetworkCallbacks();
        UnbindStatsListeners();

        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    private bool IsPauseMenuOpen()
    {
        return menuState != null && menuState.IsMenuOpen;
    }

    private InputAction ScoreboardInput
    {
        get
        {
            if (resolvedScoreboardAction != null)
                return resolvedScoreboardAction;

            if (scoreboardAction != null && scoreboardAction.action != null)
            {
                resolvedScoreboardAction = scoreboardAction.action;
                return resolvedScoreboardAction;
            }

            if (inputBinding != null && inputBinding.PlayerActions != null)
                resolvedScoreboardAction = inputBinding.PlayerActions.FindAction("Scoreboard");

            return resolvedScoreboardAction;
        }
    }

    private void BindScoreboardAction()
    {
        InputAction action = ScoreboardInput;
        if (action == null)
            return;

        action.started -= OnScoreboardStarted;
        action.canceled -= OnScoreboardCanceled;
        action.started += OnScoreboardStarted;
        action.canceled += OnScoreboardCanceled;
        action.Enable();
    }

    private void UnbindScoreboardAction()
    {
        if (resolvedScoreboardAction == null)
            return;

        resolvedScoreboardAction.started -= OnScoreboardStarted;
        resolvedScoreboardAction.canceled -= OnScoreboardCanceled;
    }

    private void OnScoreboardStarted(InputAction.CallbackContext context)
    {
        if (!ownerEnabled || !context.started)
            return;

        Show();
    }

    private void OnScoreboardCanceled(InputAction.CallbackContext context)
    {
        Hide();
    }

    private void BindNetworkCallbacks()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkCallbacksBound)
            return;

        networkManager.OnClientConnectedCallback += OnClientRosterChanged;
        networkManager.OnClientDisconnectCallback += OnClientRosterChanged;
        networkCallbacksBound = true;
    }

    private void UnbindNetworkCallbacks()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkCallbacksBound)
        {
            networkManager.OnClientConnectedCallback -= OnClientRosterChanged;
            networkManager.OnClientDisconnectCallback -= OnClientRosterChanged;
        }

        networkCallbacksBound = false;
    }

    private void OnClientRosterChanged(ulong clientId)
    {
        if (!visible)
            return;

        BindStatsListeners();
        RefreshRows();
    }

    private void BindStatsListeners()
    {
        UnbindStatsListeners();

        PlayerStats[] players = FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude);
        for (int i = 0; i < players.Length; i++)
        {
            PlayerStats stats = players[i];
            if (stats == null || !stats.IsSpawned)
                continue;

            stats.StatsChanged += OnAnyStatsChanged;
            subscribedStats.Add(stats);
        }
    }

    private void UnbindStatsListeners()
    {
        for (int i = 0; i < subscribedStats.Count; i++)
        {
            if (subscribedStats[i] != null)
                subscribedStats[i].StatsChanged -= OnAnyStatsChanged;
        }

        subscribedStats.Clear();
    }

    private void OnAnyStatsChanged()
    {
        if (visible)
            RefreshRows();
    }

    private void RefreshRows()
    {
        ulong localClientId = NetworkManager != null ? NetworkManager.LocalClientId : OwnerClientId;
        CurrentMatchStatistics.CollectSortedRows(rows, localClientId);
        lastPlayerCount = rows.Count;
        EnsureRowWidgets(rows.Count);

        for (int i = 0; i < rowWidgets.Count; i++)
        {
            if (i < rows.Count)
            {
                rowWidgets[i].Root.SetActive(true);
                ApplyRow(rowWidgets[i], rows[i]);
            }
            else
            {
                rowWidgets[i].Root.SetActive(false);
            }
        }
    }

    private void EnsureRowWidgets(int count)
    {
        EnsureUi();
        while (rowWidgets.Count < count)
            rowWidgets.Add(CreateRow(rowWidgets.Count));
    }

    private void ApplyRow(ScoreboardRowWidgets widgets, MatchScoreboardRow row)
    {
        widgets.Placement.text = row.Placement.ToString();
        widgets.Name.text = row.DisplayName;
        widgets.Eliminations.text = row.Eliminations.ToString();
        widgets.Assists.text = row.Assists.ToString();
        widgets.Deaths.text = row.Deaths.ToString();

        Color nameColor = row.IsLocalPlayer
            ? new Color(0.62f, 1f, 0.7f, 1f)
            : Color.white;
        widgets.Name.fontStyle = row.IsLocalPlayer ? FontStyle.Bold : FontStyle.Normal;
        widgets.Name.color = nameColor;
        widgets.Placement.color = nameColor;
        widgets.Eliminations.color = Color.white;
        widgets.Assists.color = Color.white;
        widgets.Deaths.color = Color.white;

        int stripe = widgets.Root.transform.GetSiblingIndex();
        widgets.Highlight.color = row.IsLocalPlayer
            ? new Color(0.22f, 0.72f, 0.34f, 0.28f)
            : new Color(1f, 1f, 1f, stripe % 2 == 0 ? 0.04f : 0.08f);
    }

    private void EnsureUi()
    {
        if (built)
            return;

        GameObject canvasObject = new GameObject("MatchScoreboardCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image panel = CreateImage(canvasObject.transform, "ScoreboardPanel", new Color(0.07f, 0.08f, 0.11f, 0.88f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1080f, 560f);
        panelRect.anchoredPosition = new Vector2(0f, 36f);

        CreateColumnLabel(panel.transform, "Title", "SCOREBOARD", 36, new Vector2(0f, 232f), new Vector2(640f, 48f), TextAnchor.MiddleCenter);

        Image headerBar = CreateImage(panel.transform, "ColumnHeaders", new Color(0.12f, 0.14f, 0.18f, 0.92f));
        headerBar.rectTransform.sizeDelta = new Vector2(1000f, 40f);
        headerBar.rectTransform.anchoredPosition = new Vector2(0f, 178f);

        CreateColumnLabel(headerBar.transform, "PlacementHeader", "#", 18, ColumnPosition(0), ColumnSize(0), TextAnchor.MiddleCenter);
        CreateColumnLabel(headerBar.transform, "PlayerHeader", "PLAYER", 18, ColumnPosition(1), ColumnSize(1), TextAnchor.MiddleLeft);
        CreateColumnLabel(headerBar.transform, "EliminationsHeader", "ELIMINATIONS", 18, ColumnPosition(2), ColumnSize(2), TextAnchor.MiddleCenter);
        CreateColumnLabel(headerBar.transform, "AssistsHeader", "ASSISTS", 18, ColumnPosition(3), ColumnSize(3), TextAnchor.MiddleCenter);
        CreateColumnLabel(headerBar.transform, "DeathsHeader", "DEATHS", 18, ColumnPosition(4), ColumnSize(4), TextAnchor.MiddleCenter);

        GameObject rowsObject = new GameObject("PlayerRows", typeof(RectTransform));
        rowsObject.transform.SetParent(panel.transform, false);
        RectTransform rowsRect = rowsObject.GetComponent<RectTransform>();
        rowsRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowsRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowsRect.pivot = new Vector2(0.5f, 0.5f);
        rowsRect.sizeDelta = new Vector2(1000f, 400f);
        rowsRect.anchoredPosition = new Vector2(0f, -42f);
        rowParent = rowsRect;

        built = true;
        canvasObject.SetActive(false);
    }

    private ScoreboardRowWidgets CreateRow(int index)
    {
        Image background = CreateImage(rowParent, "ScoreboardPlayerRow" + index, new Color(1f, 1f, 1f, 0.06f));
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(1000f, 44f);
        rect.anchoredPosition = new Vector2(0f, -index * 46f);

        return new ScoreboardRowWidgets
        {
            Root = background.gameObject,
            Highlight = background,
            Placement = CreateColumnLabel(background.transform, "Placement", "1", 22, ColumnPosition(0), ColumnSize(0), TextAnchor.MiddleCenter),
            Name = CreateColumnLabel(background.transform, "Name", "Player", 22, ColumnPosition(1), ColumnSize(1), TextAnchor.MiddleLeft),
            Eliminations = CreateColumnLabel(background.transform, "Eliminations", "0", 22, ColumnPosition(2), ColumnSize(2), TextAnchor.MiddleCenter),
            Assists = CreateColumnLabel(background.transform, "Assists", "0", 22, ColumnPosition(3), ColumnSize(3), TextAnchor.MiddleCenter),
            Deaths = CreateColumnLabel(background.transform, "Deaths", "0", 22, ColumnPosition(4), ColumnSize(4), TextAnchor.MiddleCenter)
        };
    }

    private static Vector2 ColumnPosition(int column)
    {
        switch (column)
        {
            case 0: return new Vector2(-450f, 0f);
            case 1: return new Vector2(-250f, 0f);
            case 2: return new Vector2(80f, 0f);
            case 3: return new Vector2(280f, 0f);
            default: return new Vector2(450f, 0f);
        }
    }

    private static Vector2 ColumnSize(int column)
    {
        switch (column)
        {
            case 0: return new Vector2(70f, 36f);
            case 1: return new Vector2(280f, 36f);
            case 2: return new Vector2(180f, 36f);
            case 3: return new Vector2(140f, 36f);
            default: return new Vector2(140f, 36f);
        }
    }

    private static Text CreateColumnLabel(Transform parent, string name, string text, int size, Vector2 position, Vector2 sizeDelta, TextAnchor alignment)
    {
        Text label = MenuUiFactory.CreateLabel(parent, name, text, size, position, sizeDelta, alignment);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        Image image = MenuUiFactory.CreateImage(parent, name, color);
        image.raycastTarget = false;
        return image;
    }

    private sealed class ScoreboardRowWidgets
    {
        public GameObject Root;
        public Image Highlight;
        public Text Placement;
        public Text Name;
        public Text Eliminations;
        public Text Assists;
        public Text Deaths;
    }
}
