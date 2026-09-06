using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Temporary suction field that detaches bullseyes using the existing
/// detach controller, then pulls those discs toward this grenade.
/// </summary>
public class SuctionGrenade : Grenade
{
    private static readonly List<SuctionGrenade> ActiveFields = new();
    private static readonly Collider[] OverlapBuffer = new Collider[48];

    [Header("Suction")]
    [SerializeField] private float suctionDuration = 6f;
    [SerializeField] private float suctionRadius = 3.5f;
    [SerializeField] private float suctionForce = 90f;
    [SerializeField] private float maximumSuctionSpeed = 18f;
    [SerializeField] private float ownerGracePeriod = 0.4f;
    [SerializeField] private float overlapInterval = 0.05f;

    [Header("Field Visual")]
    [SerializeField] private Color fieldColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Material visualMaterial;

    [Header("Debug")]
    [SerializeField] private bool showSuctionDebug;

    private readonly HashSet<ulong> affectedPlayers = new();
    private readonly List<PlayerHealth> detectedPlayers = new();
    private Coroutine suctionRoutine;
    private ParticleSystem fieldParticles;
    private float activatedAt = -1f;
    private bool fieldActive;

    public override GrenadeType Type => GrenadeType.Suction;
    public bool IsFieldActive => fieldActive && IsSpawned;
    public float SuctionRadius => suctionRadius;
    public float SuctionDuration => suctionDuration;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ConfigureVisualIdentity();
        EnsureFieldVisual();
        SetFieldVisualActive(true);

        if (!IsServer)
            return;

        if (!ActiveFields.Contains(this))
            ActiveFields.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        ActiveFields.Remove(this);
        fieldActive = false;
        if (suctionRoutine != null)
        {
            StopCoroutine(suctionRoutine);
            suctionRoutine = null;
        }

