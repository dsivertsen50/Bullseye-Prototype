using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private InputActionReference lookAction;

    [SerializeField] private float sensitivityX = 4f;
    [SerializeField] private float sensitivityY = 4f;
    [SerializeField] private float controllerSensitivityX = 35f;
    [SerializeField] private float controllerSensitivityY = 35f;
    [SerializeField, Range(20f, 89.7f)] private float maxCameraAngle = 89.7f;
    [SerializeField, Range(0.1f, 1f)]
    [Tooltip("Fallback ADS look scale if no weapon is equipped. Equipped weapons use WeaponDefinition ADS Sensitivity instead.")]
    private float aimingSensitivityMultiplier = 0.4f;

    private float yaw;
    private float pitch;
    private float currentSensitivityMultiplier = 1f;
    private Rigidbody rb;
    private PlayerHealth playerHealth;
    private PlayerAimZoom playerAimZoom;
    private WeaponPresentationController weaponPresentation;
    private PlayerWeaponInventory inventory;

    public float Yaw => yaw;
    public float Pitch => pitch;

    public void SetLookTransform(Transform lookTransform)
    {
        playerCamera = lookTransform;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerHealth = GetComponent<PlayerHealth>();
        playerAimZoom = GetComponent<PlayerAimZoom>();
        weaponPresentation = GetComponent<WeaponPresentationController>();
        inventory = GetComponent<PlayerWeaponInventory>();
        yaw = transform.eulerAngles.y;
        ApplyPersistedSettings();
    }

    private void OnEnable()
    {
        lookAction.action.Enable();
        PlayerGameSettings.Changed += ApplyPersistedSettings;

        if (weaponPresentation != null)
            weaponPresentation.RecoilRequested += ApplyRecoil;
    }

    private void OnDisable()
    {
        PlayerGameSettings.Changed -= ApplyPersistedSettings;

        if (weaponPresentation != null)
            weaponPresentation.RecoilRequested -= ApplyRecoil;
    }

    private void Start()
    {
        if (!LocalPlayerMenuState.IsOpen(this))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            currentSensitivityMultiplier = 1f;
            return;
        }

        if (LocalPlayerMenuState.IsOpen(this))
        {
            currentSensitivityMultiplier = 1f;
            return;
        }

        Vector2 input = lookAction.action.ReadValue<Vector2>();
        float sensitivityMultiplier = TickAimSensitivity();
        float invert = PlayerGameSettings.InvertY ? -1f : 1f;

        float yawDelta;
        float pitchDelta;

        if (lookAction.action.activeControl?.device is Mouse)
        {
            yawDelta = input.x * sensitivityX * Time.fixedDeltaTime;
            pitchDelta = input.y * sensitivityY * Time.fixedDeltaTime;
        }
        else
        {
            yawDelta = input.x * controllerSensitivityX * Time.deltaTime;
            pitchDelta = input.y * controllerSensitivityY * Time.deltaTime;
        }

        yaw += yawDelta * sensitivityMultiplier;
        pitch -= pitchDelta * sensitivityMultiplier * invert;
        pitch = Mathf.Clamp(pitch, -maxCameraAngle, maxCameraAngle);

        ApplyRotation();
    }

    public void ResetAfterRespawn()
    {
        yaw = 0f;
        pitch = 0f;
        currentSensitivityMultiplier = 1f;
        ApplyRotation();
    }

    private float TickAimSensitivity()
    {
        float aimedScale = GetAimedLookScale();
        float target = IsAiming() ? aimedScale : 1f;
        float duration = playerAimZoom != null
            ? playerAimZoom.CurrentAdsTransitionDuration
            : 0.15f;
        float span = Mathf.Max(0.0001f, Mathf.Abs(1f - aimedScale));
        currentSensitivityMultiplier = Mathf.MoveTowards(
            currentSensitivityMultiplier,
            target,
            (span / duration) * Time.deltaTime);
        return currentSensitivityMultiplier;
    }

    private float GetAimedLookScale()
    {
        if (playerAimZoom != null)
            return playerAimZoom.CurrentAdsLookScale;

        float globalAds = Mathf.Clamp(PlayerGameSettings.AimSensitivityMultiplier, 0.1f, 1.5f);
        float weaponAds = inventory != null && inventory.ActiveDefinition != null
            ? inventory.ActiveDefinition.AdsSensitivityMultiplier
            : aimingSensitivityMultiplier;
        return globalAds * weaponAds;
    }

    private void ApplyRecoil(float recoilPitch, float recoilYaw)
    {
        pitch -= recoilPitch;
        yaw += recoilYaw;
        pitch = Mathf.Clamp(pitch, -maxCameraAngle, maxCameraAngle);
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = yawRotation;

        if (rb != null && !rb.isKinematic)
            rb.MoveRotation(yawRotation);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private bool IsAiming()
    {
        if (playerAimZoom != null)
            return playerAimZoom.IsAiming;
        if (weaponPresentation != null)
            return weaponPresentation.IsAiming;
        return false;
    }

    private void ApplyPersistedSettings()
    {
        sensitivityX = PlayerGameSettings.MouseSensitivityX;
        sensitivityY = PlayerGameSettings.MouseSensitivityY;
        controllerSensitivityX = PlayerGameSettings.ControllerSensitivityX;
        controllerSensitivityY = PlayerGameSettings.ControllerSensitivityY;
    }
}
