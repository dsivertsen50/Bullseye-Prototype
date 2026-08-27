using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner-only first-person camera presentation adapted from Cowsins CameraEffects
/// feel, without Cowsins player dependencies. Cosmetic and not networked.
/// Writes only to CameraEffectsRoot so gameplay look/crouch stay authoritative.
/// </summary>
[DefaultExecutionOrder(-500)]
public class PlayerCameraEffects : NetworkBehaviour
{
    [SerializeField] private Transform lookRoot;
    [SerializeField] private Transform effectsRoot;
    [SerializeField] private Camera playerCamera;

    [Header("Idle")]
    [SerializeField] private float breathingAmplitude = 0.012f;
    [SerializeField] private float breathingFrequency = 1.15f;
    [SerializeField] private float breathingRotation = 0.12f;

    [Header("Walking")]
    [SerializeField] private float walkBobAmplitude = 0.03f;
    [SerializeField] private float walkBobFrequency = 1.55f;
    [SerializeField] private float walkSwayAmplitude = 0.018f;
    [SerializeField] private float walkRoll = 0.35f;

    [Header("Sprinting")]
    [SerializeField] private float sprintBobAmplitude = 0.05f;
    [SerializeField] private float sprintBobFrequency = 1.95f;
    [SerializeField] private float sprintSwayAmplitude = 0.035f;
    [SerializeField] private float sprintRoll = 1.1f;
    [SerializeField] private float sprintForwardMotion = 0.012f;

    [Header("Landing")]
    [SerializeField] private float landingIntensity = 0.12f;
    [SerializeField] private float landingRecoverySpeed = 7f;
    [SerializeField] private float landingPitch = 2.4f;
    [SerializeField] private float minLandingSpeed = 2.5f;
    [SerializeField] private float fullLandingSpeed = 12f;

    [Header("Dolphin Dive")]
    [SerializeField] private float divePitch = 8f;
    [SerializeField] private float diveForwardMotion = 0.04f;
    [SerializeField] private float diveRoll = 2.5f;

    [Header("Jump")]
    [SerializeField] private float jumpDepartureAmount = 0.025f;
    [SerializeField] private float jumpRecoverySpeed = 8f;

    [Header("Blending")]
    [SerializeField] private float idleSpeedThreshold = 0.35f;
    [SerializeField] private float motionBlendSpeed = 8f;
    [SerializeField] private float aimingMotionMultiplier = 0.35f;

    private PlayerMovement movement;
    private PlayerHealth playerHealth;
    private PlayerAimZoom playerAimZoom;
    private WeaponPresentationController weaponPresentation;
    private bool ownerEffectsEnabled;

    private float bobTime;
    private float breatheTime;
    private float walkWeight;
    private float sprintWeight;
    private float landingOffset;
    private float landingPitchOffset;
    private float jumpOffset;
    private Vector3 currentPosition;
    private Vector3 currentEuler;

