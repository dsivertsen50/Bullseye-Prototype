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
    private const float VariableZoomFollowDuration = 0.08f;

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
    private bool wasAiming;
    private float runtimeMagnification = 1f;
    private WeaponDefinition lastZoomDefinition;
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
            if (definition == null)
                return 1f;
            if (definition.UsesVariableAdsMagnification)
                return Mathf.Clamp(
                    runtimeMagnification,
                    definition.MinAdsMagnification,
                    definition.MaxAdsMagnification);
            return definition.AdsMagnification;
        }
    }

    public bool UsesMagnifiedAds
    {
        get
        {
            WeaponDefinition definition = ActiveDefinition;
            if (definition == null || !definition.UsesMagnifiedAds)
                return false;
            return CurrentMagnification > 1.0001f || definition.AdsMagnification > 1.0001f;
        }
    }

    public bool LocksHorizontalLocomotion =>
        IsAiming && ActiveDefinition != null && ActiveDefinition.LocksHorizontalLocomotionWhileAds;

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
    /// Final ADS look scale: player ADS setting × weapon ADS multiplier,
    /// optionally reduced further as optical magnification increases.
    /// Applied after mouse / gamepad base sensitivity.
    /// </summary>
    public float CurrentAdsLookScale
    {
        get
        {
            float globalAds = Mathf.Clamp(PlayerGameSettings.AimSensitivityMultiplier, 0.1f, 1.5f);
            WeaponDefinition definition = ActiveDefinition;
            float weaponAds = definition != null ? definition.AdsSensitivityMultiplier : 0.4f;
            float scale = globalAds * weaponAds;
            if (definition != null &&
                definition.AdsSensitivityScalesWithMagnification &&
                definition.UsesMagnifiedAds)
            {
                float reference = definition.UsesVariableAdsMagnification
                    ? definition.DefaultAdsMagnification
                    : Mathf.Max(1f, definition.AdsMagnification);
                float current = Mathf.Max(1f, CurrentMagnification);
                if (reference > 0.0001f && current > reference + 0.0001f)
                    scale *= reference / current;
            }

            return scale;
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
        runtimeMagnification = ResolveDefaultMagnification(ActiveDefinition);
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
            ResetRuntimeZoomIfNeeded();
            SnapFov(defaultFov);
            wasAiming = false;
            return;
        }

        if (LocalPlayerMenuState.IsOpen(this))
        {
            ClearAimState();
            ResetRuntimeZoomIfNeeded();
            ApplyFov(defaultFov, CurrentAdsExitDuration);
            wasAiming = false;
            return;
        }

        bool sprinting = playerMovement != null && playerMovement.IsSprinting;
        bool wallRunning = playerMovement != null && playerMovement.IsWallRunning;
        bool diving = playerMovement != null && playerMovement.IsDolphinDiving;
        bool airborne = IsUnsafeAirborneForScopedLock();
        WeaponDefinition definition = ActiveDefinition;
        SyncRuntimeZoomDefinition(definition);

        bool wantAim = false;
        if (wallRunning || diving)
        {
            ClearAimState();
        }
        else if (airborne &&
                 definition != null &&
                 definition.LocksHorizontalLocomotionWhileAds &&
                 definition.ExitAdsWhenAirborne)
        {
            ClearAimState();
        }
        else if (sprinting && (definition == null || !definition.LocksHorizontalLocomotionWhileAds))
        {
            ClearAimState();
        }
        else
        {
            wantAim = ReadAimInput();
            if (wantAim && definition != null && definition.LocksHorizontalLocomotionWhileAds && sprinting)
                playerMovement.CancelSprintForScopedAim();
            IsAiming = wantAim;
        }

        bool previouslyAiming = wasAiming;
        TickVariableZoom(definition);

        float targetFov = ResolveTargetFov(sprinting && !LocksHorizontalLocomotion);
        bool zoomingIn = targetFov < currentFov - 0.01f;
        float duration = ResolveFovDuration(zoomingIn, previouslyAiming);
        ApplyFov(targetFov, duration);
        wasAiming = IsAiming;
    }

    private void SyncRuntimeZoomDefinition(WeaponDefinition definition)
    {
        if (definition == lastZoomDefinition)
            return;

        lastZoomDefinition = definition;
        runtimeMagnification = ResolveDefaultMagnification(definition);
        wasAiming = false;
    }

    private void TickVariableZoom(WeaponDefinition definition)
    {
        if (definition == null || !definition.UsesVariableAdsMagnification)
            return;

        if (IsAiming)
        {
            if (!wasAiming)
                runtimeMagnification = definition.DefaultAdsMagnification;
            else
                ApplyZoomInput(definition);
            return;
        }

        if (wasAiming && !definition.PreserveAdsMagnificationOnExit)
            runtimeMagnification = definition.DefaultAdsMagnification;
    }

    private void ApplyZoomInput(WeaponDefinition definition)
    {
        float axis = playerMovement != null ? playerMovement.MoveInput.y : 0f;
        float deadzone = definition.AdsZoomInputDeadzone;
        if (Mathf.Abs(axis) <= deadzone)
            return;

        float magnitude = deadzone >= 0.999f
            ? 1f
            : Mathf.InverseLerp(deadzone, 1f, Mathf.Abs(axis));
        float delta = Mathf.Sign(axis) * magnitude * definition.AdsZoomAdjustmentSpeed * Time.deltaTime;
        runtimeMagnification = Mathf.Clamp(
            runtimeMagnification + delta,
            definition.MinAdsMagnification,
            definition.MaxAdsMagnification);
    }

    private void ResetRuntimeZoomIfNeeded()
    {
        WeaponDefinition definition = ActiveDefinition;
        if (definition == null || definition.PreserveAdsMagnificationOnExit)
            return;
        runtimeMagnification = definition.DefaultAdsMagnification;
    }

    private bool IsUnsafeAirborneForScopedLock()
    {
        if (playerMovement == null || playerMovement.Grounded)
            return false;
        if (playerMovement.IsProne || playerMovement.IsCrouched)
            return false;
        return Mathf.Abs(playerMovement.VerticalVelocity) > 0.75f;
    }

    private static float ResolveDefaultMagnification(WeaponDefinition definition)
    {
        if (definition == null)
            return 1f;
        if (definition.UsesVariableAdsMagnification)
            return definition.DefaultAdsMagnification;
        return definition.AdsMagnification;
    }

    private float ResolveFovDuration(bool zoomingIn, bool previouslyAiming)
    {
        if (IsAiming && previouslyAiming && ActiveDefinition != null && ActiveDefinition.UsesVariableAdsMagnification)
            return VariableZoomFollowDuration;
        return zoomingIn ? CurrentAdsEnterDuration : CurrentAdsExitDuration;
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
