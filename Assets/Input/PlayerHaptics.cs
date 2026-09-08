using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Haptics;

/// <summary>
/// Local-player controller rumble mixer. Gameplay systems request named
/// effects; this component owns motor writes, priority, and shutdown.
/// </summary>
[DefaultExecutionOrder(50)]
public class PlayerHaptics : MonoBehaviour
{
    public enum HapticPriority
    {
        SprintStep = 10,
        SprintStart = 20,
        Jump = 30,
        Fire = 34,
        WallRun = 36,
        Landing = 40,
        Damage = 45,
        Explosion = 60
    }

    [Serializable]
    public struct RumbleSettings
    {
        [Range(0f, 1f)] public float lowFrequency;
        [Range(0f, 1f)] public float highFrequency;
        [Min(0f)] public float duration;
    }

    private sealed class PlayingEffect
    {
        public HapticPriority Priority;
        public float Duration;
        public float Elapsed;
        public float Low;
        public float High;
        public float Scale;
        public AnimationCurve Envelope;
        public bool IncludeMouse;
    }

    private static readonly List<PlayerHaptics> ActiveControllers = new();

    [Header("General")]
    [SerializeField, Range(0f, 1f)] private float masterHapticStrength = 1f;
    [SerializeField] private bool hapticsEnabled = true;

    [Header("Fire Rumble")]
    [SerializeField] private RumbleSettings fireRumble = new()
    {
        lowFrequency = 0.5f,
        highFrequency = 0.75f,
        duration = 0.1f
    };

    [Header("Damage Rumble")]
    [SerializeField] private RumbleSettings damageRumble = new()
    {
        lowFrequency = 0.22f,
        highFrequency = 0.3f,
        duration = 0.15f
    };

    [Header("Wall Run Rumble")]
    [SerializeField] private RumbleSettings wallRunRumble = new()
    {
        lowFrequency = 0.2f,
        highFrequency = 0.32f,
        duration = 0.12f
    };

    [Header("Sprint Start")]
    [SerializeField] private RumbleSettings sprintStartRumble = new()
    {
        lowFrequency = 0.18f,
        highFrequency = 0.28f,
        duration = 0.12f
    };

    [Header("Sprint Steps")]
    [SerializeField] private RumbleSettings sprintStepRumble = new()
    {
        lowFrequency = 0.08f,
        highFrequency = 0.12f,
        duration = 0.05f
    };
    [SerializeField, Min(0.05f)] private float sprintStepInterval = 0.34f;
    [SerializeField] private bool alternateSprintStepMotors = true;

    [Header("Jump")]
    [SerializeField] private RumbleSettings jumpRumble = new()
    {
        lowFrequency = 0.16f,
        highFrequency = 0.22f,
        duration = 0.1f
    };

    [Header("Landing")]
    [SerializeField] private RumbleSettings landingMinRumble = new()
    {
        lowFrequency = 0.28f,
        highFrequency = 0.22f,
        duration = 0.12f
    };
    [SerializeField] private RumbleSettings landingMaxRumble = new()
    {
        lowFrequency = 0.62f,
        highFrequency = 0.38f,
        duration = 0.2f
    };
    [SerializeField, Min(0f)] private float minimumLandingVelocity = 2.5f;
    [SerializeField, Min(0.1f)] private float maximumLandingVelocity = 18f;
    [SerializeField, Min(0f)] private float minimumAirborneTimeForLanding = 0.1f;

    [Header("Explosion")]
    [SerializeField, Range(0f, 1f)] private float explosionLowMotorStrength = 0.95f;
    [SerializeField, Range(0f, 1f)] private float explosionHighMotorStrength = 0.72f;
    [SerializeField, Min(0.05f)] private float explosionDuration = 0.4f;
    [SerializeField] private AnimationCurve explosionEnvelope;
    [SerializeField] private AnimationCurve explosionDistanceFalloff;
    [SerializeField, Min(0.1f)] private float defaultExplosionHapticRadius = 16f;

    private readonly List<PlayingEffect> playingEffects = new();
    private LocalPlayerInputBinding inputBinding;
    private PlayerMovement movement;
    private PlayerHealth playerHealth;
    private Gamepad rumblingGamepad;
    private IDualMotorRumble rumblingMouse;
    private bool motorsActive;
    private bool wasSprinting;
    private bool wasGrounded = true;
    private float airborneStartTime = -1f;
    private float sprintStepTimer;
    private bool sprintStepIsLeft = true;

