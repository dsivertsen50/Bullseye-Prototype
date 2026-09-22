using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Dedicated join-code entry: physical keyboard typing plus a controller
/// virtual keyboard. Both input methods write the same buffer.
/// </summary>
public class JoinCodeEntryUI : MonoBehaviour
{
    public const int MinCodeLength = LocalSessionRegistry.MinJoinCodeLength;
    public const int MaxCodeLength = LocalSessionRegistry.MaxJoinCodeLength;
    public const string KeyboardCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private const int KeysPerRow = 8;
    private const float KeyWidth = 68f;
    private const float KeyHeight = 48f;
    private const float KeyGap = 10f;

    private MenuAudioController menuAudio;
    private Action onJoin;
    private Action onBack;

    private GameObject panel;
    private Text titleLabel;
    private Text publicTitleLabel;
    private Text publicListLabel;
    private Transform publicListRoot;
    private Text codeHeadingLabel;
    private Text codeDisplayLabel;
    private Text errorLabel;
    private Text statusLabel;
    private readonly List<Button> keyButtons = new List<Button>();
    private Button backspaceButton;
    private Button clearButton;
    private Button joinButton;
    private Button backButton;
    private string code = string.Empty;
    private bool busy;
    private bool consumeKeyboard;

    public GameObject Panel => panel;
    public Transform PublicListRoot => publicListRoot;
    public Text PublicListLabel => publicListLabel;
    public Text PublicTitleLabel => publicTitleLabel;
    public string CurrentCode => code;
    public bool IsBusy => busy;

    public Selectable DefaultSelectable
    {
        get
        {
            if (keyButtons.Count > 0 && keyButtons[0] != null && keyButtons[0].IsInteractable())
                return keyButtons[0];
            return joinButton;
        }
    }

    public static JoinCodeEntryUI Create(
        Transform canvasParent,
        float leftPadding,
        MenuAudioController audio,
        Action join,
        Action back)
    {
        GameObject host = new GameObject("JoinCodeEntryUI", typeof(RectTransform));
        host.transform.SetParent(canvasParent, false);
        MenuUiFactory.Stretch(host.GetComponent<RectTransform>());
        JoinCodeEntryUI ui = host.AddComponent<JoinCodeEntryUI>();
        ui.menuAudio = audio;
        ui.onJoin = join;
        ui.onBack = back;
        ui.Build(leftPadding);
        return ui;
    }

    public void Open(bool clearCode)
    {
        if (panel != null)
            panel.SetActive(true);
        consumeKeyboard = true;
        busy = false;
        if (clearCode)
            code = string.Empty;
        SetError(null);
        Refresh();
    }

    public void Hide()
    {
        consumeKeyboard = false;
        if (panel != null)
            panel.SetActive(false);
    }

    public void Prepare(bool localTestAvailable)
    {
        if (titleLabel != null)
            titleLabel.text = "JOIN GAME";
        if (publicTitleLabel != null)
            publicTitleLabel.text = localTestAvailable ? "Local Test Games" : "Public Games";
        SetBusy(false);
        if (errorLabel != null && string.IsNullOrEmpty(errorLabel.text))
            errorLabel.text = string.Empty;
        Refresh();
    }

    public void SetError(string message)
    {
        if (errorLabel != null)
            errorLabel.text = message ?? string.Empty;
    }

