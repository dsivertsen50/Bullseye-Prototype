using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private float range = 100f;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private PlayerHaptics playerHaptics;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private WeaponPresentationCoordinator weaponPresentationCoordinator;
    private WeaponPresentationController weaponPresentation;

    private void Awake()
    {
        playerHaptics = GetComponent<PlayerHaptics>();
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        weaponPresentationCoordinator = GetComponent<WeaponPresentationCoordinator>();
        weaponPresentation = GetComponent<WeaponPresentationController>();
    }

    private void OnEnable()
    {
        // Enable only. Player instances share this action in-process.
        fireAction.action.Enable();
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

        if (fireAction.action.WasPressedThisFrame())
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (playerHaptics != null)
            playerHaptics.PlayFireRumble();

        if (weaponPresentationCoordinator != null)
            weaponPresentationCoordinator.NotifyFire();
        else if (weaponPresentation != null)
            weaponPresentation.PlayFirePresentation();

        if (playerCamera == null)
            return;

        if (!TryGetHitscanHit(out RaycastHit hit))
            return;

        if (!hit.collider.TryGetComponent(out BullseyeTarget target))
            return;

        if (!target.TryRegisterHit(OwnerClientId))
            return;

        if (TryGetComponent(out Reticle reticle))
            reticle.ShowHitMarker();

        Debug.Log("BULLSEYE HIT!");
    }

    private bool TryGetHitscanHit(out RaycastHit selectedHit)
    {
        selectedHit = default;

        int hitCount = Physics.RaycastNonAlloc(
            playerCamera.transform.position,
            playerCamera.transform.forward,
            hits,
            range,
            ~0,
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
