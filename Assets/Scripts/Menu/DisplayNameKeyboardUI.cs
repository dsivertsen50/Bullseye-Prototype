using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// On-screen display-name keyboard for controller menu navigation.
/// Physical keyboard input writes the same draft.
/// </summary>
public class DisplayNameKeyboardUI : MonoBehaviour
{
    private const float KeyWidth = 52f;
    private const float KeyHeight = 40f;
    private const float KeyGap = 6f;

    private static readonly string[] LetterRows =
    {
        "ABCDEFG",
        "HIJKLMN",
        "OPQRSTU",
        "VWXYZ"
    };

    private MenuAudioController menuAudio;
    private Action<string> onConfirm;
    private Action onCancel;
    private RectTransform alignTo;

    private GameObject panel;
    private Text previewLabel;
    private Text errorLabel;
    private readonly List<List<Button>> rows = new List<List<Button>>();
    private Button firstKey;
    private string draft = string.Empty;
    private Keyboard subscribedKeyboard;
    private bool selectFirstNextFrame;

    public bool IsOpen => isActiveAndEnabled && panel != null && panel.activeInHierarchy;

    public static DisplayNameKeyboardUI Create(
        Transform parent,
        RectTransform menuPanel,
        MenuAudioController audio,
        Action<string> confirm,
        Action cancel)
    {
        GameObject host = new GameObject("DisplayNameKeyboard", typeof(RectTransform));
        host.transform.SetParent(parent, false);
        MenuUiFactory.Stretch(host.GetComponent<RectTransform>());
        DisplayNameKeyboardUI ui = host.AddComponent<DisplayNameKeyboardUI>();
        ui.alignTo = menuPanel;
        ui.menuAudio = audio;
        ui.onConfirm = confirm;
        ui.onCancel = cancel;
        ui.Build();
        host.SetActive(false);
        return ui;
    }