    public static void NotifyLocalExplosion(Vector3 origin, float hapticRadius, float intensityMultiplier = 1f)
    {
        for (int i = 0; i < ActiveControllers.Count; i++)
        {
            PlayerHaptics haptics = ActiveControllers[i];
            if (haptics != null)
                haptics.PlayExplosion(origin, hapticRadius, intensityMultiplier);
        }
    }

    private void Awake()
    {
        inputBinding = GetComponent<LocalPlayerInputBinding>();
        movement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        EnsureCurves();
    }

    private void OnEnable()
    {
        if (!ActiveControllers.Contains(this))
            ActiveControllers.Add(this);

        InputSystem.onDeviceChange += OnDeviceChange;
        BindMovementEvents(true);
        wasSprinting = movement != null && movement.IsSprinting;
        wasGrounded = movement == null || movement.Grounded;
        airborneStartTime = wasGrounded ? -1f : Time.time;
    }

    private void OnDisable()
    {
        ActiveControllers.Remove(this);
        InputSystem.onDeviceChange -= OnDeviceChange;
        BindMovementEvents(false);
        StopHaptics();
    }

    private void OnDestroy()
    {
        ActiveControllers.Remove(this);
        InputSystem.onDeviceChange -= OnDeviceChange;
        BindMovementEvents(false);
        StopHaptics();
    }

    private void OnApplicationQuit()
    {
        StopHaptics();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            StopHaptics();
    }

    private void Update()
    {
        if (ShouldSuppressHaptics())
        {
            if (playerHealth != null && playerHealth.IsDead)
                ResetSprintTracking();
            StopHaptics();
            return;
        }

        TickPlayingEffects(Time.deltaTime);
        TickSprintHaptics();
        ApplyMotors();
    }

    public void PlayFireRumble()
    {
        PlayRumble(fireRumble, HapticPriority.Fire);
    }

    public void PlayDamageRumble()
    {
        PlayRumble(damageRumble, HapticPriority.Damage);
    }

    public void PlayWallRunRumble()
    {
        PlayRumble(wallRunRumble, HapticPriority.WallRun, includeMouse: true);
    }

    public void PlaySprintStart()
    {
        PlayRumble(sprintStartRumble, HapticPriority.SprintStart);
    }

    public void PlaySprintStep()
    {
        float low = sprintStepRumble.lowFrequency;
        float high = sprintStepRumble.highFrequency;
        if (alternateSprintStepMotors)
        {
            if (sprintStepIsLeft)
            {
                low *= 1.2f;
                high *= 0.7f;
            }
            else
            {
                low *= 0.7f;
                high *= 1.2f;
            }

            sprintStepIsLeft = !sprintStepIsLeft;
        }

        PlayRumble(low, high, sprintStepRumble.duration, HapticPriority.SprintStep);
    }

    public void PlayJump()
    {
        PlayRumble(jumpRumble, HapticPriority.Jump);
    }

    public void PlayLanding(float downwardSpeed)
    {
        float t = Mathf.InverseLerp(minimumLandingVelocity, maximumLandingVelocity, Mathf.Max(0f, downwardSpeed));
        float low = Mathf.Lerp(landingMinRumble.lowFrequency, landingMaxRumble.lowFrequency, t);
        float high = Mathf.Lerp(landingMinRumble.highFrequency, landingMaxRumble.highFrequency, t);
        float duration = Mathf.Lerp(landingMinRumble.duration, landingMaxRumble.duration, t);
        PlayRumble(low, high, duration, HapticPriority.Landing);
    }

    public void PlayExplosion(Vector3 origin, float hapticRadius, float intensityMultiplier = 1f)
    {
        if (!CanPlayHaptics())
            return;

        float radius = hapticRadius > 0.01f ? hapticRadius : defaultExplosionHapticRadius;
        Vector3 playerPosition = transform.position + Vector3.up;
        float distance = Vector3.Distance(origin, playerPosition);
        if (distance > radius)
            return;

        EnsureCurves();
        float distance01 = Mathf.Clamp01(distance / Mathf.Max(0.01f, radius));
        float falloff = explosionDistanceFalloff.Evaluate(distance01);
        float scale = Mathf.Clamp01(falloff * Mathf.Max(0f, intensityMultiplier));
        if (scale < 0.02f)
            return;

        EnqueueEffect(
            HapticPriority.Explosion,
            explosionLowMotorStrength,
            explosionHighMotorStrength,
            explosionDuration,
            scale,
            explosionEnvelope,
            includeMouse: false);
        ApplyMotors();
    }

