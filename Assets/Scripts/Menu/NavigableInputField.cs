using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Menu InputField that types through the Input System.
/// Gamepad Submit does not open the caret; the profile screen uses that
/// press to show the on-screen keyboard instead.
/// </summary>
public class NavigableInputField : InputField
{
    public event Action GamepadSubmit;

    public override void OnSelect(BaseEventData eventData)
    {
        base.OnSelect(eventData);
        if (isFocused)
            return;

        bool fromPointer = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool fromKeyboard = Keyboard.current != null && Keyboard.current.anyKey.isPressed;
        if (fromPointer || fromKeyboard)
            ActivateInputField();
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        Gamepad pad = Gamepad.current;
        if (pad != null && pad.buttonSouth.wasPressedThisFrame)
        {
            if (isFocused)
                DeactivateInputField();
            GamepadSubmit?.Invoke();
            return;
        }

        if (isFocused)
        {
            DeactivateInputField();
            return;
        }

        base.OnSubmit(eventData);
    }
}