        base.OnNetworkDespawn();
    }

    protected override void StartThrownLifecycle()
    {
        activatedAt = Time.time;
        fieldActive = true;
        suctionRoutine = StartCoroutine(RunSuctionLifetime());
    }

    private IEnumerator RunSuctionLifetime()
    {
        float duration = Mathf.Max(0.05f, suctionDuration);
        float elapsed = 0f;
        float nextOverlap = 0f;

        while (elapsed < duration && IsSpawned)
        {
            if (Time.time >= nextOverlap)
            {
                EvaluateField();
                nextOverlap = Time.time + Mathf.Max(0.02f, overlapInterval);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        suctionRoutine = null;
        DeactivateField();
        if (IsSpawned)
            DespawnIfSpawned();
    }

    private void FixedUpdate()
    {
        if (!IsServer || !fieldActive || !IsSpawned)
            return;

        AttractKnownDetachedBullseyes();
    }

    private void EvaluateField()
    {
        if (!IsServer || !fieldActive)
            return;

        detectedPlayers.Clear();
        Vector3 origin = transform.position;
        float radius = Mathf.Max(0.1f, suctionRadius);
        int count = Physics.OverlapSphereNonAlloc(
            origin,
            radius,
            OverlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            PlayerHealth health = ResolvePlayer(OverlapBuffer[i]);
            if (health == null || detectedPlayers.Contains(health))
                continue;

            detectedPlayers.Add(health);
            TryAffectPlayer(health, origin, radius);
        }
    }

    private void TryAffectPlayer(PlayerHealth health, Vector3 origin, float radius)
    {
        if (health == null || !health.IsSpawned || health.IsDead)
            return;

        BullseyeDetachController controller = health.GetComponent<BullseyeDetachController>();
        if (controller == null || !controller.IsSpawned)
            return;

        if (controller.IsDetached)
            return;

        if (!PlayerBodyIntersects(health, origin, radius))
            return;

        if (!IsPlayerEligibleForDetach(health))
            return;

        if (!controller.TryDetachBySuction())
            return;

        affectedPlayers.Add(health.OwnerClientId);
    }

    private void AttractKnownDetachedBullseyes()
    {
        for (int i = 0; i < detectedPlayers.Count; i++)
        {
            PlayerHealth health = detectedPlayers[i];
            if (health == null || health.IsDead)
                continue;

            BullseyeDetachController controller = health.GetComponent<BullseyeDetachController>();
            if (controller == null || !controller.IsDetached)
                continue;

            AttractIfClosest(controller);
        }
    }

    private void AttractIfClosest(BullseyeDetachController controller)
    {
        if (controller == null || controller.BullseyeTransform == null)
            return;

        Vector3 bullseyePosition = controller.BullseyeTransform.position;
        if (GetClosestActive(bullseyePosition) != this)
            return;

        Vector3 velocity = Body != null ? Body.linearVelocity : Vector3.zero;
        controller.ApplySuctionAttraction(
            transform.position,
            velocity,
            suctionForce,
            maximumSuctionSpeed);
    }

    private bool IsPlayerEligibleForDetach(PlayerHealth health)
    {
        if (affectedPlayers.Contains(health.OwnerClientId))
            return false;

        if (health.OwnerClientId != ThrowerClientId)
            return true;

        return activatedAt >= 0f && Time.time >= activatedAt + Mathf.Max(0f, ownerGracePeriod);
    }

    private static bool PlayerBodyIntersects(PlayerHealth health, Vector3 origin, float radius)
    {
        CapsuleCollider capsule = health.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null && capsule.enabled)
        {
            Vector3 closest = capsule.ClosestPoint(origin);
            return (closest - origin).sqrMagnitude <= radius * radius;
        }

        Vector3 fallback = health.transform.position + Vector3.up;
        return (fallback - origin).sqrMagnitude <= radius * radius;
    }

    private static PlayerHealth ResolvePlayer(Collider hit)
    {
        if (hit == null)
            return null;

        PlayerHealth health = hit.GetComponentInParent<PlayerHealth>();
        if (health != null)
            return health;

        BullseyeTarget target = hit.GetComponentInParent<BullseyeTarget>();
        return target != null ? target.OwnerHealth : null;
    }

    private static SuctionGrenade GetClosestActive(Vector3 position)
    {
        SuctionGrenade closest = null;
        float bestSq = float.MaxValue;

        for (int i = 0; i < ActiveFields.Count; i++)
        {
            SuctionGrenade field = ActiveFields[i];
            if (field == null || !field.IsFieldActive)
                continue;

            float radius = Mathf.Max(0.1f, field.suctionRadius);
            float sq = (field.transform.position - position).sqrMagnitude;
            if (sq > radius * radius)
                continue;

            bool closer = sq < bestSq - 0.0001f;
            bool tiedButLowerId = closest != null
                && Mathf.Abs(sq - bestSq) <= 0.0001f
                && field.NetworkObjectId < closest.NetworkObjectId;

            if (!closer && !tiedButLowerId)
                continue;

            closest = field;
            bestSq = sq;
        }

        return closest;
    }

    private void DeactivateField()
    {
        fieldActive = false;
        ActiveFields.Remove(this);
        SetFieldVisualActive(false);
    }

    private void ConfigureVisualIdentity()
    {
        MeshRenderer renderer = GetComponentInChildren<MeshRenderer>();
        if (renderer == null)
            return;

        if (visualMaterial != null)
        {
            renderer.sharedMaterial = visualMaterial;
            return;
        }

        renderer.material.color = fieldColor;
    }

    private void EnsureFieldVisual()
    {
        if (fieldParticles != null)
            return;

        Transform existing = transform.Find("SuctionFieldVisual");
        GameObject visual = existing != null ? existing.gameObject : new GameObject("SuctionFieldVisual");
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;

        fieldParticles = visual.GetComponent<ParticleSystem>();
        if (fieldParticles == null)
            fieldParticles = visual.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = fieldParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 1f;
        main.startLifetime = 0.7f;
        main.startSpeed = -2.4f;
        main.startSize = 0.12f;
        main.startColor = fieldColor;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 48;

        ParticleSystem.EmissionModule emission = fieldParticles.emission;
        emission.rateOverTime = 28f;

        ParticleSystem.ShapeModule shape = fieldParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.35f, suctionRadius * 0.55f);
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = fieldParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(fieldColor, 0f),
                new GradientColorKey(fieldColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.55f, 0f),
                new GradientAlphaKey(0.05f, 1f)
            });
        colorOverLifetime.color = gradient;
    }

    private void SetFieldVisualActive(bool active)
    {
        if (fieldParticles == null)
            return;

        if (active)
            fieldParticles.Play(true);
        else
            fieldParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnValidate()
    {
        suctionDuration = Mathf.Max(0.05f, suctionDuration);
        suctionRadius = Mathf.Max(0.1f, suctionRadius);
        suctionForce = Mathf.Max(0f, suctionForce);
        maximumSuctionSpeed = Mathf.Max(0.1f, maximumSuctionSpeed);
        ownerGracePeriod = Mathf.Max(0f, ownerGracePeriod);
        overlapInterval = Mathf.Max(0.02f, overlapInterval);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showSuctionDebug)
            return;

        Color color = fieldActive || !Application.isPlaying
            ? new Color(0.15f, 0.75f, 1f, 0.28f)
            : new Color(0.4f, 0.4f, 0.4f, 0.12f);
        Gizmos.color = color;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.1f, suctionRadius));
        Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.08f);

        if (!Application.isPlaying)
            return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < detectedPlayers.Count; i++)
        {
            PlayerHealth health = detectedPlayers[i];
            if (health == null)
                continue;

            Gizmos.DrawLine(transform.position, health.transform.position + Vector3.up);
        }
    }
#endif
}
