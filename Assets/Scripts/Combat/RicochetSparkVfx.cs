using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Short local spark burst at world-surface bullet marks.
/// </summary>
public static class RicochetSparkVfx
{
    private const int PoolSize = 24;
    private static readonly Queue<ParticleSystem> pool = new();
    private static Transform poolRoot;
    private static Material sparkMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pool.Clear();
        poolRoot = null;
        sparkMaterial = null;
    }

    public static void Play(Vector3 point, Vector3 normal)
    {
        ParticleSystem system = Rent();
        if (system == null)
            return;

        Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        system.transform.SetPositionAndRotation(point + n * 0.012f, Quaternion.LookRotation(n));
        system.Clear(true);
        system.Play(true);
    }

    private static ParticleSystem Rent()
    {
        EnsurePool();
        int count = pool.Count;
        for (int i = 0; i < count; i++)
        {
            ParticleSystem candidate = pool.Dequeue();
            pool.Enqueue(candidate);
            if (candidate != null && !candidate.isPlaying)
                return candidate;
        }

        return pool.Count > 0 ? pool.Peek() : null;
    }

    private static void EnsurePool()
    {
        if (poolRoot != null && pool.Count > 0)
            return;

        var rootObject = new GameObject("RicochetSparkPool");
        Object.DontDestroyOnLoad(rootObject);
        poolRoot = rootObject.transform;
        sparkMaterial = CreateMaterial();

        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("RicochetSpark");
            go.transform.SetParent(poolRoot, false);
            ParticleSystem system = go.AddComponent<ParticleSystem>();
            Configure(system);
            pool.Enqueue(system);
        }
    }

    private static void Configure(ParticleSystem system)
    {
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.18f;
        main.startLifetime = 0.14f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.95f, 0.7f, 1f),
            new Color(1f, 0.55f, 0.15f, 1f));
        main.gravityModifier = 1.1f;
        main.maxParticles = 24;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12, 18) });

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.008f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = system.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = system.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = sparkMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static Material CreateMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("Hidden/HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        var material = new Material(shader)
        {
            name = "RicochetSparkUnlit"
        };
        if (material.HasProperty("_SurfaceType"))
            material.SetFloat("_SurfaceType", 1f);
        if (material.HasProperty("_BlendMode"))
            material.SetFloat("_BlendMode", 0f);
        if (material.HasProperty("_UnlitColor"))
            material.SetColor("_UnlitColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        return material;
    }
}
