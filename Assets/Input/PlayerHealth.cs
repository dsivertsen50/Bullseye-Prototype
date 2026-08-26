using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 2f, 0f);
    [SerializeField] private Transform bullseye;
    [SerializeField] private CapsuleCollider bodyCapsule;
    [SerializeField] private BullseyeTarget bullseyeTarget;

    [Header("Health")]
    [SerializeField] private int maxHealth = 8;

    [Header("Hit Location Multipliers")]
    [SerializeField, Tooltip("Multiplier applied after weapon and distance damage. Pistol baseline headshot is 8.")]
    private int headDamage = 8;
    [SerializeField, Tooltip("Multiplier applied after weapon and distance damage. Pistol baseline torso hit is 4.")]
    private int torsoDamage = 4;
    [SerializeField, Tooltip("Multiplier applied after weapon and distance damage. Pistol baseline lower-body hit is 2.")]
    private int lowerBodyDamage = 2;

    [Header("Debug")]
    [SerializeField, Tooltip("Logs resolved weapon damage events. Leave off for normal play.")]
    private bool logDamageEvents;

    [Header("Regeneration")]
    [SerializeField] private float regenerationDelay = 5f;
    [SerializeField] private float regenerationRate = 1f;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 3f;

    [Header("Prototype Region Bounds")]
    [SerializeField, Range(0.05f, 0.95f)] private float lowerTorsoBoundary = 1f / 3f;
    [SerializeField, Range(0.05f, 0.95f)] private float torsoHeadBoundary = 2f / 3f;

    private readonly NetworkVariable<int> currentHealth = new(
        8,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isDead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> respawnAtServerTime = new(
        0d,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHaptics playerHaptics;
    private BullseyeDetachController detachController;
    private PlayerGrenadeThrower grenadeThrower;
    private BullseyeShatterController shatterController;
    private Collider bullseyeCollider;
    private Coroutine respawnRoutine;
    private float regenerationDelayRemaining;
    private float regenerationAccumulator;

    public int CurrentHealth => currentHealth.Value;
    public int MaxHealth => GetMaxHealth();
    public bool IsDead => isDead.Value;
    public bool AreDeathVisualsHidden =>
        IsDead && (shatterController == null || shatterController.AreCorpseVisualsHidden);
    public float RespawnDelay => Mathf.Max(0f, respawnDelay);
    public event System.Action<int, int> HealthChanged;

    private void Awake()
    {
        playerHaptics = GetComponent<PlayerHaptics>();
        detachController = GetComponent<BullseyeDetachController>();
        grenadeThrower = GetComponent<PlayerGrenadeThrower>();
        shatterController = GetComponent<BullseyeShatterController>();

        if (bodyCapsule == null)
            bodyCapsule = GetComponentInChildren<CapsuleCollider>();

        if (bullseyeTarget == null)
            bullseyeTarget = GetComponentInChildren<BullseyeTarget>();

        if (bullseye == null && bullseyeTarget != null)
            bullseye = bullseyeTarget.transform;

        if (bullseyeTarget != null)
            bullseyeCollider = bullseyeTarget.GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnCurrentHealthChanged;
        isDead.OnValueChanged += OnDeadChanged;
        ApplyDeadPresentation(isDead.Value);

        if (!IsServer)
            return;

        RestoreFullHealth();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnCurrentHealthChanged;
        isDead.OnValueChanged -= OnDeadChanged;
        StopRespawnRoutine();
        if (shatterController != null)
            shatterController.CleanupForDespawn();
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        TickRegeneration(Time.deltaTime);
    }

    public void RegisterBullseyeHit()
    {
        RegisterBullseyeHits(System.Array.Empty<float>());
    }

    public void RegisterBullseyeHits(float[] distances)
    {
        if (!IsSpawned)
            return;

        HitServerRpc(distances ?? System.Array.Empty<float>());
    }

    [Rpc(SendTo.Server)]
    private void HitServerRpc(float[] hitDistances, RpcParams rpcParams = default)
    {
        ulong attackerId = rpcParams.Receive.SenderClientId;
        if (attackerId == OwnerClientId)
            return;

        if (isDead.Value || currentHealth.Value <= 0)
            return;

        WeaponDefinition weapon = ResolveAttackerWeapon(attackerId);
        WeaponDamageSettings settings = weapon != null
            ? weapon.DamageSettings
            : WeaponDamageSettings.Fallback;

        int incomingHits = hitDistances != null && hitDistances.Length > 0
            ? hitDistances.Length
            : 1;
        int hitsToApply = Mathf.Clamp(incomingHits, 1, settings.ProjectileCount);

        BullseyeBodyZone zone = ResolveZone();
        float locationMultiplier = GetZoneMultiplier(zone);
        float rawTotal = 0f;
        float totalDistance = 0f;
        float totalBeforeFalloff = 0f;
        int appliedHits = 0;

        for (int i = 0; i < hitsToApply; i++)
        {
            if (WeaponDamageCalculator.ToHealthUnits(rawTotal) >= currentHealth.Value)
                break;

            float distance = ResolveHitDistance(hitDistances, i, attackerId);
            DamageInfo info = WeaponDamageCalculator.Evaluate(
                settings,
                weapon,
                distance,
                zone,
                locationMultiplier,
                i,
                attackerId,
                OwnerClientId);

            rawTotal += info.RawDamage;
            totalDistance += distance;
            totalBeforeFalloff += info.BaseDamage;
            appliedHits++;
        }

        int totalDamage = WeaponDamageCalculator.ToHealthUnits(rawTotal);
        if (settings.GuaranteeLethalHeadshot && zone == BullseyeBodyZone.Head)
            totalDamage = Mathf.Max(totalDamage, GetMaxHealth());

        if (totalDamage <= 0)
            return;

        ApplyDamage(DamageContext.FromFirearm(
            attackerId,
            OwnerClientId,
            totalDamage,
            weapon != null ? weapon.WeaponId : "unknown"));

        float averageDistance = appliedHits > 0 ? totalDistance / appliedHits : 0f;
        LogResolvedDamage(
            weapon,
            settings,
            zone,
            appliedHits,
            averageDistance,
            totalBeforeFalloff,
            totalDamage);
    }

    /// <summary>
    /// Server-only damage entry point. Weapons report attacker information
    /// here; this component applies health change and death.
    /// </summary>
    public void ApplyDamage(DamageContext context)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (isDead.Value || currentHealth.Value <= 0)
            return;

        int amount = Mathf.Max(0, context.Amount);
        if (amount <= 0)
            return;

        SetHealth(currentHealth.Value - amount);
        InterruptRegeneration();
        PlayDamageRumbleOwnerRpc();
        FlashBullseyeRpc();

        if (currentHealth.Value <= 0)
            HandleDeath(context);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void PlayDamageRumbleOwnerRpc()
    {
        if (playerHaptics != null)
            playerHaptics.PlayDamageRumble();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void FlashBullseyeRpc()
    {
        if (bullseyeTarget != null)
            bullseyeTarget.PlayHitFlash();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void RespawnOwnerRpc()
    {
        PerformRespawn();
    }

    private void HandleDeath(DamageContext context)
    {
        if (isDead.Value)
            return;

        RecordKillAndDeath(context);

        isDead.Value = true;
        ClearRegeneration();
        respawnAtServerTime.Value = NetworkManager.ServerTime.Time + RespawnDelay;

        if (detachController != null)
            detachController.HandleOwnerDied();

        if (TryGetComponent(out PlayerWeaponInventory inventory))
            inventory.DropTemporaryWeaponOnDeath();

        StopRespawnRoutine();
        respawnRoutine = StartCoroutine(RespawnAfterDelay());
    }

    private void RecordKillAndDeath(DamageContext context)
    {
        if (TryGetComponent(out PlayerStats victimStats))
            victimStats.AddDeath();

        if (!context.HasAttacker || context.AttackerClientId == OwnerClientId)
            return;

        PlayerStats attackerStats = PlayerStats.FindOwnedByClient(context.AttackerClientId);
        if (attackerStats == null || attackerStats == victimStats)
            return;

        attackerStats.AddKill();

        bool bullseyeWasKnockedOff = detachController != null && !detachController.IsAttached;
        if (bullseyeWasKnockedOff)
            attackerStats.AddDetachedBullseyeKill();
    }

    private IEnumerator RespawnAfterDelay()
    {
        while (IsSpawned && NetworkManager.ServerTime.Time < respawnAtServerTime.Value)
            yield return null;

        respawnRoutine = null;

        if (!IsSpawned || !isDead.Value)
            yield break;

        CompleteRespawn();
    }

    private void CompleteRespawn()
    {
        if (TryGetComponent(out BullseyeMover mover))
            mover.RestartIndependentRandomization();

        if (detachController != null)
            detachController.HandleOwnerRespawned();

        if (grenadeThrower != null)
            grenadeThrower.ResetGrenades();

        RespawnOwnerRpc();
        RestoreFullHealth();

        if (TryGetComponent(out PlayerWeaponInventory inventory))
            inventory.RestoreDefaultLoadout();
    }

    private void StopRespawnRoutine()
    {
        if (respawnRoutine == null)
            return;

        StopCoroutine(respawnRoutine);
        respawnRoutine = null;
    }

    public int GetRespawnCountdownNumber()
    {
        if (!isDead.Value)
            return 0;

        double remaining = RespawnDelay;
        if (NetworkManager != null && respawnAtServerTime.Value > 0d)
            remaining = respawnAtServerTime.Value - NetworkManager.ServerTime.Time;

        int number = Mathf.CeilToInt((float)remaining);
        return number > 0 ? number : 0;
    }

    public float GetElapsedDeathTime()
    {
        if (!isDead.Value)
            return 0f;

        if (NetworkManager == null || respawnAtServerTime.Value <= 0d)
            return 0f;

        double remaining = respawnAtServerTime.Value - NetworkManager.ServerTime.Time;
        return Mathf.Max(0f, RespawnDelay - (float)remaining);
    }

    private void OnCurrentHealthChanged(int previous, int next)
    {
        HealthChanged?.Invoke(previous, next);
    }

    private void OnDeadChanged(bool previous, bool next)
    {
        ApplyDeadPresentation(next);
    }

    private void ApplyDeadPresentation(bool dead)
    {
        if (bullseyeCollider != null)
            bullseyeCollider.enabled = !dead;

        if (shatterController != null)
            shatterController.HandleDeadChanged(dead);
    }

    private void PerformRespawn()
    {
        CharacterController controller = GetComponent<CharacterController>();
        Rigidbody body = GetComponent<Rigidbody>();
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();

        if (controller != null)
            controller.enabled = false;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        networkTransform.Teleport(
            respawnPosition,
            Quaternion.identity,
            transform.localScale
        );

        if (controller != null)
            controller.enabled = true;

        if (TryGetComponent(out PlayerMovement movement))
            movement.ResetAfterRespawn();

        if (TryGetComponent(out PlayerLook look))
            look.ResetAfterRespawn();

        if (TryGetComponent(out PlayerThirdPersonAnimator thirdPersonAnimator))
            thirdPersonAnimator.ResetAfterRespawn();

        if (TryGetComponent(out BullseyeMover mover))
            mover.ResetTurnTracking();

        Debug.Log("You were hit! Respawning.");
    }

    private void TickRegeneration(float deltaTime)
    {
        if (isDead.Value || currentHealth.Value <= 0 || currentHealth.Value >= GetMaxHealth())
            return;

        if (regenerationDelayRemaining > 0f)
        {
            regenerationDelayRemaining -= deltaTime;
            return;
        }

        if (regenerationRate <= 0f)
            return;

        regenerationAccumulator += deltaTime * regenerationRate;
        int gained = Mathf.FloorToInt(regenerationAccumulator);
        if (gained <= 0)
            return;

        regenerationAccumulator -= gained;
        SetHealth(currentHealth.Value + gained);

        if (currentHealth.Value >= GetMaxHealth())
            ClearRegeneration();
    }

    private void InterruptRegeneration()
    {
        regenerationDelayRemaining = Mathf.Max(0f, regenerationDelay);
        regenerationAccumulator = 0f;
    }

    private void RestoreFullHealth()
    {
        isDead.Value = false;
        respawnAtServerTime.Value = 0d;
        ClearRegeneration();
        SetHealth(GetMaxHealth());
    }

    private void ClearRegeneration()
    {
        regenerationDelayRemaining = 0f;
        regenerationAccumulator = 0f;
    }

    private void SetHealth(int value)
    {
        int clamped = Mathf.Clamp(value, 0, GetMaxHealth());
        if (currentHealth.Value != clamped)
            currentHealth.Value = clamped;
    }

    private BullseyeBodyZone ResolveZone()
    {
        if (detachController != null && detachController.IsDetached)
            return BullseyeBodyZone.Head;

        if (bodyCapsule == null || bullseye == null)
            return BullseyeBodyZone.Head;

        return BullseyeDamageZones.Classify(
            bodyCapsule,
            bullseye.position,
            lowerTorsoBoundary,
            torsoHeadBoundary);
    }

    private float GetZoneMultiplier(BullseyeBodyZone zone)
    {
        return zone switch
        {
            BullseyeBodyZone.Head => Mathf.Max(0, headDamage),
            BullseyeBodyZone.Torso => Mathf.Max(0, torsoDamage),
            _ => Mathf.Max(0, lowerBodyDamage)
        };
    }

    private WeaponDefinition ResolveAttackerWeapon(ulong attackerId)
    {
        if (NetworkManager == null || NetworkManager.SpawnManager == null)
            return null;

        NetworkObject attackerObject = NetworkManager.SpawnManager.GetPlayerNetworkObject(attackerId);
        if (attackerObject == null)
            return null;

        return attackerObject.TryGetComponent(out PlayerWeaponInventory inventory)
            ? inventory.ActiveDefinition
            : null;
    }

    private float ResolveHitDistance(float[] hitDistances, int index, ulong attackerId)
    {
        if (hitDistances != null && index >= 0 && index < hitDistances.Length && hitDistances[index] > 0f)
            return hitDistances[index];

        if (NetworkManager == null || NetworkManager.SpawnManager == null)
            return 0f;

        NetworkObject attackerObject = NetworkManager.SpawnManager.GetPlayerNetworkObject(attackerId);
        if (attackerObject == null)
            return 0f;

        Vector3 point = bullseye != null ? bullseye.position : transform.position;
        return Vector3.Distance(attackerObject.transform.position, point);
    }

    private void LogResolvedDamage(
        WeaponDefinition weapon,
        WeaponDamageSettings settings,
        BullseyeBodyZone zone,
        int pelletsHit,
        float averageDistance,
        float damageBeforeFalloff,
        int finalDamage)
    {
        bool shouldLog = logDamageEvents || (settings != null && settings.LogDamage);
        if (!shouldLog)
        {
            Debug.Log(
                $"Bullseye {zone} hit for {finalDamage} damage. Health: {currentHealth.Value}/{GetMaxHealth()}");
            return;
        }

        float multiplier = WeaponDamageCalculator.EvaluateDistanceMultiplier(settings, averageDistance);
        string weaponName = weapon != null ? weapon.DisplayName : "Weapon";
        Debug.Log(
            $"Weapon: {weaponName} Zone: {zone} Distance: {averageDistance:0.0}m " +
            $"Pellets Hit: {pelletsHit} Damage Before Falloff: {damageBeforeFalloff:0.00} " +
            $"Distance Multiplier: {multiplier:0.00} Final Damage: {finalDamage} " +
            $"Health: {currentHealth.Value}/{GetMaxHealth()}");
    }

    private int GetMaxHealth()
    {
        return Mathf.Max(1, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        headDamage = Mathf.Max(0, headDamage);
        torsoDamage = Mathf.Max(0, torsoDamage);
        lowerBodyDamage = Mathf.Max(0, lowerBodyDamage);
        regenerationDelay = Mathf.Max(0f, regenerationDelay);
        regenerationRate = Mathf.Max(0f, regenerationRate);
        respawnDelay = Mathf.Max(0f, respawnDelay);

        lowerTorsoBoundary = Mathf.Clamp(lowerTorsoBoundary, 0.05f, 0.95f);
        torsoHeadBoundary = Mathf.Clamp(torsoHeadBoundary, 0.05f, 0.95f);
        if (torsoHeadBoundary < lowerTorsoBoundary)
            torsoHeadBoundary = lowerTorsoBoundary;
    }
}