    public void SetPublicListMessage(string message)
    {
        if (publicListLabel == null)
            return;
        publicListLabel.text = message ?? string.Empty;
        publicListLabel.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    public void SetBusy(bool value)
    {
        busy = value;
        Refresh();
    }

    public void ClearCode()
    {
        code = string.Empty;
        Refresh();
    }

    /// <summary>
    /// Gamepad B: backspace when the buffer has characters.
    /// Returns false when empty so the caller can leave the screen.
    /// Keyboard Escape should not call this.
    /// </summary>
    public bool TryBackspace()
    {
        if (busy)
            return false;
        if (code.Length == 0)
            return false;

        code = code.Substring(0, code.Length - 1);
        PlaySelect();
        Refresh();
        return true;
    }

    public bool CanSubmit => !busy && LocalSessionRegistry.NormalizeCode(code).Length >= MinCodeLength;

    public void WirePublicNavigation(IReadOnlyList<Button> publicButtons)
    {
        Selectable firstKey = keyButtons.Count > 0 ? keyButtons[0] : joinButton;
        Selectable lastPublic = publicButtons != null && publicButtons.Count > 0
            ? publicButtons[publicButtons.Count - 1]
            : null;
        Selectable firstPublic = publicButtons != null && publicButtons.Count > 0
            ? publicButtons[0]
            : null;

        if (publicButtons != null)
        {
            for (int i = 0; i < publicButtons.Count; i++)
            {
                Selectable up = i == 0 ? backButton : publicButtons[i - 1];
                Selectable down = i == publicButtons.Count - 1 ? firstKey : publicButtons[i + 1];
                MenuUiFactory.SetVerticalNav(publicButtons[i], up, down);
            }
        }

        Selectable keyUp = lastPublic != null ? lastPublic : backButton;
        WireKeyboardNavigation(keyUp);
        MenuUiFactory.SetNav(backspaceButton, LastKeyButton(), joinButton, clearButton, clearButton);
        MenuUiFactory.SetNav(clearButton, LastKeyButton(), joinButton, backspaceButton, backspaceButton);
        MenuUiFactory.SetNav(joinButton, backspaceButton, firstPublic != null ? firstPublic : firstKey, backButton, backButton);
        MenuUiFactory.SetNav(backButton, clearButton, firstPublic != null ? firstPublic : firstKey, joinButton, joinButton);
    }

    private void Update()
    {
        if (!consumeKeyboard || panel == null || !panel.activeInHierarchy || busy)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
            TickPhysicalKeyboard(keyboard);

        Gamepad pad = Gamepad.current;
        if (pad != null && CanSubmit && pad.startButton.wasPressedThisFrame)
            RequestJoin();
    }

    private void TickPhysicalKeyboard(Keyboard keyboard)
    {
        if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            RequestJoin();
            return;
        }

        if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
        {
            TryBackspace();
            return;
        }

        for (int i = 0; i < KeyboardCharacters.Length; i++)
        {
            char character = KeyboardCharacters[i];
            Key key = KeyForCharacter(character);
            if (key == Key.None || !keyboard[key].wasPressedThisFrame)
                continue;
            Append(character);
            return;
        }

        for (int digit = 0; digit <= 9; digit++)
        {
            Key numpad = Key.Numpad0 + digit;
            if (keyboard[numpad].wasPressedThisFrame)
            {
                Append((char)('0' + digit));
                return;
            }
        }
    }

    private static Key KeyForCharacter(char character)
    {
        if (character >= 'A' && character <= 'Z')
            return Key.A + (character - 'A');
        if (character >= '0' && character <= '9')
            return Key.Digit0 + (character - '0');
        return Key.None;
    }

    private void Build(float leftPadding)
    {
        panel = MenuUiFactory.CreatePanel(transform, "JoinPanel", new Vector2(1100f, 920f), new Color(0.07f, 0.08f, 0.11f, 0.78f));
        MenuUiFactory.DockLeft(panel, leftPadding);
        panel.SetActive(false);
        Transform root = panel.transform;

        titleLabel = MenuUiFactory.CreateLabel(root, "Title", "JOIN GAME", 44, new Vector2(0f, 410f), new Vector2(1000f, 56f));
        codeHeadingLabel = MenuUiFactory.CreateLabel(root, "CodeHeading", "ENTER JOIN CODE", 22, new Vector2(0f, 350f), new Vector2(900f, 28f));
        codeDisplayLabel = MenuUiFactory.CreateLabel(root, "CodeDisplay", "_", 48, new Vector2(0f, 295f), new Vector2(900f, 64f));
        codeDisplayLabel.color = Color.white;

        BuildVirtualKeyboard(root);
        backspaceButton = MenuUiFactory.CreateButton(root, "Backspace", "Backspace", new Vector2(-150f, -90f), () => TryBackspace(), new Vector2(240f, 50f));
        clearButton = MenuUiFactory.CreateButton(root, "Clear", "Clear", new Vector2(150f, -90f), ClearAll, new Vector2(240f, 50f));
        joinButton = MenuUiFactory.CreateButton(root, "Join", "Join", new Vector2(-150f, -155f), RequestJoin, new Vector2(240f, 54f));
        backButton = MenuUiFactory.CreateButton(root, "Back", "Back", new Vector2(150f, -155f), () => onBack?.Invoke(), new Vector2(240f, 54f));
        errorLabel = MenuUiFactory.CreateLabel(root, "Error", string.Empty, 20, new Vector2(0f, -210f), new Vector2(1000f, 36f));
        errorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);
        statusLabel = MenuUiFactory.CreateLabel(root, "Status", string.Empty, 18, new Vector2(0f, -245f), new Vector2(1000f, 24f));
        statusLabel.color = ProfileUiFactory.MutedColor;
        publicTitleLabel = MenuUiFactory.CreateLabel(root, "PublicTitle", "Public Games", 18, new Vector2(0f, -285f), new Vector2(1000f, 24f));
        publicListRoot = new GameObject("PublicList", typeof(RectTransform)).transform;
        publicListRoot.SetParent(root, false);
        RectTransform publicRect = publicListRoot.GetComponent<RectTransform>();
        publicRect.anchorMin = new Vector2(0.5f, 0.5f);
        publicRect.anchorMax = new Vector2(0.5f, 0.5f);
        publicRect.pivot = new Vector2(0.5f, 0.5f);
        publicRect.anchoredPosition = Vector2.zero;
        publicRect.sizeDelta = Vector2.zero;
        publicListLabel = MenuUiFactory.CreateLabel(root, "PublicPlaceholder", string.Empty, 16, new Vector2(0f, -340f), new Vector2(1000f, 48f));
        publicListLabel.color = ProfileUiFactory.MutedColor;
        publicListLabel.gameObject.SetActive(false);

