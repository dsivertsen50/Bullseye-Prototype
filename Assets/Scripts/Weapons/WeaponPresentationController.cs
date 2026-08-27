using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner-only first-person weapon presentation. Visual and audio only;
/// hitscan damage stays on PlayerShoot.
/// </summary>
public class WeaponPresentationController : NetworkBehaviour
{
    [SerializeField] private WeaponPresentationConfig config;
    [SerializeField] private Transform weaponEffectsRoot;
    [SerializeField] private Transform aimRoot;
    [SerializeField] private Transform weaponMount;
    [SerializeField] private Transform weaponKick;
    [SerializeField] private Transform aimPoint;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Prone / Dive Presentation")]
    [SerializeField] private Vector3 proneLocalPosition = new Vector3(0f, -0.04f, 0.02f);
    [SerializeField] private Vector3 proneLocalEuler = new Vector3(6f, 0f, 0f);
    [SerializeField] private Vector3 diveLocalPosition = new Vector3(0.02f, -0.12f, -0.06f);
    [SerializeField] private Vector3 diveLocalEuler = new Vector3(18f, 8f, -12f);

    private static readonly int IsProneHash = Animator.StringToHash("IsProne");
    private static readonly int IsDolphinDivingHash = Animator.StringToHash("IsDolphinDiving");

    private readonly List<GameObject> activeMuzzleEffects = new();
    private readonly HashSet<string> missingAnimationWarnings = new();
    private bool ownerPresentationEnabled;
    private bool wasDead;
    private Coroutine kickRoutine;
    private Coroutine reloadRoutine;
    private bool isReloadPresenting;
    private Vector3 reloadPositionOffset;
    private Vector3 reloadEulerOffset;
    private float sprintTime;
    private bool isMoving;
    private string currentPlayedState;
    private float locomotionLockUntil;
    private Vector3 kickRestLocalPosition;
    private Quaternion kickRestLocalRotation;
    private Vector3 mountRestLocalPosition;
    private Quaternion mountRestLocalRotation;
    private Animator cachedPostureAnimator;
    private RuntimeAnimatorController cachedPostureController;
    private bool hasIsProneParameter;
    private bool hasIsDolphinDivingParameter;
    private Vector3 adsTargetLocalPosition;
    private Quaternion adsTargetLocalRotation;
    private bool aiming;
    private float aimBlend;
    private float bobTime;
    private float idleTime;
    private float sprintWeight;
    private float holsterWeight;
    private float holsterTarget;
    private float proneWeight;
    private float diveWeight;
    private float previousYaw;
    private float previousPitch;
    private Vector3 swayPosition;
    private Vector3 swayEuler;
    private Vector3 effectsRestLocalPosition;
    private Quaternion effectsRestLocalRotation;
    private bool adsTargetCached;
    private bool wasMenuOpen;
    private WeaponDefinition appliedDefinition;

    private PlayerAimZoom playerAimZoom;
    private PlayerMovement playerMovement;
    private PlayerLook playerLook;
    private WeaponPresentationCoordinator coordinator;

    public event Action<float, float> RecoilRequested;
    public Transform WeaponEffectsRoot => weaponEffectsRoot;
    public Transform WeaponMount => weaponMount;
    public Transform AimRoot => aimRoot;
    public bool IsAiming => aiming;
    public float AimBlend => aimBlend;
    public float CurrentSwayMultiplier => Mathf.Lerp(1f, config != null ? config.AdsSwayMultiplier : 1f, aimBlend);
    public float CurrentBobMultiplier => Mathf.Lerp(1f, config != null ? config.AdsBobMultiplier : 1f, aimBlend);
    public float HolsterDuration => config != null && config.HolsterDuration > 0.01f ? config.HolsterDuration : 0.16f;
    public float UnholsterDuration => config != null && config.UnholsterDuration > 0.01f ? config.UnholsterDuration : 0.2f;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        playerAimZoom = GetComponent<PlayerAimZoom>();
        playerMovement = GetComponent<PlayerMovement>();
        playerLook = GetComponent<PlayerLook>();
        coordinator = GetComponent<WeaponPresentationCoordinator>();

