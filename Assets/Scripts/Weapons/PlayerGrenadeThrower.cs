using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner input and server-authoritative grenade spawning.
/// Keyboard throw: C. Gamepad throw: Left Shoulder / LB.
/// Next grenade: N / D-Pad Right.
/// </summary>
public class PlayerGrenadeThrower : NetworkBehaviour
{
    [Header("Inventory")]
    [SerializeField] private int startingGrenades = 1;

    [Header("Throw")]
    [SerializeField] private Grenade grenadePrefab;
    [SerializeField] private Grenade suctionGrenadePrefab;
    [SerializeField] private Transform throwOrigin;
    [SerializeField] private float throwForce = 20f;
    [SerializeField] private float throwUpwardBias = 0.28f;
    [SerializeField] private float throwOriginForward = 0.7f;
    [SerializeField] private float throwOriginRight = -0.22f;
    [SerializeField] private float throwOriginDown = 0.18f;
    [SerializeField] private float maxReportedOriginDistance = 3.5f;

    [Header("Feedback")]
    [SerializeField] private AudioClip grenadeThrowSfx;
    [SerializeField] private float throwSfxVolume = 0.7f;

    [Header("Input")]
    [SerializeField] private InputActionReference grenadeAction;
    [SerializeField] private InputActionReference nextGrenadeAction;
    [SerializeField] private Camera playerCamera;

    private readonly NetworkVariable<int> remainingGrenades = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<byte> selectedGrenadeType = new(
        (byte)GrenadeType.Standard,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHealth playerHealth;
    private InputAction resolvedGrenadeAction;
    private InputAction resolvedNextGrenadeAction;
    private AudioSource audioSource;

    public int RemainingGrenades => remainingGrenades.Value;
    public int StartingGrenades => Mathf.Max(0, startingGrenades);
    public GrenadeType SelectedGrenadeType => (GrenadeType)selectedGrenadeType.Value;
    public string SelectedGrenadeDisplayName => GrenadeTypeNames.DisplayName(SelectedGrenadeType);

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            remainingGrenades.Value = StartingGrenades;
            selectedGrenadeType.Value = (byte)FirstAvailableType();
        }

        if (!IsOwner)
            return;

