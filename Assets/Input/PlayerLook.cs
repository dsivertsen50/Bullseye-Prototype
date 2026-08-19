using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private InputActionReference lookAction;

    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float gamepadSensitivity = 150f;

    private float pitch;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        // Enable only. Non-owner players disable this component on spawn;
        // disabling the shared action there would also stop the local owner.
        lookAction.action.Enable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return;

        Vector2 input = lookAction.action.ReadValue<Vector2>();

        float yaw;
        float pitchChange;

        if (lookAction.action.activeControl?.device is Mouse)
        {
            yaw = input.x * mouseSensitivity;
            pitchChange = input.y * mouseSensitivity;
        }
        else
        {
            yaw = input.x * gamepadSensitivity * Time.deltaTime;
            pitchChange = input.y * gamepadSensitivity * Time.deltaTime;
        }

        transform.Rotate(Vector3.up * yaw);

        pitch -= pitchChange;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}