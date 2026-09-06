using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client-local pooled bullet marks. Cosmetic only: no damage, physics, or
/// network objects.
/// </summary>
public class BulletImpactManager : MonoBehaviour
{
    private static BulletImpactManager instance;
    private static BulletImpactSettings cachedSettings;

    private readonly List<BulletImpactDecal> active = new(128);
    private readonly Queue<BulletImpactDecal> pool = new();
    private readonly List<PendingImpact> pending = new(16);

    private struct PendingImpact
    {
        public Vector3 point;
        public Vector3 normal;
        public float size;
        public Material material;
        public float rotation;
        public float spawnTime;
    }

    private BulletImpactSettings settings;
    private Transform poolRoot;
    private bool loggedMissingPrefab;
    private static int skippedLayerMask = int.MinValue;

    public static BulletImpactManager Instance => instance;
    public int ActiveCount => active.Count;
    public BulletImpactSettings Settings => settings;

    public static float OverlapDistance
    {
        get
        {
            BulletImpactSettings loaded = ResolveSettings();
            return loaded != null ? loaded.OverlapDistance : 0.05f;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        cachedSettings = null;
        skippedLayerMask = int.MinValue;
    }

    public static BulletImpactManager Ensure()
    {
        if (instance != null)
            return instance;

        instance = FindAnyObjectByType<BulletImpactManager>();
        if (instance != null)
        {
            instance.Bootstrap();
            return instance;
        }

        var go = new GameObject("BulletImpactManager");
        if (Application.isPlaying)
            DontDestroyOnLoad(go);
        instance = go.AddComponent<BulletImpactManager>();
        instance.Bootstrap();
        return instance;
    }

    public static bool IsValidSurface(Collider collider)
    {
        if (collider == null || collider.isTrigger)
            return false;

        if (collider.GetComponentInParent<RejectBulletDecal>() != null)
            return false;

        if (collider.GetComponentInParent<PlayerHealth>() != null)
            return false;

        if (collider.GetComponentInParent<BullseyeTarget>() != null)
            return false;

        int layer = collider.gameObject.layer;
        if ((SkippedLayers & (1 << layer)) != 0)
            return false;

        BulletImpactSettings loaded = ResolveSettings();
        if (loaded != null && (loaded.ValidLayers.value & (1 << layer)) == 0)
            return false;

        return true;
    }

    public static void LogDistanceRejected(float distance, float maxDistance, Vector3 point, Vector3 normal)
    {
        BulletImpactSettings loaded = ResolveSettings();
        if (loaded == null || !loaded.DebugImpacts)
            return;

        if (!DebugEnabled)
            return;

        Debug.Log($"Bullet decal rejected: distance {distance:0.0}m exceeds {maxDistance:0.0}m");
        Debug.DrawRay(point, normal * 0.35f, Color.red, 1.5f);
    }

    public void SpawnImpacts(
        IList<Vector3> points,
        IList<Vector3> normals,
        float scale,
        int seed,
        BulletImpactDecalSet variantSet)
    {
        SpawnImpacts(points, normals, scale, seed, variantSet, null, null);
    }

    public void SpawnImpacts(
        IList<Vector3> points,
        IList<Vector3> normals,
        float scale,
        int seed,
        BulletImpactDecalSet variantSet,
        IList<float> delays,
        IList<bool> sparks)
    {
        if (points == null || normals == null || points.Count == 0)
            return;

        Bootstrap();
        if (settings == null || settings.DecalPrefab == null)
        {
            if (!loggedMissingPrefab)
            {
                Debug.LogWarning("BulletImpactSettings is missing a decal prefab.");
                loggedMissingPrefab = true;
            }

            return;
        }

        var rng = new System.Random(seed);
        float size = settings.BaseSize * Mathf.Max(0.01f, scale);
        BulletImpactDecalSet set = variantSet != null && variantSet.HasVariants
            ? variantSet
            : settings.DefaultVariantSet;

        int count = Mathf.Min(points.Count, normals.Count);
        for (int i = 0; i < count; i++)
        {
            Material material = set != null ? set.GetVariant(rng.Next()) : null;
            float rotation = (float)(rng.NextDouble() * 360.0);
            float delay = delays != null && i < delays.Count ? Mathf.Max(0f, delays[i]) : 0f;
            if (delay <= 0f)
            {
                SpawnOne(points[i], normals[i], size, material, rotation);
                continue;
            }

            pending.Add(new PendingImpact
            {
                point = points[i],
                normal = normals[i],
                size = size,
                material = material,
                rotation = rotation,
                spawnTime = Time.unscaledTime + delay
            });
        }
    }

    private void Update()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            PendingImpact waiting = pending[i];
            if (Time.unscaledTime < waiting.spawnTime)
                continue;

            pending.RemoveAt(i);
            SpawnOne(waiting.point, waiting.normal, waiting.size, waiting.material, waiting.rotation);
        }

