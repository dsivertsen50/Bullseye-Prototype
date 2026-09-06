using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private float range = 100f;
    [SerializeField] private bool showRicochetDebug;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly List<(BullseyeTarget target, float distance)> pelletHits = new(16);
    private readonly List<Vector3> impactPoints = new(16);
    private readonly List<Vector3> impactNormals = new(16);
    private readonly List<float> impactDelays = new(16);
    private readonly List<bool> impactSparks = new(16);
    private readonly List<Vector3> ricochetAudioPoints = new(16);
    private readonly List<Vector3> standardAudioPoints = new(16);
    private readonly List<Vector3> shotOrigins = new(16);
    private readonly List<Vector3> shotEnds = new(16);
    private readonly List<PlayerHealth> directHitPlayers = new(8);
    private readonly List<ulong> nearMissClientIds = new(8);
    private readonly List<Vector3> nearMissPoints = new(8);
    private readonly List<float> nearMissDistances = new(8);
    private PlayerHaptics playerHaptics;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private WeaponPresentationCoordinator weaponPresentationCoordinator;
    private WeaponPresentationController weaponPresentation;
    private PlayerWeaponInventory inventory;
    private PlayerWeaponController weaponController;
    private PlayerWeaponInteractor interactor;
    private WeaponAccuracyController accuracy;
    private InputAction reloadAction;
    private float nextFireTime;

    private void Awake()
    {
        playerHaptics = GetComponent<PlayerHaptics>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        weaponPresentationCoordinator = GetComponent<WeaponPresentationCoordinator>();
        weaponPresentation = GetComponent<WeaponPresentationController>();
        inventory = GetComponent<PlayerWeaponInventory>();
        weaponController = GetComponent<PlayerWeaponController>();
        interactor = GetComponent<PlayerWeaponInteractor>();
        accuracy = GetComponent<WeaponAccuracyController>();
    }

    private void OnEnable()
    {
        // Enable only. Player instances share this action in-process.
        fireAction.action.Enable();
        reloadAction?.Enable();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;

        if (TryGetComponent(out LocalPlayerInputBinding binding) && binding.PlayerActions != null)
            reloadAction = binding.PlayerActions.FindAction("Reload");

        reloadAction?.Enable();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (playerMovement != null && playerMovement.BlocksCombat)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        if (playerMovement != null &&
            playerMovement.IsSprinting &&
            !playerMovement.CanRunWhileShooting &&
            !playerMovement.IsWallRunning)
            return;

        if (reloadAction != null && reloadAction.WasPressedThisFrame() && !ShouldSuppressReload())
            inventory?.RequestReload();

        if (!WantsToFire())
            return;

        if (Time.time < nextFireTime)
            return;

        if (!CanFire())
            return;

        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        nextFireTime = Time.time + (definition != null ? definition.FireRate : 0.12f);
        Shoot();
    }

    private void Shoot()
    {
        if (inventory != null)
            inventory.NotifyShotFired();

        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        WeaponDamageSettings damageSettings = definition != null
            ? definition.DamageSettings
            : WeaponDamageSettings.Fallback;
        int projectileCount = damageSettings.ProjectileCount;
        float shotRange = damageSettings != null ? damageSettings.MaximumRange : range;
        float pelletSpread = ResolvePelletSpread(damageSettings);

        WeaponImpactDecalSettings impactSettings = definition != null
            ? definition.ImpactDecalSettings
            : WeaponImpactDecalSettings.Fallback;
        int maxDecals = impactSettings != null ? impactSettings.ResolveMaxDecalsForShot() : 0;

        pelletHits.Clear();
        impactPoints.Clear();
        impactNormals.Clear();
        impactDelays.Clear();
        impactSparks.Clear();
        shotOrigins.Clear();
        shotEnds.Clear();
        directHitPlayers.Clear();
        bool allowRicochet = definition == null || definition.CanRicochet;
        for (int i = 0; i < projectileCount; i++)
        {
            bool hitSomething = TryTraceHitscan(
                shotRange,
                pelletSpread,
                allowRicochet,
                out HitscanRicochet.TraceResult trace,
                out Vector3 origin,
                out Vector3 direction);

            CollectShotSegments(origin, trace);

            if (showRicochetDebug || (definition != null && definition.DamageSettings != null && definition.DamageSettings.LogDamage))
            {
                if (trace.hasBounce)
                    HitscanRicochet.DrawDebug(trace, 0.35f);
                else
                    Debug.DrawRay(origin, direction * shotRange, Color.yellow, 0.2f);
            }

            int decalBudget = maxDecals;
            if (trace.bounceCount > 0 && maxDecals > 0)
                decalBudget = maxDecals + trace.bounceCount;

            for (int b = 0; b < trace.bounceCount; b++)
            {
                if (!trace.TryGetBounce(b, out HitscanRicochet.BounceRecord bounce))
                    continue;

                TryCollectSurfaceImpact(
                    bounce.hit,
                    impactSettings,
                    decalBudget,
                    bounce.traveledDistance,
                    b == 0 ? 0f : HitscanRicochet.SubsequentBounceDecalDelay,
                    spark: true);
            }

            if (!hitSomething || !trace.hasFinalHit)
                continue;

            RaycastHit hit = trace.finalHit;
            TrackDirectPlayerHit(hit.collider);

            if (HitscanRicochet.TryGetBullseyeTarget(hit.collider, out BullseyeTarget target))
            {
                pelletHits.Add((target, trace.totalDistance));
                continue;
            }

            if (decalBudget > 0)
            {
                TryCollectSurfaceImpact(
                    hit,
                    impactSettings,
                    decalBudget,
                    trace.totalDistance,
                    trace.bounceCount > 0 ? HitscanRicochet.SubsequentBounceDecalDelay : 0f,
                    RicochetSurface.TryGetEnabled(hit.collider, out _));
            }
        }

        if (damageSettings.LogDamage)
            LogShotDebug(definition, damageSettings, projectileCount);

        bool scoredHit = RegisterGroupedHits();
        if (scoredHit && TryGetComponent(out Reticle reticle))
            reticle.ShowHitMarker();

        if (scoredHit)
            Debug.Log("BULLSEYE HIT!");

        SubmitSurfaceImpacts(impactSettings);
        SubmitNearMisses();

        // Recoil kicks the camera immediately. Resolve the shot first so ADS
        // shots land on the reticle instead of above it.
        if (playerHaptics != null)
            playerHaptics.PlayFireRumble();

        if (weaponPresentationCoordinator != null)
            weaponPresentationCoordinator.NotifyFire();
        else if (weaponPresentation != null)
            weaponPresentation.PlayFirePresentation();

        if (accuracy != null)
            accuracy.NotifyShotFired();
    }

    private void CollectShotSegments(Vector3 origin, in HitscanRicochet.TraceResult trace)
    {
        Vector3 previous = origin;
        for (int i = 0; i < trace.bounceCount; i++)
        {
            if (!trace.TryGetBounce(i, out HitscanRicochet.BounceRecord bounce))
                continue;

            shotOrigins.Add(previous);
            shotEnds.Add(bounce.hit.point);
            previous = bounce.reflectedOrigin;
        }

        shotOrigins.Add(previous);
        shotEnds.Add(trace.endPoint);
    }

    private void TryCollectSurfaceImpact(
        RaycastHit hit,
        WeaponImpactDecalSettings impactSettings,
        int maxDecals,
        float traveledDistance,
        float delay,
        bool spark)
    {
        if (impactPoints.Count >= maxDecals)
            return;

        if (!BulletImpactManager.IsValidSurface(hit.collider))
            return;

        if (traveledDistance > impactSettings.MaximumDecalDistance)
        {
            BulletImpactManager.LogDistanceRejected(traveledDistance, impactSettings.MaximumDecalDistance, hit.point, hit.normal);
            return;
        }

        float overlap = BulletImpactManager.OverlapDistance;
        if (overlap > 0f)
        {
            float overlapSqr = overlap * overlap;
            for (int i = 0; i < impactPoints.Count; i++)
            {
                if ((impactPoints[i] - hit.point).sqrMagnitude < overlapSqr)
                    return;
            }
        }

        impactPoints.Add(hit.point);
        impactNormals.Add(hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up);
        impactDelays.Add(Mathf.Max(0f, delay));
        impactSparks.Add(spark);
    }

    private void SubmitSurfaceImpacts(WeaponImpactDecalSettings impactSettings)
    {
        if (impactPoints.Count == 0 || impactSettings == null || !impactSettings.Enabled)
            return;

        float scale = impactSettings.DecalScale;
        int seed = Random.Range(int.MinValue, int.MaxValue);
        BulletImpactDecalSet variantSet = impactSettings.VariantSet;

        BulletImpactManager.Ensure().SpawnImpacts(
            impactPoints,
            impactNormals,
            scale,
            seed,
            variantSet,
            impactDelays,
            impactSparks);
        PlayImpactAudio(impactPoints, impactSparks);

        if (!IsSpawned)
            return;

        SurfaceImpactRpc(
            impactPoints.ToArray(),
            impactNormals.ToArray(),
            scale,
            seed,
            impactDelays.ToArray(),
            impactSparks.ToArray());
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Owner)]
    private void SurfaceImpactRpc(
        Vector3[] points,
        Vector3[] normals,
        float scale,
        int seed,
        float[] delays,
        bool[] sparks)
    {
        if (IsOwner)
            return;

        if (points == null || points.Length == 0)
            return;

        BulletImpactDecalSet variantSet = null;
        if (inventory != null && inventory.ActiveDefinition != null)
            variantSet = inventory.ActiveDefinition.ImpactDecalSettings.VariantSet;

        BulletImpactManager.Ensure().SpawnImpacts(points, normals, scale, seed, variantSet, delays, sparks);
        PlayImpactAudio(points, sparks);
    }

    private void PlayImpactAudio(IList<Vector3> points, IList<bool> sparks)
    {
        WeaponShotAudioOverrides overrides = inventory != null && inventory.ActiveDefinition != null
            ? inventory.ActiveDefinition.ShotAudioOverrides
            : null;

        ricochetAudioPoints.Clear();
        standardAudioPoints.Clear();
        if (points == null)
            return;

        for (int i = 0; i < points.Count; i++)
        {
            if (sparks != null && i < sparks.Count && sparks[i])
                ricochetAudioPoints.Add(points[i]);
            else
                standardAudioPoints.Add(points[i]);
        }

        WeaponShotAudio.PlayImpacts(standardAudioPoints, overrides);
        WeaponShotAudio.PlayRicochets(ricochetAudioPoints);
    }

    private void SubmitNearMisses()
    {
        WeaponShotAudioSettings settings = WeaponShotAudio.Settings;
        if (settings == null || !settings.NearMissEnabled)
            return;

        WeaponShotAudioOverrides overrides = inventory != null && inventory.ActiveDefinition != null
            ? inventory.ActiveDefinition.ShotAudioOverrides
            : null;
        if (overrides != null && !overrides.NearMissEnabled)
            return;

        if (shotOrigins.Count == 0)
            return;

        NearMissReceiver.EvaluateShot(
            NetworkObject,
            shotOrigins,
            shotEnds,
            directHitPlayers,
            settings.NearMissRadius,
            settings.DebugNearMiss,
            nearMissClientIds,
            nearMissPoints,
            nearMissDistances);

        if (nearMissClientIds.Count == 0 || !IsSpawned)
            return;

        NearMissRpc(nearMissClientIds.ToArray(), nearMissPoints.ToArray(), nearMissDistances.ToArray());
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Owner)]
    private void NearMissRpc(ulong[] clientIds, Vector3[] points, float[] distances)
    {
        if (clientIds == null || points == null || distances == null)
            return;

        if (NetworkManager == null)
            return;

        ulong localId = NetworkManager.LocalClientId;
        if (localId == OwnerClientId)
            return;

        int count = Mathf.Min(clientIds.Length, Mathf.Min(points.Length, distances.Length));
        for (int i = 0; i < count; i++)
        {
            if (clientIds[i] != localId)
                continue;

            WeaponShotAudioOverrides overrides = inventory != null && inventory.ActiveDefinition != null
                ? inventory.ActiveDefinition.ShotAudioOverrides
                : null;
            WeaponShotAudio.PlayFlyby(points[i], distances[i], overrides);
            return;
        }
    }

    private void TrackDirectPlayerHit(Collider collider)
    {
        if (collider == null)
            return;

        PlayerHealth hitHealth = collider.GetComponentInParent<PlayerHealth>();
        if (hitHealth == null || hitHealth == playerHealth)
            return;

        if (!directHitPlayers.Contains(hitHealth))
            directHitPlayers.Add(hitHealth);
    }

    private bool RegisterGroupedHits()
    {
        bool scoredHit = false;

        for (int i = 0; i < pelletHits.Count; i++)
        {
            BullseyeTarget target = pelletHits[i].target;
            if (target == null)
                continue;

            int count = 0;
            for (int j = 0; j < pelletHits.Count; j++)
            {
                if (pelletHits[j].target == target)
                    count++;
            }

            var distances = new float[count];
            int write = 0;
            for (int j = 0; j < pelletHits.Count; j++)
            {
                if (pelletHits[j].target != target)
                    continue;

                distances[write++] = pelletHits[j].distance;
                if (j != i)
                    pelletHits[j] = default;
            }

            if (target.TryRegisterHits(OwnerClientId, distances))
                scoredHit = true;

            pelletHits[i] = default;
        }

        return scoredHit;
    }

    private float ResolvePelletSpread(WeaponDamageSettings damageSettings)
    {
        float reticleSpread = accuracy != null ? accuracy.CurrentSpread : WeaponAccuracySettings.MinimumVisualGap;
        if (damageSettings == null || !damageSettings.IsPelletHitscan)
            return reticleSpread;

        if (damageSettings.PelletSpread <= 0f)
            return reticleSpread;

        return Mathf.Min(damageSettings.PelletSpread, reticleSpread);
    }

    private void LogShotDebug(WeaponDefinition definition, WeaponDamageSettings settings, int pelletsFired)
    {
        int pelletsHit = pelletHits.Count;
        float totalDistance = 0f;
        float damageBeforeFalloff = 0f;
        float damageAfterFalloff = 0f;

        for (int i = 0; i < pelletHits.Count; i++)
        {
            float distance = pelletHits[i].distance;
            totalDistance += distance;
            damageBeforeFalloff += settings.ProjectileDamage;
            damageAfterFalloff += WeaponDamageCalculator.EvaluateProjectileDamage(settings, distance);
        }

        float averageDistance = pelletsHit > 0 ? totalDistance / pelletsHit : 0f;
        float distanceMultiplier = WeaponDamageCalculator.EvaluateDistanceMultiplier(settings, averageDistance);
        string weaponName = definition != null ? definition.DisplayName : "Weapon";

        Debug.Log(
            $"Weapon: {weaponName} Distance: {averageDistance:0.0}m " +
            $"Pellets Fired: {pelletsFired} Pellets Hit: {pelletsHit} " +
            $"Damage Before Falloff: {damageBeforeFalloff:0.00} " +
            $"Distance Multiplier: {distanceMultiplier:0.00} " +
            $"Weapon Damage: {damageAfterFalloff:0.00}");
    }

    private bool WantsToFire()
    {
        if (fireAction == null)
            return false;

        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        if (definition != null && definition.Automatic)
            return fireAction.action.IsPressed();

        return fireAction.action.WasPressedThisFrame();
    }

    private bool CanFire()
    {
        if (playerMovement != null && playerMovement.BlocksCombat)
            return false;

        if (weaponController != null && weaponController.BlocksFiring)
            return false;

        if (inventory != null && !inventory.CanFireActive())
            return false;

        return true;
    }

    private bool ShouldSuppressReload()
    {
        return interactor != null && interactor.ShouldSuppressReload;
    }

    private bool TryTraceHitscan(
        float shotRange,
        float spreadAt1080,
        bool allowRicochet,
        out HitscanRicochet.TraceResult result,
        out Vector3 origin,
        out Vector3 direction)
    {
        origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        direction = playerCamera != null ? playerCamera.transform.forward : transform.forward;

        if (accuracy != null && playerCamera != null)
        {
            Ray ray = accuracy.GetHitscanRay(playerCamera, spreadAt1080);
            origin = ray.origin;
            direction = ray.direction;
        }

        return HitscanRicochet.Trace(
            origin,
            direction,
            shotRange,
            allowRicochet,
            HitscanRicochet.DefaultMaxRicochets,
            excludePlayersFromReflectedRay: false,
            NetworkObject,
            hits,
            out result);
    }
}
