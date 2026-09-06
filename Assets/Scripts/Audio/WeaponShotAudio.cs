using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled 3D SFX for bullet impacts and near-miss flybys. Cosmetic only.
/// Routes through the SFX mixer so the player's SFX volume applies.
/// </summary>
public class WeaponShotAudio : MonoBehaviour
{
    private const int PrewarmCount = 12;
    private const int MaxSources = 24;

    private static WeaponShotAudio instance;
    private static WeaponShotAudioSettings cachedSettings;
    private static float nextFlybyTime;

    private readonly List<AudioSource> active = new(16);
    private readonly Queue<AudioSource> pool = new();
    private readonly List<Vector3> selectedImpactPoints = new(8);

    public static WeaponShotAudioSettings Settings
    {
        get
        {
            if (cachedSettings == null)
                cachedSettings = WeaponShotAudioSettings.Load();
            return cachedSettings;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        cachedSettings = null;
        nextFlybyTime = 0f;
    }

    public static WeaponShotAudio Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<WeaponShotAudio>();
        if (instance != null)
        {
            instance.Bootstrap();
            return instance;
        }

        var go = new GameObject("WeaponShotAudio");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        instance = go.AddComponent<WeaponShotAudio>();
        instance.Bootstrap();
        return instance;
    }

    public static void PlayImpacts(IList<Vector3> points, WeaponShotAudioOverrides overrides)
    {
        WeaponShotAudioSettings settings = Settings;
        if (settings == null || !settings.ImpactEnabled)
            return;

        if (points == null || points.Count == 0)
            return;

        AudioClip[] clips = overrides != null
            ? overrides.ResolveImpactClips(settings.ImpactClips)
            : settings.ImpactClips;
        if (!HasAnyClip(clips))
            return;

        Ensure().PlayImpactInternal(points, clips, settings);
    }

    public static void PlayRicochets(IList<Vector3> points)
    {
        WeaponShotAudioSettings settings = Settings;
        if (settings == null || !settings.RicochetEnabled)
            return;

        if (points == null || points.Count == 0)
            return;

        AudioClip[] clips = settings.RicochetClips;
        if (!HasAnyClip(clips))
            return;

        Ensure().PlayRicochetInternal(points, clips, settings);
    }

    public static void PlayFlyby(Vector3 closestPoint, float distance, WeaponShotAudioOverrides overrides)
    {
        WeaponShotAudioSettings settings = Settings;
        if (settings == null || !settings.NearMissEnabled)
            return;
        if (overrides != null && !overrides.NearMissEnabled)
            return;

        if (Time.time < nextFlybyTime)
        {
            if (settings.DebugNearMiss && DebugEnabled)
                Debug.Log("Near miss cooldown active");
            return;
        }

        AudioClip[] clips = overrides != null
            ? overrides.ResolveFlybyClips(settings.FlybyClips)
            : settings.FlybyClips;
        if (!HasAnyClip(clips))
            return;

        AudioClip clip = PickClip(clips);
        if (clip == null)
            return;

        float volume = settings.NearMissVolume * RandomVolumeScale(0.08f);
        if (settings.InnerNearMissRadius > 0f && distance <= settings.InnerNearMissRadius)
            volume *= settings.InnerNearMissVolumeMultiplier;

        float pitch = RandomPitch(settings.NearMissPitchVariation);
        Ensure().PlayOne(
            clip,
            closestPoint,
            volume,
            pitch,
            settings.FlybyMinDistance,
            settings.FlybyMaxDistance);

        nextFlybyTime = Time.time + settings.NearMissCooldown;

        if (settings.DebugNearMiss && DebugEnabled)
            Debug.Log($"Near miss flyby at {closestPoint} distance {distance:0.00}m");
    }

    private void Update()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            AudioSource source = active[i];
            if (source == null)
            {
                active.RemoveAt(i);
                continue;
            }

            if (source.isPlaying)
                continue;

