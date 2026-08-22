using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner-only weapon switching input, switch cooldown, and holster/draw presentation.
/// </summary>
public class PlayerWeaponController : NetworkBehaviour
{
    [SerializeField] private float holsterDuration = 0.16f;
    [SerializeField] private float drawDuration = 0.2f;
    [SerializeField] private float switchInputCooldown = 0.22f;

    private PlayerWeaponInventory inventory;
    private WeaponPresentationCoordinator coordinator;
    private WeaponPresentationController firstPersonWeapon;
    private PlayerHealth playerHealth;
    private InputAction switchWeaponAction;
    private InputAction weaponSwitchAxis;
    private float nextSwitchTime;
    private int lastPresentedCatalogIndex = int.MinValue;
    private bool holsteredForSwitch;

    public float HolsterDuration => Mathf.Max(0.05f, holsterDuration);
    public float DrawDuration => Mathf.Max(0.05f, drawDuration);

    private void Awake()
    {
        inventory = GetComponent<PlayerWeaponInventory>();
        coordinator = GetComponent<WeaponPresentationCoordinator>();
        firstPersonWeapon = GetComponent<WeaponPresentationController>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        BindActions();
        if (inventory != null)
            inventory.InventoryChanged += OnInventoryChanged;
        if (coordinator != null)
            coordinator.WeaponChanged += OnWeaponChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (inventory != null)
            inventory.InventoryChanged -= OnInventoryChanged;
        if (coordinator != null)
            coordinator.WeaponChanged -= OnWeaponChanged;
    }

    private void Update()
    {
        if (!IsOwner || inventory == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        if (inventory.CanRequestSwitch() && WasSwitchPressed())
            inventory.RequestSwitch();
    }

    public bool BlocksFiring => inventory != null && inventory.IsLocallyBusy;

    private bool WasSwitchPressed()
    {
        if (inventory != null && inventory.IsLocallyBusy)
            return false;

        if (Time.time < nextSwitchTime)
            return false;

        bool pressed = false;
        if (switchWeaponAction != null && switchWeaponAction.WasPressedThisFrame())
            pressed = true;

        if (!pressed && weaponSwitchAxis != null)
        {
            float axis = weaponSwitchAxis.ReadValue<float>();
            if (Mathf.Abs(axis) > 0.15f)
                pressed = true;
        }

        if (!pressed)
            return false;

        nextSwitchTime = Time.time + Mathf.Max(0.05f, switchInputCooldown);
        PlayHolsterIfPossible();
        return true;
    }

    private void PlayHolsterIfPossible()
    {
        if (holsteredForSwitch || firstPersonWeapon == null)
            return;

        firstPersonWeapon.PlayHolsterPresentation();
        holsteredForSwitch = true;
    }

    private void OnInventoryChanged()
    {
        if (inventory == null)
            return;

        if (ShouldHolsterExhaustedTemporary())
        {
            PlayHolsterIfPossible();
            return;
        }

        if (inventory.IsSwitching)
            return;

        holsteredForSwitch = false;
    }

    private bool ShouldHolsterExhaustedTemporary()
    {
        if (!inventory.IsTemporaryActive)
            return false;

        WeaponRuntimeState temporary = inventory.TemporaryState;
        return !temporary.IsEmpty && temporary.TotalAmmo <= 0;
    }

    private void OnWeaponChanged(string weaponId)
    {
        if (inventory == null)
            return;

        int catalogIndex = inventory.ActiveState.CatalogIndex;
        bool swapped = catalogIndex != lastPresentedCatalogIndex;
        lastPresentedCatalogIndex = catalogIndex;
        holsteredForSwitch = false;

        if (swapped && firstPersonWeapon != null && IsOwner)
            firstPersonWeapon.PlayUnholsterPresentation();
    }

    private void BindActions()
    {
        InputActionAsset actions = null;
        if (TryGetComponent(out LocalPlayerInputBinding binding))
            actions = binding.PlayerActions;

        if (actions == null)
            return;

        switchWeaponAction = actions.FindAction("SwitchWeapon");
        weaponSwitchAxis = actions.FindAction("WeaponSwitch");
        switchWeaponAction?.Enable();
        weaponSwitchAxis?.Enable();
    }
}
