using UnityEngine;

public class PlayerHealthHud : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private float margin = 24f;
    [SerializeField] private float segmentWidth = 18f;
    [SerializeField] private float segmentHeight = 22f;
    [SerializeField] private float segmentGap = 4f;
    [SerializeField] private Color filledColor = new Color(0.22f, 0.86f, 0.32f, 0.95f);
    [SerializeField] private Color emptyColor = new Color(0.12f, 0.12f, 0.12f, 0.72f);

    private Texture2D filledTexture;
    private Texture2D emptyTexture;
    private GUIStyle labelStyle;
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

        int current = Mathf.Clamp(playerHealth.CurrentHealth, 0, playerHealth.MaxHealth);
        int max = Mathf.Max(1, playerHealth.MaxHealth);

        float totalWidth = max * segmentWidth + (max - 1) * segmentGap;
        float x = margin;
        float y = Screen.height - margin - segmentHeight;

        GUI.Label(
            new Rect(x, y - 22f, Mathf.Max(120f, totalWidth), 20f),
            $"{current} / {max}",
            GetLabelStyle());

        for (int i = 0; i < max; i++)
        {
            Rect segment = new Rect(
                x + i * (segmentWidth + segmentGap),
                y,
                segmentWidth,
                segmentHeight);

            GUI.DrawTexture(segment, i < current ? GetFilledTexture() : GetEmptyTexture());
        }

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

    private GUIStyle GetLabelStyle()
    {
        if (labelStyle != null)
            return labelStyle;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        labelStyle.normal.textColor = Color.white;
        return labelStyle;
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

    private Texture2D GetFilledTexture()
    {
        return GetOrCreateTexture(ref filledTexture, filledColor);
    }

    private Texture2D GetEmptyTexture()
    {
        return GetOrCreateTexture(ref emptyTexture, emptyColor);
    }

    private static Texture2D GetOrCreateTexture(ref Texture2D texture, Color color)
    {
        if (texture != null)
            return texture;

        texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        texture.hideFlags = HideFlags.HideAndDontSave;
        return texture;
    }

    private void OnDestroy()
    {
        DestroyTexture(ref filledTexture);
        DestroyTexture(ref emptyTexture);
    }

    private static void DestroyTexture(ref Texture2D texture)
    {
        if (texture == null)
            return;

        Destroy(texture);
        texture = null;
    }
}
