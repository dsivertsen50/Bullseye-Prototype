using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-simulated throwable. Explodes after a fuse and applies knockback /
/// bullseye detachment only. Never subtracts health.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Grenade : NetworkBehaviour
{
    [Header("Fuse")]
    [SerializeField] private float fuseDuration = 2.75f;

    [Header("Explosion")]
    [SerializeField] private float knockbackRadius = 6.5f;
    [SerializeField] private float explosionForce = 14f;
    [SerializeField] private float upwardModifier = 0.45f;
    [SerializeField] private float bullseyeDetachRadius = 3.25f;
    [SerializeField] private float bullseyeExplosionForce = 9f;

    [Header("Feedback")]
    [SerializeField] private GameObject explosionVfx;
    [SerializeField] private AudioClip[] explosionSfx;
    [SerializeField] private float explosionSfxVolume = 0.9f;
    [SerializeField] private AudioClip[] collisionSfx;
    [SerializeField] private float collisionSfxVolume = 0.65f;
    [SerializeField] private float minCollisionSpeed = 1.2f;
    [SerializeField] private float maxCollisionSpeed = 10f;
    [SerializeField] private float collisionSfxCooldown = 0.12f;
    [SerializeField] private float vfxLifetime = 2.5f;

    private Rigidbody body;
    private ulong throwerClientId = ulong.MaxValue;
    private Vector3 pendingVelocity;
    private bool hasPendingVelocity;
    private bool exploded;
    private Coroutine fuseRoutine;
    private float nextCollisionSfxTime;

    public virtual GrenadeType Type => GrenadeType.Standard;
    public ulong ThrowerClientId => throwerClientId;
    public float FuseDuration => fuseDuration;
    public float KnockbackRadius => knockbackRadius;
    public float ExplosionForce => explosionForce;
    public float BullseyeDetachRadius => bullseyeDetachRadius;
    protected Rigidbody Body => body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void InitializeThrow(ulong ownerClientId, Vector3 velocity)
    {
        throwerClientId = ownerClientId;
        pendingVelocity = velocity;
        hasPendingVelocity = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (body == null)
            body = GetComponent<Rigidbody>();

        if (hasPendingVelocity && body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = pendingVelocity;
            body.angularVelocity = Random.insideUnitSphere * 6f;
        }

        IgnoreThrowerCollisions();
        StartThrownLifecycle();
    }

    protected virtual void StartThrownLifecycle()
    {
        fuseRoutine = StartCoroutine(FuseThenExplode());
    }

    protected void DespawnIfSpawned()
    {
        if (IsSpawned && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    public override void OnNetworkDespawn()
    {
        if (fuseRoutine != null)
        {
            StopCoroutine(fuseRoutine);
            fuseRoutine = null;
        }
    }

    private IEnumerator FuseThenExplode()
    {
        float delay = Mathf.Max(0.05f, fuseDuration);
        float elapsed = 0f;
        while (elapsed < delay && IsSpawned)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        fuseRoutine = null;
        if (IsSpawned)
            Explode();
    }

    private void Explode()
    {
        if (exploded)
            return;

        exploded = true;
        Vector3 origin = transform.position;

        ApplyExplosionEffects(origin);
        PlayExplosionFxRpc(origin);
        DespawnIfSpawned();
    }

    private void ApplyExplosionEffects(Vector3 origin)
    {
        ApplyPlayerKnockback(origin);
        ApplyBullseyeEffects(origin);
        ApplyOtherGrenadeForces(origin);
    }

    private void ApplyPlayerKnockback(Vector3 origin)
    {
        PlayerMovement[] movers = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude);
        for (int i = 0; i < movers.Length; i++)
        {
            PlayerMovement movement = movers[i];
            if (movement == null || !movement.IsSpawned)
                continue;

            PlayerHealth health = movement.GetComponent<PlayerHealth>();
            if (health != null && health.IsDead)
                continue;

            Vector3 target = movement.transform.position + Vector3.up * 1f;
            if (Vector3.Distance(origin, target) > knockbackRadius)
                continue;

            movement.ApplyExplosionKnockbackOwnerRpc(
                origin,
                explosionForce,
                knockbackRadius,
                upwardModifier);
        }
    }

    private void ApplyBullseyeEffects(Vector3 origin)
    {
        BullseyeDetachController[] controllers =
            FindObjectsByType<BullseyeDetachController>(FindObjectsInactive.Exclude);
        for (int i = 0; i < controllers.Length; i++)
        {
            BullseyeDetachController controller = controllers[i];
            if (controller == null || !controller.IsSpawned)
                continue;

            controller.NotifyExplosion(
                origin,
                bullseyeExplosionForce,
                knockbackRadius,
                bullseyeDetachRadius,
                upwardModifier);
        }
    }

    private void ApplyOtherGrenadeForces(Vector3 origin)
    {
        if (body == null)
            return;

        Collider[] hits = Physics.OverlapSphere(origin, knockbackRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            Rigidbody other = hits[i] != null ? hits[i].attachedRigidbody : null;
            if (other == null || other == body)
                continue;

            if (other.GetComponent<Grenade>() == null)
                continue;

            other.AddExplosionForce(explosionForce, origin, knockbackRadius, upwardModifier, ForceMode.Impulse);
        }
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlayExplosionFxRpc(Vector3 origin)
    {
        PlayExplosionFx(origin);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || exploded || collision == null)
            return;

        if (!TryGetImpactVolume(collision.relativeVelocity.magnitude, out float volume))
            return;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        PlayCollisionSfxRpc(point, volume);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlayCollisionSfxRpc(Vector3 point, float volume)
    {
        AudioClip clip = PickRandom(collisionSfx);
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, point, volume);
    }

    private bool TryGetImpactVolume(float speed, out float volume)
    {
        volume = 0f;
        if (speed < minCollisionSpeed || Time.time < nextCollisionSfxTime)
            return false;

        nextCollisionSfxTime = Time.time + Mathf.Max(0.02f, collisionSfxCooldown);
        float t = Mathf.InverseLerp(minCollisionSpeed, maxCollisionSpeed, speed);
        volume = Mathf.Lerp(collisionSfxVolume * 0.35f, collisionSfxVolume, t);
        return volume > 0.01f;
    }

    private void PlayExplosionFx(Vector3 origin)
    {
        AudioClip blast = PickRandom(explosionSfx);
        if (blast != null)
            AudioSource.PlayClipAtPoint(blast, origin, Mathf.Clamp01(explosionSfxVolume));

        GameObject effect = explosionVfx != null
            ? Instantiate(explosionVfx, origin, Quaternion.identity)
            : CreatePlaceholderBurst(origin);

        if (effect != null)
            Destroy(effect, Mathf.Max(0.25f, vfxLifetime));
    }

    private static GameObject CreatePlaceholderBurst(Vector3 origin)
    {
        var go = new GameObject("GrenadeExplosionPlaceholder");
        go.transform.position = origin;

        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.35f;
        main.loop = false;
        main.startLifetime = 0.4f;
        main.startSpeed = 6f;
        main.startSize = 0.28f;
        main.startColor = new Color(1f, 0.55f, 0.15f, 1f);
        main.gravityModifier = 0.4f;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        return go;
    }

    private void IgnoreThrowerCollisions()
    {
        if (NetworkManager == null || NetworkManager.SpawnManager == null)
            return;

        NetworkObject thrower = NetworkManager.SpawnManager.GetPlayerNetworkObject(throwerClientId);
        if (thrower == null)
            return;

        Collider[] grenadeColliders = GetComponentsInChildren<Collider>(true);
        Collider[] throwerColliders = thrower.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < grenadeColliders.Length; i++)
        {
            if (grenadeColliders[i] == null || grenadeColliders[i].isTrigger)
                continue;

            for (int j = 0; j < throwerColliders.Length; j++)
            {
                if (throwerColliders[j] == null)
                    continue;
                Physics.IgnoreCollision(grenadeColliders[i], throwerColliders[j], true);
            }
        }
    }

    private void OnValidate()
    {
        fuseDuration = Mathf.Max(0.05f, fuseDuration);
        knockbackRadius = Mathf.Max(0.1f, knockbackRadius);
        explosionForce = Mathf.Max(0f, explosionForce);
        upwardModifier = Mathf.Max(0f, upwardModifier);
        bullseyeDetachRadius = Mathf.Max(0.1f, bullseyeDetachRadius);
        bullseyeExplosionForce = Mathf.Max(0f, bullseyeExplosionForce);
        explosionSfxVolume = Mathf.Clamp01(explosionSfxVolume);
        collisionSfxVolume = Mathf.Clamp01(collisionSfxVolume);
        minCollisionSpeed = Mathf.Max(0.05f, minCollisionSpeed);
        maxCollisionSpeed = Mathf.Max(minCollisionSpeed, maxCollisionSpeed);
        collisionSfxCooldown = Mathf.Max(0.02f, collisionSfxCooldown);
        vfxLifetime = Mathf.Max(0.1f, vfxLifetime);
    }

    private static AudioClip PickRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int assigned = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                assigned++;
        }

        if (assigned <= 0)
            return null;

        int pick = Random.Range(0, assigned);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (pick == 0)
                return clips[i];

            pick--;
        }

        return null;
    }
}
