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
    private PlayerWeaponInventory inventory;
    private PlayerWeaponController weaponController;
    private PlayerWeaponInteractor interactor;
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

    private bool TryGetHitscanHit(out RaycastHit selectedHit)
    {
        selectedHit = default;

        int mask = ~0;
        if (pickupLayer >= 0)
            mask &= ~(1 << pickupLayer);

        int hitCount = Physics.RaycastNonAlloc(
            playerCamera.transform.position,
            playerCamera.transform.forward,
            hits,
            range,
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
