using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Local-only orbit camera for the eliminated owner. Remote clients never
/// run this view; their first-person cameras stay disabled.
/// </summary>
[DefaultExecutionOrder(250)]
public class EliminationCamera : MonoBehaviour
{
    private const string LocalPlayerBodyLayerName = "LocalPlayerBody";

    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform focus;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private float cameraDistance = 3.4f;
    [SerializeField] private float minDistance = 1.4f;
    [SerializeField] private float maxDistance = 5.5f;
    [SerializeField] private float sensitivity = 1f;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 55f;
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private float collisionRadius = 0.18f;
    [SerializeField] private float focusHeight = 1.15f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private PlayerHealth playerHealth;
    private PlayerLook playerLook;
    private PlayerNetworkSetup networkSetup;
    private bool active;
    private bool restoredCulling;
    private float yaw;
    private float pitch;
    private float distance;
    private float transition;
    private Vector3 startWorldPosition;
    private Quaternion startWorldRotation;
    private Vector3 cameraLocalPosition;
    private Quaternion cameraLocalRotation;
    private int cachedCullingMask;
    private bool cachedCulling;

    public bool IsActive => active;

    public void Configure(
        Camera camera,
        Transform focusPoint,
        InputActionReference look,
        float distanceValue,
        float minDistanceValue,
        float maxDistanceValue,
        float sensitivityValue,
        float minPitchValue,
        float maxPitchValue,
        float transitionDurationValue)
    {
        if (camera != null)
            playerCamera = camera;
        if (focusPoint != null)
            focus = focusPoint;
        if (look != null)
            lookAction = look;
        cameraDistance = distanceValue;
        minDistance = minDistanceValue;
        maxDistance = maxDistanceValue;
        sensitivity = sensitivityValue;
        minPitch = minPitchValue;
        maxPitch = maxPitchValue;
        transitionDuration = transitionDurationValue;
    }

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerLook = GetComponent<PlayerLook>();
        networkSetup = GetComponent<PlayerNetworkSetup>();
        ResolveCamera();
    }

    public void Begin()
    {
        if (active)
            return;
        if (playerHealth != null && playerHealth.IsSpawned && !playerHealth.IsOwner)
            return;

        ResolveCamera();
        if (playerCamera == null)
            return;

        active = true;
        restoredCulling = false;
        transition = 0f;
        distance = Mathf.Clamp(cameraDistance, MinDistance, MaxDistance);
        yaw = transform.eulerAngles.y + 160f;
        pitch = 12f;
        startWorldPosition = playerCamera.transform.position;
        startWorldRotation = playerCamera.transform.rotation;
        cameraLocalPosition = playerCamera.transform.localPosition;
        cameraLocalRotation = playerCamera.transform.localRotation;
        cachedCullingMask = playerCamera.cullingMask;
        cachedCulling = true;
        ShowLocalBody(true);
    }

    public void Restore()
    {
        if (!active && !cachedCulling)
            return;

        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = cameraLocalPosition;
            playerCamera.transform.localRotation = cameraLocalRotation;
            if (cachedCulling)
                playerCamera.cullingMask = cachedCullingMask;
        }

        ShowLocalBody(false);
        active = false;
        cachedCulling = false;
        restoredCulling = true;
    }

    private void LateUpdate()
    {
        if (!active || playerCamera == null)
            return;
        if (playerHealth != null && playerHealth.IsSpawned && !playerHealth.IsOwner)
        {
            Restore();
            return;
        }

        TickLook();
        transition += Time.deltaTime;
        float blend = TransitionBlend;
        Vector3 focusPoint = ResolveFocusPoint();
        Vector3 desired = ResolveDesiredPosition(focusPoint);
        desired = ResolveCollision(focusPoint, desired);

        Vector3 position = Vector3.Lerp(startWorldPosition, desired, blend);
        Quaternion rotation = Quaternion.Slerp(
            startWorldRotation,
            Quaternion.LookRotation(focusPoint - position, Vector3.up),
            blend);

        playerCamera.transform.SetPositionAndRotation(position, rotation);
    }

    private void TickLook()
    {
        if (LocalPlayerMenuState.IsOpen(this))
            return;

        Vector2 input = ReadLookInput();
        if (input.sqrMagnitude < 0.000001f)
            return;

        float invert = PlayerGameSettings.InvertY ? -1f : 1f;
        float scale = Mathf.Max(0.05f, sensitivity);
        float yawDelta;
        float pitchDelta;
        if (IsMouseLook())
        {
            yawDelta = input.x * 4f * scale * Time.fixedDeltaTime;
            pitchDelta = input.y * 4f * scale * Time.fixedDeltaTime;
        }
        else
        {
            yawDelta = input.x * 35f * scale * Time.deltaTime;
            pitchDelta = input.y * 35f * scale * Time.deltaTime;
        }

        yaw += yawDelta;
        pitch = Mathf.Clamp(pitch - pitchDelta * invert, minPitch, maxPitch);
    }

    private Vector3 ResolveDesiredPosition(Vector3 focusPoint)
    {
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        return focusPoint - orbit * Vector3.forward * distance;
    }

    private Vector3 ResolveCollision(Vector3 focusPoint, Vector3 desired)
    {
        Vector3 toCamera = desired - focusPoint;
        float desiredDistance = toCamera.magnitude;
        if (desiredDistance < 0.01f)
            return desired;

        Vector3 direction = toCamera / desiredDistance;
        float radius = Mathf.Max(0.05f, collisionRadius);
        int mask = collisionMask.value == 0 ? ~0 : collisionMask;
        float max = Mathf.Min(desiredDistance, MaxDistance);
        if (Physics.SphereCast(
                focusPoint,
                radius,
                direction,
                out RaycastHit hit,
                max,
                mask,
                QueryTriggerInteraction.Ignore)
            && !IsOwnCollider(hit.collider))
        {
            float safe = Mathf.Max(MinDistance * 0.35f, hit.distance - radius);
            return focusPoint + direction * safe;
        }

        return desired;
    }

    private Vector3 ResolveFocusPoint()
    {
        if (focus != null)
            return focus.position + Vector3.up * focusHeight;
        return transform.position + Vector3.up * focusHeight;
    }

    private Vector2 ReadLookInput()
    {
        if (playerLook != null)
            return playerLook.ReadRawLookInput();
        if (lookAction == null || lookAction.action == null)
            return Vector2.zero;
        return lookAction.action.ReadValue<Vector2>();
    }

    private bool IsMouseLook()
    {
        if (playerLook != null)
            return playerLook.IsLookFromMouse;
        return lookAction != null && lookAction.action != null && lookAction.action.activeControl?.device is Mouse;
    }

    private void ShowLocalBody(bool visible)
    {
        if (networkSetup != null)
        {
            networkSetup.SetLocalBodyVisibleToCamera(visible);
            return;
        }

        if (playerCamera == null)
            return;

        int layer = LayerMask.NameToLayer(LocalPlayerBodyLayerName);
        if (layer < 0)
            return;

        if (visible)
            playerCamera.cullingMask |= 1 << layer;
        else
            playerCamera.cullingMask &= ~(1 << layer);
    }

    private void ResolveCamera()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (focus == null)
        {
            Transform visual = transform.Find("VisualRoot");
            focus = visual != null ? visual : transform;
        }
    }

    private bool IsOwnCollider(Collider collider)
    {
        return collider != null && collider.transform.IsChildOf(transform);
    }

    private float TransitionBlend
    {
        get
        {
            float duration = Mathf.Max(0.01f, transitionDuration);
            float t = Mathf.Clamp01(transition / duration);
            return t * t * (3f - 2f * t);
        }
    }

    private float MinDistance => Mathf.Max(0.35f, Mathf.Min(minDistance, maxDistance));
    private float MaxDistance => Mathf.Max(MinDistance, maxDistance);

    private void OnDisable()
    {
        if (active && !restoredCulling)
            Restore();
    }
}
