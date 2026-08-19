using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float sprintSpeedMultiplier = 2.25f;
    [SerializeField] private float maxSprintDuration = 7.5f;
    [SerializeField] private InputActivationMode sprintActivation = InputActivationMode.Hold;
    [SerializeField] private float crouchSpeedMultiplier = 0.6f;
    [SerializeField] private float crouchedHeight = 1.2f;
    [SerializeField] private float crouchedCameraLocalY = 0.76f;
    [SerializeField] private InputActivationMode crouchActivation = InputActivationMode.Toggle;
    [SerializeField] private float crouchTransitionDuration = 0.4f;

    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference crouchAction;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform bodyVisual;

    private readonly NetworkVariable<bool> crouched = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly RaycastHit[] standHits = new RaycastHit[8];

    private CharacterController controller;
    private BullseyeMover bullseyeMover;
    private PlayerHealth playerHealth;
    private float verticalVelocity;
    private float sprintHoldTime;
    private bool sprintExhausted;
    private bool sprintToggledOn;
    private float standingHeight;
    private Vector3 standingCenter;
    private Vector3 standingBodyPosition;
    private Vector3 standingBodyScale;
    private float standingCameraLocalY;
    private float stanceBlend;
    private bool crouchToggledOn;

    private InputAction resolvedCrouchAction;

    public bool IsCrouched => crouched.Value;

    private InputAction CrouchInput
    {
        get
        {
            if (crouchAction != null && crouchAction.action != null)
                return crouchAction.action;

            if (resolvedCrouchAction != null)
                return resolvedCrouchAction;

            if (moveAction != null && moveAction.action != null)
                resolvedCrouchAction = moveAction.action.actionMap.FindAction("Crouch");

            return resolvedCrouchAction;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        bullseyeMover = GetComponent<BullseyeMover>();
        playerHealth = GetComponent<PlayerHealth>();

        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                playerCamera = cam.transform;
        }

        if (bodyVisual == null)
        {
            CapsuleCollider capsule = GetComponentInChildren<CapsuleCollider>();
            if (capsule != null)
                bodyVisual = capsule.transform;
        }

        CacheStandingPose();
    }

    public override void OnNetworkSpawn()
    {
        crouched.OnValueChanged += OnCrouchedChanged;
        crouchToggledOn = crouched.Value;
        stanceBlend = crouched.Value ? 1f : 0f;
        ApplyStanceBlend(stanceBlend);
    }

    public override void OnNetworkDespawn()
    {
        crouched.OnValueChanged -= OnCrouchedChanged;
    }

    private void OnEnable()
    {
        // Enable only. Player instances share these actions in-process.
        moveAction.action.Enable();
        jumpAction.action.Enable();
        sprintAction.action.Enable();

        if (crouchAction != null)
            crouchAction.action.Enable();
        else if (CrouchInput != null)
            CrouchInput.Enable();
    }

    private void Update()
    {
        if (!IsSpawned || IsOwner)
        {
            if (playerHealth == null || !playerHealth.IsDead)
            {
                UpdateCrouchState();
                MoveOwner();
            }
        }

        TickStanceTransition();
    }

    public void ResetAfterRespawn()
    {
        sprintHoldTime = 0f;
        sprintExhausted = false;
        sprintToggledOn = false;
        verticalVelocity = -2f;

        crouchToggledOn = false;

        if (IsSpawned && IsOwner && crouched.Value)
            crouched.Value = false;

        stanceBlend = 0f;
        ApplyStanceBlend(0f);
    }

    private void MoveOwner()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();

        Vector3 horizontalMove =
            transform.right * input.x +
            transform.forward * input.y;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (controller.isGrounded && jumpAction.action.WasPressedThisFrame())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            if (bullseyeMover != null)
                bullseyeMover.NotifyJump();
        }

        verticalVelocity += gravity * Time.deltaTime;

        float speed = moveSpeed * GetMoveMultiplier();
        Vector3 movement = horizontalMove * speed;
        movement.y = verticalVelocity;

        controller.Move(movement * Time.deltaTime);
    }

    private void UpdateCrouchState()
    {
        InputAction crouchInput = CrouchInput;
        if (crouchInput == null)
            return;

        bool wantsCrouch = ReadCrouchInput();
        bool currentlyCrouched = crouched.Value;

        if (wantsCrouch == currentlyCrouched)
            return;

        if (wantsCrouch)
        {
            SetCrouched(true);
            return;
        }

        if (CanStand())
            SetCrouched(false);
        else
            crouchToggledOn = true;
    }

    private bool ReadCrouchInput()
    {
        InputAction crouchInput = CrouchInput;
        if (crouchActivation == InputActivationMode.Hold)
        {
            crouchToggledOn = crouchInput.IsPressed();
            return crouchToggledOn;
        }

        if (crouchInput.WasPressedThisFrame())
            crouchToggledOn = !crouchToggledOn;

        return crouchToggledOn;
    }

    private void SetCrouched(bool value)
    {
        if (crouched.Value == value)
            return;

        crouched.Value = value;
        crouchToggledOn = value;

        if (bullseyeMover != null)
            bullseyeMover.NotifyCrouchChanged();
    }

    private void OnCrouchedChanged(bool previous, bool next)
    {
        crouchToggledOn = next;
    }

    private void TickStanceTransition()
    {
        float target = crouched.Value ? 1f : 0f;
        float duration = Mathf.Max(0.0001f, crouchTransitionDuration);
        stanceBlend = Mathf.MoveTowards(stanceBlend, target, Time.deltaTime / duration);
        ApplyStanceBlend(stanceBlend);
    }

    private void ApplyStanceBlend(float blend)
    {
        float crouchedBodyHeight = controller != null
            ? Mathf.Max(controller.radius * 2f, crouchedHeight)
            : crouchedHeight;
        float height = Mathf.Lerp(standingHeight, crouchedBodyHeight, blend);

        if (controller != null)
        {
            controller.height = height;
            Vector3 center = standingCenter;
            center.y = height * 0.5f;
            controller.center = center;
        }

        if (bodyVisual != null)
        {
            float ratio = standingHeight > 0.0001f ? height / standingHeight : 1f;
            Vector3 scale = standingBodyScale;
            scale.y = standingBodyScale.y * ratio;
            bodyVisual.localScale = scale;
            bodyVisual.localPosition = standingBodyPosition +
                Vector3.up * (standingBodyScale.y * (ratio - 1f));
        }

        if (playerCamera != null)
        {
            Vector3 cameraPosition = playerCamera.localPosition;
            cameraPosition.y = Mathf.Lerp(standingCameraLocalY, crouchedCameraLocalY, blend);
            playerCamera.localPosition = cameraPosition;
        }
    }

    private bool CanStand()
    {
        if (controller == null)
            return true;

        float targetHeight = standingHeight;
        float currentHeight = controller.height;
        float rise = targetHeight - currentHeight;
        if (rise <= 0.001f)
            return true;

        float radius = Mathf.Max(0.01f, controller.radius - controller.skinWidth);
        Vector3 origin = transform.position + transform.up * (currentHeight * 0.5f);
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            radius,
            transform.up,
            standHits,
            rise,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = standHits[i].transform;
            if (hitTransform == null)
                continue;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            return false;
        }

        return true;
    }

    private float GetMoveMultiplier()
    {
        if (crouched.Value)
            return crouchSpeedMultiplier;

        return GetSprintMultiplier();
    }

    private float GetSprintMultiplier()
    {
        bool wantsSprint = ReadSprintInput();

        if (!wantsSprint)
        {
            sprintHoldTime = 0f;
            sprintExhausted = false;
            sprintToggledOn = false;
            return 1f;
        }

        if (sprintExhausted)
        {
            sprintToggledOn = false;
            return 1f;
        }

        sprintHoldTime += Time.deltaTime;
        if (sprintHoldTime >= maxSprintDuration)
        {
            sprintExhausted = true;
            sprintToggledOn = false;
            return 1f;
        }

        return sprintSpeedMultiplier;
    }

    private bool ReadSprintInput()
    {
        if (sprintActivation == InputActivationMode.Hold)
            return sprintAction.action.IsPressed();

        if (sprintAction.action.WasPressedThisFrame())
            sprintToggledOn = !sprintToggledOn;

        return sprintToggledOn;
    }

    private void CacheStandingPose()
    {
        if (controller != null)
        {
            standingHeight = controller.height;
            standingCenter = controller.center;
        }

        if (bodyVisual != null)
        {
            standingBodyPosition = bodyVisual.localPosition;
            standingBodyScale = bodyVisual.localScale;
        }

        if (playerCamera != null)
            standingCameraLocalY = playerCamera.localPosition.y;
    }
}

public enum InputActivationMode
{
    Toggle,
    Hold
}
