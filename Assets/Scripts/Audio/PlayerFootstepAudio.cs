using UnityEngine;

/// <summary>
/// Plays walk / sprint footsteps from FootstepAudioSettings.
/// Uses networked animation locomotion so remotes hear steps without extra RPCs.
/// </summary>
[DefaultExecutionOrder(60)]
public class PlayerFootstepAudio : MonoBehaviour
{
    private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

    [SerializeField] private FootstepAudioSettings settings;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Min(0.05f)] private float probeHeight = 0.45f;
    [SerializeField, Min(0.2f)] private float probeDistance = 1.6f;
    [SerializeField, Range(0f, 1f), Tooltip("Local player footsteps stay mostly 2D. Remotes are fully 3D.")]
    private float ownerSpatialBlend = 0.22f;

    private PlayerMovement movement;
    private PlayerAnimationState animationState;
    private PlayerHealth health;
    private AudioSource audioSource;
    private float stepTimer;
    private bool wasStepping;
    private AudioClip lastClip;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        animationState = GetComponent<PlayerAnimationState>();
        health = GetComponent<PlayerHealth>();
        EnsureSource();
    }

    private void Update()
    {
        FootstepAudioSettings resolved = ResolveSettings();
        if (resolved == null || !CanStep(resolved))
        {
            stepTimer = 0f;
            wasStepping = false;
            return;
        }

        bool sprinting = IsSprintingNow();
        bool crouched = movement != null && movement.IsCrouched;
        float interval = resolved.ResolveInterval(sprinting, crouched);

        if (!wasStepping)
        {
            PlayStep(resolved, sprinting, crouched);
            stepTimer = 0f;
            wasStepping = true;
            return;
        }

        stepTimer += Time.deltaTime;
        if (stepTimer < interval)
            return;

        stepTimer -= interval;
        PlayStep(resolved, sprinting, crouched);
    }

    private bool CanStep(FootstepAudioSettings resolved)
    {
        if (health != null && health.IsDead)
            return false;
        if (!IsGroundedNow())
            return false;
        if (movement != null)
        {
            if (movement.IsProne || movement.IsDolphinDiving)
                return false;
            if (movement.IsSliding || movement.IsWallRunning)
                return false;
        }

        return MoveSpeedNow() >= resolved.MinimumMoveSpeed;
    }

    private void PlayStep(FootstepAudioSettings resolved, bool sprinting, bool crouched)
    {
        FootstepSurfaceKind kind = ProbeSurface();
        FootstepClipSet set = resolved.GetSet(kind);
        AudioClip[] clips = set != null ? set.Resolve(sprinting) : null;
        AudioClip clip = PickClip(clips);
        if (clip == null)
            return;

        EnsureSource();
        if (audioSource == null)
            return;

        audioSource.spatialBlend = IsLocalListener() ? Mathf.Clamp01(ownerSpatialBlend) : 1f;
        audioSource.minDistance = resolved.MinDistance;
        audioSource.maxDistance = resolved.MaxDistance;
        audioSource.pitch = resolved.ResolvePitch(sprinting);
        audioSource.volume = resolved.ResolveVolume(sprinting, crouched);
        PlayerGameSettings.RouteToSfx(audioSource);
        audioSource.PlayOneShot(clip);
        lastClip = clip;
    }

    private FootstepSurfaceKind ProbeSurface()
    {
        Vector3 origin = transform.position + Vector3.up * Mathf.Max(0.05f, probeHeight);
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            GroundHits,
            Mathf.Max(0.2f, probeDistance),
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        Collider best = null;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.collider == null)
                continue;
            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                continue;
            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            best = hit.collider;
        }

        return FootstepSurface.TryGet(best, out FootstepSurfaceKind kind)
            ? kind
            : FootstepSurfaceKind.Default;
    }

    private AudioClip PickClip(AudioClip[] clips)
    {
        if (!FootstepClipSet.HasAny(clips))
            return null;

        int usable = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                usable++;
        }

        if (usable <= 0)
            return null;

        int skipLast = lastClip != null && usable > 1 ? 1 : 0;
        int choice = Random.Range(0, usable - skipLast);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[i];
            if (clip == null || (skipLast > 0 && clip == lastClip))
                continue;
            if (choice == 0)
                return clip;
            choice--;
        }

        return lastClip;
    }

    private bool IsGroundedNow()
    {
        if (animationState != null && animationState.isActiveAndEnabled && animationState.IsSpawned)
            return animationState.IsGrounded;
        return movement == null || movement.Grounded;
    }

    private bool IsSprintingNow()
    {
        if (animationState != null && animationState.isActiveAndEnabled && animationState.IsSpawned)
            return animationState.IsSprinting;
        return movement != null && movement.IsSprinting;
    }

    private float MoveSpeedNow()
    {
        if (animationState != null && animationState.isActiveAndEnabled && animationState.IsSpawned)
            return animationState.Speed;
        return movement != null ? movement.HorizontalSpeed : 0f;
    }

    private bool IsLocalListener()
    {
        if (animationState != null && animationState.IsSpawned)
            return animationState.IsOwner;
        if (movement != null && movement.IsSpawned)
            return movement.IsOwner;
        return true;
    }

    private FootstepAudioSettings ResolveSettings()
    {
        if (settings == null)
            settings = FootstepAudioSettings.Load();
        return settings;
    }

    private void EnsureSource()
    {
        if (audioSource != null)
            return;

        Transform existing = transform.Find("FootstepAudio");
        GameObject host = existing != null ? existing.gameObject : new GameObject("FootstepAudio");
        if (existing == null)
            host.transform.SetParent(transform, false);

        audioSource = host.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = host.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.dopplerLevel = 0f;
        audioSource.spread = 0f;
        audioSource.priority = 128;
        PlayerGameSettings.RouteToSfx(audioSource);
    }
}
