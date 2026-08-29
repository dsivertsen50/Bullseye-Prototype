using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// Owner-driven Rigidbody FPS locomotion adapted from Cowsins FPS Engine
/// movement behaviour, without Cowsins InputManager, PlayerDependencies, or
/// the single-player player stack. NGO ownership and bullseye influence stay
/// on Bullseye systems.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMovement : NetworkBehaviour
{
    private const float ExtraGravityForce = 30.19f;
    private const float ExtraGravityMultiplier = 10f;
    private const float FrictionThreshold = 0.1f;
    private const float MaxSlopeAngle = 60f;
    private const float JumpIgnoreDuration = 0.2f;
    private const float UngroundDelay = 0.1f;
    private const float SlideAirborneTolerance = 0.25f;

    [Header("Speed")]
        [SerializeField] private float walkSpeed = 6.25f;
        [SerializeField] private float runSpeed = 12.5f;
    [SerializeField] private float crouchSpeed = 3f;
    [SerializeField] private float acceleration = 4500f;
    [SerializeField] private float maxSpeedAllowed = 20f;
    [SerializeField, Range(0f, 1f)] private float controlAirborne = 0.5f;
    [SerializeField, Range(0f, 1f)] private float controlsResponsiveness = 0.4f;

    [Header("Sprint")]
    [SerializeField] private InputActivationMode sprintActivation = InputActivationMode.Hold;
    [SerializeField] private bool canRunBackwards;
    [SerializeField] private bool canRunSideways = true;
    [SerializeField] private bool canRunWhileShooting;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 20f;
    [SerializeField] private float jumpCooldown = 0.25f;
    [SerializeField] private bool canCoyote;
    [SerializeField, Range(0f, 0.3f)] private float coyoteJumpTime = 0.1f;
    [SerializeField] private bool canJumpWhileCrouching = true;

    [Header("Crouch")]
    [SerializeField] private float crouchedHeight = 1.2f;
    [SerializeField] private float crouchedCameraLocalY = 0.76f;
    [SerializeField] private InputActivationMode crouchActivation = InputActivationMode.Toggle;
    [SerializeField] private float crouchTransitionDuration = 0.4f;
    [SerializeField] private float roofCheckDistance = 1.2f;

    [Header("Prone")]
    [SerializeField] private float proneHoldDuration = 0.6f;
    [SerializeField] private float proneMoveSpeed = 2.2f;
    [SerializeField] private float proneControllerHeight = 0.6f;
    [SerializeField] private Vector3 proneControllerCenter = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private float proneControllerRadius = 0.28f;
    [SerializeField] private float proneCameraLocalY = 0.32f;
    [SerializeField] private float proneTransitionSpeed = 8f;
    [SerializeField] private float proneCameraTransitionSpeed = 8f;
    [SerializeField] private bool applyPlaceholderPronePose;
    [SerializeField] private Vector3 proneVisualEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 proneVisualLocalOffset;

    [Header("Dolphin Dive")]
    [SerializeField] private float dolphinDiveHoldDuration = 0.3f;
    [SerializeField] private float minimumDolphinDiveSpeed = 8f;
    [SerializeField] private float dolphinDiveForwardForce = 3f;
    [SerializeField] private float dolphinDiveUpwardForce = 2f;
    [SerializeField] private float dolphinDiveDuration = 0.45f;
    [SerializeField] private float dolphinDiveMaxSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float dolphinDiveAirControl = 0.1f;
    [SerializeField] private float dolphinDiveRecoveryDuration = 0.3f;
    [SerializeField] private float dolphinDiveCooldown = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugMovementLogs;

    [Header("Slide")]
    [SerializeField] private bool allowSliding = true;
    [SerializeField] private float slideForce = 300f;
    [SerializeField] private float slideDuration = 1f;
    [SerializeField] private float slideStopSpeed = 1.5f;
    [SerializeField, Range(0f, 1f)] private float slideSteerMultiplier = 0.35f;
    [SerializeField] private float slideBoostDuration = 0.12f;
    [SerializeField] private bool allowMoveWhileSliding;
    [SerializeField] private bool applyFrictionForceOnSliding = true;
    [SerializeField, Range(0f, 1f)] private float slideFrictionForceAmount = 0.05f;

    [Header("Ground")]
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private LayerMask whatIsGround = ~0;

    [Header("Input")]
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

    private readonly NetworkVariable<bool> prone = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly NetworkVariable<bool> dolphinDiving = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private readonly RaycastHit[] standHits = new RaycastHit[8];
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    private Rigidbody rb;
    private CapsuleCollider playerCapsule;
    private CharacterController legacyController;
    private BullseyeMover bullseyeMover;
    private PlayerHealth playerHealth;
    private PlayerWeaponInventory weaponInventory;
    private InputAction resolvedCrouchAction;

    private float currentSpeed;
    private bool grounded;
    private bool hasJumped;
    private bool sprintToggledOn;
    private bool crouchToggledOn;
    private bool jumpAvailable = true;
    private float jumpCooldownTimer;
    private float jumpIgnoreTimer;
    private float coyoteTimer;
    private float ungroundTimer;
    private bool cancellingGrounded;
    private RaycastHit slopeHit;
    private bool onSlope;

    private float standingHeight;
    private Vector3 standingCenter;
    private float standingRadius = 0.5f;
    private Vector3 standingBodyPosition;
    private Quaternion standingBodyRotation = Quaternion.identity;
    private Vector3 standingBodyScale;
    private float standingCameraLocalY;
    private float stanceBlend;
    private float currentCameraLocalY;

    private bool crouchHoldActive;
    private bool crouchPressStartedCrouched;
    private bool crouchPressExitedProne;
    private bool diveCandidateThisPress;
    private float crouchHoldTime;
    private float diveElapsed;
    private float diveCooldownRemaining;
    private float diveRecoveryRemaining;
    private bool diveBecameAirborne;
    private Vector3 diveDirection = Vector3.forward;

    private bool sliding;
    private Vector3 slideDirection;
    private float slideTimer;
    private float slideBoostRemaining;
    private bool slideBoosting;
    private float slideAirborneTimer;
    private float knockbackTimer;

    public event Action<float> Landed;
    public event Action Jumped;
    public bool LastJumpFromSprint { get; private set; }
    public event Action DolphinDiveStarted;
    public event Action DolphinDiveLanded;

    public bool IsCrouched => crouched.Value && !prone.Value && !dolphinDiving.Value;
    public bool IsProne => prone.Value;
    public bool IsDolphinDiving => dolphinDiving.Value;
    public bool IsDiveRecovering => diveRecoveryRemaining > 0f;
    public bool BlocksCombat => dolphinDiving.Value || diveRecoveryRemaining > 0f;
    public Transform BodyVisual => bodyVisual;
    public bool Grounded => grounded;
    public bool IsSliding => sliding;
    public bool IsSprinting { get; private set; }
    public bool CanRunWhileShooting => canRunWhileShooting;
    public float CurrentSpeed => currentSpeed;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float ProneMoveSpeed => proneMoveSpeed;
    public float HorizontalSpeed => HorizontalVelocity().magnitude;
    public Vector2 MoveInput => ReadMoveInput();

    public void SetCameraTransform(Transform cameraTransform)
    {
        playerCamera = cameraTransform;
        if (playerCamera != null)
        {
            standingCameraLocalY = playerCamera.localPosition.y;
            currentCameraLocalY = standingCameraLocalY;
        }
    }

    private bool IsMovementOwner => !IsSpawned || IsOwner;

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
        rb = GetComponent<Rigidbody>();
        playerCapsule = GetComponent<CapsuleCollider>();
        legacyController = GetComponent<CharacterController>();
        bullseyeMover = GetComponent<BullseyeMover>();
        playerHealth = GetComponent<PlayerHealth>();
        weaponInventory = GetComponent<PlayerWeaponInventory>();

        if (legacyController != null)
            legacyController.enabled = false;

        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                playerCamera = cam.transform;
        }

        if (bodyVisual == null)
        {
            bodyVisual = transform.Find("VisualRoot");
            if (bodyVisual == null)
            {
                CapsuleCollider[] capsules = GetComponentsInChildren<CapsuleCollider>();
                for (int i = 0; i < capsules.Length; i++)
                {
                    if (capsules[i] != playerCapsule)
                    {
                        bodyVisual = capsules[i].transform;
                        break;
                    }
                }
            }
        }

        ConfigureRigidbody();
        IgnoreInternalCollisions();
        CacheStandingPose();
        currentSpeed = walkSpeed;
    }

    public override void OnNetworkSpawn()
    {
        crouched.OnValueChanged += OnCrouchedChanged;
        crouchToggledOn = crouched.Value;
        stanceBlend = ResolveStanceTarget();
        ApplyStancePose(1f);
        ConfigureAuthorityPhysics();
    }

    public override void OnNetworkDespawn()
    {
        crouched.OnValueChanged -= OnCrouchedChanged;
    }

    private void OnEnable()
    {
        if (moveAction != null)
            moveAction.action.Enable();
        if (jumpAction != null)
            jumpAction.action.Enable();
        if (sprintAction != null)
            sprintAction.action.Enable();
        if (crouchAction != null)
            crouchAction.action.Enable();
        else if (CrouchInput != null)
            CrouchInput.Enable();
    }

    private void Update()
    {
        if (!IsMovementOwner)
        {
            TickStanceTransition();
            return;
        }

        bool dead = playerHealth != null && playerHealth.IsDead;
        bool menuOpen = LocalPlayerMenuState.IsOpen(this);
        TickGroundDetection();
        TickJumpAvailability();
        TickDiveTimers();

        if (dead)
        {
            FreezeDeadBody();
        }
        else
        {
            RestoreAlivePhysics();
            if (!menuOpen)
            {
                UpdatePostureInput();
                TickSlideAirborne();
                TryJump();
                UpdateCurrentSpeed();
            }
        }

        TickStanceTransition();
    }

    private void LateUpdate()
    {
        float poseBlend = stanceBlend <= 1f ? 0f : Mathf.Clamp01(stanceBlend - 1f);
        ApplyPlaceholderPronePose(poseBlend, false);
        KeepProneVisualAboveGround(poseBlend);
    }

    private void FixedUpdate()
    {
        if (!IsMovementOwner || rb == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            FreezeDeadBody();
            return;
        }

        RestoreAlivePhysics();

        if (rb.isKinematic)
            return;

        if (!onSlope)
            rb.AddForce(Vector3.down * ExtraGravityForce, ForceMode.Acceleration);

        if (!dolphinDiving.Value)
        {
            float speedCap = knockbackTimer > 0f ? Mathf.Max(maxSpeedAllowed, 28f) : maxSpeedAllowed;
            if (rb.linearVelocity.magnitude > speedCap)
                rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, speedCap);
        }

        ApplyLocomotion();
        TickDolphinDive();
        TickSlidePhysics();
        TickKnockback();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void ApplyExplosionKnockbackOwnerRpc(
        Vector3 explosionPosition,
        float force,
        float radius,
        float upwardModifier)
    {
        ApplyExplosionKnockback(explosionPosition, force, radius, upwardModifier);
    }

    public void ApplyExplosionKnockback(
        Vector3 explosionPosition,
        float force,
        float radius,
        float upwardModifier)
    {
        if (!IsMovementOwner || rb == null || rb.isKinematic)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        RestoreAlivePhysics();

        Vector3 origin = rb.worldCenterOfMass;
        Vector3 toPlayer = origin - explosionPosition;
        float distance = toPlayer.magnitude;
        float effectiveRadius = Mathf.Max(0.1f, radius);
        if (distance > effectiveRadius)
            return;

        float falloff = 1f - Mathf.Clamp01(distance / effectiveRadius);
        falloff = falloff * falloff * (3f - 2f * falloff);

        Vector3 direction = distance > 0.05f ? toPlayer.normalized : Vector3.up;
        direction += Vector3.up * Mathf.Max(0f, upwardModifier);
        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector3.up;

        rb.AddForce(direction.normalized * Mathf.Max(0f, force) * falloff, ForceMode.VelocityChange);

        knockbackTimer = Mathf.Lerp(0.18f, 0.5f, falloff);
        hasJumped = true;
        grounded = false;
        jumpIgnoreTimer = JumpIgnoreDuration;
        EndSlide();

        if (dolphinDiving.Value)
            diveBecameAirborne = true;
    }

    private void TickKnockback()
    {
        if (knockbackTimer <= 0f)
            return;

        knockbackTimer -= Time.fixedDeltaTime;
        if (knockbackTimer < 0f)
            knockbackTimer = 0f;
    }

    public void FreezeForDeath()
    {
        CancelDolphinDive(false);
        FreezeDeadBody();
    }

    public void ResetAfterRespawn()
    {
        sprintToggledOn = false;
        crouchToggledOn = false;
        IsSprinting = false;
        hasJumped = false;
        jumpAvailable = true;
        jumpCooldownTimer = 0f;
        jumpIgnoreTimer = 0f;
        coyoteTimer = 0f;
        ungroundTimer = 0f;
        cancellingGrounded = false;
        currentSpeed = walkSpeed;
        knockbackTimer = 0f;
        crouchHoldActive = false;
        crouchHoldTime = 0f;
        diveCandidateThisPress = false;
        diveElapsed = 0f;
        diveCooldownRemaining = 0f;
        diveRecoveryRemaining = 0f;
        diveBecameAirborne = false;
        EndSlide();
        CancelDolphinDive(false);

        if (IsSpawned && IsOwner)
        {
            if (prone.Value)
                prone.Value = false;
            if (crouched.Value)
                crouched.Value = false;
            if (dolphinDiving.Value)
                dolphinDiving.Value = false;
        }

        stanceBlend = 0f;
        ApplyStancePose(1f);
        RestoreBodyVisualPose();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = true;
            if (IsMovementOwner)
                rb.isKinematic = false;
        }
    }

    private void ConfigureRigidbody()
    {
        if (rb == null)
            return;

        rb.mass = 1.5f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
    }

    private void ConfigureAuthorityPhysics()
    {
        if (rb == null)
            return;

        rb.isKinematic = !IsOwner;
        if (IsOwner)
            ConfigureRigidbody();
    }

    private void FreezeDeadBody()
    {
        EndSlide();
        if (rb == null)
            return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void RestoreAlivePhysics()
    {
        if (rb == null || (playerHealth != null && playerHealth.IsDead))
            return;

        rb.useGravity = true;
        if (IsMovementOwner)
            rb.isKinematic = false;
    }

    private Vector2 ReadMoveInput()
    {
        if (LocalPlayerMenuState.IsOpen(this))
            return Vector2.zero;
        if (moveAction == null || moveAction.action == null)
            return Vector2.zero;
        return moveAction.action.ReadValue<Vector2>();
    }

    private void ApplyLocomotion()
    {
        if (dolphinDiving.Value)
        {
            ApplyDiveAirControl();
            return;
        }

        if (diveRecoveryRemaining > 0f)
        {
            FrictionForce(0f, 0f, FindVelRelativeToLook());
            LimitDiagonalVelocity();
            return;
        }

        Vector2 input = ReadMoveInput();
        Vector2 relativeVelocity = FindVelRelativeToLook();
        FrictionForce(input.x, input.y, relativeVelocity);
        LimitDiagonalVelocity();

        Vector3 horizontalVel = HorizontalVelocity();
        bool isCrouchSliding = crouched.Value && !prone.Value && !dolphinDiving.Value && horizontalVel.magnitude >= crouchSpeed;
        if (isCrouchSliding && !allowMoveWhileSliding && knockbackTimer <= 0f)
            return;

        float airborneMultiplier = grounded ? 1f : controlAirborne;
        float movementMultipliers = acceleration * Time.deltaTime * airborneMultiplier;
        if (isCrouchSliding && !allowMoveWhileSliding)
            movementMultipliers = 0f;
        if (onSlope)
            movementMultipliers *= 2f;
        if (isCrouchSliding)
            movementMultipliers *= slideSteerMultiplier;

        Vector3 moveDirection = CalculateMoveDirection(input);
        if (onSlope && moveDirection.sqrMagnitude < 0.0001f && !hasJumped)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        float velocityChange = (movementMultipliers / Mathf.Max(0.01f, rb.mass)) * Time.fixedDeltaTime;
        if (knockbackTimer > 0f)
            velocityChange *= 0.35f;

        Vector3 targetVelocity = rb.linearVelocity + moveDirection * velocityChange;
        Vector3 horizontalTarget = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
        float maxSpeed = knockbackTimer > 0f ? Mathf.Max(currentSpeed, horizontalTarget.magnitude) : currentSpeed;
        if (horizontalTarget.magnitude > maxSpeed)
        {
            horizontalTarget = horizontalTarget.normalized * maxSpeed;
            targetVelocity.x = horizontalTarget.x;
            targetVelocity.z = horizontalTarget.z;
        }

        if (onSlope && !hasJumped)
        {
            Vector3 slopeVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, slopeHit.normal)
                                    + moveDirection * velocityChange;
            if (slopeVelocity.magnitude > maxSpeed)
                slopeVelocity = slopeVelocity.normalized * maxSpeed;
            rb.linearVelocity = slopeVelocity;
        }
        else
        {
            float yVel = (grounded && !hasJumped && rb.linearVelocity.y <= 0f)
                ? Mathf.Min(rb.linearVelocity.y, 0f)
                : rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(targetVelocity.x, yVel, targetVelocity.z);
        }

        if (!onSlope)
            rb.AddForce(Vector3.down * Time.fixedDeltaTime * ExtraGravityMultiplier);
    }

    private Vector3 CalculateMoveDirection(Vector2 input)
    {
        Vector3 planar = (transform.forward * input.y + transform.right * input.x);
        if (onSlope)
            return Vector3.ProjectOnPlane(planar, slopeHit.normal).normalized;
        return planar.normalized;
    }

    private Vector2 FindVelRelativeToLook()
    {
        Vector3 localVel = Quaternion.Euler(0f, -transform.eulerAngles.y, 0f) * rb.linearVelocity;
        return new Vector2(localVel.x, localVel.z);
    }

    private void LimitDiagonalVelocity()
    {
        if (knockbackTimer > 0f)
            return;

        Vector3 horizontalVelocity = HorizontalVelocity();
        if (horizontalVelocity.magnitude > currentSpeed)
        {
            horizontalVelocity = horizontalVelocity.normalized * currentSpeed;
            rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
        }
    }

    private void FrictionForce(float x, float y, Vector2 mag)
    {
        if (!grounded || hasJumped || knockbackTimer > 0f)
            return;

        Vector3 horizontalVel = HorizontalVelocity();
        bool isCrouchSliding = crouched.Value && !prone.Value && !dolphinDiving.Value && horizontalVel.magnitude >= crouchSpeed;
        if (isCrouchSliding && !applyFrictionForceOnSliding)
            return;

        float friction = isCrouchSliding ? slideFrictionForceAmount : controlsResponsiveness;
        float frictionDeltaVScale = acceleration * Time.fixedDeltaTime * friction / Mathf.Max(0.01f, rb.mass);

        if (Math.Abs(mag.x) > FrictionThreshold && Math.Abs(x) < 0.5f ||
            (mag.x < -FrictionThreshold && x > 0f) ||
            (mag.x > FrictionThreshold && x < 0f))
        {
            float rawDeltaV = frictionDeltaVScale * Time.fixedDeltaTime * -mag.x;
            rawDeltaV = Mathf.Clamp(rawDeltaV, -Mathf.Abs(mag.x), Mathf.Abs(mag.x));
            rb.linearVelocity += transform.right * rawDeltaV;
        }

        if (Math.Abs(mag.y) > FrictionThreshold && Math.Abs(y) < 0.05f ||
            (mag.y < -FrictionThreshold && y > 0f) ||
            (mag.y > FrictionThreshold && y < 0f))
        {
            float rawDeltaV = frictionDeltaVScale * Time.fixedDeltaTime * -mag.y;
            rawDeltaV = Mathf.Clamp(rawDeltaV, -Mathf.Abs(mag.y), Mathf.Abs(mag.y));
            rb.linearVelocity += transform.forward * rawDeltaV;
        }
    }

    private void TryJump()
    {
        if (jumpAction == null || jumpAction.action == null)
            return;
        if (!jumpAction.action.WasPressedThisFrame())
            return;

        if (dolphinDiving.Value)
            return;

        if (prone.Value)
        {
            TryExitProneToCrouch();
            return;
        }

        if (crouched.Value && CanStand())
        {
            SetCrouched(false);
            EndSlide();
        }

        if (!CanJump())
            return;

        LastJumpFromSprint = IsSprinting && HorizontalVelocity().magnitude >= 0.2f;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        hasJumped = true;
        jumpAvailable = false;
        jumpCooldownTimer = jumpCooldown;
        jumpIgnoreTimer = JumpIgnoreDuration;
        grounded = false;
        EndSlide();

        if (bullseyeMover != null)
            bullseyeMover.NotifyJump();

        Jumped?.Invoke();
    }

    private bool CanJump()
    {
        if (!jumpAvailable)
            return false;
        if (prone.Value || dolphinDiving.Value)
            return false;
        if (crouched.Value && !canJumpWhileCrouching)
            return false;

        bool coyoteValid = canCoyote && !grounded && coyoteTimer > 0f;
        return grounded || coyoteValid || onSlope;
    }

    private void TickJumpAvailability()
    {
        if (!jumpAvailable)
        {
            jumpCooldownTimer -= Time.deltaTime;
            if (jumpCooldownTimer <= 0f)
                jumpAvailable = true;
        }

        if (canCoyote)
        {
            if (grounded)
            {
                coyoteTimer = coyoteJumpTime;
                hasJumped = false;
            }
            else
            {
                coyoteTimer -= Time.deltaTime;
            }
        }
    }

    private void UpdateCurrentSpeed()
    {
        if (sliding)
            return;

        if (dolphinDiving.Value)
        {
            IsSprinting = false;
            return;
        }

        if (prone.Value)
        {
            currentSpeed = diveRecoveryRemaining > 0f ? 0f : proneMoveSpeed;
            IsSprinting = false;
            return;
        }

        if (crouched.Value)
        {
            currentSpeed = crouchSpeed;
            IsSprinting = false;
            return;
        }

        IsSprinting = CanSprint();
        if (IsSprinting)
            currentSpeed = runSpeed;
        else
            currentSpeed = walkSpeed;
    }

    private bool CanSprint()
    {
        if (!ReadSprintInput())
            return false;

        Vector2 input = ReadMoveInput();
        bool movingForward = input.y > 0.1f;
        bool movingBackward = input.y < -0.1f;
        bool movingSideways = Mathf.Abs(input.x) > 0.1f;

        if (prone.Value || dolphinDiving.Value)
            return false;
        if (crouched.Value)
            return false;
        if (movingBackward && !canRunBackwards)
            return false;
        if (movingSideways && !movingForward && !canRunSideways)
            return false;

        return movingForward || movingSideways || movingBackward;
    }

    private bool ReadSprintInput()
    {
        if (LocalPlayerMenuState.IsOpen(this))
            return false;
        if (sprintAction == null || sprintAction.action == null)
            return false;

        if (sprintActivation == InputActivationMode.Hold)
            return sprintAction.action.IsPressed();

        if (sprintAction.action.WasPressedThisFrame())
            sprintToggledOn = !sprintToggledOn;

        return sprintToggledOn;
    }

    private void UpdatePostureInput()
    {
        InputAction crouchInput = CrouchInput;
        if (crouchInput == null)
            return;

        if (dolphinDiving.Value)
        {
            if (grounded && (crouchInput.WasPressedThisFrame() || JumpWasPressed()))
                LandDolphinDive();
            return;
        }

        if (!prone.Value && crouched.Value && !crouchHoldActive && ReadSprintInput() && CanStand())
        {
            ExitToStanding();
            return;
        }

        if (crouchInput.WasPressedThisFrame())
            OnCrouchPressed();

        if (crouchHoldActive)
            TickCrouchHold();

        if (crouchInput.WasReleasedThisFrame())
            OnCrouchReleased();
    }

    private void OnCrouchPressed()
    {
        crouchHoldActive = true;
        crouchHoldTime = 0f;
        crouchPressExitedProne = false;
        crouchPressStartedCrouched = crouched.Value && !prone.Value;
        diveCandidateThisPress = CanBeginDiveCandidate();

        if (prone.Value)
        {
            if (TryExitProneToCrouch())
                crouchPressExitedProne = true;
            else
                LogMovement("Cannot exit prone — not enough clearance.");

            return;
        }

        if (crouchPressStartedCrouched)
            return;

        SetCrouched(true);
        TryStartSlide();
        LogMovement("Entered crouch.");
    }

    private void TickCrouchHold()
    {
        if (crouchPressExitedProne || dolphinDiving.Value)
            return;

        crouchHoldTime += Time.deltaTime;

        if (!prone.Value && diveCandidateThisPress && CanTriggerDolphinDive() &&
            crouchHoldTime >= Mathf.Max(0.01f, dolphinDiveHoldDuration))
        {
            StartDolphinDive();
            crouchHoldActive = false;
            return;
        }

        if (!prone.Value && !dolphinDiving.Value && crouchHoldTime >= Mathf.Max(0.01f, proneHoldDuration))
        {
            EnterProne();
            crouchHoldActive = false;
            LogMovement($"Crouch held {crouchHoldTime:0.00}s — entering prone.");
        }
    }

    private void OnCrouchReleased()
    {
        float held = crouchHoldTime;
        crouchHoldActive = false;
        crouchHoldTime = 0f;
        diveCandidateThisPress = false;

        if (crouchPressExitedProne || dolphinDiving.Value || prone.Value)
            return;

        if (crouchActivation == InputActivationMode.Hold)
        {
            if (crouched.Value && CanStand())
                ExitToStanding();
            return;
        }

        if (crouchPressStartedCrouched && held < proneHoldDuration && crouched.Value && CanStand())
            ExitToStanding();
    }

    private bool CanBeginDiveCandidate()
    {
        if (prone.Value || dolphinDiving.Value || !grounded)
            return false;
        if (diveCooldownRemaining > 0f)
            return false;

        return IsSprinting && HorizontalVelocity().magnitude >= minimumDolphinDiveSpeed;
    }

    private bool CanTriggerDolphinDive()
    {
        if (!diveCandidateThisPress || prone.Value || dolphinDiving.Value)
            return false;
        if (diveCooldownRemaining > 0f || !grounded)
            return false;

        return HorizontalVelocity().magnitude >= minimumDolphinDiveSpeed;
    }

    private void StartDolphinDive()
    {
        if (dolphinDiving.Value)
            return;

        EndSlide();
        IsSprinting = false;
        sprintToggledOn = false;
        SetCrouched(true);
        if (prone.Value)
            prone.Value = false;

        dolphinDiving.Value = true;
        diveElapsed = 0f;
        diveBecameAirborne = false;
        diveRecoveryRemaining = 0f;
        diveCooldownRemaining = Mathf.Max(0f, dolphinDiveCooldown);
        crouchHoldActive = false;

        diveDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (diveDirection.sqrMagnitude < 0.0001f)
        {
            Vector3 horizontal = HorizontalVelocity();
            diveDirection = horizontal.sqrMagnitude > 0.01f ? horizontal.normalized : Vector3.forward;
        }
        else
        {
            diveDirection.Normalize();
        }

        if (rb != null && !rb.isKinematic)
        {
            Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            float planarSpeed = Mathf.Min(
                Mathf.Max(planar.magnitude, currentSpeed),
                Mathf.Max(0.01f, dolphinDiveMaxSpeed));
            rb.linearVelocity = diveDirection * planarSpeed + Vector3.up * Mathf.Max(0f, rb.linearVelocity.y);
            rb.AddForce(
                diveDirection * Mathf.Max(0f, dolphinDiveForwardForce) +
                Vector3.up * Mathf.Max(0f, dolphinDiveUpwardForce),
                ForceMode.VelocityChange);
            ClampDivePlanarSpeed();
        }

        weaponInventory?.InterruptReloadForDive();
        DolphinDiveStarted?.Invoke();
        LogMovement("Sprint + crouch hold detected — dolphin dive.");
        LogMovement("Dolphin dive launched.");
    }

    private void TickDolphinDive()
    {
        if (!dolphinDiving.Value)
            return;

        diveElapsed += Time.fixedDeltaTime;
        if (!grounded)
            diveBecameAirborne = true;

        bool shouldLand = false;
        if (grounded)
        {
            if (diveBecameAirborne && diveElapsed >= 0.15f)
                shouldLand = true;
            else if (!diveBecameAirborne && diveElapsed >= Mathf.Max(0.05f, dolphinDiveDuration))
                shouldLand = true;
            else if (diveElapsed > 0.12f && HorizontalVelocity().magnitude < 1.25f)
                shouldLand = true;
        }

        if (shouldLand)
            LandDolphinDive();
    }

    private void ApplyDiveAirControl()
    {
        if (rb == null || rb.isKinematic || dolphinDiveAirControl <= 0f)
            return;

        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude < 0.0001f)
            return;

        Vector3 steer = (transform.forward * input.y + transform.right * input.x);
        steer.y = 0f;
        if (steer.sqrMagnitude < 0.0001f)
            return;

        float scale = acceleration * Time.fixedDeltaTime * dolphinDiveAirControl / Mathf.Max(0.01f, rb.mass);
        rb.AddForce(steer.normalized * scale, ForceMode.VelocityChange);
        ClampDivePlanarSpeed();
    }

    private void ClampDivePlanarSpeed()
    {
        if (rb == null || rb.isKinematic)
            return;

        float maxSpeed = Mathf.Max(0.01f, dolphinDiveMaxSpeed);
        Vector3 velocity = rb.linearVelocity;
        Vector3 planar = Vector3.ProjectOnPlane(velocity, Vector3.up);
        if (planar.magnitude <= maxSpeed)
            return;

        planar = planar.normalized * maxSpeed;
        rb.linearVelocity = planar + Vector3.up * velocity.y;
    }

    private void LandDolphinDive()
    {
        if (!dolphinDiving.Value)
            return;

        dolphinDiving.Value = false;
        diveBecameAirborne = false;
        diveElapsed = 0f;
        diveRecoveryRemaining = Mathf.Max(0f, dolphinDiveRecoveryDuration);
        EnterProne();
        DolphinDiveLanded?.Invoke();
        LogMovement("Dolphin dive landed — entering prone.");
    }

    private void CancelDolphinDive(bool landProne)
    {
        crouchHoldActive = false;
        diveCandidateThisPress = false;
        diveBecameAirborne = false;
        diveElapsed = 0f;

        if (dolphinDiving.Value && (!IsSpawned || IsOwner))
            dolphinDiving.Value = false;

        if (landProne)
        {
            diveRecoveryRemaining = Mathf.Max(0f, dolphinDiveRecoveryDuration);
            EnterProne();
            return;
        }

        diveRecoveryRemaining = 0f;
    }

    private void TickDiveTimers()
    {
        if (diveCooldownRemaining > 0f)
        {
            diveCooldownRemaining -= Time.deltaTime;
            if (diveCooldownRemaining < 0f)
                diveCooldownRemaining = 0f;
        }

        if (diveRecoveryRemaining > 0f && !dolphinDiving.Value)
        {
            diveRecoveryRemaining -= Time.deltaTime;
            if (diveRecoveryRemaining < 0f)
                diveRecoveryRemaining = 0f;
        }
    }

    private void EnterProne()
    {
        EndSlide();
        IsSprinting = false;
        sprintToggledOn = false;
        SetCrouched(true);
        if (!prone.Value)
        {
            prone.Value = true;
            NotifyPostureChanged();
        }
    }

    private bool JumpWasPressed()
    {
        return jumpAction != null && jumpAction.action != null && jumpAction.action.WasPressedThisFrame();
    }

    private bool TryExitProneToCrouch()
    {
        if (!prone.Value)
            return false;

        if (!CanRiseToHeight(ResolveCrouchHeight(), ResolveCrouchRadius()))
        {
            LogMovement("Cannot exit prone — not enough clearance.");
            return false;
        }

        diveRecoveryRemaining = 0f;
        ExitProneToCrouch();
        return true;
    }

    private void ExitProneToCrouch()
    {
        if (!prone.Value)
            return;

        prone.Value = false;
        SetCrouched(true);
        crouchToggledOn = true;
        NotifyPostureChanged();
        LogMovement("Exited prone.");
    }

    private void ExitToStanding()
    {
        if (prone.Value && !CanRiseToHeight(ResolveCrouchHeight()))
            return;
        if (!CanStand())
        {
            crouchToggledOn = true;
            return;
        }

        if (prone.Value)
        {
            prone.Value = false;
            LogMovement("Exited prone.");
        }

        SetCrouched(false);
        EndSlide();
    }

    private void SetCrouched(bool value)
    {
        if (crouched.Value == value)
        {
            crouchToggledOn = value;
            return;
        }

        crouched.Value = value;
        crouchToggledOn = value;
        if (!value && prone.Value)
            prone.Value = false;

        NotifyPostureChanged();
    }

    private void NotifyPostureChanged()
    {
        if (bullseyeMover != null)
            bullseyeMover.NotifyCrouchChanged();
    }

    private void LogMovement(string message)
    {
        if (!debugMovementLogs)
            return;

        Debug.Log("[Movement] " + message);
    }

    private void TryStartSlide()
    {
        if (!allowSliding || !grounded || hasJumped || rb == null)
            return;
        if (prone.Value || dolphinDiving.Value)
            return;

        Vector3 horizontalVel = HorizontalVelocity();
        if (horizontalVel.magnitude < walkSpeed || currentSpeed <= walkSpeed)
            return;

        sliding = true;
        slideDirection = horizontalVel.sqrMagnitude > 0.01f
            ? horizontalVel.normalized
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        slideBoostRemaining = Mathf.Max(0.0001f, slideBoostDuration);
        slideBoosting = true;
        slideTimer = slideDuration;
        slideAirborneTimer = 0f;
    }

    private void TickSlidePhysics()
    {
        if (!sliding || dolphinDiving.Value)
            return;

        if (!grounded)
            return;

        if (onSlope && !hasJumped)
            rb.linearVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, slopeHit.normal);
        else if (rb.linearVelocity.y > 0f)
        {
            Vector3 v = rb.linearVelocity;
            v.y = 0f;
            rb.linearVelocity = v;
        }

        if (slideBoosting && slideBoostRemaining > 0f)
        {
            float dt = Time.fixedDeltaTime;
            float boostAmount = slideForce * dt / Mathf.Max(slideBoostDuration, dt);
            rb.AddForce(slideDirection * boostAmount, ForceMode.Acceleration);
            slideBoostRemaining -= dt;
            if (slideBoostRemaining <= 0f)
                slideBoosting = false;
        }

        Vector2 input = ReadMoveInput();
        Vector3 inputDir = transform.forward * input.y + transform.right * input.x;
        inputDir.y = 0f;
        if (inputDir.sqrMagnitude > 0.0001f)
        {
            Vector3 steer = inputDir.normalized * acceleration * slideSteerMultiplier * Time.fixedDeltaTime;
            rb.AddForce(steer, ForceMode.Acceleration);
        }

        slideTimer -= Time.fixedDeltaTime;
        if (slideTimer <= 0f || HorizontalVelocity().magnitude < slideStopSpeed)
            EndSlide();
    }

    private void TickSlideAirborne()
    {
        if (!sliding)
            return;

        if (!grounded)
        {
            slideAirborneTimer += Time.deltaTime;
            if (slideAirborneTimer > SlideAirborneTolerance)
                EndSlide();
            return;
        }

        slideAirborneTimer = 0f;
    }

    private void EndSlide()
    {
        if (!sliding)
            return;

        sliding = false;
        slideBoosting = false;
        slideAirborneTimer = 0f;

        if (rb == null || rb.isKinematic)
            return;

        Vector3 vel = rb.linearVelocity;
        Vector3 horizontal = new Vector3(vel.x, 0f, vel.z);
        float reduced = horizontal.magnitude * 0.6f;
        Vector3 dir = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : Vector3.zero;
        rb.linearVelocity = new Vector3(dir.x * reduced, vel.y, dir.z * reduced);
    }

    private void TickGroundDetection()
    {
        if (hasJumped && jumpIgnoreTimer > 0f)
            jumpIgnoreTimer -= Time.deltaTime;

        bool foundGround = PerformGroundCheck(out RaycastHit hit);
        onSlope = foundGround && IsFloor(hit.normal) && Vector3.Angle(Vector3.up, hit.normal) > 0.01f;
        slopeHit = hit;

        bool wasGrounded = grounded;

        if (hasJumped && rb != null && rb.linearVelocity.y > 0.1f && jumpIgnoreTimer > 0f)
        {
            grounded = false;
            if (wasGrounded)
                cancellingGrounded = true;
            return;
        }

        if (foundGround)
        {
            cancellingGrounded = false;
            ungroundTimer = 0f;
            grounded = true;
            if (!wasGrounded)
            {
                hasJumped = false;
                float downwardSpeed = rb != null ? Mathf.Max(0f, -rb.linearVelocity.y) : 0f;
                Landed?.Invoke(downwardSpeed);
            }
            return;
        }

        if (wasGrounded && !cancellingGrounded)
        {
            cancellingGrounded = true;
            ungroundTimer = UngroundDelay;
        }

        if (cancellingGrounded)
        {
            ungroundTimer -= Time.deltaTime;
            if (ungroundTimer <= 0f)
            {
                grounded = false;
                cancellingGrounded = false;
                coyoteTimer = coyoteJumpTime;
            }
        }
    }

    private bool PerformGroundCheck(out RaycastHit hit)
    {
        hit = default;
        if (playerCapsule == null)
            return false;

        GetCapsuleWorld(out Vector3 top, out Vector3 bottom, out float radius);
        float startOffset = 0.1f;
        bottom += Vector3.up * startOffset;
        top += Vector3.up * startOffset;
        float castDistance = Mathf.Max(groundCheckDistance, 0.2f) + startOffset;

        int hitCount = Physics.CapsuleCastNonAlloc(
            top,
            bottom,
            radius * 0.8f,
            Vector3.down,
            groundHits,
            castDistance,
            whatIsGround,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = groundHits[i];
            if (candidate.collider == null || IsOwnCollider(candidate.collider))
                continue;
            if (!IsFloor(candidate.normal))
                continue;
            if (candidate.distance >= bestDistance)
                continue;

            bestDistance = candidate.distance;
            hit = candidate;
            found = true;
        }

        return found;
    }

    private void GetCapsuleWorld(out Vector3 top, out Vector3 bottom, out float radius)
    {
        Vector3 center = transform.TransformPoint(playerCapsule.center);
        float height = playerCapsule.height * Mathf.Abs(transform.lossyScale.y);
        radius = playerCapsule.radius * Mathf.Abs(transform.lossyScale.x);
        float halfHeight = Mathf.Max(0f, (height * 0.5f) - radius);
        bottom = center - Vector3.up * halfHeight;
        top = center + Vector3.up * halfHeight;
    }

    private static bool IsFloor(Vector3 normal)
    {
        return Vector3.Angle(Vector3.up, normal) <= MaxSlopeAngle;
    }

    private bool IsOwnCollider(Collider collider)
    {
        return collider != null && collider.transform.IsChildOf(transform);
    }

    private void OnCrouchedChanged(bool previous, bool next)
    {
        crouchToggledOn = next;
    }

    private void TickStanceTransition()
    {
        float target = ResolveStanceTarget();
        float blendSpeed = ResolveStanceBlendSpeed();
        stanceBlend = Mathf.MoveTowards(stanceBlend, target, blendSpeed * Time.deltaTime);
        ApplyStancePose(blendSpeed * Time.deltaTime);
    }

    private float ResolveStanceTarget()
    {
        if (prone.Value || dolphinDiving.Value)
            return 2f;
        if (crouched.Value)
            return 1f;
        return 0f;
    }

    private float ResolveStanceBlendSpeed()
    {
        float target = ResolveStanceTarget();
        if (target >= 1.5f || stanceBlend >= 1.5f)
            return Mathf.Max(0.1f, proneTransitionSpeed);
        return 1f / Mathf.Max(0.0001f, crouchTransitionDuration);
    }

    private void ApplyStancePose(float snapAmount)
    {
        float crouchHeight = ResolveCrouchHeight();
        float proneHeight = ResolveProneHeight();
        Vector3 crouchCenter = standingCenter;
        crouchCenter.y = crouchHeight * 0.5f;
        Vector3 proneCenter = proneControllerCenter;
        if (proneCenter.sqrMagnitude < 0.0001f)
            proneCenter = new Vector3(standingCenter.x, proneHeight * 0.5f, standingCenter.z);

        float targetHeight = standingHeight;
        Vector3 targetCenter = standingCenter;
        float targetRadius = standingRadius;
        float targetCameraY = standingCameraLocalY;
        float poseBlend = 0f;

        if (stanceBlend <= 1f)
        {
            targetHeight = Mathf.Lerp(standingHeight, crouchHeight, stanceBlend);
            targetCenter = Vector3.Lerp(standingCenter, crouchCenter, stanceBlend);
            targetCameraY = Mathf.Lerp(standingCameraLocalY, crouchedCameraLocalY, stanceBlend);
        }
        else
        {
            float t = Mathf.Clamp01(stanceBlend - 1f);
            targetHeight = Mathf.Lerp(crouchHeight, proneHeight, t);
            targetCenter = Vector3.Lerp(crouchCenter, proneCenter, t);
            targetRadius = Mathf.Lerp(standingRadius, ResolveProneRadius(), t);
            targetCameraY = Mathf.Lerp(crouchedCameraLocalY, proneCameraLocalY, t);
            poseBlend = t;
        }

        if (playerCapsule != null)
        {
            playerCapsule.height = targetHeight;
            playerCapsule.center = targetCenter;
            playerCapsule.radius = targetRadius;
        }

        if (playerCamera != null)
        {
            float cameraSpeed = stanceBlend >= 1.5f || prone.Value || dolphinDiving.Value
                ? Mathf.Max(0.1f, proneCameraTransitionSpeed)
                : 1f / Mathf.Max(0.0001f, crouchTransitionDuration);
            Vector3 cameraPosition = playerCamera.localPosition;
            if (currentCameraLocalY <= 0f)
                currentCameraLocalY = cameraPosition.y;
            currentCameraLocalY = Mathf.MoveTowards(currentCameraLocalY, targetCameraY, cameraSpeed * Time.deltaTime);
            if (snapAmount >= 1f)
                currentCameraLocalY = targetCameraY;
            cameraPosition.y = currentCameraLocalY;
            playerCamera.localPosition = cameraPosition;
        }
    }

    private void ApplyPlaceholderPronePose(float poseBlend, bool snap)
    {
        if (!applyPlaceholderPronePose || bodyVisual == null)
            return;

        Quaternion targetRotation = Quaternion.Slerp(
            standingBodyRotation,
            standingBodyRotation * Quaternion.Euler(proneVisualEuler),
            poseBlend);
        Vector3 targetPosition = Vector3.Lerp(
            standingBodyPosition,
            standingBodyPosition + ResolveProneVisualOffset(),
            poseBlend);

        if (snap)
        {
            bodyVisual.localRotation = targetRotation;
            bodyVisual.localPosition = targetPosition;
            return;
        }

        float speed = Mathf.Max(0.1f, proneTransitionSpeed);
        bodyVisual.localRotation = Quaternion.Slerp(bodyVisual.localRotation, targetRotation, 1f - Mathf.Exp(-speed * Time.deltaTime));
        bodyVisual.localPosition = Vector3.MoveTowards(bodyVisual.localPosition, targetPosition, speed * Time.deltaTime);
    }

    private void KeepProneVisualAboveGround(float poseBlend)
    {
        if (!applyPlaceholderPronePose || bodyVisual == null || poseBlend <= 0.01f)
            return;

        Renderer renderer = bodyVisual.GetComponentInChildren<Renderer>();
        if (renderer == null)
            return;

        float floorY = transform.position.y + 0.06f;
        float meshMinY = renderer.bounds.min.y;
        if (meshMinY >= floorY)
            return;

        bodyVisual.position += Vector3.up * (floorY - meshMinY);
    }

    private Vector3 ResolveProneVisualOffset()
    {
        Vector3 desiredCenter = proneControllerCenter;
        if (desiredCenter.sqrMagnitude < 0.0001f)
            desiredCenter = new Vector3(standingCenter.x, ResolveProneHeight() * 0.5f, standingCenter.z);

        Quaternion rot = Quaternion.Euler(proneVisualEuler);
        Vector3 rotatedStandingCenter = rot * standingCenter;
        return desiredCenter - rotatedStandingCenter + proneVisualLocalOffset;
    }

    private float ResolveCrouchHeight()
    {
        return Mathf.Max(ResolveCrouchRadius() * 2f, crouchedHeight);
    }

    private float ResolveCrouchRadius()
    {
        return standingRadius;
    }

    private float ResolveProneHeight()
    {
        float radius = ResolveProneRadius();
        return Mathf.Max(radius * 2f, proneControllerHeight);
    }

    private float ResolveProneRadius()
    {
        float maxRadius = Mathf.Max(0.08f, proneControllerHeight * 0.5f);
        if (proneControllerRadius > 0.01f)
            return Mathf.Min(standingRadius, Mathf.Min(proneControllerRadius, maxRadius));
        return Mathf.Min(standingRadius, maxRadius);
    }

    private bool CanStand()
    {
        return CanRiseToHeight(standingHeight, standingRadius);
    }

    private bool CanRiseToHeight(float targetHeight)
    {
        return CanRiseToHeight(targetHeight, ResolveCrouchRadius());
    }

    private bool CanRiseToHeight(float targetHeight, float targetRadius)
    {
        if (playerCapsule == null)
            return true;

        float currentHeight = playerCapsule.height;
        float currentRadius = Mathf.Max(0.05f, playerCapsule.radius);
        float rise = targetHeight - currentHeight;
        if (rise <= 0.001f && targetRadius <= currentRadius + 0.001f)
            return true;

        float radius = Mathf.Max(0.05f, Mathf.Min(currentRadius, targetRadius) * 0.9f);
        float bottomY = radius + 0.05f;
        float currentTopY = Mathf.Max(bottomY + 0.01f, currentHeight - radius);
        Vector3 bottom = transform.position + transform.up * bottomY;
        Vector3 top = transform.position + transform.up * currentTopY;
        float castDistance = Mathf.Max(0.01f, targetHeight - currentTopY - 0.01f);

        int hitCount = Physics.CapsuleCastNonAlloc(
            top,
            bottom,
            radius,
            transform.up,
            standHits,
            castDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = standHits[i];
            if (hit.collider == null || IsOwnCollider(hit.collider))
                continue;
            if (IsFloor(hit.normal))
                continue;

            return false;
        }

        float roofDistance = Mathf.Max(0.01f, Mathf.Min(roofCheckDistance, targetHeight - currentTopY));
        if (Physics.Raycast(top, transform.up, out RaycastHit roofHit, roofDistance, ~0, QueryTriggerInteraction.Ignore)
            && roofHit.collider != null
            && !IsOwnCollider(roofHit.collider)
            && !IsFloor(roofHit.normal))
            return false;

        return true;
    }

    private Vector3 HorizontalVelocity()
    {
        if (rb == null)
            return Vector3.zero;
        return new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    private void IgnoreInternalCollisions()
    {
        if (playerCapsule == null)
            return;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || colliders[i] == playerCapsule)
                continue;
            Physics.IgnoreCollision(playerCapsule, colliders[i], true);
        }
    }

    private void CacheStandingPose()
    {
        if (playerCapsule != null)
        {
            if (Mathf.Abs(playerCapsule.center.y) < 0.01f)
            {
                playerCapsule.height = 2f;
                playerCapsule.radius = 0.5f;
                playerCapsule.center = new Vector3(0f, 1f, 0f);
            }

            standingHeight = playerCapsule.height;
            standingCenter = playerCapsule.center;
            standingRadius = playerCapsule.radius;
        }
        else
        {
            standingHeight = 2f;
            standingCenter = new Vector3(0f, 1f, 0f);
            standingRadius = 0.5f;
        }

        if (bodyVisual != null)
        {
            standingBodyPosition = bodyVisual.localPosition;
            standingBodyRotation = bodyVisual.localRotation;
            standingBodyScale = bodyVisual.localScale;
        }

        if (playerCamera != null)
        {
            standingCameraLocalY = playerCamera.localPosition.y;
            currentCameraLocalY = standingCameraLocalY;
        }
    }

    private void RestoreBodyVisualPose()
    {
        if (bodyVisual == null)
            return;

        bodyVisual.localPosition = standingBodyPosition;
        bodyVisual.localRotation = standingBodyRotation;
        bodyVisual.localScale = standingBodyScale;
    }
}

public enum InputActivationMode
{
    Toggle,
    Hold
}