    public void Open(string currentName)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        AlignOverMenu();
        draft = currentName ?? string.Empty;
        if (draft.Length > PlayerProfileConstants.MaxDisplayNameLength)
            draft = draft.Substring(0, PlayerProfileConstants.MaxDisplayNameLength);
        SetError(null);
        RefreshPreview();
        if (panel != null)
            panel.SetActive(true);
        selectFirstNextFrame = true;
        SubscribeKeyboard();
    }

    public void Hide()
    {
        selectFirstNextFrame = false;
        UnsubscribeKeyboard();
        if (panel != null)
            panel.SetActive(false);
        gameObject.SetActive(false);
    }

    public void Cancel()
    {
        Hide();
        onCancel?.Invoke();
    }

    public void SetError(string message)
    {
        if (errorLabel == null)
            return;
        errorLabel.text = message ?? string.Empty;
    }

    private void OnDisable()
    {
        UnsubscribeKeyboard();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (selectFirstNextFrame)
        {
            selectFirstNextFrame = false;
            if (firstKey != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstKey.gameObject);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != subscribedKeyboard)
        {
            UnsubscribeKeyboard();
            SubscribeKeyboard();
        }

        if (keyboard == null)
            return;

        if (keyboard.backspaceKey.wasPressedThisFrame || keyboard.deleteKey.wasPressedThisFrame)
            Backspace();
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            Confirm();
    }

    private void Build()
    {
        Image backdrop = MenuUiFactory.CreateImage(transform, "Backdrop", new Color(0.01f, 0.02f, 0.04f, 0.78f));
        MenuUiFactory.Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        const float panelWidth = 520f;
        const float panelHeight = 560f;
        panel = MenuUiFactory.CreatePanel(transform, "KeyboardPanel", new Vector2(panelWidth, panelHeight), new Color(0.07f, 0.08f, 0.11f, 0.98f));
        Transform root = panel.transform;

        MenuUiFactory.CreateLabel(root, "Title", "EDIT DISPLAY NAME", 28, new Vector2(0f, 236f), new Vector2(480f, 36f));
        previewLabel = MenuUiFactory.CreateLabel(root, "Preview", string.Empty, 26, new Vector2(0f, 188f), new Vector2(460f, 40f));
        errorLabel = MenuUiFactory.CreateLabel(root, "Error", string.Empty, 16, new Vector2(0f, 156f), new Vector2(460f, 22f));
        errorLabel.color = new Color(1f, 0.45f, 0.4f, 1f);

        float startY = 112f;
        for (int i = 0; i < LetterRows.Length; i++)
            rows.Add(BuildCharacterRow(root, LetterRows[i], startY - i * (KeyHeight + KeyGap)));

        rows.Add(BuildCharacterRow(root, "0123456", startY - 4 * (KeyHeight + KeyGap)));
        rows.Add(BuildCharacterRow(root, "789", startY - 5 * (KeyHeight + KeyGap)));

        float actionY = startY - 6 * (KeyHeight + KeyGap) - 6f;
        Button space = MenuUiFactory.CreateButton(root, "Space", "Space", new Vector2(-92f, actionY), AppendSpace, new Vector2(168f, 40f));
        Button backspace = MenuUiFactory.CreateButton(root, "Backspace", "Backspace", new Vector2(92f, actionY), Backspace, new Vector2(168f, 40f));
        float confirmY = actionY - 52f;
        Button cancel = MenuUiFactory.CreateButton(root, "Cancel", "Cancel", new Vector2(-92f, confirmY), Cancel, new Vector2(168f, 44f));
        Button confirm = MenuUiFactory.CreateButton(root, "Confirm", "Confirm", new Vector2(92f, confirmY), Confirm, new Vector2(168f, 44f));
        rows.Add(new List<Button> { space, backspace });
        rows.Add(new List<Button> { cancel, confirm });

        firstKey = rows.Count > 0 && rows[0].Count > 0 ? rows[0][0] : confirm;
        WireNavigation();
        MenuUiFactory.CreateLabel(root, "Hint", "D-pad to move     A  Type     B  Cancel", 14, new Vector2(0f, -250f), new Vector2(480f, 22f)).color = ProfileUiFactory.MutedColor;
    }

    private void AlignOverMenu()
    {
        if (alignTo == null || panel == null)
            return;

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.position = alignTo.TransformPoint(alignTo.rect.center);
    }

    private List<Button> BuildCharacterRow(Transform parent, string characters, float y)
    {
        var row = new List<Button>(characters.Length);
        float rowWidth = characters.Length * KeyWidth + (characters.Length - 1) * KeyGap;
        float startX = -rowWidth * 0.5f + KeyWidth * 0.5f;
        for (int i = 0; i < characters.Length; i++)
        {
            char captured = characters[i];
            Button button = MenuUiFactory.CreateButton(
                parent,
                "Key" + captured,
                captured.ToString(),
                new Vector2(startX + i * (KeyWidth + KeyGap), y),
                () => Append(captured),
                new Vector2(KeyWidth, KeyHeight));
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.fontSize = 22;
            row.Add(button);
        }

        return row;
    }

    private void WireNavigation()
    {
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            List<Button> row = rows[rowIndex];
            for (int column = 0; column < row.Count; column++)
            {
                Selectable up = ButtonAt(rowIndex - 1, column, rowIndex == 0 ? rows.Count - 1 : rowIndex - 1);
                Selectable down = ButtonAt(rowIndex + 1, column, rowIndex == rows.Count - 1 ? 0 : rowIndex + 1);
                Selectable left = row[column == 0 ? row.Count - 1 : column - 1];
                Selectable right = row[column == row.Count - 1 ? 0 : column + 1];
                MenuUiFactory.SetNav(row[column], up, down, left, right);
            }
        }
    }

    private Selectable ButtonAt(int preferredRow, int column, int fallbackRow)
    {
        if (preferredRow >= 0 && preferredRow < rows.Count && rows[preferredRow].Count > 0)
            return rows[preferredRow][Mathf.Clamp(column, 0, rows[preferredRow].Count - 1)];
        if (fallbackRow >= 0 && fallbackRow < rows.Count && rows[fallbackRow].Count > 0)
            return rows[fallbackRow][Mathf.Clamp(column, 0, rows[fallbackRow].Count - 1)];
        return firstKey;
    }

    private void Append(char character)
    {
        if (!DisplayNameRules.IsAllowed(character) || draft.Length >= PlayerProfileConstants.MaxDisplayNameLength)
            return;

        draft += character;
        SetError(null);
        PlaySelect();
        RefreshPreview();
    }

    private void AppendSpace()
    {
        if (draft.Length == 0 || draft.EndsWith(" ") || draft.Length >= PlayerProfileConstants.MaxDisplayNameLength)
            return;
        Append(' ');
    }

    private void Backspace()
    {
        if (draft.Length == 0)
            return;

        draft = draft.Substring(0, draft.Length - 1);
        SetError(null);
        PlaySelect();
        RefreshPreview();
    }

    private void Confirm()
    {
        onConfirm?.Invoke(draft);
    }

    private void RefreshPreview()
    {
        if (previewLabel == null)
            return;

        string shown = draft.Length >= PlayerProfileConstants.MaxDisplayNameLength ? draft : draft + "_";
        previewLabel.text = shown;
    }

    private void SubscribeKeyboard()
    {
        subscribedKeyboard = Keyboard.current;
        if (subscribedKeyboard != null)
            subscribedKeyboard.onTextInput += HandlePhysicalText;
    }

    private void UnsubscribeKeyboard()
    {
        if (subscribedKeyboard != null)
            subscribedKeyboard.onTextInput -= HandlePhysicalText;
        subscribedKeyboard = null;
    }

    private void HandlePhysicalText(char character)
    {
        if (!IsOpen)
            return;

        if (character < ' ' || !DisplayNameRules.IsAllowed(character))
            return;

        Append(character);
    }

    private void PlaySelect()
    {
        if (menuAudio != null)
            menuAudio.PlaySelect();
    }
}
