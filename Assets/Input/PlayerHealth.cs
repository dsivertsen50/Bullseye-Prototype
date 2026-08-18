using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 2f, 0f);
    [SerializeField] private Transform bullseye;
    [SerializeField] private CapsuleCollider bodyCapsule;
    [SerializeField] private BullseyeTarget bullseyeTarget;

    [SerializeField] private int maxHealth = 6;
    [SerializeField] private int upperHitsToKill = 1;
    [SerializeField] private int middleHitsToKill = 2;
    [SerializeField] private int lowerHitsToKill = 3;

    [SerializeField, Range(0.05f, 0.95f)] private float lowerMiddleBoundary = 1f / 3f;
    [SerializeField, Range(0.05f, 0.95f)] private float middleUpperBoundary = 2f / 3f;

    private readonly NetworkVariable<int> currentHealth = new(
        6,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private void Awake()
    {
        if (bodyCapsule == null)
            bodyCapsule = GetComponentInChildren<CapsuleCollider>();

        if (bullseyeTarget == null)
            bullseyeTarget = GetComponentInChildren<BullseyeTarget>();

        if (bullseye == null && bullseyeTarget != null)
            bullseye = bullseyeTarget.transform;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            currentHealth.Value = GetMaxHealth();
    }

    public void RegisterBullseyeHit()
    {
        if (!IsSpawned)
            return;

        HitServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void HitServerRpc()
    {
        if (currentHealth.Value <= 0)
            return;

        BullseyeBodyZone zone = ResolveZone();
        int damage = GetZoneDamage(zone);
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - damage);

        Debug.Log(
            $"Bullseye {zone} hit for {damage} damage. Health: {currentHealth.Value}/{GetMaxHealth()}");

        FlashBullseyeRpc();

        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = GetMaxHealth();

            if (TryGetComponent(out BullseyeMover mover))
                mover.ClearInfluence();

            RespawnOwnerRpc();
        }
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

    private void PerformRespawn()
    {
        CharacterController controller = GetComponent<CharacterController>();
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();

        controller.enabled = false;

        networkTransform.Teleport(
            respawnPosition,
            Quaternion.identity,
            transform.localScale
        );

        controller.enabled = true;

        if (TryGetComponent(out PlayerMovement movement))
            movement.ResetAfterRespawn();

        if (TryGetComponent(out BullseyeMover mover))
            mover.ResetTurnTracking();

        Debug.Log("You were hit! Respawning.");
    }

    private BullseyeBodyZone ResolveZone()
    {
        if (bodyCapsule == null || bullseye == null)
            return BullseyeBodyZone.Upper;

        return BullseyeDamageZones.Classify(
            bodyCapsule,
            bullseye.position,
            lowerMiddleBoundary,
            middleUpperBoundary);
    }

    private int GetZoneDamage(BullseyeBodyZone zone)
    {
        int hitsToKill = zone switch
        {
            BullseyeBodyZone.Upper => upperHitsToKill,
            BullseyeBodyZone.Middle => middleHitsToKill,
            _ => lowerHitsToKill
        };

        hitsToKill = Mathf.Max(1, hitsToKill);
        return Mathf.Max(1, Mathf.CeilToInt(GetMaxHealth() / (float)hitsToKill));
    }

    private int GetMaxHealth()
    {
        return Mathf.Max(1, maxHealth);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        upperHitsToKill = Mathf.Max(1, upperHitsToKill);
        middleHitsToKill = Mathf.Max(1, middleHitsToKill);
        lowerHitsToKill = Mathf.Max(1, lowerHitsToKill);

        lowerMiddleBoundary = Mathf.Clamp(lowerMiddleBoundary, 0.05f, 0.95f);
        middleUpperBoundary = Mathf.Clamp(middleUpperBoundary, 0.05f, 0.95f);
        if (middleUpperBoundary < lowerMiddleBoundary)
            middleUpperBoundary = lowerMiddleBoundary;
    }
}
