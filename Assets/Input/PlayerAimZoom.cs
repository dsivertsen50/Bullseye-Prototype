using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAimZoom : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference aimAction;

    [SerializeField] [Range(0f, 0.9f)] private float fovReduction = 0.25f;
    [SerializeField] private float zoomTransitionDuration = 0.12f;
    [SerializeField] private InputActivationMode aimActivation = InputActivationMode.Toggle;

    private float defaultFov;
    private float currentFov;
    private bool aimToggledOn;

    public float FovReduction
    {
        get => fovReduction;
        set => fovReduction = Mathf.Clamp(value, 0f, 0.9f);
    }

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

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

        bool aiming = ReadAimInput();
        float targetFov = aiming
            ? defaultFov * (1f - fovReduction)
            : defaultFov;

        float duration = Mathf.Max(0.0001f, zoomTransitionDuration);
        float maxDelta = Mathf.Abs(defaultFov - defaultFov * (1f - fovReduction)) / duration;
        currentFov = Mathf.MoveTowards(currentFov, targetFov, maxDelta * Time.deltaTime);
        playerCamera.fieldOfView = currentFov;
    }

    private bool ReadAimInput()
    {
        if (aimActivation == InputActivationMode.Hold)
            return aimAction.action.IsPressed();

        if (aimAction.action.WasPressedThisFrame())
            aimToggledOn = !aimToggledOn;

        return aimToggledOn;
    }
}
