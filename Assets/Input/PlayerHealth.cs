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

    [Header("Damage")]
    [SerializeField] private int headDamage = 8;
    [SerializeField] private int torsoDamage = 4;
    [SerializeField] private int lowerBodyDamage = 2;

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
    private Collider bullseyeCollider;
    private Coroutine respawnRoutine;
    private float regenerationDelayRemaining;
    private float regenerationAccumulator;

    public int CurrentHealth => currentHealth.Value;
    public int MaxHealth => GetMaxHealth();
    public bool IsDead => isDead.Value;
    public float RespawnDelay => Mathf.Max(0f, respawnDelay);
    public event System.Action<int, int> HealthChanged;

    private void Awake()
    {
        playerHaptics = GetComponent<PlayerHaptics>();

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
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        TickRegeneration(Time.deltaTime);
    }

    public void RegisterBullseyeHit()
    {
        if (!IsSpawned)
            return;

        HitServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void HitServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == OwnerClientId)
            return;

        if (isDead.Value || currentHealth.Value <= 0)
            return;

        BullseyeBodyZone zone = ResolveZone();
        int damage = GetZoneDamage(zone);
        if (damage <= 0)
            return;

        SetHealth(currentHealth.Value - damage);
        InterruptRegeneration();

        Debug.Log(
            $"Bullseye {zone} hit for {damage} damage. Health: {currentHealth.Value}/{GetMaxHealth()}");

        PlayDamageRumbleOwnerRpc();
        FlashBullseyeRpc();

        if (currentHealth.Value <= 0)
            HandleDeath();
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

    private void HandleDeath()
    {
        if (isDead.Value)
            return;

        isDead.Value = true;
        ClearRegeneration();
        respawnAtServerTime.Value = NetworkManager.ServerTime.Time + RespawnDelay;

        StopRespawnRoutine();
        respawnRoutine = StartCoroutine(RespawnAfterDelay());
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

        RespawnOwnerRpc();
        RestoreFullHealth();
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
        if (bodyCapsule == null || bullseye == null)
            return BullseyeBodyZone.Head;

        return BullseyeDamageZones.Classify(
            bodyCapsule,
            bullseye.position,
            lowerTorsoBoundary,
            torsoHeadBoundary);
    }

    private int GetZoneDamage(BullseyeBodyZone zone)
    {
        return zone switch
        {
            BullseyeBodyZone.Head => Mathf.Max(0, headDamage),
            BullseyeBodyZone.Torso => Mathf.Max(0, torsoDamage),
            _ => Mathf.Max(0, lowerBodyDamage)
        };
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
