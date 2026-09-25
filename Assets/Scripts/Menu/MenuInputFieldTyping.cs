using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Feeds a physical keyboard into a uGUI InputField.
/// The project runs Input System only, so InputField never sees Input.inputString.
/// </summary>
[RequireComponent(typeof(InputField))]
public class MenuInputFieldTyping : MonoBehaviour
{
    private InputField field;
    private Keyboard subscribed;

    private void Awake()
    {
        field = GetComponent<InputField>();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != subscribed)
        {
            Unsubscribe();
            if (keyboard != null)
                keyboard.onTextInput += HandleTextInput;
            subscribed = keyboard;
        }

        if (field == null || !field.isFocused || keyboard == null)
            return;

        if (keyboard.backspaceKey.wasPressedThisFrame)
        {
            field.ProcessEvent(Event.KeyboardEvent("backspace"));
            return;
        }

        if (keyboard.deleteKey.wasPressedThisFrame)
            field.ProcessEvent(Event.KeyboardEvent("delete"));
    }

    private void HandleTextInput(char character)
    {
        if (field == null || !field.isFocused)
            return;
        if (character < ' ')
            return;

        var textEvent = new Event
        {
            type = EventType.KeyDown,
            character = character,
            keyCode = KeyCode.None
        };
        field.ProcessEvent(textEvent);
    }

    private void Unsubscribe()
    {
        if (subscribed != null)
            subscribed.onTextInput -= HandleTextInput;
        subscribed = null;
    }
}
