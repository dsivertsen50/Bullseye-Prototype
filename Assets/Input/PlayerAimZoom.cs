using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(50)]
public class PlayerAimZoom : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference aimAction;

    [SerializeField] [Range(0f, 0.9f)] private float fovReduction = 0.25f;
    [SerializeField] private float zoomTransitionDuration = 0.12f;
    [SerializeField] private float sprintFovIncrease = 8f;
    [SerializeField] private InputActivationMode aimActivation = InputActivationMode.Toggle;

    private float defaultFov;
    private float currentFov;
    private bool aimToggledOn;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;

    public bool IsAiming { get; private set; }

    public float ZoomTransitionDuration => Mathf.Max(0.0001f, zoomTransitionDuration);

    public float FovReduction
    {
        get => fovReduction;
        set => fovReduction = Mathf.Clamp(value, 0f, 0.9f);
    }

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();

        defaultFov = playerCamera != null ? playerCamera.fieldOfView : 60f;
        currentFov = defaultFov;
    }

    private void OnEnable()
    {
        if (aimAction != null)
            aimAction.action.Enable();
    }

    private void Update()
    {
        if (playerCamera == null || aimAction == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            IsAiming = false;
            aimToggledOn = false;
            ApplyFov(defaultFov);
            return;
        }

        if (LocalPlayerMenuState.IsOpen(this))
        {
            IsAiming = false;
            aimToggledOn = false;
            ApplyFov(defaultFov);
            return;
        }

        bool sprinting = playerMovement != null && playerMovement.IsSprinting;
        if (sprinting)
        {
            IsAiming = false;
            aimToggledOn = false;
        }
        else
        {
            IsAiming = ReadAimInput();
        }

        float sprintFov = defaultFov + (sprinting ? sprintFovIncrease : 0f);
        float targetFov = IsAiming
            ? defaultFov * (1f - fovReduction)
            : sprintFov;

        ApplyFov(targetFov);
    }

    private bool ReadAimInput()
    {
        // Left Trigger is hold-to-aim. Mouse / keyboard keep the serialized toggle-or-hold setting.
        if (IsGamepadAimHeld())
        {
            aimToggledOn = false;
            return true;
        }

        if (aimActivation == InputActivationMode.Hold)
            return aimAction.action.IsPressed();

        if (aimAction.action.WasPressedThisFrame() && !IsGamepadAimControl())
            aimToggledOn = !aimToggledOn;

        return aimToggledOn;
    }

    private bool IsGamepadAimHeld()
    {
        return aimAction.action.IsPressed() && IsGamepadAimControl();
    }

    private bool IsGamepadAimControl()
    {
        InputControl control = aimAction.action.activeControl;
        return control != null && control.device is Gamepad;
    }

    private void ApplyFov(float targetFov)
    {
        float duration = Mathf.Max(0.0001f, zoomTransitionDuration);
        float fovSpan = Mathf.Max(
            Mathf.Abs(defaultFov - defaultFov * (1f - fovReduction)),
            sprintFovIncrease);
        float maxDelta = fovSpan / duration;
        currentFov = Mathf.MoveTowards(currentFov, targetFov, maxDelta * Time.deltaTime);
        playerCamera.fieldOfView = currentFov;
    }
}
