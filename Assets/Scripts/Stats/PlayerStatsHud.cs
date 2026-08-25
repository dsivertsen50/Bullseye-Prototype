using UnityEngine;

/// <summary>
/// Temporary local-player K/D readout. A later requirement can replace this
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

        var rect = new Rect(24f, 20f, 280f, 64f);
        DrawShadowedLabel(rect, $"Kills: {playerStats.Kills}\nDeaths: {playerStats.Deaths}", GetStatsStyle());
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
    {
        Color previous = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = previous;
        GUI.Label(rect, text, style);
    }

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
}