    public void PlayRumble(RumbleSettings settings)
    {
        PlayRumble(settings, HapticPriority.Fire);
    }

    public void PlayRumble(float lowFrequency, float highFrequency, float duration)
    {
        PlayRumble(lowFrequency, highFrequency, duration, HapticPriority.Fire);
    }

    public void PlayRumble(RumbleSettings settings, bool includeMouse)
    {
        PlayRumble(settings, includeMouse ? HapticPriority.WallRun : HapticPriority.Fire, includeMouse);
    }

    public void PlayRumble(float lowFrequency, float highFrequency, float duration, bool includeMouse)
    {
        PlayRumble(lowFrequency, highFrequency, duration, includeMouse ? HapticPriority.WallRun : HapticPriority.Fire, includeMouse);
    }

    public void PlayRumble(RumbleSettings settings, HapticPriority priority, bool includeMouse = false)
    {
        PlayRumble(settings.lowFrequency, settings.highFrequency, settings.duration, priority, includeMouse);
    }

    public void PlayRumble(float lowFrequency, float highFrequency, float duration, HapticPriority priority, bool includeMouse = false)
    {
        if (!CanPlayHaptics())
            return;

        EnqueueEffect(priority, lowFrequency, highFrequency, duration, 1f, null, includeMouse);
        ApplyMotors();
    }

    public void StopRumble()
    {
        StopHaptics();
    }

    public void StopHaptics()
    {
        playingEffects.Clear();
        ResetSprintStepTimer();
        ResetMotors();
    }

    private void BindMovementEvents(bool bind)
    {
        if (movement == null)
            return;

        if (bind)
        {
            movement.WallRunStarted += PlayWallRunRumble;
            movement.Jumped += OnJumped;
            movement.Landed += OnLanded;
            return;
        }

        movement.WallRunStarted -= PlayWallRunRumble;
        movement.Jumped -= OnJumped;
        movement.Landed -= OnLanded;
    }

    private void OnJumped()
    {
        sprintStepTimer = 0f;
        PlayJump();
    }

    private void OnLanded(float downwardSpeed)
    {
        float airborneTime = airborneStartTime >= 0f ? Time.time - airborneStartTime : 0f;
        bool tooBrief = airborneTime < minimumAirborneTimeForLanding;
        bool tooSoft = downwardSpeed < minimumLandingVelocity;
        if (tooBrief && tooSoft)
            return;

        PlayLanding(downwardSpeed);
    }

    private void TickSprintHaptics()
    {
        bool sprinting = movement != null && movement.IsSprinting && CanUseSprintHaptics();
        if (!wasSprinting && sprinting && movement.Grounded)
            PlaySprintStart();

        wasSprinting = movement != null && movement.IsSprinting;

        bool grounded = movement != null && movement.Grounded;
        if (grounded)
        {
            airborneStartTime = -1f;
        }
        else if (wasGrounded)
        {
            airborneStartTime = Time.time;
        }

        wasGrounded = grounded;

        if (!CanPlaySprintSteps())
        {
            ResetSprintStepTimer();
            return;
        }

        sprintStepTimer += Time.deltaTime;
        float interval = Mathf.Max(0.05f, sprintStepInterval);
        if (sprintStepTimer < interval)
            return;

        sprintStepTimer -= interval;
        PlaySprintStep();
    }

    private bool CanUseSprintHaptics()
    {
        if (movement == null)
            return false;
        if (movement.IsCrouched || movement.IsProne)
            return false;
        if (movement.IsDolphinDiving || movement.IsWallRunning)
            return false;
        if (playerHealth != null && playerHealth.IsDead)
            return false;
        return true;
    }

    private bool CanPlaySprintSteps()
    {
        return movement != null
               && movement.IsSprinting
               && movement.Grounded
               && CanUseSprintHaptics();
    }

    private void ResetSprintTracking()
    {
        wasSprinting = false;
        ResetSprintStepTimer();
    }

