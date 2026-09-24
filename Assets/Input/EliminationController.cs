using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// REQ-066 elimination presentation. Gameplay death and respawn stay on
/// PlayerHealth; this component freezes the pose, runs the local orbit camera,
/// and plays the digital despawn on the shared server timeline.
/// </summary>
[DefaultExecutionOrder(120)]
public class EliminationController : NetworkBehaviour
{
    [Header("Timing")]
    [SerializeField] private float frozenDuration = 2.5f;
    [SerializeField] private float dissolveDuration = 2.5f;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference lookAction;
    [SerializeField] private float cameraTransitionDuration = 0.5f;
    [SerializeField] private float eliminationCameraDistance = 3.4f;
    [SerializeField] private float eliminationCameraMinDistance = 1.4f;
    [SerializeField] private float eliminationCameraMaxDistance = 5.5f;
    [SerializeField] private float eliminationCameraSensitivity = 1f;
    [SerializeField] private float eliminationCameraMinPitch = -25f;
    [SerializeField] private float eliminationCameraMaxPitch = 55f;

    [Header("Digital Despawn")]
    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private ParticleSystem digitalDespawnParticles;
    [SerializeField] private DigitalDespawnDirection dissolveDirection = DigitalDespawnDirection.BottomToTop;

    [Header("Audio")]
    [SerializeField] private AudioClip freezeSound;
    [SerializeField] private AudioClip digitalDespawnSound;
    [SerializeField, Range(0f, 1f)] private float effectVolume = 0.85f;

    [Header("Debug")]
    [SerializeField] private bool logEliminationPresentation;

    private readonly NetworkVariable<int> frozenAnimatorState = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> frozenNormalizedTime = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly List<Collider> disabledColliders = new();
    private readonly List<Animator> frozenAnimators = new();
    private readonly List<float> frozenAnimatorSpeeds = new();

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private BullseyeShatterController shatterController;
    private PlayerThirdPersonAnimator thirdPersonAnimator;
    private EliminationCamera eliminationCamera;
    private DigitalDespawnPresenter despawnPresenter;
    private WorldWeaponView worldWeapon;
    private ThirdPersonWeaponRig thirdPersonRig;
    private Rigidbody body;
    private bool presenting;
    private bool visualsHidden;
    private bool despawnStarted;
    private bool freezeSoundPlayed;
    private bool despawnSoundPlayed;
    private bool reportedPose;
    private bool collisionDisabled;
    private bool detectCollisionsWasEnabled;
    private bool hasDetectCollisionsState;

    public float FrozenDuration => Mathf.Max(0f, frozenDuration);
    public float DissolveDuration => Mathf.Max(0f, dissolveDuration);
    public float TotalDuration => FrozenDuration + DissolveDuration;
    public float CameraTransitionDuration => Mathf.Clamp(cameraTransitionDuration, 0.25f, 0.75f);
    public bool AreVisualsHidden => visualsHidden;
    public bool ShowOwnerWorldWeapon => presenting && !visualsHidden;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        shatterController = GetComponent<BullseyeShatterController>();
        thirdPersonAnimator = GetComponent<PlayerThirdPersonAnimator>();
        eliminationCamera = GetComponent<EliminationCamera>();
        despawnPresenter = GetComponent<DigitalDespawnPresenter>();
        worldWeapon = GetComponent<WorldWeaponView>();
        thirdPersonRig = GetComponent<ThirdPersonWeaponRig>();
        body = GetComponent<Rigidbody>();

