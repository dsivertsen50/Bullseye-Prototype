using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner input and server-authoritative grenade spawning.
/// Keyboard: C. Gamepad: Left Trigger. Left Trigger was unused.
/// </summary>
public class PlayerGrenadeThrower : NetworkBehaviour
{
    [Header("Inventory")]
    [SerializeField] private int startingGrenades = 1;

    [Header("Throw")]
    [SerializeField] private Grenade grenadePrefab;
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
    [SerializeField] private Camera playerCamera;

    private readonly NetworkVariable<int> remainingGrenades = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerHealth playerHealth;
    private InputAction resolvedGrenadeAction;
    private AudioSource audioSource;

    public int RemainingGrenades => remainingGrenades.Value;
    public int StartingGrenades => Mathf.Max(0, startingGrenades);

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            remainingGrenades.Value = StartingGrenades;

        if (!IsOwner)
            return;

        BindGrenadeAction();
        resolvedGrenadeAction?.Enable();
    }

    public override void OnNetworkDespawn()
    {
        resolvedGrenadeAction = null;
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

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
        if (remainingGrenades.Value <= 0 || grenadePrefab == null)
            return;

        ResolveThrowPose(out Vector3 origin, out Vector3 velocity);
        PlayThrowFeedback();
        if (TryGetComponent(out PlayerAnimationState animationState))
            animationState.NotifyThrowStarted();
        ThrowServerRpc(origin, velocity);
    }

    [Rpc(SendTo.Server)]
    private void ThrowServerRpc(Vector3 reportedOrigin, Vector3 reportedVelocity, RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (remainingGrenades.Value <= 0 || grenadePrefab == null)
            return;

        if (NetworkManager == null || !NetworkManager.IsListening)
            return;

        SanitizeThrow(reportedOrigin, reportedVelocity, out Vector3 origin, out Vector3 velocity);

        remainingGrenades.Value = Mathf.Max(0, remainingGrenades.Value - 1);

        Grenade instance = Instantiate(grenadePrefab, origin, Quaternion.LookRotation(velocity.normalized, Vector3.up));
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

    private void BindGrenadeAction()
    {
        if (grenadeAction != null && grenadeAction.action != null)
        {
            resolvedGrenadeAction = grenadeAction.action;
            return;
        }

        if (TryGetComponent(out LocalPlayerInputBinding binding) && binding.PlayerActions != null)
            resolvedGrenadeAction = binding.PlayerActions.FindAction("Grenade");
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
