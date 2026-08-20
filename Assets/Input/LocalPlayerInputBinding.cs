using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XInput;

/// <summary>
/// Restricts PlayerControls to one gamepad per locally owned networked player,
/// and keeps that Xbox pad readable even when this editor/player window is not focused.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class LocalPlayerInputBinding : NetworkBehaviour
{
    [SerializeField] private InputActionAsset playerActions;
    [SerializeField] private bool assignKeyboardAndMouseToFirstPlayer = true;

    private Gamepad assignedGamepad;
    private int xinputUserIndex = -1;

    public Gamepad AssignedGamepad => assignedGamepad;
    public InputActionAsset PlayerActions => playerActions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnableBackgroundControllerInput()
    {
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner || playerActions == null)
            return;

        InputSystem.onDeviceChange += OnDeviceChange;
        ApplyDeviceBinding();
        EnableGameplayActions();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
            return;

        InputSystem.onDeviceChange -= OnDeviceChange;
        assignedGamepad = null;
        xinputUserIndex = -1;
        playerActions.devices = null;
    }

    public override void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        base.OnDestroy();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!IsOwner || playerActions == null)
            return;

        if (change == InputDeviceChange.Added || change == InputDeviceChange.Removed)
            ApplyDeviceBinding();
    }

    private void ApplyDeviceBinding()
    {
        int playerIndex = (int)NetworkManager.LocalClientId;
        var devices = new List<InputDevice>();
        assignedGamepad = null;
        xinputUserIndex = -1;

        if (playerIndex >= 0 && playerIndex < Gamepad.all.Count)
        {
            assignedGamepad = Gamepad.all[playerIndex];
            devices.Add(assignedGamepad);

            if (assignedGamepad is XInputController)
                xinputUserIndex = playerIndex;
        }

        if (assignKeyboardAndMouseToFirstPlayer && playerIndex == 0)
        {
            if (Keyboard.current != null)
                devices.Add(Keyboard.current);

            if (Mouse.current != null)
                devices.Add(Mouse.current);
        }

        playerActions.devices = devices.ToArray();

        Debug.Log(
            "LocalPlayerInputBinding: local client " + playerIndex +
            " bound to " + DescribeDevices(devices));
    }

    public void SetMenuInputActive(bool menuOpen)
    {
        if (playerActions == null)
            return;

        InputActionMap playerMap = playerActions.FindActionMap("Player");
        InputActionMap uiMap = playerActions.FindActionMap("UI");

        if (menuOpen)
        {
            if (uiMap != null)
                uiMap.Enable();
            SetGameplayActionsEnabled(playerMap, false);
        }
        else
        {
            EnableGameplayActions();
        }
    }

    private void EnableGameplayActions()
    {
        if (playerActions == null)
            return;

        InputActionMap uiMap = playerActions.FindActionMap("UI");
        if (uiMap != null)
        {
            uiMap.Disable();
            SetAllActionsEnabled(uiMap, false);
        }

        InputActionMap playerMap = playerActions.FindActionMap("Player");
        if (playerMap != null)
        {
            playerMap.Enable();
            SetAllActionsEnabled(playerMap, true);
        }
    }

    private static void SetGameplayActionsEnabled(InputActionMap playerMap, bool enabled)
    {
        if (playerMap == null)
            return;

        foreach (InputAction action in playerMap.actions)
        {
            if (action.name == "Pause")
            {
                action.Enable();
                continue;
            }

            if (enabled)
                action.Enable();
            else
                action.Disable();
        }
    }

    private static void SetAllActionsEnabled(InputActionMap map, bool enabled)
    {
        if (map == null)
            return;

        foreach (InputAction action in map.actions)
        {
            if (enabled)
                action.Enable();
            else
                action.Disable();
        }
    }

    private void Update()
    {
        if (!IsOwner || assignedGamepad == null || xinputUserIndex < 0)
            return;

        PushXInputStateToAssignedGamepad();
    }

    private void PushXInputStateToAssignedGamepad()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (!TryGetXInputState(xinputUserIndex, out RawXInputState raw))
            return;

        var state = new XInputGamepadState
        {
            buttons = raw.buttons,
            leftTrigger = raw.leftTrigger,
            rightTrigger = raw.rightTrigger,
            leftStickX = raw.leftStickX,
            leftStickY = raw.leftStickY,
            rightStickX = raw.rightStickX,
            rightStickY = raw.rightStickY
        };

        InputState.Change(assignedGamepad, state);
#endif
    }

    private static string DescribeDevices(List<InputDevice> devices)
    {
        if (devices.Count == 0)
            return "(no devices)";

        var names = new string[devices.Count];
        for (int i = 0; i < devices.Count; i++)
            names[i] = devices[i].name;

        return string.Join(", ", names);
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    private const int XInputSuccess = 0;
    private static bool useXInput14 = true;

    [StructLayout(LayoutKind.Sequential)]
    private struct RawXInputState
    {
        public uint packetNumber;
        public ushort buttons;
        public byte leftTrigger;
        public byte rightTrigger;
        public short leftStickX;
        public short leftStickY;
        public short rightStickX;
        public short rightStickY;
    }

    [StructLayout(LayoutKind.Explicit, Size = 12)]
    private struct XInputGamepadState : IInputStateTypeInfo
    {
        public FourCC format => new FourCC('X', 'I', 'N', 'P');

        [FieldOffset(0)] public ushort buttons;
        [FieldOffset(2)] public byte leftTrigger;
        [FieldOffset(3)] public byte rightTrigger;
        [FieldOffset(4)] public short leftStickX;
        [FieldOffset(6)] public short leftStickY;
        [FieldOffset(8)] public short rightStickX;
        [FieldOffset(10)] public short rightStickY;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState14(int userIndex, out RawXInputState state);

    [DllImport("xinput1_3.dll", EntryPoint = "XInputGetState")]
    private static extern int XInputGetState13(int userIndex, out RawXInputState state);

    private static bool TryGetXInputState(int userIndex, out RawXInputState state)
    {
        try
        {
            if (useXInput14)
            {
                int result = XInputGetState14(userIndex, out state);
                return result == XInputSuccess;
            }
        }
        catch (System.DllNotFoundException)
        {
            useXInput14 = false;
        }

        try
        {
            return XInputGetState13(userIndex, out state) == XInputSuccess;
        }
        catch (System.DllNotFoundException)
        {
            state = default;
            return false;
        }
    }
#endif
}
