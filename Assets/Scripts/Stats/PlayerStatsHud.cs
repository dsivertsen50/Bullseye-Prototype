using UnityEngine;

/// <summary>
/// Temporary local-player match readout. A later requirement can replace this
/// with a full scoreboard. Displays this object's owner stats only.
/// </summary>
public class PlayerStatsHud : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;

    private GUIStyle statsStyle;

    private void Awake()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
    }

    private void OnGUI()
    {
        if (playerStats == null || !playerStats.IsSpawned || !playerStats.IsOwner)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        if (TryGetComponent(out MatchScoreboardController scoreboard) && scoreboard.IsVisible)
            return;

        var rect = new Rect(24f, 20f, 320f, 88f);
        DrawShadowedLabel(
            rect,
            $"Eliminations: {playerStats.Eliminations}\nAssists: {playerStats.Assists}\nDeaths: {playerStats.Deaths}",
            GetStatsStyle());

        var hintRect = new Rect(24f, 110f, 320f, 22f);
        DrawShadowedLabel(hintRect, "F8: Combat Telemetry", GetHintStyle());
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
    {
        Color previous = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = previous;
        GUI.Label(rect, text, style);
    }

    private GUIStyle hintStyle;

    private GUIStyle GetStatsStyle()
    {
        if (statsStyle != null)
            return statsStyle;

        statsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        statsStyle.normal.textColor = Color.white;
        return statsStyle;
    }

    private GUIStyle GetHintStyle()
    {
        if (hintStyle != null)
            return hintStyle;

        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.UpperLeft
        };
        hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        return hintStyle;
    }
}