    private void ResetSprintStepTimer()
    {
        sprintStepTimer = 0f;
    }

    private void EnqueueEffect(
        HapticPriority priority,
        float lowFrequency,
        float highFrequency,
        float duration,
        float scale,
        AnimationCurve envelope,
        bool includeMouse)
    {
        float clampedDuration = Mathf.Max(0f, duration);
        if (clampedDuration <= 0f)
            return;

        playingEffects.Add(new PlayingEffect
        {
            Priority = priority,
            Duration = clampedDuration,
            Elapsed = 0f,
            Low = Mathf.Clamp01(lowFrequency),
            High = Mathf.Clamp01(highFrequency),
            Scale = Mathf.Clamp01(scale),
            Envelope = envelope,
            IncludeMouse = includeMouse
        });
    }

    private void TickPlayingEffects(float deltaTime)
    {
        for (int i = playingEffects.Count - 1; i >= 0; i--)
        {
            PlayingEffect effect = playingEffects[i];
            effect.Elapsed += Mathf.Max(0f, deltaTime);
            if (effect.Elapsed >= effect.Duration)
                playingEffects.RemoveAt(i);
        }
    }

    private void ApplyMotors()
    {
        if (!TryGetWinningEffect(out PlayingEffect effect, out float envelope))
        {
            ResetMotors();
            return;
        }

        float master = hapticsEnabled ? Mathf.Clamp01(masterHapticStrength) : 0f;
        float low = Mathf.Clamp01(effect.Low * effect.Scale * envelope * master);
        float high = Mathf.Clamp01(effect.High * effect.Scale * envelope * master);
        if (low <= 0.001f && high <= 0.001f)
        {
            ResetMotors();
            return;
        }

        Gamepad gamepad = ResolveLocalGamepad();
        IDualMotorRumble mouseRumble = effect.IncludeMouse ? ResolveLocalMouseRumble() : null;
        if (gamepad == null && mouseRumble == null)
        {
            ResetMotors();
            return;
        }

        rumblingGamepad = gamepad;
        rumblingMouse = mouseRumble;
        TrySetMotors(gamepad, low, high);
        TrySetMouseMotors(mouseRumble, low, high);
        motorsActive = true;
    }

    private bool TryGetWinningEffect(out PlayingEffect winner, out float envelope)
    {
        winner = null;
        envelope = 0f;
        for (int i = 0; i < playingEffects.Count; i++)
        {
            PlayingEffect candidate = playingEffects[i];
            if (winner == null || candidate.Priority > winner.Priority)
                winner = candidate;
        }

        if (winner == null)
            return false;

        float t = winner.Duration > 0f ? Mathf.Clamp01(winner.Elapsed / winner.Duration) : 1f;
        envelope = winner.Envelope != null && winner.Envelope.length > 0
            ? Mathf.Clamp01(winner.Envelope.Evaluate(t))
            : 1f;
        return true;
    }

    private bool ShouldSuppressHaptics()
    {
        if (!isActiveAndEnabled || !IsLocalOwner())
            return true;
        if (playerHealth != null && playerHealth.IsDead)
            return true;
        if (LocalPlayerMenuState.IsOpen(this))
            return true;
        if (Time.timeScale <= 0f)
            return true;
        return false;
    }

    private bool CanPlayHaptics()
    {
        if (!hapticsEnabled || masterHapticStrength <= 0f)
            return false;
        return !ShouldSuppressHaptics();
    }

    private bool IsLocalOwner()
    {
        return TryGetComponent(out NetworkObject networkObject) && networkObject.IsOwner;
    }

    private Gamepad ResolveLocalGamepad()
    {
        if (inputBinding != null)
            return inputBinding.AssignedGamepad;

        if (NetworkManager.Singleton == null)
            return null;

        int playerIndex = (int)NetworkManager.Singleton.LocalClientId;
        if (playerIndex >= 0 && playerIndex < Gamepad.all.Count)
            return Gamepad.all[playerIndex];

        return null;
    }

