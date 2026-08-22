using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Local pause-menu interaction sounds. Clips are optional Inspector
/// assignments; empty slots stay silent and do not throw.
/// </summary>
public class MenuAudioController : MonoBehaviour
{
    [Header("Menu Audio")]
    [SerializeField] private AudioSource menuAudioSource;
    [SerializeField] private AudioClip navigateSound;
    [SerializeField] private AudioClip selectSound;
    [SerializeField] private AudioClip backSound;
    [SerializeField] private AudioClip sliderAdjustSound;

    [Header("Slider Feedback")]
    [SerializeField] private float sliderSoundInterval = 0.12f;

    private float lastSliderSoundTime = -999f;

    private void Awake()
    {
        EnsureAudioSource();
    }

    public void PlayNavigate()
    {
        Play(navigateSound);
    }

    public void PlaySelect()
    {
        Play(selectSound);
    }

    public void PlayBack()
    {
        Play(backSound);
    }

    public void PlaySliderAdjust()
    {
        if (Time.unscaledTime - lastSliderSoundTime < Mathf.Max(0.04f, sliderSoundInterval))
            return;

        lastSliderSoundTime = Time.unscaledTime;
        Play(sliderAdjustSound != null ? sliderAdjustSound : navigateSound);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null)
            return;

        EnsureAudioSource();
        if (menuAudioSource == null)
            return;

        menuAudioSource.PlayOneShot(clip);
    }

    private void EnsureAudioSource()
    {
        if (menuAudioSource == null)
            menuAudioSource = GetComponent<AudioSource>();

        if (menuAudioSource == null)
            menuAudioSource = gameObject.AddComponent<AudioSource>();

        menuAudioSource.playOnAwake = false;
        menuAudioSource.loop = false;
        menuAudioSource.spatialBlend = 0f;
        menuAudioSource.ignoreListenerPause = true;

        AudioMixerGroup uiGroup = PlayerGameSettings.UiMixerGroup;
        if (uiGroup != null)
            menuAudioSource.outputAudioMixerGroup = uiGroup;
    }
}
