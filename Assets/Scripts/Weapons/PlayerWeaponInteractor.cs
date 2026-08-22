using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner-only ground-weapon targeting. Temporary weapons require a deliberate
/// interact hold. Permanent default weapons are never picked up.
/// </summary>
public class PlayerWeaponInteractor : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float pickupDistance = 2.5f;
    [SerializeField] private float pickupHoldDuration = 0.4f;

    private PlayerWeaponInventory inventory;
    private PlayerHealth playerHealth;
    private InputAction interactAction;
    private GroundWeaponPickup targetedPickup;
    private float holdTime;
    private bool usingGamepadPrompt;

    public GroundWeaponPickup TargetedPickup => targetedPickup;
    public float HoldProgress => pickupHoldDuration <= 0.01f ? 1f : Mathf.Clamp01(holdTime / pickupHoldDuration);
    public bool IsHoldingPickup => holdTime > 0f && targetedPickup != null && IsPickupTarget(targetedPickup);
    public bool ShouldSuppressReload => IsHoldingPickup || (targetedPickup != null && IsPickupTarget(targetedPickup) && IsInteractPressed());

    public override void OnNetworkSpawn()
    {
        inventory = GetComponent<PlayerWeaponInventory>();
        playerHealth = GetComponent<PlayerHealth>();
        if (playerCamera == null)
        {
            Transform cameraTransform = transform.Find("CameraRoot/CameraEffectsRoot/Camera");
            if (cameraTransform != null)
                playerCamera = cameraTransform.GetComponent<Camera>();
        }

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        BindInteract();
    }

    private void Update()
    {
        if (!IsOwner || inventory == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            ClearTarget();
            return;
        }

        if (LocalPlayerMenuState.IsOpen(this))
        {
            ClearTarget();
            return;
        }

        UpdateDevicePrompt();
        UpdateTargetedPickup();
        TickPickupHold();
    }

    public string GetPromptText()
    {
        if (targetedPickup == null || !IsPickupTarget(targetedPickup))
            return string.Empty;

        WeaponDefinition definition = targetedPickup.Definition;
        string weaponName = definition != null ? definition.DisplayName.ToUpperInvariant() : "WEAPON";
        string button = usingGamepadPrompt ? "X" : "E";
        string action = inventory != null && inventory.HasTemporaryWeapon ? "Swap" : "Pick Up";
        return $"{weaponName}\n{targetedPickup.TotalAmmo} rounds remaining\nHold {button} to {action}";
    }

    private void UpdateTargetedPickup()
    {
        targetedPickup = RaycastPickup();
        if (targetedPickup != null && !IsPickupTarget(targetedPickup))
            targetedPickup = null;
    }

    private GroundWeaponPickup RaycastPickup()
    {
        if (playerCamera == null)
            return null;

        int pickupLayer = LayerMask.NameToLayer("WeaponPickup");
        int mask = pickupLayer >= 0 ? 1 << pickupLayer : ~0;
        if (!Physics.Raycast(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                out RaycastHit hit,
                pickupDistance,
                mask,
                QueryTriggerInteraction.Collide))
            return null;

        return hit.collider.GetComponentInParent<GroundWeaponPickup>();
    }

    private void TickPickupHold()
    {
        if (targetedPickup == null || !IsPickupTarget(targetedPickup) || (inventory != null && inventory.IsLocallyBusy))
        {
            holdTime = 0f;
            return;
        }

        if (!IsInteractPressed())
        {
            holdTime = 0f;
            return;
        }

        holdTime += Time.deltaTime;
        if (holdTime < Mathf.Max(0.05f, pickupHoldDuration))
            return;

        inventory.RequestSwapPickup(targetedPickup.NetworkObject);
        holdTime = 0f;
        targetedPickup = null;
    }

    private bool IsPickupTarget(GroundWeaponPickup pickup)
    {
        if (pickup == null || !pickup.IsAvailable)
            return false;

        if (pickup.IsIgnoredFor(OwnerClientId))
            return false;

        WeaponDefinition definition = pickup.Definition;
        return definition != null && !definition.IsPermanentDefault;
    }

    private bool IsInteractPressed()
    {
        return interactAction != null && interactAction.IsPressed();
    }

    private void ClearTarget()
    {
        targetedPickup = null;
        holdTime = 0f;
    }

    private void UpdateDevicePrompt()
    {
        Keyboard keyboard = Keyboard.current;
        if (interactAction != null && interactAction.IsPressed() && interactAction.activeControl != null)
            usingGamepadPrompt = interactAction.activeControl.device is Gamepad;
        else if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            usingGamepadPrompt = false;
    }

    private void BindInteract()
    {
        InputActionAsset actions = null;
        if (TryGetComponent(out LocalPlayerInputBinding binding))
            actions = binding.PlayerActions;

        if (actions == null)
            return;

        interactAction = actions.FindAction("Interact");
        interactAction?.Enable();
    }
}
