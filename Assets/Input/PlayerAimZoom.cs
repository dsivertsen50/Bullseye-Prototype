using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owner-side ADS input and gameplay-camera FOV owner.
/// Optical magnification comes from the active WeaponDefinition; this
/// component never branches on weapon names. FOV is local and not networked.
/// </summary>
[DefaultExecutionOrder(50)]
public class PlayerAimZoom : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference aimAction;

    [SerializeField] private float sprintFovIncrease = 8f;
    [SerializeField] private float fallbackAdsEnterDuration = 0.18f;
    [SerializeField] private float fallbackAdsExitDuration = 0.15f;
    [SerializeField] private InputActivationMode aimActivation = InputActivationMode.Toggle;

    private float defaultFov = 60f;
    private float currentFov;
    private bool aimToggledOn;
    private bool baseFovCaptured;
    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private PlayerWeaponInventory inventory;

    public bool IsAiming { get; private set; }

    public float BaseFov => defaultFov;

    public float CurrentFov => currentFov;

    public float CurrentMagnification
    {
        get
        {
            WeaponDefinition definition = ActiveDefinition;
            return definition != null ? definition.AdsMagnification : 1f;
        }
    }

    public bool UsesMagnifiedAds
    {
        get
        {
            WeaponDefinition definition = ActiveDefinition;
            return definition != null && definition.UsesMagnifiedAds && definition.AdsMagnification > 1.0001f;
        }
    }

    /// <summary>
    /// First-person weapon overlay FOV. Ignores optical ADS magnification so
    /// the viewmodel does not scale with the world camera.
    /// </summary>
    public float ViewmodelFov => defaultFov;

    public float ZoomTransitionDuration => CurrentAdsTransitionDuration;

    public float CurrentAdsTransitionDuration => IsAiming
        ? CurrentAdsEnterDuration
        : CurrentAdsExitDuration;

    public float CurrentAdsEnterDuration
    {
        get
        {
            WeaponDefinition definition = ActiveDefinition;
            return definition != null
                ? definition.AdsEnterDuration
                : Mathf.Max(0.01f, fallbackAdsEnterDuration);
        }
    }

    public float CurrentAdsExitDuration
    {
        get
        {
            WeaponDefinition definition = ActiveDefinition;
            return definition != null
                ? definition.AdsExitDuration
                : Mathf.Max(0.01f, fallbackAdsExitDuration);
        }
    }

    /// <summary>
    /// Final ADS look scale: player ADS setting × weapon ADS multiplier.
    /// Applied after mouse / gamepad base sensitivity.
    /// </summary>
    public float CurrentAdsLookScale
    {
        get
        {
            float globalAds = Mathf.Clamp(PlayerGameSettings.AimSensitivityMultiplier, 0.1f, 1.5f);
            WeaponDefinition definition = ActiveDefinition;
            float weaponAds = definition != null ? definition.AdsSensitivityMultiplier : 0.4f;
            return globalAds * weaponAds;
        }
    }

    private WeaponDefinition ActiveDefinition => inventory != null ? inventory.ActiveDefinition : null;

    public void SetBaseFov(float fov)
    {
        defaultFov = Mathf.Clamp(fov, 10f, 170f);
        if (!IsAiming && (playerMovement == null || !playerMovement.IsSprinting))
            currentFov = defaultFov;
    }

    public static float CalculateMagnifiedFov(float baseVerticalFov, float magnification)
    {
        float mag = Mathf.Max(1f, magnification);
        if (mag <= 1.0001f)
            return baseVerticalFov;

        float halfBase = baseVerticalFov * 0.5f * Mathf.Deg2Rad;
        float adsFov = 2f * Mathf.Atan(Mathf.Tan(halfBase) / mag) * Mathf.Rad2Deg;
        return Mathf.Clamp(adsFov, 1f, 179f);
    }

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        inventory = GetComponent<PlayerWeaponInventory>();

        CaptureBaseFovIfNeeded();
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

        CaptureBaseFovIfNeeded();

        if (playerHealth != null && playerHealth.IsDead)
        {
            ClearAimState();
            SnapFov(defaultFov);
            return;
        }

        if (LocalPlayerMenuState.IsOpen(this))
        {
            ClearAimState();
            ApplyFov(defaultFov, CurrentAdsExitDuration);
            return;
        }

        bool sprinting = playerMovement != null && playerMovement.IsSprinting;
        bool wallRunning = playerMovement != null && playerMovement.IsWallRunning;
        if (sprinting || wallRunning)
        {
            ClearAimState();
        }
        else
        {
            IsAiming = ReadAimInput();
        }

        float targetFov = ResolveTargetFov(sprinting);
        bool zoomingIn = targetFov < currentFov - 0.01f;
        float duration = zoomingIn ? CurrentAdsEnterDuration : CurrentAdsExitDuration;
        ApplyFov(targetFov, duration);
    }

    private void CaptureBaseFovIfNeeded()
    {
        if (baseFovCaptured || playerCamera == null)
            return;

        defaultFov = playerCamera.fieldOfView;
        currentFov = defaultFov;
        baseFovCaptured = true;
    }

    private float ResolveTargetFov(bool sprinting)
    {
        if (IsAiming && UsesMagnifiedAds)
            return CalculateMagnifiedFov(defaultFov, CurrentMagnification);

        if (sprinting)
            return defaultFov + sprintFovIncrease;

        return defaultFov;
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

    private void ClearAimState()
    {
        IsAiming = false;
        aimToggledOn = false;
    }

    private void SnapFov(float fov)
    {
        currentFov = fov;
        if (playerCamera != null)
            playerCamera.fieldOfView = currentFov;
    }

    private void ApplyFov(float targetFov, float duration)
    {
        duration = Mathf.Max(0.0001f, duration);
        float adsFov = CalculateMagnifiedFov(defaultFov, Mathf.Max(1f, CurrentMagnification));
        float fovSpan = Mathf.Max(
            Mathf.Abs(defaultFov - adsFov),
            Mathf.Abs(currentFov - targetFov),
            Mathf.Abs(sprintFovIncrease),
            1f);
        float maxDelta = fovSpan / duration;
        currentFov = Mathf.MoveTowards(currentFov, targetFov, maxDelta * Time.deltaTime);
        playerCamera.fieldOfView = currentFov;
    }
}
