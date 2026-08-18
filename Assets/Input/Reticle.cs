using UnityEngine;
using Unity.Netcode;

public class Reticle : NetworkBehaviour
{
    [SerializeField] private float size = 4f;
    [SerializeField] private float length = 12f;
    [SerializeField] private float hitMarkerDuration = 0.15f;
    [SerializeField] private float hitMarkerSize = 6f;
    [SerializeField] private float hitMarkerLength = 20f;

    private float hitMarkerExpireTime;
    private Texture2D hitMarkerTexture;

    public void ShowHitMarker()
    {
        hitMarkerExpireTime = Time.unscaledTime + hitMarkerDuration;
    }

    private void OnGUI()
    {
        if (!IsOwner)
            return;

        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        GUI.DrawTexture(
            new Rect(centerX - size / 2f, centerY - length / 2f, size, length),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(centerX - length / 2f, centerY - size / 2f, length, size),
            Texture2D.whiteTexture);

        if (Time.unscaledTime >= hitMarkerExpireTime)
            return;

        Texture2D marker = GetHitMarkerTexture();
        GUI.DrawTexture(
            new Rect(centerX - hitMarkerSize / 2f, centerY - hitMarkerLength / 2f, hitMarkerSize, hitMarkerLength),
            marker);
        GUI.DrawTexture(
            new Rect(centerX - hitMarkerLength / 2f, centerY - hitMarkerSize / 2f, hitMarkerLength, hitMarkerSize),
            marker);
    }

    private Texture2D GetHitMarkerTexture()
    {
        if (hitMarkerTexture != null)
            return hitMarkerTexture;

        hitMarkerTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        hitMarkerTexture.SetPixel(0, 0, new Color(1f, 0.85f, 0.15f, 1f));
        hitMarkerTexture.Apply();
        hitMarkerTexture.hideFlags = HideFlags.HideAndDontSave;
        return hitMarkerTexture;
    }
}