    public Transform LookRoot => lookRoot;
    public Transform EffectsRoot => effectsRoot;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        playerAimZoom = GetComponent<PlayerAimZoom>();
        weaponPresentation = GetComponent<WeaponPresentationController>();
        EnsureCameraHierarchy();
    }

    public override void OnNetworkSpawn()
    {
        ownerEffectsEnabled = IsOwner;
        if (!ownerEffectsEnabled)
        {
            ResetEffects();
            enabled = false;
            return;
        }

        if (movement != null)
        {
            movement.Landed += OnLanded;
            movement.Jumped += OnJumped;
            movement.DolphinDiveLanded += OnDiveLanded;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (movement != null)
        {
            movement.Landed -= OnLanded;
            movement.Jumped -= OnJumped;
            movement.DolphinDiveLanded -= OnDiveLanded;
        }

        ownerEffectsEnabled = false;
        ResetEffects();
    }

    private void LateUpdate()
    {
        if (!ownerEffectsEnabled || effectsRoot == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            ResetEffects();
            return;
        }

        float dt = Time.deltaTime;
        bool grounded = movement == null || movement.Grounded;
        float speed = movement != null ? movement.HorizontalSpeed : 0f;
        bool sprinting = movement != null && movement.IsSprinting;
        Vector2 moveInput = movement != null ? movement.MoveInput : Vector2.zero;
        float aimMul = 1f;
        if (weaponPresentation != null)
            aimMul = weaponPresentation.CurrentBobMultiplier;
        else if (IsAiming())
            aimMul = aimingMotionMultiplier;

        float targetWalk = grounded && speed > idleSpeedThreshold ? 1f : 0f;
        float targetSprint = grounded && sprinting ? 1f : 0f;
        walkWeight = Mathf.MoveTowards(walkWeight, targetWalk, motionBlendSpeed * dt);
        sprintWeight = Mathf.MoveTowards(sprintWeight, targetSprint, motionBlendSpeed * dt);

        breatheTime += dt * breathingFrequency * Mathf.PI * 2f;
        float idleWeight = (1f - walkWeight) * (grounded ? 1f : 0f);

        Vector3 targetPos = Vector3.zero;
        Vector3 targetEuler = Vector3.zero;

        float breathe = Mathf.Sin(breatheTime);
        float breatheCos = Mathf.Cos(breatheTime * 0.5f);
        targetPos.y += breathe * breathingAmplitude * idleWeight;
        targetPos.x += breatheCos * breathingAmplitude * 0.45f * idleWeight;
        targetEuler.x += breatheCos * breathingRotation * idleWeight;

        float bobAmp = Mathf.Lerp(walkBobAmplitude, sprintBobAmplitude, sprintWeight) * walkWeight * aimMul;
        float swayAmp = Mathf.Lerp(walkSwayAmplitude, sprintSwayAmplitude, sprintWeight) * walkWeight * aimMul;
        float bobFreq = Mathf.Lerp(walkBobFrequency, sprintBobFrequency, sprintWeight);
        float speedScale = movement != null
            ? Mathf.Clamp(speed / Mathf.Max(0.01f, movement.WalkSpeed), 0.35f, 1.6f)
            : 1f;
        bobTime += dt * bobFreq * speedScale * Mathf.PI * 2f * walkWeight;

        float bobSin = Mathf.Sin(bobTime);
        float bobCos = Mathf.Cos(bobTime);
        targetPos.y += Mathf.Abs(bobSin) * bobAmp;
        targetPos.x += bobCos * swayAmp;
        targetPos.z += sprintWeight * sprintForwardMotion * walkWeight * aimMul;
        targetEuler.x += bobSin * bobAmp * 12f;
        float rollAmount = Mathf.Lerp(walkRoll, sprintRoll, sprintWeight);
        targetEuler.z += -moveInput.x * rollAmount * walkWeight * aimMul;
        targetEuler.z += bobCos * swayAmp * 20f * sprintWeight;

        landingOffset = Mathf.MoveTowards(landingOffset, 0f, landingRecoverySpeed * dt);
        landingPitchOffset = Mathf.MoveTowards(landingPitchOffset, 0f, landingRecoverySpeed * 8f * dt);
        jumpOffset = Mathf.MoveTowards(jumpOffset, 0f, jumpRecoverySpeed * dt);
        targetPos.y += landingOffset + jumpOffset;
        targetEuler.x += landingPitchOffset;

        bool diving = movement != null && movement.IsDolphinDiving;
        if (diving)
        {
            targetPos.z += diveForwardMotion;
            targetEuler.x += divePitch;
            targetEuler.z += -moveInput.x * diveRoll;
        }

        currentPosition = Vector3.Lerp(currentPosition, targetPos, 1f - Mathf.Exp(-14f * dt));
        currentEuler = Vector3.Lerp(currentEuler, targetEuler, 1f - Mathf.Exp(-12f * dt));

        effectsRoot.localPosition = currentPosition;
        effectsRoot.localRotation = Quaternion.Euler(currentEuler);
    }

    public void SetHierarchy(Transform newLookRoot, Transform newEffectsRoot, Camera camera)
    {
        lookRoot = newLookRoot;
        effectsRoot = newEffectsRoot;
        playerCamera = camera;
        RetargetGameplayLook();
    }

    private void OnLanded(float downwardSpeed)
    {
        if (!ownerEffectsEnabled || downwardSpeed < minLandingSpeed)
            return;

        float scale = Mathf.InverseLerp(minLandingSpeed, fullLandingSpeed, downwardSpeed);
        landingOffset = -landingIntensity * Mathf.Lerp(0.35f, 1f, scale);
        landingPitchOffset = landingPitch * Mathf.Lerp(0.35f, 1f, scale);
    }

    private void OnDiveLanded()
    {
        if (!ownerEffectsEnabled)
            return;

        landingOffset = -landingIntensity * 0.85f;
        landingPitchOffset = landingPitch * 0.85f;
    }

    private void OnJumped()
    {
        if (!ownerEffectsEnabled)
            return;

        jumpOffset = jumpDepartureAmount;
    }

    private void ResetEffects()
    {
        walkWeight = 0f;
        sprintWeight = 0f;
        landingOffset = 0f;
        landingPitchOffset = 0f;
        jumpOffset = 0f;
        currentPosition = Vector3.zero;
        currentEuler = Vector3.zero;
        if (effectsRoot != null)
        {
            effectsRoot.localPosition = Vector3.zero;
            effectsRoot.localRotation = Quaternion.identity;
        }
    }

    private bool IsAiming()
    {
        if (playerAimZoom != null)
            return playerAimZoom.IsAiming;
        if (weaponPresentation != null)
            return weaponPresentation.IsAiming;
        return false;
    }

    private void EnsureCameraHierarchy()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
            return;

        if (effectsRoot != null && lookRoot != null)
        {
            RetargetGameplayLook();
            return;
        }

        Transform camTransform = playerCamera.transform;
        if (camTransform.parent != null && camTransform.parent.name == "CameraEffectsRoot")
        {
            effectsRoot = camTransform.parent;
            lookRoot = effectsRoot.parent;
            RetargetGameplayLook();
            return;
        }

        Transform originalParent = camTransform.parent;
        int siblingIndex = camTransform.GetSiblingIndex();
        Vector3 localPos = camTransform.localPosition;
        Quaternion localRot = camTransform.localRotation;

        Transform createdLook = lookRoot;
        if (createdLook == null)
        {
            GameObject lookObject = new GameObject("CameraRoot");
            createdLook = lookObject.transform;
            createdLook.SetParent(originalParent, false);
            createdLook.localPosition = localPos;
            createdLook.localRotation = localRot;
            createdLook.SetSiblingIndex(siblingIndex);
        }

        Transform createdEffects = effectsRoot;
        if (createdEffects == null)
        {
            GameObject effectsObject = new GameObject("CameraEffectsRoot");
            createdEffects = effectsObject.transform;
            createdEffects.SetParent(createdLook, false);
        }

        camTransform.SetParent(createdEffects, false);
        camTransform.localPosition = Vector3.zero;
        camTransform.localRotation = Quaternion.identity;

        lookRoot = createdLook;
        effectsRoot = createdEffects;
        RetargetGameplayLook();
    }

    private void RetargetGameplayLook()
    {
        if (lookRoot == null)
            return;

        if (TryGetComponent(out PlayerLook look))
            look.SetLookTransform(lookRoot);

        if (TryGetComponent(out PlayerMovement playerMovement))
            playerMovement.SetCameraTransform(lookRoot);
    }
}
