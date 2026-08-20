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
    [SerializeField] private Transform weaponMount;
    [SerializeField] private Transform weaponKick;
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
    private bool aiming;

    public event Action<float, float> RecoilRequested;
    public Transform WeaponEffectsRoot => weaponEffectsRoot;
    public Transform WeaponMount => weaponMount;
    public bool IsAiming => aiming;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        ResolveHierarchyFallbacks();
        CacheRestPoses();
        PrepareAudioSource();
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
        TickAds(Time.deltaTime);
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
        aiming = isAiming;
        if (!CanPresent() || weaponAnimator == null || config == null)
            return;

        if (isAiming)
            PlayAnimationState(config.AimAnimationState, 1f);
        else
            PlayAnimationState(config.IdleAnimationState, 1f);
    }

    public void ResetPresentation()
    {
        StopKick();
        RestoreKickRestPose();
        RestoreMountRestPose();
        ClearTransientEffects();
        aiming = false;
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
        if (weaponMount == null || config == null)
            return;

        Vector3 targetPos = aiming
            ? mountRestLocalPosition + config.AdsLocalPosition
            : mountRestLocalPosition;
        Quaternion targetRot = aiming
            ? mountRestLocalRotation * Quaternion.Euler(config.AdsLocalEuler)
            : mountRestLocalRotation;

        float duration = Mathf.Max(0.0001f, config.AdsBlendDuration);
        float alpha = 1f - Mathf.Exp(-deltaTime / duration);
        weaponMount.localPosition = Vector3.Lerp(weaponMount.localPosition, targetPos, alpha);
        weaponMount.localRotation = Quaternion.Slerp(weaponMount.localRotation, targetRot, alpha);
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
    }

    private void ResolveHierarchyFallbacks()
    {
        Transform cameraTransform = transform.Find("Camera");
        Transform weaponView = cameraTransform != null ? cameraTransform.Find("WeaponView") : null;

        if (weaponEffectsRoot == null && weaponView != null)
            weaponEffectsRoot = weaponView.Find("WeaponEffectsRoot");

        if (weaponMount == null && weaponEffectsRoot != null)
            weaponMount = weaponEffectsRoot.Find("WeaponMount");
        if (weaponMount == null && weaponView != null)
            weaponMount = weaponView.Find("WeaponMount");

        if (weaponKick == null && weaponMount != null)
            weaponKick = weaponMount.Find("WeaponKick");

        if (muzzlePoint == null)
            muzzlePoint = FindChildByName(weaponKick != null ? weaponKick : weaponMount, "MuzzlePoint");
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
