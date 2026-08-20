using System;
using UnityEngine;

/// <summary>
/// Local, persisted player preferences. Prototype storage uses PlayerPrefs.
/// Not networked.
/// </summary>
public static class PlayerGameSettings
{
    private const string MouseXKey = "Bullseye_MouseSensX";
    private const string MouseYKey = "Bullseye_MouseSensY";
    private const string ControllerXKey = "Bullseye_ControllerSensX";
    private const string ControllerYKey = "Bullseye_ControllerSensY";
    private const string AimSensitivityKey = "Bullseye_AimSensitivity";
    private const string InvertYKey = "Bullseye_InvertY";
    private const string MasterVolumeKey = "Bullseye_MasterVolume";

    public const float DefaultMouseSensitivity = 4f;
    public const float DefaultControllerSensitivity = 35f;
    public const float MinControllerSensitivity = 5f;
    public const float MaxControllerSensitivity = 200f;
    public const float DefaultAimSensitivity = 0.4f;
    public const float DefaultMasterVolume = 1f;

    public static event Action Changed;

    public static float MouseSensitivityX { get; private set; } = DefaultMouseSensitivity;
    public static float MouseSensitivityY { get; private set; } = DefaultMouseSensitivity;
    public static float ControllerSensitivityX { get; private set; } = DefaultControllerSensitivity;
    public static float ControllerSensitivityY { get; private set; } = DefaultControllerSensitivity;
    public static float AimSensitivityMultiplier { get; private set; } = DefaultAimSensitivity;
    public static bool InvertY { get; private set; }
    public static float MasterVolume { get; private set; } = DefaultMasterVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        Load();
        ApplyAudio();
    }

    public static void Load()
    {
        MouseSensitivityX = PlayerPrefs.GetFloat(MouseXKey, DefaultMouseSensitivity);
        MouseSensitivityY = PlayerPrefs.GetFloat(MouseYKey, DefaultMouseSensitivity);
        ControllerSensitivityX = PlayerPrefs.GetFloat(ControllerXKey, DefaultControllerSensitivity);
        ControllerSensitivityY = PlayerPrefs.GetFloat(ControllerYKey, DefaultControllerSensitivity);
        AimSensitivityMultiplier = PlayerPrefs.GetFloat(AimSensitivityKey, DefaultAimSensitivity);
        InvertY = PlayerPrefs.GetInt(InvertYKey, 0) != 0;
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume);
    }

    public static void SetMouseSensitivity(float x, float y)
    {
        MouseSensitivityX = Mathf.Clamp(x, 0.2f, 20f);
        MouseSensitivityY = Mathf.Clamp(y, 0.2f, 20f);
        PlayerPrefs.SetFloat(MouseXKey, MouseSensitivityX);
        PlayerPrefs.SetFloat(MouseYKey, MouseSensitivityY);
        SaveAndNotify();
    }

    public static void SetControllerSensitivity(float x, float y)
    {
        ControllerSensitivityX = Mathf.Clamp(x, MinControllerSensitivity, MaxControllerSensitivity);
        ControllerSensitivityY = Mathf.Clamp(y, MinControllerSensitivity, MaxControllerSensitivity);
        PlayerPrefs.SetFloat(ControllerXKey, ControllerSensitivityX);
        PlayerPrefs.SetFloat(ControllerYKey, ControllerSensitivityY);
        SaveAndNotify();
    }

    public static void SetAimSensitivity(float value)
    {
        AimSensitivityMultiplier = Mathf.Clamp(value, 0.1f, 1f);
        PlayerPrefs.SetFloat(AimSensitivityKey, AimSensitivityMultiplier);
        SaveAndNotify();
    }

    public static void SetInvertY(bool invert)
    {
        InvertY = invert;
        PlayerPrefs.SetInt(InvertYKey, invert ? 1 : 0);
        SaveAndNotify();
    }

    public static void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        ApplyAudio();
        SaveAndNotify();
    }

    private static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
    }

    private static void SaveAndNotify()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
