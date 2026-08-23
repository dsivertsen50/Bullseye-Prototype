using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Local-only pickup prompt and ammo readout. Not networked.
/// </summary>
public class PlayerWeaponHud : NetworkBehaviour
{
    private PlayerWeaponInventory inventory;
    private PlayerWeaponInteractor interactor;
    private PlayerHealth playerHealth;
    private GUIStyle promptStyle;
    private GUIStyle ammoStyle;

    public override void OnNetworkSpawn()
    {
        inventory = GetComponent<PlayerWeaponInventory>();
        interactor = GetComponent<PlayerWeaponInteractor>();
        playerHealth = GetComponent<PlayerHealth>();
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    private void OnGUI()
    {
        if (!IsOwner || inventory == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        DrawAmmo();
        DrawPrompt();
    }

    private void DrawAmmo()
    {
        WeaponDefinition definition = inventory.ActiveDefinition;
        WeaponRuntimeState state = inventory.ActiveState;
        if (definition == null || state.IsEmpty)
            return;

        string ammo = definition.HasUnlimitedReserve
            ? $"{state.Magazine} / ∞"
            : $"{state.Magazine} / {state.Reserve}";
        string text = $"{definition.DisplayName}\n{ammo}";
        var rect = new Rect(Screen.width - 360f, Screen.height - 92f, 340f, 72f);
        DrawShadowedLabel(rect, text, GetAmmoStyle());
    }

    private void DrawPrompt()
    {
        if (interactor == null)
            return;

        string prompt = interactor.GetPromptText();
        if (string.IsNullOrEmpty(prompt))
            return;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.62f;
        var rect = new Rect(centerX - 280f, centerY, 560f, 80f);
        DrawShadowedLabel(rect, prompt, GetPromptStyle());
    }

    private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style)
    {
        Color previous = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
        style.normal.textColor = previous;
        GUI.Label(rect, text, style);
    }

    private GUIStyle GetPromptStyle()
    {
        if (promptStyle != null)
            return promptStyle;

        promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        promptStyle.normal.textColor = Color.white;
        return promptStyle;
    }

    private GUIStyle GetAmmoStyle()
    {
        if (ammoStyle != null)
            return ammoStyle;

        ammoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerRight
        };
        ammoStyle.normal.textColor = Color.white;
        return ammoStyle;
    }
}