        for (int i = active.Count - 1; i >= 0; i--)
        {
            BulletImpactDecal decal = active[i];
            if (decal == null)
            {
                active.RemoveAt(i);
                continue;
            }

            decal.Tick();
            if (decal.IsExpired)
                RecycleAt(i);
        }
    }

    private void Bootstrap()
    {
        if (settings == null)
            settings = ResolveSettings();

        if (poolRoot == null)
        {
            poolRoot = transform;
            if (Application.isPlaying)
                DontDestroyOnLoad(gameObject);
        }

        if (settings == null || settings.DecalPrefab == null)
            return;

        int desired = settings.PrewarmCount;
        int have = pool.Count + active.Count;
        for (int i = have; i < desired; i++)
        {
            BulletImpactDecal created = CreateInstance();
            if (created == null)
                break;
            created.Sleep();
            pool.Enqueue(created);
        }
    }

    private void SpawnOne(Vector3 point, Vector3 normal, float size, Material material, float rotation)
    {
        if (settings != null && settings.DebugImpacts && DebugEnabled)
            Debug.DrawRay(point, normal.normalized * 0.3f, Color.green, 2f);

        RicochetSparkVfx.Play(point, normal);

        BulletImpactDecal decal = Rent();
        if (decal == null)
            return;

        decal.Play(
            point,
            normal,
            size,
            material,
            settings.Lifetime,
            settings.FadeDuration,
            settings.SurfaceOffset,
            rotation);
        active.Add(decal);
    }

    private BulletImpactDecal Rent()
    {
        int max = settings != null ? settings.MaxActiveDecals : 200;
        if (active.Count >= max)
            RecycleAt(0);

        while (pool.Count > 0)
        {
            BulletImpactDecal pooled = pool.Dequeue();
            if (pooled != null)
                return pooled;
        }

        if (active.Count >= max)
        {
            BulletImpactDecal oldest = active[0];
            active.RemoveAt(0);
            return oldest;
        }

        return CreateInstance();
    }

    private BulletImpactDecal RecycleAt(int index)
    {
        if (index < 0 || index >= active.Count)
            return null;

        BulletImpactDecal decal = active[index];
        active.RemoveAt(index);
        if (decal == null)
            return null;

        decal.Sleep();
        pool.Enqueue(decal);
        return decal;
    }

    private BulletImpactDecal CreateInstance()
    {
        if (settings == null || settings.DecalPrefab == null)
            return null;

        GameObject spawned = Instantiate(settings.DecalPrefab, poolRoot);
        spawned.name = "BulletImpactDecal";
        spawned.SetActive(false);
        if (!spawned.TryGetComponent(out BulletImpactDecal decal))
            decal = spawned.AddComponent<BulletImpactDecal>();
        return decal;
    }

    private static BulletImpactSettings ResolveSettings()
    {
        if (cachedSettings == null)
            cachedSettings = BulletImpactSettings.Load();
        return cachedSettings;
    }

    private static int SkippedLayers
    {
        get
        {
            if (skippedLayerMask != int.MinValue)
                return skippedLayerMask;

            skippedLayerMask = LayerBit("Ignore Raycast")
                | LayerBit("Water")
                | LayerBit("UI")
                | LayerBit("FirstPersonWeapon")
                | LayerBit("WorldWeapon")
                | LayerBit("WeaponPickup")
                | LayerBit("WeaponPickup ")
                | LayerBit("BullseyeDebris")
                | LayerBit("LocalPlayerBody");
            return skippedLayerMask;
        }
    }

    private static int LayerBit(string name)
    {
        int layer = LayerMask.NameToLayer(name);
        return layer >= 0 ? (1 << layer) : 0;
    }

    private static bool DebugEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            BulletImpactSettings loaded = cachedSettings;
            return loaded != null && loaded.DebugImpacts;
#endif
        }
    }
}