        WirePublicNavigation(null);
        Refresh();
    }

    private void BuildVirtualKeyboard(Transform parent)
    {
        keyButtons.Clear();
        float rowWidth = KeysPerRow * KeyWidth + (KeysPerRow - 1) * KeyGap;
        float startX = -rowWidth * 0.5f + KeyWidth * 0.5f;
        float startY = 200f;

        for (int i = 0; i < KeyboardCharacters.Length; i++)
        {
            int row = i / KeysPerRow;
            int column = i % KeysPerRow;
            char character = KeyboardCharacters[i];
            Vector2 position = new Vector2(startX + column * (KeyWidth + KeyGap), startY - row * (KeyHeight + KeyGap));
            char captured = character;
            Button button = MenuUiFactory.CreateButton(
                parent,
                "Key" + character,
                character.ToString(),
                position,
                () => Append(captured),
                new Vector2(KeyWidth, KeyHeight));
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.fontSize = 22;
            keyButtons.Add(button);
        }
    }

    private void WireKeyboardNavigation(Selectable upFromFirstRow)
    {
        int count = keyButtons.Count;
        if (count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            int row = i / KeysPerRow;
            int column = i % KeysPerRow;
            int lastRow = (count - 1) / KeysPerRow;
            int rowCount = row == lastRow
                ? count - lastRow * KeysPerRow
                : KeysPerRow;

            Selectable up = row == 0
                ? upFromFirstRow
                : keyButtons[i - KeysPerRow];
            Selectable down;
            if (row < lastRow)
            {
                int downIndex = i + KeysPerRow;
                if (downIndex >= count)
                    downIndex = count - 1;
                down = keyButtons[downIndex];
            }
            else
            {
                down = column < KeysPerRow / 2 ? backspaceButton : clearButton;
            }

            int leftIndex = column == 0 ? row * KeysPerRow + rowCount - 1 : i - 1;
            int rightIndex = column == rowCount - 1 ? row * KeysPerRow : i + 1;
            MenuUiFactory.SetNav(keyButtons[i], up, down, keyButtons[leftIndex], keyButtons[rightIndex]);
        }
    }

    private Selectable LastKeyButton()
    {
        return keyButtons.Count > 0 ? keyButtons[keyButtons.Count - 1] : joinButton;
    }

    private void Append(char character)
    {
        if (busy)
            return;

        string next = LocalSessionRegistry.NormalizeCode(code + character);
        if (next.Length == 0 || next.Length > MaxCodeLength)
            return;
        if (next == code)
            return;

        code = next;
        PlaySelect();
        Refresh();
    }

    private void ClearAll()
    {
        if (busy || code.Length == 0)
            return;
        code = string.Empty;
        PlaySelect();
        Refresh();
    }

    private void RequestJoin()
    {
        if (busy)
            return;

        string normalized = LocalSessionRegistry.NormalizeCode(code);
        if (normalized.Length < MinCodeLength)
        {
            SetError("Enter a valid join code.");
            return;
        }

        onJoin?.Invoke();
    }

    private void Refresh()
    {
        if (codeDisplayLabel != null)
        {
            if (busy)
                codeDisplayLabel.text = code;
            else if (code.Length >= MaxCodeLength)
                codeDisplayLabel.text = code;
            else
                codeDisplayLabel.text = code + "_";
        }

        if (statusLabel != null)
            statusLabel.text = busy ? "Joining lobby..." : string.Empty;

        bool keysEnabled = !busy;
        for (int i = 0; i < keyButtons.Count; i++)
        {
            if (keyButtons[i] != null)
                keyButtons[i].interactable = keysEnabled;
        }

        if (backspaceButton != null)
            backspaceButton.interactable = keysEnabled;
        if (clearButton != null)
            clearButton.interactable = keysEnabled;
        if (joinButton != null)
            joinButton.interactable = CanSubmit;
        if (backButton != null)
            backButton.interactable = true;
    }

    private void PlaySelect()
    {
        if (menuAudio != null)
            menuAudio.PlaySelect();
    }
}
