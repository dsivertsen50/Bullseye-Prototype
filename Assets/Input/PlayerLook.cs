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
    [SerializeField, Range(0.1f, 1f)] private float aimingSensitivityMultiplier = 0.4f;

    private float yaw;
    private float pitch;
    private Rigidbody rb;
    private PlayerHealth playerHealth;
    private PlayerAimZoom playerAimZoom;
    private WeaponPresentationController weaponPresentation;

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
            return;

        if (LocalPlayerMenuState.IsOpen(this))
            return;

        Vector2 input = lookAction.action.ReadValue<Vector2>();
        float sensitivityMultiplier = IsAiming() ? aimingSensitivityMultiplier : 1f;
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
        ApplyRotation();
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
        aimingSensitivityMultiplier = PlayerGameSettings.AimSensitivityMultiplier;
    }
}
