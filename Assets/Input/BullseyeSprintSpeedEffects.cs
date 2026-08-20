using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Owner-only Cowsins-style sprint speed-line particles. Cosmetic and not
/// networked. Driven by Bullseye movement state instead of Cowsins player
/// dependencies.
/// </summary>
public class BullseyeSprintSpeedEffects : NetworkBehaviour
{
    [SerializeField] private Transform effectsRoot;
    [SerializeField] private ParticleSystem speedLines;
    [SerializeField] private Material speedLineMaterial;

    [Header("Activation")]
    [SerializeField] private float minMovementSpeed = 6f;
    [SerializeField, Range(0.1f, 2f)] private float sprintIntensity = 1f;

    [Header("Emission")]
    [SerializeField] private float slowSprintEmission = 70f;
    [SerializeField] private float fullSprintEmission = 160f;
    [SerializeField] private float fadeInSpeed = 280f;
    [SerializeField] private float fadeOutSpeed = 420f;

    [Header("Particles")]
    [SerializeField] private float particleSpeed = 15f;
    [SerializeField] private float particleSize = 0.9f;
    [SerializeField] private float spawnDistance = 4.91f;
    [SerializeField] private float spawnRadius = 15f;
    [SerializeField, Range(0.05f, 1f)] private float effectOpacity = 0.32f;
    [SerializeField] private float stretchLength = 12f;

    private PlayerMovement movement;
    private PlayerHealth playerHealth;
    private PlayerCameraEffects cameraEffects;
    private ParticleSystemRenderer speedLinesRenderer;
    private bool ownerEffectsEnabled;
    private float currentEmission;
    private bool clearedOnDeath;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        cameraEffects = GetComponent<PlayerCameraEffects>();
    }

    public override void OnNetworkSpawn()
    {
        ownerEffectsEnabled = IsOwner;
        if (!ownerEffectsEnabled)
        {
            enabled = false;
            if (speedLines != null)
                speedLines.gameObject.SetActive(false);
            return;
        }

        EnsureParticleSystem();
        StopImmediate();
    }

    public override void OnNetworkDespawn()
    {
        ownerEffectsEnabled = false;
        StopImmediate();
    }

    private void LateUpdate()
    {
        if (!ownerEffectsEnabled)
            return;

        if (speedLines == null)
            EnsureParticleSystem();
        if (speedLines == null)
            return;

        bool dead = playerHealth != null && playerHealth.IsDead;
        if (dead)
        {
            if (!clearedOnDeath)
            {
                StopImmediate();
                clearedOnDeath = true;
            }
            return;
        }

        if (clearedOnDeath)
        {
            StopImmediate();
            clearedOnDeath = false;
        }

        bool paused = LocalPlayerMenuState.IsOpen(this);
        bool sprinting = movement != null && movement.IsSprinting;
        float speed = movement != null ? movement.HorizontalSpeed : 0f;
        bool shouldPlay = !paused && sprinting && speed >= minMovementSpeed;

        float targetEmission = 0f;
        if (shouldPlay)
        {
            float runSpeed = movement != null ? Mathf.Max(0.01f, movement.RunSpeed) : 10f;
            float speedScale = Mathf.InverseLerp(minMovementSpeed, runSpeed, speed);
            targetEmission = Mathf.Lerp(slowSprintEmission, fullSprintEmission, speedScale) * sprintIntensity;
        }

        float fadeSpeed = shouldPlay ? fadeInSpeed : fadeOutSpeed;
        currentEmission = Mathf.MoveTowards(currentEmission, targetEmission, fadeSpeed * Time.deltaTime);

        ParticleSystem.EmissionModule emission = speedLines.emission;
        emission.rateOverTime = currentEmission;

        if (currentEmission > 0.5f)
        {
            if (!speedLines.isPlaying)
                speedLines.Play(true);
        }
        else if (speedLines.isPlaying)
        {
            speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void EnsureParticleSystem()
    {
        if (effectsRoot == null && cameraEffects != null)
            effectsRoot = cameraEffects.EffectsRoot;

        if (effectsRoot == null)
        {
            Transform found = transform.Find("CameraRoot/CameraEffectsRoot");
            if (found != null)
                effectsRoot = found;
        }

        if (speedLines != null)
        {
            speedLinesRenderer = speedLines.GetComponent<ParticleSystemRenderer>();
            ApplyRendererMaterial();
            return;
        }

        if (effectsRoot == null)
            return;

        Transform existing = effectsRoot.Find("SprintSpeedEffects");
        GameObject root = existing != null
            ? existing.gameObject
            : new GameObject("SprintSpeedEffects");

        root.transform.SetParent(effectsRoot, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Transform existingLines = root.transform.Find("SpeedLines");
        if (existingLines != null)
        {
            speedLines = existingLines.GetComponent<ParticleSystem>();
            if (speedLines == null)
                speedLines = existingLines.gameObject.AddComponent<ParticleSystem>();
        }
        else
        {
            GameObject particleObject = new GameObject("SpeedLines");
            particleObject.transform.SetParent(root.transform, false);
            speedLines = particleObject.AddComponent<ParticleSystem>();
        }

        speedLines.transform.localPosition = new Vector3(0f, 0f, spawnDistance);
        speedLines.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        speedLinesRenderer = speedLines.GetComponent<ParticleSystemRenderer>();
        speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureParticleSystem();
        ApplyRendererMaterial();
        speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ConfigureParticleSystem()
    {
        if (speedLines.isPlaying)
            speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = speedLines.main;
        main.playOnAwake = false;
        main.loop = true;
        main.startLifetime = 1f;
        main.startSpeed = particleSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.85f, particleSize);
        main.startColor = new Color(1f, 1f, 1f, effectOpacity);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = 0f;
        main.maxParticles = 400;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = speedLines.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = speedLines.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = spawnRadius;
        shape.radiusThickness = 0f;
        shape.arc = 360f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = speedLines.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(effectOpacity, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = speedLines.sizeOverLifetime;
        sizeOverLifetime.enabled = false;

        if (speedLinesRenderer == null)
            speedLinesRenderer = speedLines.GetComponent<ParticleSystemRenderer>();

        speedLinesRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        speedLinesRenderer.lengthScale = stretchLength;
        speedLinesRenderer.velocityScale = 0f;
        speedLinesRenderer.maxParticleSize = 0.5f;
        speedLinesRenderer.minParticleSize = 0f;
        speedLinesRenderer.alignment = ParticleSystemRenderSpace.View;
        speedLinesRenderer.shadowCastingMode = ShadowCastingMode.Off;
        speedLinesRenderer.receiveShadows = false;
        speedLinesRenderer.allowRoll = true;
    }

    private void ApplyRendererMaterial()
    {
        if (speedLinesRenderer == null)
            return;

        if (speedLineMaterial == null)
            speedLineMaterial = CreateFallbackMaterial();

        if (speedLineMaterial != null)
            speedLinesRenderer.sharedMaterial = speedLineMaterial;
    }

    private Material CreateFallbackMaterial()
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("Hidden/HDRP/Unlit");
        if (shader == null)
            return null;

        Material material = new Material(shader);
        material.name = "SpeedLineFallback";
        material.SetFloat("_SurfaceType", 1f);
        material.SetFloat("_BlendMode", 0f);
        material.SetColor("_UnlitColor", new Color(1f, 1f, 1f, effectOpacity));
        return material;
    }

    private void StopImmediate()
    {
        currentEmission = 0f;
        if (speedLines == null)
            return;

        ParticleSystem.EmissionModule emission = speedLines.emission;
        emission.rateOverTime = 0f;
        speedLines.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
