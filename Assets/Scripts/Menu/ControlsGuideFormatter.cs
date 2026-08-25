using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds keyboard/mouse and gamepad control text from the live Input Action asset.
/// </summary>
public static class ControlsGuideFormatter
{
    private static readonly string[] PreferredActionOrder =
    {
        "Move",
        "Look",
        "Fire",
        "Aim",
        "Sprint",
        "Jump",
        "Grenade",
        "Crouch",
        "Reload",
        "Interact",
        "Melee",
        "SwitchWeapon",
        "WeaponSwitch",
        "Pause"
    };

    public static void Build(InputActionAsset actions, out string keyboardMouse, out string gamepad)
    {
        var keyboardLines = new List<string>();
        var gamepadLines = new List<string>();

        InputActionMap playerMap = actions != null ? actions.FindActionMap("Player") : null;
        if (playerMap == null)
        {
            keyboardMouse = FallbackKeyboard();
            gamepad = FallbackGamepad();
            return;
        }

        HashSet<string> written = new HashSet<string>();
        for (int i = 0; i < PreferredActionOrder.Length; i++)
        {
            InputAction action = playerMap.FindAction(PreferredActionOrder[i]);
            if (action == null || !written.Add(action.name))
                continue;

            AppendAction(action, keyboardLines, gamepadLines);
        }

        foreach (InputAction action in playerMap.actions)
        {
            if (action == null || !written.Add(action.name))
                continue;
            AppendAction(action, keyboardLines, gamepadLines);
        }

        keyboardMouse = keyboardLines.Count > 0 ? string.Join("\n", keyboardLines) : FallbackKeyboard();
        gamepad = gamepadLines.Count > 0 ? string.Join("\n", gamepadLines) : FallbackGamepad();
    }

    private static void AppendAction(InputAction action, List<string> keyboardLines, List<string> gamepadLines)
    {
        string displayName = DisplayNameForAction(action.name);
        var keyboardBindings = new List<string>();
        var gamepadBindings = new List<string>();

        foreach (InputBinding binding in action.bindings)
        {
            if (binding.isComposite)
                continue;

            string path = binding.effectivePath;
            if (string.IsNullOrEmpty(path))
                path = binding.path;
            if (string.IsNullOrEmpty(path))
                continue;

            string friendly = FriendlyControlName(path);
            if (string.IsNullOrEmpty(friendly))
                continue;

            if (IsGamepadPath(path))
                AddUnique(gamepadBindings, friendly);
            else if (IsKeyboardOrMousePath(path))
                AddUnique(keyboardBindings, friendly);
        }

        if (keyboardBindings.Count > 0)
            keyboardLines.Add(displayName + "  —  " + string.Join(" / ", keyboardBindings));
        if (gamepadBindings.Count > 0)
            gamepadLines.Add(displayName + "  —  " + string.Join(" / ", gamepadBindings));
    }

    private static string DisplayNameForAction(string actionName)
    {
        switch (actionName)
        {
            case "Aim": return "Aim / Zoom";
            case "SwitchWeapon": return "Switch Weapon";
            case "WeaponSwitch": return "Cycle Weapons";
            case "Fire": return "Fire";
            case "Grenade": return "Grenade";
            default: return SplitCamel(actionName);
        }
    }

    private static string SplitCamel(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var builder = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                builder.Append(' ');
            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    private static bool IsGamepadPath(string path)
    {
        return path.IndexOf("Gamepad", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("XInput", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsKeyboardOrMousePath(string path)
    {
        return path.IndexOf("Keyboard", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("Mouse", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string FriendlyControlName(string path)
    {
        string control = path;
        int slash = path.LastIndexOf('/');
        if (slash >= 0 && slash < path.Length - 1)
            control = path.Substring(slash + 1);

        switch (control)
        {
            case "leftStick": return "Left Stick";
            case "rightStick": return "Right Stick";
            case "leftTrigger": return "Left Trigger";
            case "rightTrigger": return "Right Trigger";
            case "leftStickPress": return "Left Stick Click";
            case "rightStickPress": return "Right Stick Click";
            case "leftShoulder": return "Left Shoulder";
            case "rightShoulder": return "Right Shoulder";
            case "buttonSouth": return "South Button (A)";
            case "buttonEast": return "East Button (B)";
            case "buttonWest": return "West Button (X)";
            case "buttonNorth": return "North Button (Y)";
            case "start": return "Menu Button";
            case "select": return "View Button";
            case "leftButton": return "Left Mouse";
            case "rightButton": return "Right Mouse";
            case "middleButton": return "Middle Mouse";
            case "delta": return "Mouse";
            case "y": return path.IndexOf("scroll", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Mouse Scroll" : control.ToUpperInvariant();
            case "leftShift": return "Left Shift";
            case "leftCtrl": return "Left Ctrl";
            case "escape": return "Escape";
            case "space": return "Space";
            case "up": return path.IndexOf("leftStick", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Left Stick" : "Up";
            case "down": return path.IndexOf("leftStick", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "Left Stick" : "Down";
            case "left": return path.IndexOf("dpad", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "D-Pad Left" : "Left";
            case "right": return path.IndexOf("dpad", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "D-Pad Right" : "Right";
            default:
                if (control.Length == 1)
                    return control.ToUpperInvariant();
                return SplitCamel(control);
        }
    }

    private static void AddUnique(List<string> list, string value)
    {
        if (!list.Contains(value))
            list.Add(value);
    }

    private static string FallbackKeyboard()
    {
        return
            "Move  —  WASD\n" +
            "Look  —  Mouse\n" +
            "Fire  —  Left Mouse\n" +
            "Aim / Zoom  —  Right Mouse\n" +
            "Sprint  —  Left Shift\n" +
            "Jump  —  Space\n" +
            "Grenade  —  C\n" +
            "Crouch  —  Left Ctrl\n" +
            "Reload  —  R\n" +
            "Interact  —  E\n" +
            "Switch Weapon  —  Q / Mouse Scroll\n" +
            "Pause  —  Escape";
    }

    private static string FallbackGamepad()
    {
        return
            "Move  —  Left Stick\n" +
            "Look  —  Right Stick\n" +
            "Fire  —  Right Trigger\n" +
            "Aim / Zoom  —  Right Stick Click\n" +
            "Sprint  —  Left Stick Click\n" +
            "Jump  —  South Button (A)\n" +
            "Grenade  —  Left Trigger\n" +
            "Crouch  —  East Button (B)\n" +
            "Reload / Interact  —  West Button (X)\n" +
            "Switch Weapon  —  North Button (Y) / Shoulder Buttons\n" +
            "Pause  —  Menu Button";
    }
}
