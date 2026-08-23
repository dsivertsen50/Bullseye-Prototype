using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Local first-person reticle. Visualizes the equipped weapon's current spread
/// from WeaponAccuracyController. Does not decide gameplay accuracy.
/// </summary>
[RequireComponent(typeof(WeaponAccuracyController))]
public class Reticle : NetworkBehaviour
{
    [SerializeField] private float hitMarkerDuration = 0.15f;
    [SerializeField] private float hitMarkerSize = 6f;
    [SerializeField] private float hitMarkerLength = 20f;

    private WeaponAccuracyController accuracy;
    private float hitMarkerExpireTime;
    private Texture2D hitMarkerTexture;
    private Texture2D armTexture;

    public void ShowHitMarker()
    {
        hitMarkerExpireTime = Time.unscaledTime + hitMarkerDuration;
    }

    private void Awake()
    {
        accuracy = GetComponent<WeaponAccuracyController>();
    }

    public override void OnDestroy()
    {
        if (armTexture != null)
            Destroy(armTexture);
        if (hitMarkerTexture != null)
            Destroy(hitMarkerTexture);

        base.OnDestroy();
    }

    private void OnGUI()
    {
        if (!IsOwner)
            return;

        if (TryGetComponent(out PlayerHealth health) && health.IsDead)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        DrawSpreadReticle(centerX, centerY);

        if (Time.unscaledTime >= hitMarkerExpireTime)
            return;

        Texture2D marker = GetHitMarkerTexture();
        GUI.DrawTexture(
            new Rect(centerX - hitMarkerSize * 0.5f, centerY - hitMarkerLength * 0.5f, hitMarkerSize, hitMarkerLength),
            marker);
        GUI.DrawTexture(
            new Rect(centerX - hitMarkerLength * 0.5f, centerY - hitMarkerSize * 0.5f, hitMarkerLength, hitMarkerSize),
            marker);
    }

    private void DrawSpreadReticle(float centerX, float centerY)
    {
        WeaponAccuracySettings settings = accuracy != null ? accuracy.Settings : null;
        float scale = Screen.height / WeaponAccuracySettings.ReferenceScreenHeight;
        float length = (settings != null ? settings.ReticleElementLength : 10f) * scale;
        float thickness = (settings != null ? settings.ReticleElementThickness : 2f) * scale;
        // IMGUI drops sub-2px vertical rects when using the built-in white texture.
        thickness = Mathf.Max(2f, thickness);
        length = Mathf.Max(thickness, length);
        float gap = accuracy != null
            ? accuracy.CurrentSpreadPixels
            : WeaponAccuracySettings.MinimumVisualGap * scale;
        gap = Mathf.Max(WeaponAccuracySettings.MinimumVisualGap, gap);
        Texture texture = ResolveReticleTexture(settings);

        DrawArm(texture, centerX - thickness * 0.5f, centerY - gap - length, thickness, length);
        DrawArm(texture, centerX - thickness * 0.5f, centerY + gap, thickness, length);
        DrawArm(texture, centerX - gap - length, centerY - thickness * 0.5f, length, thickness);
        DrawArm(texture, centerX + gap, centerY - thickness * 0.5f, length, thickness);
    }

    private static void DrawArm(Texture texture, float x, float y, float width, float height)
    {
        GUI.DrawTexture(new Rect(x, y, width, height), texture, ScaleMode.StretchToFill);
    }

    private Texture ResolveReticleTexture(WeaponAccuracySettings settings)
    {
        if (settings != null && settings.ReticleSprite != null && settings.ReticleSprite.texture != null)
            return settings.ReticleSprite.texture;

        return GetArmTexture();
    }

    private Texture2D GetArmTexture()
    {
        if (armTexture != null)
            return armTexture;

        armTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        armTexture.wrapMode = TextureWrapMode.Clamp;
        armTexture.filterMode = FilterMode.Point;
        armTexture.SetPixel(0, 0, Color.white);
        armTexture.Apply();
        armTexture.hideFlags = HideFlags.HideAndDontSave;
        return armTexture;
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