            source.Stop();
            source.clip = null;
            source.gameObject.SetActive(false);
            pool.Enqueue(source);
            active.RemoveAt(i);
        }
    }

    private void PlayImpactInternal(
        IList<Vector3> points,
        AudioClip[] clips,
        WeaponShotAudioSettings settings)
    {
        SelectImpactPoints(points, settings.MaxImpactSoundsPerShot, settings.ImpactSoundSeparation, selectedImpactPoints);
        for (int i = 0; i < selectedImpactPoints.Count; i++)
        {
            AudioClip clip = PickClip(clips);
            if (clip == null)
                continue;

            float volume = settings.ImpactVolume * RandomVolumeScale(settings.ImpactVolumeVariation);
            float pitch = RandomPitch(settings.ImpactPitchVariation);
            PlayOne(
                clip,
                selectedImpactPoints[i],
                volume,
                pitch,
                settings.ImpactMinDistance,
                settings.ImpactMaxDistance);
        }
    }

    private void PlayRicochetInternal(
        IList<Vector3> points,
        AudioClip[] clips,
        WeaponShotAudioSettings settings)
    {
        for (int i = 0; i < points.Count; i++)
        {
            AudioClip clip = PickClip(clips);
            if (clip == null)
                continue;

            float volume = settings.RicochetVolume * RandomVolumeScale(settings.RicochetVolumeVariation);
            float pitch = RandomPitch(settings.RicochetPitchVariation);
            PlayOne(
                clip,
                points[i],
                volume,
                pitch,
                settings.RicochetMinDistance,
                settings.RicochetMaxDistance);
        }
    }

    private void PlayOne(
        AudioClip clip,
        Vector3 position,
        float volume,
        float pitch,
        float minDistance,
        float maxDistance)
    {
        AudioSource source = Rent();
        if (source == null)
            return;

        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Max(0f, volume);
        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.minDistance = Mathf.Max(0.05f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 0.1f, maxDistance);
        source.gameObject.SetActive(true);
        PlayerGameSettings.RouteToSfx(source);
        source.Play();
        active.Add(source);
    }

    private AudioSource Rent()
    {
        RecycleFinished();

        while (pool.Count > 0)
        {
            AudioSource pooled = pool.Dequeue();
            if (pooled != null)
                return pooled;
        }

        if (active.Count >= MaxSources)
        {
            AudioSource oldest = active[0];
            active.RemoveAt(0);
            if (oldest != null)
                oldest.Stop();
            return oldest;
        }

        return CreateSource();
    }

    private void RecycleFinished()
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            AudioSource source = active[i];
            if (source != null && source.isPlaying)
                continue;

            if (source != null)
            {
                source.Stop();
                source.clip = null;
                source.gameObject.SetActive(false);
                pool.Enqueue(source);
            }

            active.RemoveAt(i);
        }
    }

    private void Bootstrap()
    {
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);

        int have = pool.Count + active.Count;
        for (int i = have; i < PrewarmCount; i++)
        {
            AudioSource created = CreateSource();
            if (created == null)
                break;
            created.gameObject.SetActive(false);
            pool.Enqueue(created);
        }
    }

    private AudioSource CreateSource()
    {
        var go = new GameObject("PooledSpatialAudio");
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.priority = 96;
        PlayerGameSettings.RouteToSfx(source);
        return source;
    }

    private static void SelectImpactPoints(
        IList<Vector3> points,
        int maxCount,
        float minSeparation,
        List<Vector3> selected)
    {
        selected.Clear();
        if (points == null || points.Count == 0 || maxCount <= 0)
            return;

        float minSqr = minSeparation * minSeparation;
        for (int i = 0; i < points.Count && selected.Count < maxCount; i++)
        {
            Vector3 point = points[i];
            bool farEnough = true;
            if (minSqr > 0f)
            {
                for (int j = 0; j < selected.Count; j++)
                {
                    if ((selected[j] - point).sqrMagnitude < minSqr)
                    {
                        farEnough = false;
                        break;
                    }
                }
            }

            if (farEnough || selected.Count == 0)
                selected.Add(point);
        }
    }

    private static AudioClip PickClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static bool HasAnyClip(AudioClip[] clips)
    {
        if (clips == null)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    private static float RandomPitch(float variation)
    {
        if (variation <= 0f)
            return 1f;
        return 1f + Random.Range(-variation, variation);
    }

    private static float RandomVolumeScale(float variation)
    {
        if (variation <= 0f)
            return 1f;
        return Random.Range(1f - variation, 1f);
    }

    private static bool DebugEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            WeaponShotAudioSettings loaded = cachedSettings;
            return loaded != null && loaded.DebugNearMiss;
#endif
        }
    }
}
