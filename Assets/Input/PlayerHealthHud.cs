using UnityEngine;

public class PlayerHealthHud : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;

    private GUIStyle countdownStyle;
    private GUIStyle countdownCaptionStyle;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnGUI()
    {
        if (playerHealth == null || !playerHealth.IsSpawned || !playerHealth.IsOwner)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        DrawRespawnCountdown();
    }

    private void DrawRespawnCountdown()
    {
        if (!playerHealth.IsDead)
            return;

        int number = playerHealth.GetRespawnCountdownNumber();
        if (number <= 0)
            return;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.42f;
        var captionRect = new Rect(centerX - 320f, centerY - 90f, 640f, 48f);
        var numberRect = new Rect(centerX - 160f, centerY - 40f, 320f, 180f);

        DrawShadowedLabel(captionRect, "RESPAWNING IN", GetCountdownCaptionStyle());
        DrawShadowedLabel(numberRect, number.ToString(), GetCountdownStyle());
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
    {
        Color previous = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
        GUI.Label(new Rect(rect.x + 3f, rect.y + 3f, rect.width, rect.height), text, style);
        style.normal.textColor = previous;
        GUI.Label(rect, text, style);
    }

    private GUIStyle GetCountdownCaptionStyle()
    {
        if (countdownCaptionStyle != null)
            return countdownCaptionStyle;

        countdownCaptionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        countdownCaptionStyle.normal.textColor = Color.white;
        return countdownCaptionStyle;
    }

    private GUIStyle GetCountdownStyle()
    {
        if (countdownStyle != null)
            return countdownStyle;

        countdownStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 96,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        countdownStyle.normal.textColor = Color.white;
        return countdownStyle;
    }
}
