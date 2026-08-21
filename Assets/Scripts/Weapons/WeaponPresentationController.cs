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

    private readonly List<GameObject> activeMuzzleEffects = new();
    private bool ownerPresentationEnabled;
    private bool wasDead;
    private Coroutine kickRoutine;
    private Vector3 kickRestLocalPosition;
    private Quaternion kickRestLocalRotation;
    private Vector3 mountRestLocalPosition;
    private Quaternion mountRestLocalRotation;
    private Vector3 adsTargetLocalPosition;
    private Quaternion adsTargetLocalRotation;
    private bool aiming;
    private float aimBlend;
    private float bobTime;
    private float previousYaw;
    private float previousPitch;
    private Vector3 swayPosition;
    private Vector3 swayEuler;
    private Vector3 effectsRestLocalPosition;
    private Quaternion effectsRestLocalRotation;
    private bool adsTargetCached;
    private bool wasMenuOpen;

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
        TickWeaponMotion(Time.deltaTime);
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
    }

    public void PlayReloadPresentation()
    {
        if (!CanPresent())
            return;

        PlayAnimationState(config != null ? config.ReloadAnimationState : "Reload", 1f);
        PlayClip(config != null ? config.ReloadSfx : null, config != null ? config.FireSfxVolume : 1f);
    }

    public void PlayHolsterPresentation()
    {
        if (!CanPresent())
            return;

        PlayAnimationState(config != null ? config.HolsterAnimationState : "Holster", 1f);
        PlayClip(config != null ? config.HolsterSfx : null, config != null ? config.FireSfxVolume : 1f);
    }

    public void PlayUnholsterPresentation()
    {
        if (!CanPresent())
            return;

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
        RestoreKickRestPose();
        RestoreMountRestPose();
        SnapAimToHip();
        RestoreEffectsRestPose();
        ClearTransientEffects();
        ClearMotionState();
        PlayAnimationState(config != null ? config.IdleAnimationState : "Idle", 1f);
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
        Vector3 kickPos = kickRestLocalPosition + config.FireKickLocalPosition;
        Quaternion kickRot = kickRestLocalRotation * Quaternion.Euler(config.FireKickLocalEuler);

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
        bool moving = grounded && speed > 0.35f && moveInput.sqrMagnitude > 0.01f;
        if (moving)
        {
            float walkSpeed = playerMovement != null ? Mathf.Max(0.01f, playerMovement.WalkSpeed) : 1f;
            bobTime += deltaTime * config.WalkBobFrequency * Mathf.Clamp(speed / walkSpeed, 0.4f, 1.6f);
            float bob = config.WalkBobAmount * bobMul;
            targetSwayPos.x += Mathf.Cos(bobTime) * bob * 0.55f;
            targetSwayPos.y += Mathf.Abs(Mathf.Sin(bobTime)) * bob;
            targetSwayEuler.z += -moveInput.x * bob * 40f;
        }
        else
        {
            bobTime = Mathf.MoveTowards(bobTime, 0f, deltaTime * 4f);
        }

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

    private void PlayAnimationState(string stateName, float speed)
    {
        if (weaponAnimator == null || string.IsNullOrEmpty(stateName))
            return;

        if (!weaponAnimator.gameObject.activeInHierarchy)
            return;

        weaponAnimator.speed = Mathf.Max(0.01f, speed);
        weaponAnimator.Play(stateName, 0, 0f);
    }

    private void RaiseRecoilRequest()
    {
        if (config == null)
            return;

        RecoilRequested?.Invoke(config.RecoilPitch, config.RecoilYaw);
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
        swayPosition = Vector3.zero;
        swayEuler = Vector3.zero;
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
            return;

        Transform host = weaponKick != null ? weaponKick : weaponMount;
        if (host == null)
            host = transform;

        audioSource = host.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = host.gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.loop = false;
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
