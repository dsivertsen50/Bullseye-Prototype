using UnityEngine;

/// <summary>
/// Small rotating voice pool for weapon fire so automatic weapons cannot
/// stack one long PlayOneShot per bullet. Unity only has a limited number
/// of real voices; uncapped overlapping gunshots lose to ricochets.
/// </summary>
public static class WeaponFireSfx
{
    public const int VoiceCount = 6;
    public const int Priority = 32;
    private const string ChildName = "WeaponFireSfx";

    public static void ConfigureOneShotSource(AudioSource source, bool spatial)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = spatial ? 1f : 0f;
        source.priority = Priority;
        PlayerGameSettings.RouteToSfx(source);
    }

    public static AudioSource[] EnsureVoices(
        Transform host,
        bool spatial,
        float minDistance,
        float maxDistance)
    {
        if (host == null)
            return null;

        Transform child = host.Find(ChildName);
        if (child == null)
        {
            var go = new GameObject(ChildName);
            child = go.transform;
            child.SetParent(host, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }

        AudioSource[] sources = child.GetComponents<AudioSource>();
        if (sources.Length < VoiceCount)
        {
            for (int i = sources.Length; i < VoiceCount; i++)
                child.gameObject.AddComponent<AudioSource>();
            sources = child.GetComponents<AudioSource>();
        }

        float min = Mathf.Max(0.05f, minDistance);
        float max = Mathf.Max(min + 0.1f, maxDistance);
        var voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            voices[i] = sources[i];
            ConfigureVoice(voices[i], spatial, min, max);
        }

        return voices;
    }

    public static void Play(AudioSource[] voices, ref int nextIndex, AudioClip clip, float volume)
    {
        if (voices == null || voices.Length == 0 || clip == null)
            return;

        int index = nextIndex % voices.Length;
        nextIndex = (index + 1) % voices.Length;
        AudioSource source = voices[index];
        if (source == null || !source.isActiveAndEnabled)
            return;

        source.Stop();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = 1f;
        PlayerGameSettings.RouteToSfx(source);
        source.Play();
    }

    private static void ConfigureVoice(AudioSource source, bool spatial, float minDistance, float maxDistance)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.priority = Priority;
        source.spatialBlend = spatial ? 1f : 0f;
        source.dopplerLevel = spatial ? 0.15f : 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        PlayerGameSettings.RouteToSfx(source);
    }
}
