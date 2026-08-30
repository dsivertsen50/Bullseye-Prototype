using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

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
    private const string AimSensitivityKey = "Bullseye_AimLookScale";
    private const string InvertYKey = "Bullseye_InvertY";
    private const string MasterVolumeKey = "Bullseye_MasterVolume";
    private const string SfxVolumeKey = "Bullseye_SFXVolume";
    private const string MusicVolumeKey = "Bullseye_MusicVolume";
    private const string BrightnessKey = "Bullseye_Brightness";
    private const string MixerResourcePath = "GameAudioMixer";
    private const string MasterParam = "MasterVolume";
    private const string SfxParam = "SFXVolume";
    private const string MusicParam = "MusicVolume";
    private const float MuteDecibels = -80f;

    public const float DefaultMouseSensitivity = 4f;
    public const float DefaultControllerSensitivity = 35f;
    public const float MinControllerSensitivity = 5f;
    public const float MaxControllerSensitivity = 200f;
    public const float DefaultAimSensitivity = 1f;
    public const float DefaultMasterVolume = 1f;
    public const float DefaultSfxVolume = 1f;
    public const float DefaultMusicVolume = 1f;
    public const float DefaultBrightness = 1f;
    public const float MinBrightness = 0.5f;
    public const float MaxBrightness = 1.5f;

    public static event Action Changed;

    public static float MouseSensitivityX { get; private set; } = DefaultMouseSensitivity;
    public static float MouseSensitivityY { get; private set; } = DefaultMouseSensitivity;
    public static float ControllerSensitivityX { get; private set; } = DefaultControllerSensitivity;
    public static float ControllerSensitivityY { get; private set; } = DefaultControllerSensitivity;
    public static float AimSensitivityMultiplier { get; private set; } = DefaultAimSensitivity;
    public static bool InvertY { get; private set; }
    public static float MasterVolume { get; private set; } = DefaultMasterVolume;
    public static float SfxVolume { get; private set; } = DefaultSfxVolume;
    public static float MusicVolume { get; private set; } = DefaultMusicVolume;
    public static float Brightness { get; private set; } = DefaultBrightness;

    public static AudioMixerGroup SfxMixerGroup { get; private set; }
    public static AudioMixerGroup MusicMixerGroup { get; private set; }
    public static AudioMixerGroup UiMixerGroup { get; private set; }

    private static AudioMixer mixer;
    private static Volume brightnessVolume;
    private static Exposure brightnessExposure;
    private static bool mixerResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadOnStartup()
    {
        Load();
        ResolveMixer();
        ApplyAudio();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ApplyAfterSceneLoad()
    {
        ResolveMixer();
        ApplyAudio();
        ApplyBrightness();
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
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
        Brightness = Mathf.Clamp(PlayerPrefs.GetFloat(BrightnessKey, DefaultBrightness), MinBrightness, MaxBrightness);
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

    public static void SetSfxVolume(float volume)
    {
        SfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        ApplyAudio();
        SaveAndNotify();
    }

    public static void SetMusicVolume(float volume)
    {
        MusicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        ApplyAudio();
        SaveAndNotify();
    }

    public static void SetBrightness(float brightness)
    {
        Brightness = Mathf.Clamp(brightness, MinBrightness, MaxBrightness);
        PlayerPrefs.SetFloat(BrightnessKey, Brightness);
        ApplyBrightness();
        SaveAndNotify();
    }

    public static void RouteToSfx(AudioSource source)
    {
        Route(source, SfxMixerGroup);
    }

    public static void RouteToUi(AudioSource source)
    {
        Route(source, UiMixerGroup != null ? UiMixerGroup : SfxMixerGroup);
    }

    public static void RouteToMusic(AudioSource source)
    {
        Route(source, MusicMixerGroup);
    }

    private static void Route(AudioSource source, AudioMixerGroup group)
    {
        if (source == null || group == null)
            return;

        source.outputAudioMixerGroup = group;
    }

    private static void ResolveMixer()
    {
        if (mixerResolved && mixer != null)
            return;

        mixerResolved = true;
        mixer = Resources.Load<AudioMixer>(MixerResourcePath);
        if (mixer == null)
            return;

        SfxMixerGroup = FindGroup("SFX");
        MusicMixerGroup = FindGroup("Music");
        UiMixerGroup = FindGroup("UI");
        if (UiMixerGroup == null)
            UiMixerGroup = SfxMixerGroup;
    }

    private static AudioMixerGroup FindGroup(string name)
    {
        if (mixer == null || string.IsNullOrEmpty(name))
            return null;

        AudioMixerGroup[] groups = mixer.FindMatchingGroups(name);
        if (groups == null)
            return null;

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i] != null && groups[i].name == name)
                return groups[i];
        }

        return groups.Length > 0 ? groups[0] : null;
    }

    private static void ApplyAudio()
    {
        ResolveMixer();

        if (mixer != null)
        {
            AudioListener.volume = 1f;
            mixer.SetFloat(MasterParam, LinearToDecibels(MasterVolume));
            mixer.SetFloat(SfxParam, LinearToDecibels(SfxVolume));
            mixer.SetFloat(MusicParam, LinearToDecibels(MusicVolume));
            return;
        }

        AudioListener.volume = MasterVolume * SfxVolume;
    }

    private static void ApplyBrightness()
    {
        EnsureBrightnessVolume();
        if (brightnessExposure == null)
            return;

        // 100% keeps the current HDRP look (0 EV). 50% / 150% shift by 1 EV.
        float exposureCompensation = (Brightness - DefaultBrightness) * 2f;
        brightnessExposure.compensation.Override(exposureCompensation);
    }

    private static void EnsureBrightnessVolume()
    {
        if (brightnessVolume != null && brightnessExposure != null)
            return;

        GameObject host = GameObject.Find("LocalBrightnessSettings");
        if (host == null)
        {
            host = new GameObject("LocalBrightnessSettings");
            UnityEngine.Object.DontDestroyOnLoad(host);
        }

        brightnessVolume = host.GetComponent<Volume>();
        if (brightnessVolume == null)
            brightnessVolume = host.AddComponent<Volume>();

        brightnessVolume.isGlobal = true;
        brightnessVolume.priority = 99f;
        brightnessVolume.weight = 1f;

        VolumeProfile profile = brightnessVolume.profile;
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            brightnessVolume.profile = profile;
        }

        if (!profile.TryGet(out brightnessExposure))
            brightnessExposure = profile.Add<Exposure>(false);

        brightnessExposure.active = true;
        brightnessExposure.compensation.Override((Brightness - DefaultBrightness) * 2f);
    }

    private static float LinearToDecibels(float linear)
    {
        if (linear <= 0.0001f)
            return MuteDecibels;

        return Mathf.Clamp(Mathf.Log10(linear) * 20f, MuteDecibels, 0f);
    }

    private static void SaveAndNotify()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