        BindGrenadeActions();
        resolvedGrenadeAction?.Enable();
        resolvedNextGrenadeAction?.Enable();
    }

    public override void OnNetworkDespawn()
    {
        resolvedGrenadeAction = null;
        resolvedNextGrenadeAction = null;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (TryGetComponent(out PlayerMovement movement) && movement.BlocksCombat)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        if (resolvedNextGrenadeAction != null && resolvedNextGrenadeAction.WasPressedThisFrame())
            SelectNextGrenadeServerRpc();

        if (resolvedGrenadeAction == null || !resolvedGrenadeAction.WasPressedThisFrame())
            return;

        TryThrow();
    }

    public void ResetGrenades()
    {
        if (!IsServer)
            return;

        remainingGrenades.Value = StartingGrenades;
    }

    private void TryThrow()
    {
        Grenade prefab = GetPrefab(SelectedGrenadeType);
        if (remainingGrenades.Value <= 0 || prefab == null)
            return;

        ResolveThrowPose(out Vector3 origin, out Vector3 velocity);
        PlayThrowFeedback();
        if (TryGetComponent(out PlayerAnimationState animationState))
            animationState.NotifyThrowStarted();
        ThrowServerRpc(origin, velocity, (byte)SelectedGrenadeType);
    }

    [Rpc(SendTo.Server)]
    private void ThrowServerRpc(
        Vector3 reportedOrigin,
        Vector3 reportedVelocity,
        byte reportedType,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (TryGetComponent(out PlayerMovement movement) && movement.BlocksCombat)
            return;

        GrenadeType type = SanitizeType(reportedType);
        Grenade prefab = GetPrefab(type);
        if (remainingGrenades.Value <= 0 || prefab == null)
            return;

        if (NetworkManager == null || !NetworkManager.IsListening)
            return;

        SanitizeThrow(reportedOrigin, reportedVelocity, out Vector3 origin, out Vector3 velocity);

        remainingGrenades.Value = Mathf.Max(0, remainingGrenades.Value - 1);
        selectedGrenadeType.Value = (byte)type;

        Grenade instance = Instantiate(prefab, origin, Quaternion.LookRotation(velocity.normalized, Vector3.up));
        instance.InitializeThrow(OwnerClientId, velocity);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Destroy(instance.gameObject);
            remainingGrenades.Value = Mathf.Max(0, remainingGrenades.Value + 1);
            return;
        }

        networkObject.Spawn();
    }

    [Rpc(SendTo.Server)]
    private void SelectNextGrenadeServerRpc(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        selectedGrenadeType.Value = (byte)NextAvailableType(SelectedGrenadeType);
    }

    private void SanitizeThrow(Vector3 reportedOrigin, Vector3 reportedVelocity, out Vector3 origin, out Vector3 velocity)
    {
        ResolveThrowPose(out Vector3 fallbackOrigin, out Vector3 fallbackVelocity);

        origin = reportedOrigin;
        if ((reportedOrigin - transform.position).sqrMagnitude > maxReportedOriginDistance * maxReportedOriginDistance)
            origin = fallbackOrigin;

        velocity = reportedVelocity.sqrMagnitude > 0.01f ? reportedVelocity : fallbackVelocity;
        if (velocity.sqrMagnitude > (throwForce * 2f) * (throwForce * 2f))
            velocity = velocity.normalized * throwForce;
    }

    private void ResolveThrowPose(out Vector3 origin, out Vector3 velocity)
    {
        Transform source = throwOrigin != null
            ? throwOrigin
            : playerCamera != null ? playerCamera.transform : transform;

        if (throwOrigin != null)
        {
            origin = throwOrigin.position;
        }
        else
        {
            origin = source.position
                + source.forward * throwOriginForward
                + source.right * throwOriginRight
                - source.up * throwOriginDown;
        }

        Vector3 direction = source.forward + Vector3.up * throwUpwardBias;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward + Vector3.up * throwUpwardBias;

        velocity = direction.normalized * Mathf.Max(0.1f, throwForce);
    }

    private Grenade GetPrefab(GrenadeType type)
    {
        switch (type)
        {
            case GrenadeType.Suction:
                return suctionGrenadePrefab;
            default:
                return grenadePrefab;
        }
    }

    private GrenadeType SanitizeType(byte raw)
    {
        GrenadeType type = (GrenadeType)raw;
        if (GetPrefab(type) != null)
            return type;

        return FirstAvailableType();
    }

    private GrenadeType FirstAvailableType()
    {
        for (int i = 0; i < GrenadeTypeOrder.All.Length; i++)
        {
            GrenadeType type = GrenadeTypeOrder.All[i];
            if (GetPrefab(type) != null)
                return type;
        }

        return GrenadeType.Standard;
    }

    private GrenadeType NextAvailableType(GrenadeType current)
    {
        GrenadeType[] order = GrenadeTypeOrder.All;
        int start = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == current)
            {
                start = i;
                break;
            }
        }

        for (int i = 1; i <= order.Length; i++)
        {
            GrenadeType next = order[(start + i) % order.Length];
            if (GetPrefab(next) != null)
                return next;
        }

        return current;
    }

    private void BindGrenadeActions()
    {
        InputActionAsset actions = null;
        if (TryGetComponent(out LocalPlayerInputBinding binding))
            actions = binding.PlayerActions;

        if (grenadeAction != null && grenadeAction.action != null)
            resolvedGrenadeAction = grenadeAction.action;
        else if (actions != null)
            resolvedGrenadeAction = actions.FindAction("Grenade");

        if (nextGrenadeAction != null && nextGrenadeAction.action != null)
            resolvedNextGrenadeAction = nextGrenadeAction.action;
        else if (actions != null)
            resolvedNextGrenadeAction = actions.FindAction("NextGrenade");
    }

    private void PlayThrowFeedback()
    {
        if (grenadeThrowSfx == null)
            return;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            PlayerGameSettings.RouteToSfx(audioSource);
        }

        audioSource.PlayOneShot(grenadeThrowSfx, Mathf.Clamp01(throwSfxVolume));
    }

    private void OnValidate()
    {
        startingGrenades = Mathf.Max(0, startingGrenades);
        throwForce = Mathf.Max(0.1f, throwForce);
        throwUpwardBias = Mathf.Max(0f, throwUpwardBias);
        throwOriginForward = Mathf.Max(0f, throwOriginForward);
        maxReportedOriginDistance = Mathf.Max(0.5f, maxReportedOriginDistance);
        throwSfxVolume = Mathf.Clamp01(throwSfxVolume);
    }
}

internal static class GrenadeTypeOrder
{
    public static readonly GrenadeType[] All =
    {
        GrenadeType.Standard,
        GrenadeType.Suction
    };
}
