using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Host-facing join-code banner for Relay sessions.
/// </summary>
public class OnlineMatchHud : MonoBehaviour
{
    private Canvas canvas;
    private Text codeLabel;
    private Text titleLabel;
    private Text copiedLabel;
    private float copiedUntil;

    public static OnlineMatchHud Ensure()
    {
        OnlineMatchHud existing = FindAnyObjectByType<OnlineMatchHud>();
        if (existing != null)
            return existing;

        GameObject host = new GameObject("OnlineMatchHud");
        OnlineMatchHud hud = host.AddComponent<OnlineMatchHud>();
        DontDestroyOnLoad(host);
        return hud;
    }

    private void Update()
    {
        string joinCode = ResolveJoinCode(out bool isLocal);
        bool show = !string.IsNullOrEmpty(joinCode);
        if (canvas != null)
            canvas.gameObject.SetActive(show);
        if (!show)
            return;

        EnsureUi();
        if (codeLabel != null)
            codeLabel.text = "JOIN CODE  " + joinCode;
        if (titleLabel != null)
            titleLabel.text = isLocal ? "LOCAL MATCH" : "ONLINE MATCH";
        if (copiedLabel != null)
            copiedLabel.enabled = Time.unscaledTime < copiedUntil;
    }

    private static string ResolveJoinCode(out bool isLocal)
    {
        isLocal = false;
        MultiplayerSessionManager manager = MultiplayerSessionManager.Instance;
        if (manager != null &&
            manager.IsRelaySession &&
            manager.IsHostSession &&
            !string.IsNullOrEmpty(manager.JoinCode))
        {
            return manager.JoinCode;
        }

        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        NetworkManager networkManager = NetworkManager.Singleton;
        if (coordinator == null ||
            coordinator.ActiveSession == null ||
            networkManager == null ||
            !networkManager.IsHost)
        {
            return null;
        }

        if (coordinator.ActiveSession.ConnectionMode != MultiplayerConnectionMode.Local)
            return null;

        isLocal = true;
        return coordinator.ActiveSession.JoinCode;
    }

    private void CopyCode()
    {
        string joinCode = ResolveJoinCode(out _);
        if (string.IsNullOrEmpty(joinCode))
            return;

        GUIUtility.systemCopyBuffer = joinCode;
        copiedUntil = Time.unscaledTime + 1.6f;
        MultiplayerLog.Info("Join code copied.");
    }

    public void Show()
    {
        EnsureUi();
        if (canvas != null)
            canvas.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (canvas != null)
            canvas.gameObject.SetActive(false);
    }

    private void EnsureUi()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject("OnlineMatchCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image panel = MenuUiFactory.CreateImage(canvasObject.transform, "Banner", new Color(0.06f, 0.07f, 0.1f, 0.78f));
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(520f, 92f);
        rect.anchoredPosition = new Vector2(0f, -16f);

        titleLabel = MenuUiFactory.CreateLabel(panel.transform, "Title", "ONLINE MATCH", 16, new Vector2(0f, 28f), new Vector2(500f, 22f));
        codeLabel = MenuUiFactory.CreateLabel(panel.transform, "Code", "JOIN CODE", 26, new Vector2(-70f, -2f), new Vector2(320f, 36f));
        Button copy = MenuUiFactory.CreateButton(panel.transform, "Copy", "Copy", new Vector2(180f, -8f), CopyCode, new Vector2(120f, 40f));
        copy.GetComponentInChildren<Text>().fontSize = 18;
        copiedLabel = MenuUiFactory.CreateLabel(panel.transform, "Copied", "Copied", 16, new Vector2(180f, -36f), new Vector2(140f, 20f));
        copiedLabel.enabled = false;
    }
}
