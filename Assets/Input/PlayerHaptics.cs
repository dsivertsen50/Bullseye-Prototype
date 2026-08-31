using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Haptics;

/// <summary>
/// Local controller rumble for the owning player. Future weapons can request
/// custom effects through PlayRumble without embedding vibration in weapon logic.
/// </summary>
public class PlayerHaptics : MonoBehaviour
{
    [Serializable]
    public struct RumbleSettings
    {
        [Range(0f, 1f)] public float lowFrequency;
        [Range(0f, 1f)] public float highFrequency;
        [Min(0f)] public float duration;
    }

    [Header("Fire Rumble")]
    [SerializeField] private RumbleSettings fireRumble = new()
    {
        lowFrequency = 0.5f,
        highFrequency = 0.75f,
        duration = 0.1f
    };

    [Header("Damage Rumble")]
    [SerializeField] private RumbleSettings damageRumble = new()
    {
        lowFrequency = 0.22f,
        highFrequency = 0.3f,
        duration = 0.15f
    };

    [Header("Wall Run Rumble")]
    [SerializeField] private RumbleSettings wallRunRumble = new()
    {
        lowFrequency = 0.2f,
        highFrequency = 0.32f,
        duration = 0.12f
    };

    private LocalPlayerInputBinding inputBinding;
    private PlayerMovement movement;
    private Coroutine rumbleRoutine;
    private Gamepad rumblingGamepad;
    private IDualMotorRumble rumblingMouse;

    private void Awake()
    {
        inputBinding = GetComponent<LocalPlayerInputBinding>();
        movement = GetComponent<PlayerMovement>();
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        if (movement != null)
            movement.WallRunStarted += PlayWallRunRumble;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (movement != null)
            movement.WallRunStarted -= PlayWallRunRumble;
        StopRumble();
    }