        ResolveHierarchyFallbacks();
        CacheRestPoses();
        ApplyConfiguredHipPose();
        CacheAdsTarget();
        PrepareAudioSource();
        previousYaw = playerLook != null ? playerLook.Yaw : transform.eulerAngles.y;
        previousPitch = playerLook != null ? playerLook.Pitch : 0f;
    }

    public override void OnNetworkSpawn()
    {
        ownerPresentationEnabled = IsOwner;
        if (!ownerPresentationEnabled)
        {
            enabled = false;
            return;
        }

        ResetPresentation();
    }

    public override void OnNetworkDespawn()
    {
        ownerPresentationEnabled = false;
        ClearTransientEffects();
        StopKick();
    }

    private void Update()
    {
        if (!ownerPresentationEnabled)
            return;

        bool dead = playerHealth != null && playerHealth.IsDead;
        if (dead && !wasDead)
            HandleDeathPresentation();
        else if (!dead && wasDead)
            HandleRespawnPresentation();

        wasDead = dead;
    }

    private void LateUpdate()
    {
        if (!ownerPresentationEnabled)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        bool menuOpen = LocalPlayerMenuState.IsOpen(this);
        if (menuOpen)
        {
            if (!wasMenuOpen)
                HandlePausePresentation();
            wasMenuOpen = true;
            return;
        }

        wasMenuOpen = false;
        SyncAimingFromGameplay();
        TickAds(Time.deltaTime);
        TickSprintAndHolster(Time.deltaTime);
        TickWeaponMotion(Time.deltaTime);
        TickAnimatorLocomotion();
    }

    public void ApplyDefinition(WeaponDefinition definition)
    {
        config = definition != null ? definition.Presentation : null;
        if (IsSpawned && !IsOwner)
            return;

        if (definition == appliedDefinition && HasFirstPersonModel())
            return;

        appliedDefinition = definition;
        missingAnimationWarnings.Clear();
        RebuildFirstPersonModel(definition);
        ApplyConfiguredHipPose();
        adsTargetCached = false;
        CacheAdsTarget();
        ResetPresentation();
    }

    public void PlayFirePresentation()
    {
        if (!CanPresent())
            return;

        PlayAnimationState(config != null ? config.FireAnimationState : "Fire", config != null ? config.FireAnimationSpeed : 1f);
        PlayClip(config != null ? config.FireSfx : null, config != null ? config.FireSfxVolume : 1f);
        SpawnMuzzleEffect();
        PlayProceduralFireKick();
        RaiseRecoilRequest();
        float fireLock = 0.08f;
        if (config != null)
            fireLock = Mathf.Max(config.FireKickDuration, config.FireRecoverDuration * 0.45f);
        locomotionLockUntil = Time.time + fireLock;
    }

    public void PlayReloadPresentation()
    {
        if (!CanPresent() || isReloadPresenting)
            return;

        float gameplayDuration = appliedDefinition != null ? appliedDefinition.ReloadTime : 1.2f;
        float duration = config != null ? config.ResolveReloadDuration(gameplayDuration) : Mathf.Max(0.05f, gameplayDuration);
        float animSpeed = config != null ? config.ResolveReloadAnimatorSpeed(duration) : 1f;
        PlayAnimationState(config != null ? config.ReloadAnimationState : "Reload", animSpeed);
        PlayClip(config != null ? config.ReloadSfx : null, config != null ? config.FireSfxVolume : 1f);
        locomotionLockUntil = Time.time + duration;
        StartReloadPresentation(duration);
    }

    public void PlayHolsterPresentation()
    {
        if (!CanPresent())
            return;

        holsterTarget = 1f;
        PlayAnimationState(config != null ? config.HolsterAnimationState : "Holster", 1f);
        PlayClip(config != null ? config.HolsterSfx : null, config != null ? config.FireSfxVolume : 1f);
    }

    public void PlayUnholsterPresentation()
    {
        if (!CanPresent())
            return;

        holsterWeight = 1f;
        holsterTarget = 0f;
        PlayAnimationState(config != null ? config.UnholsterAnimationState : "Unholster", 1f);
        PlayClip(config != null ? config.UnholsterSfx : null, config != null ? config.FireSfxVolume : 1f);
    }

    public void SetAiming(bool isAiming)
    {
        if (aiming == isAiming)
            return;

        aiming = isAiming;
        coordinator?.NotifyAimChanged(isAiming);
    }

    public void ResetPresentation()
    {
        StopKick();
        StopReloadPresentation();
        RestoreKickRestPose();
        RestoreMountRestPose();
        SnapAimToHip();
        RestoreEffectsRestPose();
        ClearTransientEffects();
        ClearMotionState();
        PlayAnimationState(config != null ? config.IdleAnimationState : "Idle", 1f);
    }

    private void RebuildFirstPersonModel(WeaponDefinition definition)
    {
        if (weaponKick == null)
            ResolveHierarchyFallbacks();
        if (weaponKick == null)
            return;

        ClearKickChildren(weaponKick);
        aimPoint = null;
        muzzlePoint = null;
        weaponAnimator = null;

        if (definition == null || definition.FirstPersonPrefab == null)
            return;

        GameObject instance = Instantiate(definition.FirstPersonPrefab, weaponKick);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        ApplyLayerRecursively(instance, "FirstPersonWeapon");
        DisableGameplayCollision(instance);

        aimPoint = FindChildByName(instance.transform, "AimPoint");
        muzzlePoint = FindChildByName(instance.transform, "MuzzlePoint");
        weaponAnimator = instance.GetComponentInChildren<Animator>(true);
        ApplyConfiguredAnimator();
        RefreshPostureParameterCache();
    }

    private static void ClearKickChildren(Transform kick)
    {
        for (int i = kick.childCount - 1; i >= 0; i--)
        {
            Transform child = kick.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static void DisableGameplayCollision(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }
    }

    private bool CanPresent()
    {
        return ownerPresentationEnabled &&
               isActiveAndEnabled &&
               (playerHealth == null || !playerHealth.IsDead);
    }

    private void HandleDeathPresentation()
    {
        ResetPresentation();
    }

    private void HandleRespawnPresentation()
    {
        ResetPresentation();
    }

    private void HandlePausePresentation()
    {
        SetAiming(false);
        SnapAimToHip();
        RestoreEffectsRestPose();
        ClearMotionState();
    }

    private void SyncAimingFromGameplay()
    {
        bool gameplayAiming = playerAimZoom != null && playerAimZoom.IsAiming;
        SetAiming(gameplayAiming);
    }

    private void PlayProceduralFireKick()
    {
        if (config == null || !config.UseProceduralFireKick || weaponKick == null)
            return;

        if (kickRoutine != null)
            StopCoroutine(kickRoutine);

        kickRoutine = StartCoroutine(FireKickRoutine());
    }

    private IEnumerator FireKickRoutine()
    {
        Vector3 startPos = weaponKick.localPosition;
        Quaternion startRot = weaponKick.localRotation;
        config.SampleFireKick(out Vector3 kickOffset, out Vector3 kickEuler);
        Vector3 kickPos = kickRestLocalPosition + kickOffset;
        Quaternion kickRot = kickRestLocalRotation * Quaternion.Euler(kickEuler);

        float kickDuration = Mathf.Max(0.01f, config.FireKickDuration);
        float recoverDuration = Mathf.Max(0.01f, config.FireRecoverDuration);

        float elapsed = 0f;
        while (elapsed < kickDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / kickDuration);
            t = 1f - (1f - t) * (1f - t);
            weaponKick.localPosition = Vector3.Lerp(startPos, kickPos, t);
            weaponKick.localRotation = Quaternion.Slerp(startRot, kickRot, t);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < recoverDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / recoverDuration);
            t = t * t * (3f - 2f * t);
            weaponKick.localPosition = Vector3.Lerp(kickPos, kickRestLocalPosition, t);
            weaponKick.localRotation = Quaternion.Slerp(kickRot, kickRestLocalRotation, t);
            yield return null;
        }

        RestoreKickRestPose();
        kickRoutine = null;
    }

    private void TickAds(float deltaTime)
    {
        if (aimRoot == null)
            return;

        if (!adsTargetCached)
            CacheAdsTarget();

        float target = aiming ? 1f : 0f;
        float speed = aiming ? ResolveAimInSpeed() : ResolveAimOutSpeed();
        aimBlend = Mathf.MoveTowards(aimBlend, target, speed * deltaTime);

        float t = aimBlend * aimBlend * (3f - 2f * aimBlend);
        aimRoot.localPosition = Vector3.LerpUnclamped(Vector3.zero, adsTargetLocalPosition, t);
        aimRoot.localRotation = Quaternion.SlerpUnclamped(Quaternion.identity, adsTargetLocalRotation, t);
    }

    private void TickSprintAndHolster(float deltaTime)
    {
        if (weaponMount == null)
            return;

        bool grounded = playerMovement == null || playerMovement.Grounded;
        bool sprinting = playerMovement != null &&
                         playerMovement.IsSprinting &&
                         grounded &&
                         !aiming &&
                         !isReloadPresenting;
        float sprintDuration = config != null && config.SprintTransitionDuration > 0.01f
            ? config.SprintTransitionDuration
            : 0.18f;
        float sprintTarget = sprinting ? 1f : 0f;
        sprintWeight = Mathf.MoveTowards(sprintWeight, sprintTarget, deltaTime / sprintDuration);

        float holsterDuration = holsterTarget > holsterWeight ? HolsterDuration : UnholsterDuration;
        holsterWeight = Mathf.MoveTowards(holsterWeight, holsterTarget, deltaTime / Mathf.Max(0.05f, holsterDuration));

        bool diving = playerMovement != null && playerMovement.BlocksCombat;
        bool prone = playerMovement != null && playerMovement.IsProne && !diving;
        proneWeight = Mathf.MoveTowards(proneWeight, prone ? 1f : 0f, deltaTime / 0.18f);
        diveWeight = Mathf.MoveTowards(diveWeight, diving ? 1f : 0f, deltaTime / 0.12f);

        float sprintT = sprintWeight * sprintWeight * (3f - 2f * sprintWeight) * (1f - aimBlend);
        float holsterT = holsterWeight * holsterWeight * (3f - 2f * holsterWeight);
        float proneT = proneWeight * proneWeight * (3f - 2f * proneWeight) * (1f - diveWeight);
        float diveT = diveWeight * diveWeight * (3f - 2f * diveWeight);

        Vector3 sprintPos = config != null ? config.SprintLocalPosition : Vector3.zero;
        Vector3 sprintEuler = config != null ? config.SprintLocalEuler : Vector3.zero;
        Vector3 holsterPos = config != null ? config.HolsterLocalPosition : new Vector3(0.03f, -0.3f, -0.1f);
        Vector3 holsterEuler = config != null ? config.HolsterLocalEuler : new Vector3(42f, 16f, -20f);
        SampleSprintSway(sprintT, deltaTime, out Vector3 sprintSwayPos, out Vector3 sprintSwayEuler);

        weaponMount.localPosition = mountRestLocalPosition
            + sprintPos * sprintT
            + sprintSwayPos
            + holsterPos * holsterT
            + proneLocalPosition * proneT
            + diveLocalPosition * diveT
            + reloadPositionOffset;
        weaponMount.localRotation = mountRestLocalRotation
            * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(sprintEuler), sprintT)
            * Quaternion.Euler(sprintSwayEuler)
            * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(holsterEuler), holsterT)
            * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(proneLocalEuler), proneT)
            * Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(diveLocalEuler), diveT)
            * Quaternion.Euler(reloadEulerOffset);

        ApplyFirstPersonPostureParameters(prone, diving);
    }

    private void ApplyFirstPersonPostureParameters(bool prone, bool diving)
    {
        if (weaponAnimator == null)
            return;

        RefreshPostureParameterCache();
        if (hasIsProneParameter)
            weaponAnimator.SetBool(IsProneHash, prone);
        if (hasIsDolphinDivingParameter)
            weaponAnimator.SetBool(IsDolphinDivingHash, diving);
    }

    private void RefreshPostureParameterCache()
    {
        if (cachedPostureAnimator == weaponAnimator &&
            (weaponAnimator == null || weaponAnimator.runtimeAnimatorController == cachedPostureController))
            return;

        cachedPostureAnimator = weaponAnimator;
        cachedPostureController = weaponAnimator != null ? weaponAnimator.runtimeAnimatorController : null;
        hasIsProneParameter = false;
        hasIsDolphinDivingParameter = false;
        if (weaponAnimator == null)
            return;

        AnimatorControllerParameter[] parameters = weaponAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            int hash = parameters[i].nameHash;
            if (hash == IsProneHash)
                hasIsProneParameter = true;
            else if (hash == IsDolphinDivingHash)
                hasIsDolphinDivingParameter = true;
        }
    }

    private void TickWeaponMotion(float deltaTime)
    {
        if (weaponEffectsRoot == null || config == null)
            return;

        float yaw = playerLook != null ? playerLook.Yaw : transform.eulerAngles.y;
        float pitch = playerLook != null ? playerLook.Pitch : 0f;
        float yawDelta = Mathf.DeltaAngle(previousYaw, yaw);
        float pitchDelta = pitch - previousPitch;
        previousYaw = yaw;
        previousPitch = pitch;

        float swayMul = CurrentSwayMultiplier;
        float bobMul = CurrentBobMultiplier;
        float swayAmount = config.LookSwayAmount * swayMul;
        Vector3 targetSwayPos = new(
            Mathf.Clamp(-yawDelta * swayAmount, -0.04f, 0.04f),
            Mathf.Clamp(-pitchDelta * swayAmount, -0.03f, 0.03f),
            0f);
        Vector3 targetSwayEuler = new(
            Mathf.Clamp(-pitchDelta * swayAmount * 25f, -4f, 4f),
            Mathf.Clamp(yawDelta * swayAmount * 20f, -4f, 4f),
            Mathf.Clamp(-yawDelta * swayAmount * 30f, -5f, 5f));

        bool grounded = playerMovement == null || playerMovement.Grounded;
        float speed = playerMovement != null ? playerMovement.HorizontalSpeed : 0f;
        Vector2 moveInput = playerMovement != null ? playerMovement.MoveInput : Vector2.zero;
        isMoving = grounded && speed > 0.35f && moveInput.sqrMagnitude > 0.01f;
        float idleWeight = (!isMoving ? 1f : 0f) * (grounded ? 1f : 0f) * (1f - sprintWeight) * (1f - holsterWeight);

        if (isMoving)
        {
            float walkSpeed = playerMovement != null ? Mathf.Max(0.01f, playerMovement.WalkSpeed) : 1f;
            float bobAmount = Mathf.Lerp(config.WalkBobAmount, config.SprintBobAmount, sprintWeight) * bobMul;
            float bobFrequency = Mathf.Lerp(config.WalkBobFrequency, config.SprintBobFrequency, sprintWeight);
            bobTime += deltaTime * bobFrequency * Mathf.Clamp(speed / walkSpeed, 0.4f, 1.6f);
            targetSwayPos.x += Mathf.Cos(bobTime) * bobAmount * (0.55f + sprintWeight * 0.45f);
            targetSwayPos.y += Mathf.Abs(Mathf.Sin(bobTime)) * bobAmount;
            targetSwayPos.z += -Mathf.Abs(Mathf.Sin(bobTime)) * config.SprintForwardBob * sprintWeight;
            targetSwayEuler.x += Mathf.Sin(bobTime) * bobAmount * 18f * sprintWeight;
            targetSwayEuler.z += -moveInput.x * bobAmount * (40f + sprintWeight * 20f);
        }
        else
        {
            bobTime = Mathf.MoveTowards(bobTime, 0f, deltaTime * 4f);
        }

        idleTime += deltaTime * Mathf.Max(0.1f, config.IdleSwayFrequency) * Mathf.PI * 2f;
        float breathe = Mathf.Sin(idleTime);
        float breatheCos = Mathf.Cos(idleTime * 0.5f);
        float idleAmount = config.IdleSwayAmount * idleWeight * swayMul;
        targetSwayPos.y += breathe * idleAmount;
        targetSwayPos.x += breatheCos * idleAmount * 0.45f;
        targetSwayEuler.x += breatheCos * idleAmount * 25f;

        float smooth = Mathf.Max(0.01f, config.LookSwaySmooth);
        float alpha = 1f - Mathf.Exp(-smooth * deltaTime);
        swayPosition = Vector3.Lerp(swayPosition, targetSwayPos, alpha);
        swayEuler = Vector3.Lerp(swayEuler, targetSwayEuler, alpha);

        weaponEffectsRoot.localPosition = effectsRestLocalPosition + swayPosition;
        weaponEffectsRoot.localRotation = effectsRestLocalRotation * Quaternion.Euler(swayEuler);
    }

    private float ResolveAimInSpeed()
    {
        if (config == null)
            return 8.5f;
        if (config.AimInSpeed > 0.01f)
            return config.AimInSpeed;
        return 1f / Mathf.Max(0.0001f, config.AdsBlendDuration);
    }

    private float ResolveAimOutSpeed()
    {
        if (config == null)
            return 7f;
        if (config.AimOutSpeed > 0.01f)
            return config.AimOutSpeed;
        return 1f / Mathf.Max(0.0001f, config.AdsBlendDuration);
    }

    private void SpawnMuzzleEffect()
    {
        if (config == null || config.MuzzleVfxPrefab == null || muzzlePoint == null)
            return;

        GameObject instance = Instantiate(
            config.MuzzleVfxPrefab,
            muzzlePoint.position,
            muzzlePoint.rotation,
            muzzlePoint);

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = config.MuzzleVfxLocalScale;
        ApplyLayerRecursively(instance, "FirstPersonWeapon");
        activeMuzzleEffects.Add(instance);

        float lifetime = Mathf.Max(0.05f, config.MuzzleVfxLifetime);
        Destroy(instance, lifetime);
        StartCoroutine(RemoveMuzzleWhenDestroyed(instance, lifetime));
    }

    private IEnumerator RemoveMuzzleWhenDestroyed(GameObject instance, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        activeMuzzleEffects.Remove(instance);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (audioSource == null || clip == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void PlayAnimationState(string stateName, float speed, bool restart = true)
    {
        if (weaponAnimator == null || string.IsNullOrEmpty(stateName))
            return;

        if (!weaponAnimator.gameObject.activeInHierarchy)
            return;

        int stateHash = Animator.StringToHash(stateName);
        if (!weaponAnimator.HasState(0, stateHash))
        {
            if (missingAnimationWarnings.Add(stateName))
            {
                string weaponName = config != null ? config.WeaponName : "weapon";
                Debug.LogWarning(
                    $"Missing optional animation state '{stateName}' on {weaponName}. Continuing with procedural presentation.");
            }

            return;
        }

        if (!restart && currentPlayedState == stateName)
            return;

        weaponAnimator.speed = Mathf.Max(0.01f, speed);
        weaponAnimator.Play(stateHash, 0, 0f);
        currentPlayedState = stateName;
    }

    private void RaiseRecoilRequest()
    {
        if (config == null)
            return;

        config.SampleCameraRecoil(out float recoilPitch, out float recoilYaw);
        RecoilRequested?.Invoke(recoilPitch, recoilYaw);
    }

    private void ClearTransientEffects()
    {
        for (int i = 0; i < activeMuzzleEffects.Count; i++)
        {
            if (activeMuzzleEffects[i] != null)
                Destroy(activeMuzzleEffects[i]);
        }

        activeMuzzleEffects.Clear();
    }

    private void StopKick()
    {
        if (kickRoutine == null)
            return;

        StopCoroutine(kickRoutine);
        kickRoutine = null;
    }

    private void StartReloadPresentation(float duration)
    {
        if (config == null || !config.UseProceduralReload)
            return;

        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        reloadRoutine = StartCoroutine(ReloadPresentationRoutine(duration));
    }

    private void StopReloadPresentation()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        isReloadPresenting = false;
        reloadPositionOffset = Vector3.zero;
        reloadEulerOffset = Vector3.zero;
    }

    private IEnumerator ReloadPresentationRoutine(float duration)
    {
        isReloadPresenting = true;
        duration = Mathf.Max(0.05f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            EvaluateReloadPose(Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        reloadPositionOffset = Vector3.zero;
        reloadEulerOffset = Vector3.zero;
        isReloadPresenting = false;
        reloadRoutine = null;
    }

    private void EvaluateReloadPose(float t)
    {
        if (config == null)
        {
            reloadPositionOffset = Vector3.zero;
            reloadEulerOffset = Vector3.zero;
            return;
        }

        Vector3 lowerPos = config.ReloadLowerLocalPosition;
        Vector3 lowerEuler = config.ReloadLowerLocalEuler;
        Vector3 actionPos = config.ReloadActionLocalPosition;
        Vector3 actionEuler = config.ReloadActionLocalEuler;
        const float introEnd = 0.22f;
        const float outroStart = 0.78f;

        if (t < introEnd)
        {
            float u = Smooth01(t / introEnd);
            reloadPositionOffset = Vector3.Lerp(Vector3.zero, lowerPos, u);
            reloadEulerOffset = Vector3.Lerp(Vector3.zero, lowerEuler, u);
            return;
        }

        if (t < outroStart)
        {
            float mid = (t - introEnd) / (outroStart - introEnd);
            float cycles = config.ReloadCycleCount;
            float cycleT = Mathf.Repeat(mid * cycles, 1f);
            float pulse = cycleT < 0.5f ? cycleT * 2f : (1f - cycleT) * 2f;
            pulse = Smooth01(pulse);
            reloadPositionOffset = Vector3.Lerp(lowerPos, actionPos, pulse);
            reloadEulerOffset = Vector3.Lerp(lowerEuler, actionEuler, pulse);
            return;
        }

        float outro = Smooth01((t - outroStart) / Mathf.Max(0.0001f, 1f - outroStart));
        reloadPositionOffset = Vector3.Lerp(lowerPos, Vector3.zero, outro);
        reloadEulerOffset = Vector3.Lerp(lowerEuler, Vector3.zero, outro);
    }

    private void SampleSprintSway(float sprintT, float deltaTime, out Vector3 position, out Vector3 euler)
    {
        if (config == null || sprintT <= 0.001f)
        {
            position = Vector3.zero;
            euler = Vector3.zero;
            return;
        }

        sprintTime += deltaTime * Mathf.Max(0.1f, config.SprintSwayFrequency);
        float wave = Mathf.Sin(sprintTime * Mathf.PI * 2f);
        float bounce = Mathf.Abs(Mathf.Cos(sprintTime * Mathf.PI * 2f));
        position = new Vector3(
            wave * config.SprintSwayAmount,
            bounce * config.SprintSwayVerticalAmount,
            0f) * sprintT;
        euler = new Vector3(
            bounce * config.SprintSwayPitch,
            wave * config.SprintSwayYaw,
            -wave * config.SprintSwayRoll) * sprintT;
    }

    private void TickAnimatorLocomotion()
    {
        if (weaponAnimator == null || config == null)
            return;

        if (isReloadPresenting || holsterTarget > 0.45f || Time.time < locomotionLockUntil)
            return;

        string desired;
        if (aiming && aimBlend > 0.45f)
            desired = FirstAssignedState(config.AimAnimationState, config.IdleAnimationState);
        else if (sprintWeight > 0.55f)
            desired = FirstAssignedState(config.SprintAnimationState, config.WalkAnimationState, config.IdleAnimationState);
        else if (isMoving)
            desired = FirstAssignedState(config.WalkAnimationState, config.IdleAnimationState);
        else
            desired = config.IdleAnimationState;

        PlayAnimationState(desired, 1f, false);
    }

    private void ApplyConfiguredAnimator()
    {
        if (config == null)
            return;

        if (weaponAnimator == null)
        {
            if (missingAnimationWarnings.Add("__no_animator"))
            {
                Debug.LogWarning(
                    $"No Animator on {config.WeaponName}. Continuing with procedural presentation.");
            }

            return;
        }

        if (config.AnimatorController != null &&
            weaponAnimator.runtimeAnimatorController != config.AnimatorController)
        {
            weaponAnimator.runtimeAnimatorController = config.AnimatorController;
        }
    }

    private static string FirstAssignedState(params string[] states)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (!string.IsNullOrEmpty(states[i]))
                return states[i];
        }

        return "Idle";
    }

    private static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private void RestoreKickRestPose()
    {
        if (weaponKick == null)
            return;

        weaponKick.localPosition = kickRestLocalPosition;
        weaponKick.localRotation = kickRestLocalRotation;
    }

    private void RestoreMountRestPose()
    {
        if (weaponMount == null)
            return;

        weaponMount.localPosition = mountRestLocalPosition;
        weaponMount.localRotation = mountRestLocalRotation;
    }

    private void RestoreEffectsRestPose()
    {
        if (weaponEffectsRoot == null)
            return;

        weaponEffectsRoot.localPosition = effectsRestLocalPosition;
        weaponEffectsRoot.localRotation = effectsRestLocalRotation;
    }

    private void SnapAimToHip()
    {
        aiming = false;
        aimBlend = 0f;
        if (aimRoot == null)
            return;

        aimRoot.localPosition = Vector3.zero;
        aimRoot.localRotation = Quaternion.identity;
    }

    private void ClearMotionState()
    {
        bobTime = 0f;
        idleTime = 0f;
        sprintTime = 0f;
        sprintWeight = 0f;
        holsterWeight = 0f;
        holsterTarget = 0f;
        proneWeight = 0f;
        diveWeight = 0f;
        isMoving = false;
        currentPlayedState = null;
        locomotionLockUntil = 0f;
        swayPosition = Vector3.zero;
        swayEuler = Vector3.zero;
        reloadPositionOffset = Vector3.zero;
        reloadEulerOffset = Vector3.zero;
    }

    private bool HasFirstPersonModel()
    {
        return weaponKick != null && weaponKick.childCount > 0;
    }

    private void CacheRestPoses()
    {
        if (weaponKick != null)
        {
            kickRestLocalPosition = weaponKick.localPosition;
            kickRestLocalRotation = weaponKick.localRotation;
        }

        if (weaponMount != null)
        {
            mountRestLocalPosition = weaponMount.localPosition;
            mountRestLocalRotation = weaponMount.localRotation;
        }

        if (weaponEffectsRoot != null)
        {
            effectsRestLocalPosition = weaponEffectsRoot.localPosition;
            effectsRestLocalRotation = weaponEffectsRoot.localRotation;
        }
    }

    private void ApplyConfiguredHipPose()
    {
        if (config == null || !config.UseConfiguredHipPose || weaponMount == null)
            return;

        mountRestLocalPosition = config.HipLocalPosition;
        mountRestLocalRotation = Quaternion.Euler(config.HipLocalEuler);
        RestoreMountRestPose();
    }

    private void CacheAdsTarget()
    {
        adsTargetLocalPosition = config != null ? config.AdsLocalPosition : Vector3.zero;
        adsTargetLocalRotation = Quaternion.Euler(config != null ? config.AdsLocalEuler : Vector3.zero);

        if (config != null && config.UseAimPoint && aimRoot != null && aimPoint != null && aimRoot.parent != null)
        {
            Vector3 restRootPos = aimRoot.localPosition;
            Quaternion restRootRot = aimRoot.localRotation;
            aimRoot.localPosition = Vector3.zero;
            aimRoot.localRotation = Quaternion.identity;

            Vector3 aimPointInParent = aimRoot.parent.InverseTransformPoint(aimPoint.position);
            float distance = Mathf.Max(0.05f, config.AimDistance);
            Vector3 targetPoint = new(0f, 0f, distance);
            adsTargetLocalPosition = targetPoint - aimPointInParent + config.AdsLocalPosition;

            aimRoot.localPosition = restRootPos;
            aimRoot.localRotation = restRootRot;
        }

        adsTargetCached = aimRoot != null;
    }

    private void ResolveHierarchyFallbacks()
    {
        Transform cameraTransform = transform.Find("Camera");
        if (cameraTransform == null)
            cameraTransform = transform.Find("CameraRoot/CameraEffectsRoot/Camera");
        Transform weaponView = cameraTransform != null ? cameraTransform.Find("WeaponView") : null;

        if (weaponEffectsRoot == null && weaponView != null)
            weaponEffectsRoot = weaponView.Find("WeaponEffectsRoot");

        if (aimRoot == null && weaponEffectsRoot != null)
            aimRoot = weaponEffectsRoot.Find("AimRoot");

        if (weaponMount == null && aimRoot != null)
            weaponMount = aimRoot.Find("WeaponMount");
        if (weaponMount == null && weaponEffectsRoot != null)
            weaponMount = FindChildByName(weaponEffectsRoot, "WeaponMount");
        if (weaponMount == null && weaponView != null)
            weaponMount = FindChildByName(weaponView, "WeaponMount");

        EnsureAimRoot();

        if (weaponKick == null && weaponMount != null)
            weaponKick = weaponMount.Find("WeaponKick");

        if (aimPoint == null)
            aimPoint = FindChildByName(weaponKick != null ? weaponKick : weaponMount, "AimPoint");

        if (muzzlePoint == null)
            muzzlePoint = FindChildByName(weaponKick != null ? weaponKick : weaponMount, "MuzzlePoint");
    }

    private void EnsureAimRoot()
    {
        if (aimRoot != null || weaponEffectsRoot == null || weaponMount == null)
            return;

        GameObject aimObject = new("AimRoot");
        aimRoot = aimObject.transform;
        aimRoot.SetParent(weaponEffectsRoot, false);
        aimRoot.localPosition = Vector3.zero;
        aimRoot.localRotation = Quaternion.identity;
        aimRoot.localScale = Vector3.one;
        weaponMount.SetParent(aimRoot, true);
    }

    private void PrepareAudioSource()
    {
        if (audioSource != null)
        {
            PlayerGameSettings.RouteToSfx(audioSource);
            return;
        }

        Transform host = weaponKick != null ? weaponKick : weaponMount;
        if (host == null)
            host = transform;

        audioSource = host.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = host.gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;
        PlayerGameSettings.RouteToSfx(audioSource);
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private static void ApplyLayerRecursively(GameObject root, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0 || root == null)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
    }
}
