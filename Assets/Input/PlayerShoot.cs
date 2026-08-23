using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private float range = 100f;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly List<(BullseyeTarget target, float distance)> pelletHits = new(16);
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
    private int pickupLayer = -1;

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
        pickupLayer = LayerMask.NameToLayer("WeaponPickup");
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

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        if (playerMovement != null && playerMovement.IsSprinting && !playerMovement.CanRunWhileShooting)
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

        if (playerHaptics != null)
            playerHaptics.PlayFireRumble();

        if (weaponPresentationCoordinator != null)
            weaponPresentationCoordinator.NotifyFire();
        else if (weaponPresentation != null)
            weaponPresentation.PlayFirePresentation();

        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        WeaponDamageSettings damageSettings = definition != null
            ? definition.DamageSettings
            : WeaponDamageSettings.Fallback;
        int projectileCount = damageSettings.ProjectileCount;
        float shotRange = damageSettings != null ? damageSettings.MaximumRange : range;
        float pelletSpread = ResolvePelletSpread(damageSettings);

        pelletHits.Clear();
        for (int i = 0; i < projectileCount; i++)
        {
            if (!TryGetHitscanHit(shotRange, pelletSpread, out RaycastHit hit))
                continue;

            if (!hit.collider.TryGetComponent(out BullseyeTarget target))
                continue;

            pelletHits.Add((target, hit.distance));
        }

        if (damageSettings.LogDamage)
            LogShotDebug(definition, damageSettings, projectileCount);

        bool scoredHit = RegisterGroupedHits();
        if (scoredHit && TryGetComponent(out Reticle reticle))
            reticle.ShowHitMarker();

        if (scoredHit)
            Debug.Log("BULLSEYE HIT!");

        if (accuracy != null)
            accuracy.NotifyShotFired();
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

    private bool TryGetHitscanHit(float shotRange, float spreadAt1080, out RaycastHit selectedHit)
    {
        selectedHit = default;

        int mask = ~0;
        if (pickupLayer >= 0)
            mask &= ~(1 << pickupLayer);

        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        Vector3 direction = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        if (accuracy != null && playerCamera != null)
        {
            Ray ray = accuracy.GetHitscanRay(playerCamera, spreadAt1080);
            origin = ray.origin;
            direction = ray.direction;
        }

        if (playerCamera != null && inventory != null)
        {
            WeaponDamageSettings settings = inventory.ActiveDefinition != null
                ? inventory.ActiveDefinition.DamageSettings
                : null;
            if (settings != null && settings.LogDamage)
                Debug.DrawRay(origin, direction * shotRange, Color.yellow, 0.2f);
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            hits,
            shotRange,
            mask,
            QueryTriggerInteraction.Collide);

        float closestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider))
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            selectedHit = hit;
            found = true;
        }

        return found;
    }

    private bool IsOwnCollider(Collider collider)
    {
        NetworkObject ownerObject = collider.GetComponentInParent<NetworkObject>();
        return ownerObject != null && ownerObject == NetworkObject;
    }
}
