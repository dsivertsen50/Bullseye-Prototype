using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-authoritative straight projectile. Collision, explosion, and
/// damage are resolved on the server; clients receive transform sync and
/// replicated explosion FX. Homing is reserved via ProjectileGuidanceMode
/// and is not implemented here.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class RocketProjectile : NetworkBehaviour
{
    [SerializeField] private Rigidbody body;
    [SerializeField] private AudioSource flightAudio;
    [SerializeField] private TrailRenderer trail;
    [SerializeField] private float defaultCollisionRadius = 0.12f;

    private WeaponDefinition weapon;
    private WeaponProjectileSettings settings;
    private ulong shooterClientId = ulong.MaxValue;
    private Vector3 travelDirection = Vector3.forward;
    private Vector3 aimTarget;
    private Vector3 lastPosition;
    private float spawnedAt;
    private bool armed;
    private bool exploded;
    private int bounceCount;
    private Collider lastRicochetCollider;
    private float ignoreRicochetUntil;

    public ulong ShooterClientId => shooterClientId;
    public WeaponDefinition Weapon => weapon;
    public ProjectileGuidanceMode GuidanceMode =>
        settings != null ? settings.GuidanceMode : ProjectileGuidanceMode.Straight;

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody>();
        if (flightAudio == null)
            flightAudio = GetComponent<AudioSource>();
        if (trail == null)
            trail = GetComponentInChildren<TrailRenderer>(true);
    }

    public void Initialize(
        ulong ownerClientId,
        WeaponDefinition definition,
        WeaponProjectileSettings projectileSettings,
        Vector3 direction,
        Vector3 targetPoint)
    {
        shooterClientId = ownerClientId;
        weapon = definition;
        settings = projectileSettings ?? WeaponProjectileSettings.Fallback;
        travelDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        aimTarget = targetPoint;
    }

    public override void OnNetworkSpawn()
    {
        spawnedAt = Time.time;
        lastPosition = transform.position;
        ConfigureBody();

        if (IsServer)
        {
            IgnoreShooterCollisions();
            ApplyLaunchVelocity();
        }

        PlayFlightAudio();
        ApplyOptionalTrail();
    }

    public override void OnNetworkDespawn()
    {
        StopFlightAudio();
    }

    private void FixedUpdate()
    {
        if (!IsServer || exploded || !IsSpawned)
            return;

        if (!armed && Time.time - spawnedAt >= 0.04f)
            armed = true;

        if (Time.time - spawnedAt >= (settings != null ? settings.ProjectileLifetime : 3.5f))
        {
            Explode(transform.position, null);
            return;
        }

        travelDirection = ResolveTravelDirection(travelDirection);
        MaintainSpeed();
        ProbeThinSurfaces();
        DrawDebugFlight();
        lastPosition = transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || exploded || !armed || collision == null)
            return;

        Collider hitCollider = collision.collider;
        if (ShouldIgnore(hitCollider))
            return;

        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : -travelDirection;
        if (TryRicochet(hitCollider, point, normal, travelDirection))
            return;

        Explode(point, hitCollider);
    }

    /// <summary>
    /// Straight flight now. A future homing movement component can replace
    /// this without changing spawn, collision, or explosion.
    /// </summary>
    protected virtual Vector3 ResolveTravelDirection(Vector3 currentDirection)
    {
        if (currentDirection.sqrMagnitude < 0.0001f)
            return transform.forward;

        return currentDirection.normalized;
    }

    private void ConfigureBody()
    {
        if (body == null)
            return;

        body.useGravity = settings != null && settings.Gravity > 0.0001f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        if (body.useGravity)
            body.linearDamping = 0f;
    }

    private void ApplyLaunchVelocity()
    {
        if (body == null)
            return;

        body.isKinematic = false;
        float speed = settings != null ? settings.ProjectileSpeed : 35f;
        body.linearVelocity = travelDirection * speed;
        transform.rotation = Quaternion.LookRotation(travelDirection, Vector3.up);
    }

    private void MaintainSpeed()
    {
        if (body == null || body.isKinematic)
            return;

        float speed = settings != null ? settings.ProjectileSpeed : 35f;
        if (body.useGravity && settings != null && settings.Gravity > 0f)
            body.AddForce(Physics.gravity.normalized * settings.Gravity, ForceMode.Acceleration);

        Vector3 velocity = body.linearVelocity;
        if (!body.useGravity)
        {
            if (velocity.sqrMagnitude < 0.0001f)
                velocity = travelDirection * speed;
            else
                velocity = velocity.normalized * speed;

            body.linearVelocity = velocity;
            travelDirection = velocity.normalized;
            transform.rotation = Quaternion.LookRotation(travelDirection, Vector3.up);
            return;
        }

        Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
        if (horizontal.sqrMagnitude > 0.0001f)
            travelDirection = (horizontal + Vector3.up * velocity.y).normalized;
    }

    private void ProbeThinSurfaces()
    {
        if (!armed)
            return;

        Vector3 current = transform.position;
        Vector3 delta = current - lastPosition;
        float distance = delta.magnitude;
        if (distance < 0.001f)
            return;

        float radius = settings != null ? settings.CollisionRadius : defaultCollisionRadius;
        if (!Physics.SphereCast(
                lastPosition,
                radius,
                delta / distance,
                out RaycastHit hit,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore))
            return;

        if (ShouldIgnore(hit.collider))
            return;

        if (TryRicochet(hit.collider, hit.point, hit.normal, travelDirection))
            return;

        Explode(hit.point, hit.collider);
    }

    private bool TryRicochet(Collider hitCollider, Vector3 point, Vector3 normal, Vector3 incoming)
    {
        if (settings == null || settings.MaxRicochets <= 0 || bounceCount >= settings.MaxRicochets)
            return false;

        if (!RicochetSurface.TryGetEnabled(hitCollider, out _))
            return false;

        var hit = new RaycastHit
        {
            point = point,
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up
        };

        if (!HitscanRicochet.TryBuildReflectedRay(
                incoming,
                hit,
                1f,
                out Vector3 origin,
                out Vector3 direction,
                out _))
            return false;

        bounceCount++;
        travelDirection = direction;
        lastRicochetCollider = hitCollider;
        ignoreRicochetUntil = Time.time + 0.08f;
        lastPosition = origin;

        transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
        if (body != null)
        {
            body.position = origin;
            body.linearVelocity = direction * settings.ProjectileSpeed;
        }

        if (settings.DebugProjectileTrajectory)
            Debug.DrawRay(origin, direction * 2f, Color.cyan, 0.6f);

        return true;
    }

    private void Explode(Vector3 origin, Collider hitCollider)
    {
        if (exploded)
            return;

        exploded = true;
        PlayerHealth directVictim = ResolveDirectVictim(hitCollider);
        ExplosionDamage.Apply(origin, settings, weapon, shooterClientId, directVictim);
        PlayExplosionFxRpc(origin);
        DespawnIfSpawned();
    }

    private void DespawnIfSpawned()
    {
        if (IsSpawned && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PlayExplosionFxRpc(Vector3 origin)
    {
        PlayExplosionFx(origin);
    }

    private void PlayExplosionFx(Vector3 origin)
    {
        StopFlightAudio();

        AudioClip blast = PickRandom(settings != null ? settings.ExplosionSfx : null);
        if (blast != null)
        {
            var go = new GameObject("RocketExplosionSfx");
            go.transform.position = origin;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = blast;
            source.volume = settings != null ? settings.ExplosionSfxVolume : 0.95f;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 8f;
            source.maxDistance = 70f;
            source.dopplerLevel = 0f;
            PlayerGameSettings.RouteToSfx(source);
            source.Play();
            Destroy(go, blast.length + 0.1f);
        }

        GameObject vfxPrefab = settings != null ? settings.ExplosionVfx : null;
        GameObject effect = vfxPrefab != null
            ? Instantiate(vfxPrefab, origin, Quaternion.identity)
            : CreatePlaceholderBurst(origin);
        if (effect != null)
            Destroy(effect, settings != null ? settings.ExplosionVfxLifetime : 2.5f);

        PlayerHaptics.NotifyLocalExplosion(
            origin,
            settings != null ? Mathf.Max(8f, settings.ExplosionRadius * 3.5f) : 16f,
            1f);

        if (settings != null && settings.DebugExplosionRadius)
        {
            Debug.DrawRay(origin, Vector3.up * settings.ExplosionRadius, Color.red, 2f);
            Debug.DrawRay(origin, Vector3.right * settings.ExplosionRadius, Color.red, 2f);
            Debug.DrawRay(origin, Vector3.forward * settings.ExplosionRadius, Color.red, 2f);
        }
    }

    private void PlayFlightAudio()
    {
        AudioClip clip = PickRandom(settings != null ? settings.FlightSfx : null);
        if (clip == null || flightAudio == null)
            return;

        flightAudio.clip = clip;
        flightAudio.loop = true;
        flightAudio.spatialBlend = 1f;
        flightAudio.volume = settings != null ? settings.FlightSfxVolume : 0.55f;
        flightAudio.playOnAwake = false;
        PlayerGameSettings.RouteToSfx(flightAudio);
        flightAudio.Play();
    }

    private void StopFlightAudio()
    {
        if (flightAudio != null && flightAudio.isPlaying)
            flightAudio.Stop();
    }

    private void ApplyOptionalTrail()
    {
        if (trail != null)
            return;

        GameObject trailPrefab = settings != null ? settings.ProjectileTrail : null;
        if (trailPrefab == null)
            return;

        Instantiate(trailPrefab, transform);
    }

    private void IgnoreShooterCollisions()
    {
        if (NetworkManager == null || NetworkManager.SpawnManager == null)
            return;

        NetworkObject shooter = NetworkManager.SpawnManager.GetPlayerNetworkObject(shooterClientId);
        if (shooter == null)
            return;

        Collider[] projectileColliders = GetComponentsInChildren<Collider>(true);
        Collider[] shooterColliders = shooter.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < projectileColliders.Length; i++)
        {
            if (projectileColliders[i] == null)
                continue;

            for (int j = 0; j < shooterColliders.Length; j++)
            {
                if (shooterColliders[j] == null)
                    continue;
                Physics.IgnoreCollision(projectileColliders[i], shooterColliders[j], true);
            }
        }
    }

    private bool ShouldIgnore(Collider hitCollider)
    {
        if (hitCollider == null)
            return true;

        if (hitCollider.transform.IsChildOf(transform))
            return true;

        if (hitCollider == lastRicochetCollider && Time.time < ignoreRicochetUntil)
            return true;

        if (NetworkManager != null && NetworkManager.SpawnManager != null)
        {
            NetworkObject shooter = NetworkManager.SpawnManager.GetPlayerNetworkObject(shooterClientId);
            if (shooter != null && hitCollider.transform.IsChildOf(shooter.transform))
                return true;
        }

        return false;
    }

    private PlayerHealth ResolveDirectVictim(Collider hitCollider)
    {
        if (hitCollider == null)
            return null;

        PlayerHealth health = hitCollider.GetComponentInParent<PlayerHealth>();
        if (health == null || health.OwnerClientId == shooterClientId)
            return null;

        return health;
    }

    private void DrawDebugFlight()
    {
        if (settings == null || !settings.DebugProjectileTrajectory)
            return;

        Debug.DrawLine(lastPosition, transform.position, Color.yellow, 0.35f);
        if (settings.DebugAimTarget)
            Debug.DrawLine(transform.position, aimTarget, Color.cyan, 0.05f);
    }

    private void OnDrawGizmosSelected()
    {
        if (settings == null || !settings.DebugExplosionRadius)
            return;

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, settings.ExplosionRadius);
    }

    private static GameObject CreatePlaceholderBurst(Vector3 origin)
    {
        var go = new GameObject("RocketExplosionPlaceholder");
        go.transform.position = origin;

        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 0.45f;
        main.startSpeed = 8f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 0.45f, 0.12f, 1f);
        main.gravityModifier = 0.25f;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        return go;
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

    private void OnValidate()
    {
        defaultCollisionRadius = Mathf.Max(0.02f, defaultCollisionRadius);
        if (body == null)
            body = GetComponent<Rigidbody>();
    }
}
