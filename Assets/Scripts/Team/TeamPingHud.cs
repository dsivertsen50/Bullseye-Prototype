using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local screen-space warning markers. Renders through world geometry and
/// only ever displays pings this client was authorized to receive.
/// </summary>
[DefaultExecutionOrder(200)]
public class TeamPingHud : MonoBehaviour
{
    private const int CanvasSortOrder = 18;
    private const float MarkerSize = 54f;
    private const float CrosshairClearance = 38f;

    private static readonly Color LocationColor = new Color(1f, 0.82f, 0.18f, 1f);
    private static readonly Color EnemyColor = new Color(1f, 0.28f, 0.18f, 1f);

    private readonly Dictionary<ulong, VisiblePing> pings = new();
    private readonly Dictionary<ulong, MarkerWidget> widgets = new();
    private readonly List<ulong> expiredOwners = new(8);
    private readonly Dictionary<ulong, ulong> preferredEnemyOwner = new();

    private Canvas canvas;
    private RectTransform canvasRect;
    private Font font;

    public static TeamPingHud Instance { get; private set; }

    public static TeamPingHud Ensure()
    {
        if (Instance != null)
            return Instance;

        var go = new GameObject("TeamPingHud");
        Instance = go.AddComponent<TeamPingHud>();
        return Instance;
    }

    public struct VisiblePing
    {
        public ulong OwnerClientId;
        public TeamPingKind Kind;
        public Vector3 WorldPosition;
        public ulong TargetNetworkObjectId;
        public double ExpireServerTime;
        public int TeamId;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureCanvas();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Upsert(VisiblePing ping)
    {
        pings[ping.OwnerClientId] = ping;
    }

    public void RemoveOwner(ulong ownerClientId)
    {
        pings.Remove(ownerClientId);
        if (widgets.TryGetValue(ownerClientId, out MarkerWidget widget))
        {
            if (widget.root != null)
                Destroy(widget.root.gameObject);
            widgets.Remove(ownerClientId);
        }
    }

    public void ClearAll()
    {
        pings.Clear();
        foreach (var pair in widgets)
        {
            if (pair.Value.root != null)
                Destroy(pair.Value.root.gameObject);
        }

        widgets.Clear();
    }

    private void LateUpdate()
    {
        EnsureCanvas();
        PruneExpired();

        bool hide = ShouldHide();
        RebuildEnemyPreference();

        foreach (var pair in widgets)
            SetWidgetVisible(pair.Value, false);

        if (hide)
            return;

        Camera camera = PlayerNetworkSetup.LocalOwnedCamera;
        if (camera == null)
            camera = Camera.main;
        if (camera == null)
            return;

        foreach (var pair in pings)
        {
            VisiblePing ping = pair.Value;
            if (!TryResolveWorldPosition(ping, out Vector3 worldPosition))
                continue;

            if (ping.Kind == TeamPingKind.Enemy &&
                ping.TargetNetworkObjectId != 0 &&
                preferredEnemyOwner.TryGetValue(ping.TargetNetworkObjectId, out ulong chosenOwner) &&
                chosenOwner != ping.OwnerClientId)
            {
                continue;
            }

            if (!TryProject(camera, worldPosition, out Vector2 screenPoint))
                continue;

            MarkerWidget widget = GetOrCreateWidget(ping.OwnerClientId);
            ApplyWidget(widget, ping, screenPoint);
            SetWidgetVisible(widget, true);
        }
    }

    private void PruneExpired()
    {
        expiredOwners.Clear();
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening)
        {
            if (pings.Count > 0)
                ClearAll();
            return;
        }

        double now = networkManager.ServerTime.Time;
        foreach (var pair in pings)
        {
            if (now >= pair.Value.ExpireServerTime || !IsClientConnected(networkManager, pair.Key))
                expiredOwners.Add(pair.Key);
        }

        for (int i = 0; i < expiredOwners.Count; i++)
            RemoveOwner(expiredOwners[i]);
    }

    private void RebuildEnemyPreference()
    {
        preferredEnemyOwner.Clear();
        foreach (var pair in pings)
        {
            VisiblePing ping = pair.Value;
            if (ping.Kind != TeamPingKind.Enemy || ping.TargetNetworkObjectId == 0)
                continue;

            if (!preferredEnemyOwner.TryGetValue(ping.TargetNetworkObjectId, out ulong currentOwner))
            {
                preferredEnemyOwner[ping.TargetNetworkObjectId] = ping.OwnerClientId;
                continue;
            }

            if (pings.TryGetValue(currentOwner, out VisiblePing current) &&
                ping.ExpireServerTime <= current.ExpireServerTime)
            {
                continue;
            }

            preferredEnemyOwner[ping.TargetNetworkObjectId] = ping.OwnerClientId;
        }
    }