    private void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        if (movement != null)
            movement.WallRunStarted -= PlayWallRunRumble;
        StopRumble();
    }

    private void OnApplicationQuit()
    {
        StopRumble();
    }

    public void PlayFireRumble()
    {
        PlayRumble(fireRumble);
    }

    public void PlayDamageRumble()
    {
        PlayRumble(damageRumble);
    }

    public void PlayWallRunRumble()
    {
        PlayRumble(wallRunRumble, includeMouse: true);
    }

    public void PlayRumble(RumbleSettings settings)
    {
        PlayRumble(settings.lowFrequency, settings.highFrequency, settings.duration);
    }

    public void PlayRumble(float lowFrequency, float highFrequency, float duration)
    {
        PlayRumble(lowFrequency, highFrequency, duration, includeMouse: false);
    }

    public void PlayRumble(RumbleSettings settings, bool includeMouse)
    {
        PlayRumble(settings.lowFrequency, settings.highFrequency, settings.duration, includeMouse);
    }

    public void PlayRumble(float lowFrequency, float highFrequency, float duration, bool includeMouse)
    {
        if (!isActiveAndEnabled || !IsLocalOwner())
            return;

        Gamepad gamepad = ResolveLocalGamepad();
        IDualMotorRumble mouseRumble = includeMouse ? ResolveLocalMouseRumble() : null;
        if (gamepad == null && mouseRumble == null)
            return;

        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
            return;

        StopRumble();
        rumblingGamepad = gamepad;
        rumblingMouse = mouseRumble;
        rumbleRoutine = StartCoroutine(RumbleRoutine(
            gamepad,
            mouseRumble,
            Mathf.Clamp01(lowFrequency),
            Mathf.Clamp01(highFrequency),
            clampedDuration));
    }

    public void StopRumble()
    {
        if (rumbleRoutine != null)
        {
            StopCoroutine(rumbleRoutine);
            rumbleRoutine = null;
        }

        ResetGamepad(rumblingGamepad);
        ResetMouseRumble(rumblingMouse);
        rumblingGamepad = null;
        rumblingMouse = null;
    }

    private IEnumerator RumbleRoutine(
        Gamepad gamepad,
        IDualMotorRumble mouseRumble,
        float lowFrequency,
        float highFrequency,
        float duration)
    {
        TrySetMotors(gamepad, lowFrequency, highFrequency);
        TrySetMouseMotors(mouseRumble, lowFrequency, highFrequency);
        yield return new WaitForSecondsRealtime(duration);
        ResetGamepad(gamepad);
        ResetMouseRumble(mouseRumble);

        if (rumblingGamepad == gamepad)
            rumblingGamepad = null;
        if (rumblingMouse == mouseRumble)
            rumblingMouse = null;

        rumbleRoutine = null;
    }

    private Gamepad ResolveLocalGamepad()
    {
        if (inputBinding != null)
            return inputBinding.AssignedGamepad;

        if (NetworkManager.Singleton == null)
            return null;

        int playerIndex = (int)NetworkManager.Singleton.LocalClientId;
        if (playerIndex >= 0 && playerIndex < Gamepad.all.Count)
            return Gamepad.all[playerIndex];

        return null;
    }

    private IDualMotorRumble ResolveLocalMouseRumble()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.added)
            return null;

        return mouse as IDualMotorRumble;
    }

    private bool IsLocalOwner()
    {
        return TryGetComponent(out NetworkObject networkObject) && networkObject.IsOwner;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        bool matchesGamepad = rumblingGamepad != null && device == rumblingGamepad;
        bool matchesMouse = rumblingMouse is InputDevice rumbleDevice && device == rumbleDevice;
        if (!matchesGamepad && !matchesMouse)
            return;

        if (change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Disconnected ||
            change == InputDeviceChange.Disabled)
        {
            StopRumble();
        }
    }

    private static void ResetGamepad(Gamepad gamepad)
    {
        TrySetMotors(gamepad, 0f, 0f);

        if (gamepad == null || !gamepad.added)
            return;

        try
        {
            gamepad.ResetHaptics();
        }
        catch (Exception)
        {
        }
    }

    private static void TrySetMotors(Gamepad gamepad, float lowFrequency, float highFrequency)
    {
        if (gamepad == null || !gamepad.added)
            return;

        try
        {
            gamepad.SetMotorSpeeds(lowFrequency, highFrequency);
        }
        catch (Exception)
        {
        }
    }

    private static void TrySetMouseMotors(IDualMotorRumble mouseRumble, float lowFrequency, float highFrequency)
    {
        if (mouseRumble == null)
            return;

        if (mouseRumble is InputDevice device && !device.added)
            return;

        try
        {
            mouseRumble.SetMotorSpeeds(lowFrequency, highFrequency);
        }
        catch (Exception)
        {
        }
    }

    private static void ResetMouseRumble(IDualMotorRumble mouseRumble)
    {
        TrySetMouseMotors(mouseRumble, 0f, 0f);

        if (mouseRumble is not InputDevice device || !device.added)
            return;

        try
        {
            if (device is IHaptics haptics)
                haptics.ResetHaptics();
        }
        catch (Exception)
        {
        }
    }

    private void OnValidate()
    {
        fireRumble.lowFrequency = Mathf.Clamp01(fireRumble.lowFrequency);
        fireRumble.highFrequency = Mathf.Clamp01(fireRumble.highFrequency);
        fireRumble.duration = Mathf.Max(0f, fireRumble.duration);

        damageRumble.lowFrequency = Mathf.Clamp01(damageRumble.lowFrequency);
        damageRumble.highFrequency = Mathf.Clamp01(damageRumble.highFrequency);
        damageRumble.duration = Mathf.Max(0f, damageRumble.duration);

        wallRunRumble.lowFrequency = Mathf.Clamp01(wallRunRumble.lowFrequency);
        wallRunRumble.highFrequency = Mathf.Clamp01(wallRunRumble.highFrequency);
        wallRunRumble.duration = Mathf.Max(0f, wallRunRumble.duration);
    }
}