    private IDualMotorRumble ResolveLocalMouseRumble()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.added)
            return null;

        return mouse as IDualMotorRumble;
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        bool matchesGamepad = rumblingGamepad != null && device == rumblingGamepad;
        bool matchesMouse = rumblingMouse is InputDevice rumbleDevice && device == rumbleDevice;
        if (!matchesGamepad && !matchesMouse)
            return;

        if (change == InputDeviceChange.Removed ||
            change == InputDeviceChange.Disconnected ||
            change == InputDeviceChange.Disabled)
        {
            StopHaptics();
        }
    }

    private void ResetMotors()
    {
        if (!motorsActive && rumblingGamepad == null && rumblingMouse == null)
            return;

        ResetGamepad(rumblingGamepad);
        ResetMouseRumble(rumblingMouse);
        rumblingGamepad = null;
        rumblingMouse = null;
        motorsActive = false;
    }

    private static void ResetGamepad(Gamepad gamepad)
    {
        TrySetMotors(gamepad, 0f, 0f);

        if (gamepad == null || !gamepad.added)
            return;

        try
        {
            gamepad.ResetHaptics();
        }
        catch (Exception)
        {
        }
    }

    private static void TrySetMotors(Gamepad gamepad, float lowFrequency, float highFrequency)
    {
        if (gamepad == null || !gamepad.added)
            return;

        try
        {
            gamepad.SetMotorSpeeds(lowFrequency, highFrequency);
        }
        catch (Exception)
        {
        }
    }

    private static void TrySetMouseMotors(IDualMotorRumble mouseRumble, float lowFrequency, float highFrequency)
    {
        if (mouseRumble == null)
            return;

        if (mouseRumble is InputDevice device && !device.added)
            return;

        try
        {
            mouseRumble.SetMotorSpeeds(lowFrequency, highFrequency);
        }
        catch (Exception)
        {
        }
    }

    private static void ResetMouseRumble(IDualMotorRumble mouseRumble)
    {
        TrySetMouseMotors(mouseRumble, 0f, 0f);

        if (mouseRumble is not InputDevice device || !device.added)
            return;

        try
        {
            if (device is IHaptics haptics)
                haptics.ResetHaptics();
        }
        catch (Exception)
        {
        }
    }

    private void EnsureCurves()
    {
        if (explosionEnvelope == null || explosionEnvelope.length == 0)
            explosionEnvelope = CreateDefaultExplosionEnvelope();
        if (explosionDistanceFalloff == null || explosionDistanceFalloff.length == 0)
            explosionDistanceFalloff = CreateDefaultExplosionFalloff();
    }

    private static AnimationCurve CreateDefaultExplosionEnvelope()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.25f, 0.55f),
            new Keyframe(0.625f, 0.22f),
            new Keyframe(1f, 0f));
    }

    private static AnimationCurve CreateDefaultExplosionFalloff()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.4f, 0.55f),
            new Keyframe(0.75f, 0.18f),
            new Keyframe(1f, 0f));
    }

    private void OnValidate()
    {
        masterHapticStrength = Mathf.Clamp01(masterHapticStrength);
        ClampRumble(ref fireRumble);
        ClampRumble(ref damageRumble);
        ClampRumble(ref wallRunRumble);
        ClampRumble(ref sprintStartRumble);
        ClampRumble(ref sprintStepRumble);
        ClampRumble(ref jumpRumble);
        ClampRumble(ref landingMinRumble);
        ClampRumble(ref landingMaxRumble);
        sprintStepInterval = Mathf.Max(0.05f, sprintStepInterval);
        minimumLandingVelocity = Mathf.Max(0f, minimumLandingVelocity);
        maximumLandingVelocity = Mathf.Max(minimumLandingVelocity + 0.01f, maximumLandingVelocity);
        minimumAirborneTimeForLanding = Mathf.Max(0f, minimumAirborneTimeForLanding);
        explosionLowMotorStrength = Mathf.Clamp01(explosionLowMotorStrength);
        explosionHighMotorStrength = Mathf.Clamp01(explosionHighMotorStrength);
        explosionDuration = Mathf.Max(0.05f, explosionDuration);
        defaultExplosionHapticRadius = Mathf.Max(0.1f, defaultExplosionHapticRadius);
        EnsureCurves();
    }

    private static void ClampRumble(ref RumbleSettings settings)
    {
        settings.lowFrequency = Mathf.Clamp01(settings.lowFrequency);
        settings.highFrequency = Mathf.Clamp01(settings.highFrequency);
        settings.duration = Mathf.Max(0f, settings.duration);
    }
}