    private static bool IsClientConnected(NetworkManager networkManager, ulong clientId)
    {
        IReadOnlyList<ulong> clients = networkManager.ConnectedClientsIds;
        for (int i = 0; i < clients.Count; i++)
        {
            if (clients[i] == clientId)
                return true;
        }

        return false;
    }

    private bool ShouldHide()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        NetworkObject localPlayer = NetworkManager.Singleton.SpawnManager != null
            ? NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject()
            : null;
        return localPlayer != null && LocalPlayerMenuState.IsOpen(localPlayer);
    }

    private bool TryResolveWorldPosition(VisiblePing ping, out Vector3 worldPosition)
    {
        worldPosition = ping.WorldPosition;
        if (ping.Kind != TeamPingKind.Enemy || ping.TargetNetworkObjectId == 0)
            return true;

        if (!TryGetSpawnedObject(ping.TargetNetworkObjectId, out NetworkObject target) || target == null)
            return false;

        if (target.TryGetComponent(out PlayerHealth health) && health.IsDead)
            return false;

        worldPosition = ResolveFollowPoint(target);
        return true;
    }

    private static Vector3 ResolveFollowPoint(NetworkObject target)
    {
        float height = 1.85f;
        CapsuleCollider capsule = target.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
            height = Mathf.Max(1.2f, capsule.height * capsule.transform.lossyScale.y * 0.55f);

        return target.transform.position + Vector3.up * height;
    }

    private bool TryProject(Camera camera, Vector3 worldPosition, out Vector2 screenPoint)
    {
        screenPoint = default;
        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
        if (viewport.z <= 0.05f)
            return false;

        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        screen.y += CrosshairClearance;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screen,
            null,
            out screenPoint);
        return true;
    }

    private void ApplyWidget(MarkerWidget widget, VisiblePing ping, Vector2 screenPoint)
    {
        widget.root.anchoredPosition = screenPoint;
        bool enemy = ping.Kind == TeamPingKind.Enemy;
        widget.symbol.color = enemy ? EnemyColor : LocationColor;
        widget.dot.color = enemy ? EnemyColor : LocationColor;
        widget.label.text = enemy ? "ENEMY" : string.Empty;
        widget.label.color = enemy ? EnemyColor : LocationColor;
        widget.label.enabled = enemy;
        widget.dot.enabled = !enemy;
    }

    private MarkerWidget GetOrCreateWidget(ulong ownerClientId)
    {
        if (widgets.TryGetValue(ownerClientId, out MarkerWidget existing) && existing.root != null)
            return existing;

        var root = new GameObject("Ping_" + ownerClientId, typeof(RectTransform));
        root.transform.SetParent(canvasRect, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(MarkerSize, 72f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.35f);

        var widget = new MarkerWidget
        {
            root = rect,
            symbol = CreateLabel(rect, "Symbol", "!", 42, new Vector2(0f, 16f), new Vector2(MarkerSize, 48f)),
            label = CreateLabel(rect, "Label", string.Empty, 14, new Vector2(0f, -18f), new Vector2(90f, 20f)),
            dot = CreateDot(rect)
        };
        widgets[ownerClientId] = widget;
        return widget;
    }

    private static void SetWidgetVisible(MarkerWidget widget, bool visible)
    {
        if (widget.root != null && widget.root.gameObject.activeSelf != visible)
            widget.root.gameObject.SetActive(visible);
    }

    private Text CreateLabel(RectTransform parent, string name, string text, int size, Vector2 anchored, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchored;
        rect.sizeDelta = sizeDelta;

        Text label = go.GetComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = LocationColor;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    private Image CreateDot(RectTransform parent)
    {
        var go = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -6f);
        rect.sizeDelta = new Vector2(10f, 10f);

        Image image = go.GetComponent<Image>();
        image.sprite = UiWhiteSprite.Get();
        image.color = LocationColor;
        image.raycastTarget = false;
        return image;
    }

    private void EnsureCanvas()
    {
        if (canvas != null)
            return;

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont("Arial", 42);

        var canvasObject = new GameObject(
            "TeamPingCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;
        canvasRect = canvas.GetComponent<RectTransform>();

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GraphicRaycaster raycaster = canvasObject.GetComponent<GraphicRaycaster>();
        raycaster.enabled = false;
    }

    private static bool TryGetSpawnedObject(ulong networkObjectId, out NetworkObject networkObject)
    {
        networkObject = null;
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null)
            return false;

        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out networkObject)
               && networkObject != null
               && networkObject.IsSpawned;
    }

    private struct MarkerWidget
    {
        public RectTransform root;
        public Text symbol;
        public Text label;
        public Image dot;
    }
}
