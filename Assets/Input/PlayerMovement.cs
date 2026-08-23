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

    private readonly RaycastHit[] standHits = new RaycastHit[8];
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    private Rigidbody rb;
    private CapsuleCollider playerCapsule;
    private CharacterController legacyController;
    private BullseyeMover bullseyeMover;
    private PlayerHealth playerHealth;
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
    private Vector3 standingBodyPosition;
    private Vector3 standingBodyScale;
    private float standingCameraLocalY;
    private float stanceBlend;

    private bool sliding;
    private Vector3 slideDirection;
    private float slideTimer;
    private float slideBoostRemaining;
    private bool slideBoosting;
    private float slideAirborneTimer;
    private float knockbackTimer;

    public event Action<float> Landed;
    public event Action Jumped;

    public bool IsCrouched => crouched.Value;
    public Transform BodyVisual => bodyVisual;
    public bool Grounded => grounded;
    public bool IsSliding => sliding;
    public bool IsSprinting { get; private set; }
    public bool CanRunWhileShooting => canRunWhileShooting;
    public float CurrentSpeed => currentSpeed;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float HorizontalSpeed => HorizontalVelocity().magnitude;
    public Vector2 MoveInput => ReadMoveInput();

    public void SetCameraTransform(Transform cameraTransform)
    {
        playerCamera = cameraTransform;
        if (playerCamera != null)
            standingCameraLocalY = playerCamera.localPosition.y;
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

        ConfigureRigidbody();
        IgnoreInternalCollisions();
        CacheStandingPose();
        currentSpeed = walkSpeed;
    }

    public override void OnNetworkSpawn()
    {
        crouched.OnValueChanged += OnCrouchedChanged;
        crouchToggledOn = crouched.Value;
        stanceBlend = crouched.Value ? 1f : 0f;
        ApplyStanceBlend(stanceBlend);
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

        if (dead)
        {
            FreezeDeadBody();
        }
        else
        {
            RestoreAlivePhysics();
            if (!menuOpen)
            {
                UpdateCrouchState();
                TickSlideAirborne();
                TryJump();
                UpdateCurrentSpeed();
            }
        }

        TickStanceTransition();
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

        float speedCap = knockbackTimer > 0f ? Mathf.Max(maxSpeedAllowed, 28f) : maxSpeedAllowed;
        if (rb.linearVelocity.magnitude > speedCap)
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, speedCap);

        ApplyLocomotion();
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
    }

    private void TickKnockback()
    {
        if (knockbackTimer <= 0f)
            return;

        knockbackTimer -= Time.fixedDeltaTime;
        if (knockbackTimer < 0f)
            knockbackTimer = 0f;
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
        EndSlide();

        if (IsSpawned && IsOwner && crouched.Value)
            crouched.Value = false;

        stanceBlend = 0f;
        ApplyStanceBlend(0f);

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
        Vector2 input = ReadMoveInput();
        Vector2 relativeVelocity = FindVelRelativeToLook();
        FrictionForce(input.x, input.y, relativeVelocity);
        LimitDiagonalVelocity();

        Vector3 horizontalVel = HorizontalVelocity();
        bool isCrouchSliding = crouched.Value && horizontalVel.magnitude >= crouchSpeed;
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
        bool isCrouchSliding = crouched.Value && horizontalVel.magnitude >= crouchSpeed;
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

        if (crouched.Value && CanStand())
        {
            SetCrouched(false);
            EndSlide();
        }

        if (!CanJump())
            return;

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

    private void UpdateCrouchState()
    {
        InputAction crouchInput = CrouchInput;
        if (crouchInput == null)
            return;

        bool currentlyCrouched = crouched.Value;
        if (currentlyCrouched && ReadSprintInput() && CanStand())
        {
            crouchToggledOn = false;
            SetCrouched(false);
            EndSlide();
            return;
        }

        bool wantsCrouch = ReadCrouchInput();

        if (wantsCrouch == currentlyCrouched)
            return;

        if (wantsCrouch)
        {
            SetCrouched(true);
            TryStartSlide();
            return;
        }

        if (CanStand())
        {
            SetCrouched(false);
            EndSlide();
        }
        else
        {
            crouchToggledOn = true;
        }
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

    private void TryStartSlide()
    {
        if (!allowSliding || !grounded || hasJumped || rb == null)
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
        if (!sliding)
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
        float target = crouched.Value ? 1f : 0f;
        float duration = Mathf.Max(0.0001f, crouchTransitionDuration);
        stanceBlend = Mathf.MoveTowards(stanceBlend, target, Time.deltaTime / duration);
        ApplyStanceBlend(stanceBlend);
    }

    private void ApplyStanceBlend(float blend)
    {
        float crouchedBodyHeight = playerCapsule != null
            ? Mathf.Max(playerCapsule.radius * 2f, crouchedHeight)
            : crouchedHeight;
        float height = Mathf.Lerp(standingHeight, crouchedBodyHeight, blend);

        if (playerCapsule != null)
        {
            playerCapsule.height = height;
            Vector3 center = standingCenter;
            center.y = height * 0.5f;
            playerCapsule.center = center;
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
        if (playerCapsule == null)
            return true;

        float targetHeight = standingHeight;
        float currentHeight = playerCapsule.height;
        float rise = targetHeight - currentHeight;
        if (rise <= 0.001f)
            return true;

        float radius = Mathf.Max(0.01f, playerCapsule.radius * 0.9f);
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

        if (Physics.Raycast(transform.position, transform.up, out RaycastHit roofHit, roofCheckDistance, ~0, QueryTriggerInteraction.Ignore)
            && roofHit.collider != null
            && !IsOwnCollider(roofHit.collider))
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
        }
        else
        {
            standingHeight = 2f;
            standingCenter = new Vector3(0f, 1f, 0f);
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
