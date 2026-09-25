using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lets D-pad / stick / arrows leave a selected InputField instead of getting
/// stuck in caret movement. Typing still works after clicking or pressing Submit.
/// </summary>
[RequireComponent(typeof(InputField))]
public class MenuInputFieldNavigation : MonoBehaviour, IMoveHandler
{
    private InputField field;
    private bool moveLatched;

    private void Awake()
    {
        field = GetComponent<InputField>();
        if (field != null)
            field.shouldActivateOnSelect = false;
    }

    public void OnMove(AxisEventData eventData)
    {
        if (eventData == null)
            return;

        if (field != null && field.isFocused &&
            (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
        {
            field.ProcessEvent(Event.KeyboardEvent(eventData.moveDir == MoveDirection.Left ? "left" : "right"));
            eventData.Use();
            return;
        }

        if (TryMove(eventData.moveDir))
            eventData.Use();
    }

    private void Update()
    {
        if (field == null || !field.isFocused)
        {
            moveLatched = false;
            return;
        }

        if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != gameObject)
            return;

        Vector2 move = ReadFocusedMove();
        if (move.sqrMagnitude < 0.25f)
        {
            moveLatched = false;
            return;
        }

        if (moveLatched)
            return;

        MoveDirection direction = move.y > 0f ? MoveDirection.Up : MoveDirection.Down;
        if (TryMove(direction))
            moveLatched = true;
    }

    private bool TryMove(MoveDirection direction)
    {
        if (field == null || EventSystem.current == null)
            return false;

        Selectable next = null;
        switch (direction)
        {
            case MoveDirection.Up:
                next = field.FindSelectableOnUp();
                break;
            case MoveDirection.Down:
                next = field.FindSelectableOnDown();
                break;
            default:
                return false;
        }

        if (next == null || next == field)
            return false;

        field.DeactivateInputField();
        EventSystem.current.SetSelectedGameObject(next.gameObject);
        return true;
    }

    private static Vector2 ReadFocusedMove()
    {
        Vector2 value = Vector2.zero;
        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            value = pad.dpad.ReadValue();
            if (value.sqrMagnitude < 0.25f)
                value = pad.leftStick.ReadValue();
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && value.sqrMagnitude < 0.25f)
        {
            if (keyboard.upArrowKey.isPressed)
                value.y += 1f;
            if (keyboard.downArrowKey.isPressed)
                value.y -= 1f;
        }

        if (Mathf.Abs(value.y) < 0.45f)
            return Vector2.zero;

        return new Vector2(0f, Mathf.Sign(value.y));
    }
}