        if (eliminationCamera == null)
            eliminationCamera = gameObject.AddComponent<EliminationCamera>();
        if (despawnPresenter == null)
            despawnPresenter = gameObject.AddComponent<DigitalDespawnPresenter>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        ApplyInspectorToHelpers();
    }

    public override void OnNetworkSpawn()
    {
        frozenAnimatorState.OnValueChanged += OnFrozenPoseChanged;
        frozenNormalizedTime.OnValueChanged += OnFrozenTimeChanged;
        if (frozenAnimatorState.Value != 0)
            ApplyNetworkedPose();
    }

    public override void OnNetworkDespawn()
    {
        frozenAnimatorState.OnValueChanged -= OnFrozenPoseChanged;
        frozenNormalizedTime.OnValueChanged -= OnFrozenTimeChanged;
        CleanupForDespawn();
    }

    private void LateUpdate()
    {
        if (!presenting || playerHealth == null)
            return;

        TickTimeline(playerHealth.GetElapsedDeathTime());
    }

    public void HandleDeadChanged(bool dead)
    {
        if (dead)
            BeginPresentation();
        else
            RestoreAfterRespawn();
    }

    public void CleanupForDespawn()
    {
        RestoreAfterRespawn();
    }

    private void BeginPresentation()
    {
        if (presenting)
            return;

        presenting = true;
        visualsHidden = false;
        despawnStarted = false;
        freezeSoundPlayed = false;
        despawnSoundPlayed = false;
        reportedPose = false;

        ApplyInspectorToHelpers();
        shatterController?.PlayEliminationShatter();
        FreezeGameplay();
        DisableGameplayCollision();
        ReportOwnerPose();
        PlayFreezeSound();

        if (playerHealth != null && playerHealth.IsOwner)
        {
            worldWeapon?.BeginOwnerEliminationPresentation();
            thirdPersonRig?.AttachWeaponToFrozenHands();
            eliminationCamera?.Begin();
        }

        worldWeapon?.FreezeWeaponAnimator();
        TickTimeline(playerHealth != null ? playerHealth.GetElapsedDeathTime() : 0f);
        LogPresentation("Elimination presentation started.");
    }

    private void TickTimeline(float elapsed)
    {
        if (elapsed >= FrozenDuration && !despawnStarted)
            BeginDigitalDespawn();

        if (despawnStarted && despawnPresenter != null)
        {
            float duration = Mathf.Max(0.0001f, DissolveDuration);
            despawnPresenter.SetProgress(Mathf.Clamp01((elapsed - FrozenDuration) / duration));
        }

        if (elapsed >= TotalDuration - 0.01f)
            HideRemainingVisuals();
    }

    private void BeginDigitalDespawn()
    {
        if (despawnStarted)
            return;

        despawnStarted = true;
        despawnPresenter?.Begin(dissolveDirection);
        PlayDespawnSound();
        LogPresentation("Digital despawn started.");
    }

    private void HideRemainingVisuals()
    {
        if (visualsHidden)
            return;

        visualsHidden = true;
        despawnPresenter?.SetProgress(1f);
    }

    public void RestoreAfterRespawn()
    {
        if (!presenting && !collisionDisabled && (despawnPresenter == null || !despawnPresenter.IsPresenting))
        {
            eliminationCamera?.Restore();
            return;
        }

        presenting = false;
        visualsHidden = false;
        despawnStarted = false;
        freezeSoundPlayed = false;
        despawnSoundPlayed = false;
        reportedPose = false;

        shatterController?.RestoreBullseyeAfterElimination();
        despawnPresenter?.Restore();
        eliminationCamera?.Restore();
        RestoreAnimators();
        RestoreGameplayCollision();
        worldWeapon?.EndOwnerEliminationPresentation();
        worldWeapon?.RestoreWeaponAnimator();

        if (IsServer)
        {
            frozenAnimatorState.Value = 0;
            frozenNormalizedTime.Value = 0f;
        }

        LogPresentation("Elimination presentation restored.");
    }

    private void FreezeGameplay()
    {
        if (playerMovement != null)
            playerMovement.FreezeForDeath();

        FreezeAnimators();
    }

    private void FreezeAnimators()
    {
        RestoreAnimators();
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || !animator.enabled)
                continue;
            if (IsFirstPersonAnimator(animator.transform))
                continue;

            frozenAnimators.Add(animator);
            frozenAnimatorSpeeds.Add(animator.speed);
            animator.speed = 0f;
        }
    }

    private void RestoreAnimators()
    {
        for (int i = 0; i < frozenAnimators.Count; i++)
        {
            Animator animator = frozenAnimators[i];
            if (animator == null)
                continue;
            animator.speed = i < frozenAnimatorSpeeds.Count ? frozenAnimatorSpeeds[i] : 1f;
            if (Mathf.Abs(animator.speed) < 0.001f)
                animator.speed = 1f;
        }

        frozenAnimators.Clear();
        frozenAnimatorSpeeds.Clear();
    }

    private void DisableGameplayCollision()
    {
        if (collisionDisabled)
            return;

        disabledColliders.Clear();
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
                continue;
            if (collider.GetComponentInParent<Canvas>() != null)
                continue;

            collider.enabled = false;
            disabledColliders.Add(collider);
        }

        if (body != null)
        {
            detectCollisionsWasEnabled = body.detectCollisions;
            hasDetectCollisionsState = true;
            body.detectCollisions = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        collisionDisabled = true;
    }

    private void RestoreGameplayCollision()
    {
        for (int i = 0; i < disabledColliders.Count; i++)
        {
            if (disabledColliders[i] != null)
                disabledColliders[i].enabled = true;
        }

        disabledColliders.Clear();

        if (body != null && hasDetectCollisionsState)
            body.detectCollisions = detectCollisionsWasEnabled;

        hasDetectCollisionsState = false;
        collisionDisabled = false;
    }

    private void ReportOwnerPose()
    {
        if (reportedPose)
            return;
        if (playerHealth == null || !playerHealth.IsOwner)
            return;
        if (thirdPersonAnimator == null || !thirdPersonAnimator.TryCapturePose(out int hash, out float time))
            return;

        reportedPose = true;
        ReportFrozenPoseServerRpc(hash, time);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void ReportFrozenPoseServerRpc(int stateHash, float normalizedTime)
    {
        frozenAnimatorState.Value = stateHash;
        frozenNormalizedTime.Value = Mathf.Clamp01(normalizedTime);
    }

    private void OnFrozenPoseChanged(int previous, int next)
    {
        if (next != 0)
            ApplyNetworkedPose();
    }

    private void OnFrozenTimeChanged(float previous, float next)
    {
        if (frozenAnimatorState.Value != 0)
            ApplyNetworkedPose();
    }

    private void ApplyNetworkedPose()
    {
        if (thirdPersonAnimator == null || frozenAnimatorState.Value == 0)
            return;

        thirdPersonAnimator.ApplyFrozenPose(frozenAnimatorState.Value, frozenNormalizedTime.Value);
    }

    private void PlayFreezeSound()
    {
        if (freezeSoundPlayed || freezeSound == null)
            return;

        freezeSoundPlayed = true;
        AudioSource.PlayClipAtPoint(freezeSound, transform.position, Mathf.Clamp01(effectVolume));
    }

    private void PlayDespawnSound()
    {
        if (despawnSoundPlayed || digitalDespawnSound == null)
            return;

        despawnSoundPlayed = true;
        AudioSource.PlayClipAtPoint(digitalDespawnSound, transform.position, Mathf.Clamp01(effectVolume));
    }

    private void ApplyInspectorToHelpers()
    {
        eliminationCamera?.Configure(
            playerCamera,
            transform.Find("VisualRoot"),
            lookAction,
            eliminationCameraDistance,
            eliminationCameraMinDistance,
            eliminationCameraMaxDistance,
            eliminationCameraSensitivity,
            eliminationCameraMinPitch,
            eliminationCameraMaxPitch,
            CameraTransitionDuration);

        despawnPresenter?.Configure(dissolveMaterial, digitalDespawnParticles, dissolveDirection);
    }

    private static bool IsFirstPersonAnimator(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == "WeaponView" || current.name == "FirstPersonWeaponCamera")
                return true;
            current = current.parent;
        }

        return false;
    }

    private void LogPresentation(string message)
    {
        if (logEliminationPresentation)
            Debug.Log("[Elimination] " + message, this);
    }

    private void OnValidate()
    {
        frozenDuration = Mathf.Max(0f, frozenDuration);
        dissolveDuration = Mathf.Max(0f, dissolveDuration);
        cameraTransitionDuration = Mathf.Clamp(cameraTransitionDuration, 0.25f, 0.75f);
        eliminationCameraDistance = Mathf.Max(0.5f, eliminationCameraDistance);
        eliminationCameraMinDistance = Mathf.Max(0.35f, eliminationCameraMinDistance);
        eliminationCameraMaxDistance = Mathf.Max(eliminationCameraMinDistance, eliminationCameraMaxDistance);
        eliminationCameraSensitivity = Mathf.Max(0.05f, eliminationCameraSensitivity);
        eliminationCameraMinPitch = Mathf.Clamp(eliminationCameraMinPitch, -80f, 80f);
        eliminationCameraMaxPitch = Mathf.Clamp(eliminationCameraMaxPitch, eliminationCameraMinPitch, 80f);
        effectVolume = Mathf.Clamp01(effectVolume);
    }
}
