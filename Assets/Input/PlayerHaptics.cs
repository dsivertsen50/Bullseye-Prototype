using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

    private LocalPlayerInputBinding inputBinding;
    private Coroutine rumbleRoutine;
    private Gamepad rumblingGamepad;

    private void Awake()
    {
        inputBinding = GetComponent<LocalPlayerInputBinding>();
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
        StopRumble();
    }

    private void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
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

    public void PlayRumble(RumbleSettings settings)
    {
        PlayRumble(settings.lowFrequency, settings.highFrequency, settings.duration);
    }

    public void PlayRumble(float lowFrequency, float highFrequency, float duration)
    {
        if (!isActiveAndEnabled || !IsLocalOwner())
            return;

        Gamepad gamepad = ResolveLocalGamepad();
        if (gamepad == null)
            return;

        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
            return;

        StopRumble();
        rumblingGamepad = gamepad;
        rumbleRoutine = StartCoroutine(RumbleRoutine(
            gamepad,
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
        rumblingGamepad = null;
    }

    private IEnumerator RumbleRoutine(
        Gamepad gamepad,
        float lowFrequency,
        float highFrequency,
        float duration)
    {
        TrySetMotors(gamepad, lowFrequency, highFrequency);
        yield return new WaitForSecondsRealtime(duration);
        ResetGamepad(gamepad);

        if (rumblingGamepad == gamepad)
            rumblingGamepad = null;

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

    private bool IsLocalOwner()
    {
        return TryGetComponent(out NetworkObject networkObject) && networkObject.IsOwner;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (rumblingGamepad == null || device != rumblingGamepad)
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

    private void OnValidate()
    {
        fireRumble.lowFrequency = Mathf.Clamp01(fireRumble.lowFrequency);
        fireRumble.highFrequency = Mathf.Clamp01(fireRumble.highFrequency);
        fireRumble.duration = Mathf.Max(0f, fireRumble.duration);

        damageRumble.lowFrequency = Mathf.Clamp01(damageRumble.lowFrequency);
        damageRumble.highFrequency = Mathf.Clamp01(damageRumble.highFrequency);
        damageRumble.duration = Mathf.Max(0f, damageRumble.duration);
    }
}
