using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Optional developer overlay for Relay testing. Toggle with F9.
/// </summary>
public class OnlineDebugOverlay : MonoBehaviour
{
    public const Key ToggleKey = Key.F9;

    [SerializeField] private bool visible;

    private GUIStyle style;
    private bool keyWasDown;

    public static OnlineDebugOverlay Ensure()
    {
        OnlineDebugOverlay existing = FindAnyObjectByType<OnlineDebugOverlay>();
        if (existing != null)
            return existing;

        GameObject host = new GameObject("OnlineDebugOverlay");
        OnlineDebugOverlay overlay = host.AddComponent<OnlineDebugOverlay>();
        DontDestroyOnLoad(host);
        return overlay;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        bool down = keyboard[ToggleKey].isPressed;
        if (down && !keyWasDown)
            visible = !visible;
        keyWasDown = down;
    }

    private void OnGUI()
    {
        MultiplayerSessionManager manager = MultiplayerSessionManager.Instance;
        if (!visible || manager == null || manager.ConnectionMode != MultiplayerConnectionMode.Relay)
            return;

        LocalPlayerMenuState[] menus = FindObjectsByType<LocalPlayerMenuState>(FindObjectsSortMode.None);
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] != null && menus[i].IsOwner && menus[i].IsMenuOpen)
                return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                richText = false
            };
            style.normal.textColor = Color.white;
        }

        string text = manager.BuildDebugText() + "\nF9 hide overlay";
        GUI.Box(new Rect(12f, 12f, 360f, 280f), text, style);
    }
}
