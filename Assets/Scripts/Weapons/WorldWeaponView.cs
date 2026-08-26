using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Remote/world-space weapon presentation. Visual and spatial audio only;
/// hitscan damage stays on PlayerShoot.
/// </summary>
public class WorldWeaponView : NetworkBehaviour
{
    private const string WorldWeaponLayerName = "WorldWeapon";

    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private Transform weaponHandAnchor;
    [SerializeField] private Transform worldWeaponRoot;
    [SerializeField] private Transform weaponKick;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform stanceFollow;
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float aimPitchSmoothTime = 0.08f;
    [SerializeField] private float aimPitchSnapDegrees = 25f;

    private readonly List<GameObject> activeMuzzleEffects = new();
    private bool remotePresentationEnabled;
    private bool wasDead;
    private Coroutine kickRoutine;
    private Vector3 kickRestLocalPosition;
    private Quaternion kickRestLocalRotation;
    private float displayedAimPitch;
    private WeaponPresentationCoordinator coordinator;

    public Transform WorldWeaponRoot => worldWeaponRoot;

    private WeaponPresentationConfig Config =>
        definition != null ? definition.Presentation : null;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (coordinator == null)
            coordinator = GetComponent<WeaponPresentationCoordinator>();

        ResolveHierarchyFallbacks();
        CacheRestPoses();
        PrepareAudioSource();
        PreparePresentationObject();
        SetWorldWeaponActive(false);
    }

    public override void OnNetworkSpawn()
    {
        remotePresentationEnabled = !IsOwner;
        displayedAimPitch = coordinator != null ? coordinator.AimPitch : 0f;
        if (!remotePresentationEnabled)
        {
            SetWorldWeaponActive(false);
            enabled = false;
            return;
        }

        ApplyWorldPose();
        RefreshRemoteVisibility();
        ResetPresentation();
        ApplyAimPitch(true);
    }

    public override void OnNetworkDespawn()
    {
        remotePresentationEnabled = false;
        ClearTransientEffects();
        StopKick();
        SetWorldWeaponActive(false);
    }

    private void LateUpdate()
    {
        if (!remotePresentationEnabled)
            return;

        bool dead = playerHealth != null && playerHealth.IsDead;
        if (dead && !wasDead)
            HandleDeathPresentation();
        else if (!dead && wasDead)
            HandleRespawnPresentation();

        wasDead = dead;
        RefreshRemoteVisibility();
        ApplyWorldPose();
        ApplyAimPitch(false);
    }

    public void ApplyDefinition(WeaponDefinition next)
    {
        definition = next;
        if (IsSpawned && IsOwner)
            return;

        RebuildWorldModel(next);
        CacheRestPoses();
        PrepareAudioSource();
        PreparePresentationObject();
        ApplyWorldPose();
        ResetPresentation();
    }

    public void PlayFirePresentation()
    {
        if (!CanPresent())
            return;

        WeaponPresentationConfig config = Config;
        PlayAnimationState(config != null ? config.FireAnimationState : "Fire", config != null ? config.FireAnimationSpeed : 1f);
        PlayClip(config != null ? config.FireSfx : null, config != null ? config.WorldFireSfxVolume : 1f);
        SpawnMuzzleEffect();
        PlayProceduralFireKick();
    }

    public void PlayReloadPresentation()
    {
        if (!CanPresent())
            return;

        WeaponPresentationConfig config = Config;
        PlayAnimationState(config != null ? config.ReloadAnimationState : "Reload", 1f);
        PlayClip(config != null ? config.ReloadSfx : null, config != null ? config.WorldFireSfxVolume : 1f);
    }

    public void ResetPresentation()
    {
        StopKick();
        RestoreKickRestPose();
        ClearTransientEffects();
        displayedAimPitch = coordinator != null ? coordinator.AimPitch : 0f;
        PlayAnimationState(Config != null ? Config.IdleAnimationState : "Idle", 1f);
    }

    private void RebuildWorldModel(WeaponDefinition next)
    {
        if (weaponKick == null)
            ResolveHierarchyFallbacks();
        if (weaponKick == null)
            return;

        for (int i = weaponKick.childCount - 1; i >= 0; i--)
        {
            Transform child = weaponKick.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        muzzlePoint = null;
        weaponAnimator = null;

        if (next == null || next.WorldPrefab == null)
            return;

        GameObject instance = Instantiate(next.WorldPrefab, weaponKick);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        ApplyLayerRecursively(instance, WorldWeaponLayerName);
        DisableGameplayCollision(instance);
        muzzlePoint = FindChildByName(instance.transform, "MuzzlePoint");
        weaponAnimator = instance.GetComponentInChildren<Animator>(true);
        WeaponPresentationConfig config = next != null ? next.Presentation : null;
        if (weaponAnimator != null && config != null && config.AnimatorController != null)
            weaponAnimator.runtimeAnimatorController = config.AnimatorController;
    }

    private bool CanPresent()
    {
        return remotePresentationEnabled &&
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
        SetWorldWeaponActive(true);
        ApplyWorldPose();
        ApplyAimPitch(true);
    }

    private void RefreshRemoteVisibility()
    {
        bool hidden = playerHealth != null && playerHealth.AreDeathVisualsHidden;
        SetWorldWeaponActive(!hidden);
    }

    private void SetWorldWeaponActive(bool active)
    {
        if (weaponHandAnchor != null && weaponHandAnchor.gameObject.activeSelf != active)
            weaponHandAnchor.gameObject.SetActive(active);
    }

    private void ApplyWorldPose()
    {
        if (weaponHandAnchor == null)
            return;

        Vector3 position = definition != null ? definition.WorldLocalPosition : weaponHandAnchor.localPosition;
        Vector3 euler = definition != null ? definition.WorldLocalEuler : weaponHandAnchor.localEulerAngles;
        Vector3 scale = definition != null ? definition.WorldLocalScale : Vector3.one;
        float stanceOffset = definition != null ? definition.WorldStanceHeightOffset : 0.28f;

        Transform follow = stanceFollow;
        if (follow == null && playerMovement != null)
            follow = playerMovement.BodyVisual;

        if (follow != null)
            position.y = follow.localPosition.y + stanceOffset;

        weaponHandAnchor.localPosition = position;
        weaponHandAnchor.localRotation = Quaternion.Euler(euler);
        weaponHandAnchor.localScale = scale;
    }

    private void ApplyAimPitch(bool snap)
    {
        if (worldWeaponRoot == null)
            return;

        float target = coordinator != null ? coordinator.AimPitch : 0f;
        if (snap || Mathf.Abs(target - displayedAimPitch) >= aimPitchSnapDegrees)
        {
            displayedAimPitch = target;
        }
        else
        {
            float duration = Mathf.Max(0.0001f, aimPitchSmoothTime);
            float alpha = 1f - Mathf.Exp(-Time.deltaTime / duration);
            displayedAimPitch = Mathf.Lerp(displayedAimPitch, target, alpha);
        }

        worldWeaponRoot.localRotation = Quaternion.Euler(displayedAimPitch, 0f, 0f);
    }

    private void PlayProceduralFireKick()
    {
        WeaponPresentationConfig config = Config;
        if (config == null || !config.UseProceduralFireKick || weaponKick == null)
            return;

        if (kickRoutine != null)
            StopCoroutine(kickRoutine);

        kickRoutine = StartCoroutine(FireKickRoutine(config));
    }

    private IEnumerator FireKickRoutine(WeaponPresentationConfig config)
    {
        Vector3 startPos = weaponKick.localPosition;
        Quaternion startRot = weaponKick.localRotation;
        Vector3 kickPos = kickRestLocalPosition + config.WorldFireKickLocalPosition;
        Quaternion kickRot = kickRestLocalRotation * Quaternion.Euler(config.WorldFireKickLocalEuler);

        float kickDuration = Mathf.Max(0.01f, config.WorldFireKickDuration);
        float recoverDuration = Mathf.Max(0.01f, config.WorldFireRecoverDuration);

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

    private void SpawnMuzzleEffect()
    {
        WeaponPresentationConfig config = Config;
        if (config == null || config.MuzzleVfxPrefab == null || muzzlePoint == null)
            return;

        GameObject instance = Instantiate(
            config.MuzzleVfxPrefab,
            muzzlePoint.position,
            muzzlePoint.rotation,
            muzzlePoint);

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = config.WorldMuzzleVfxLocalScale;
        ApplyLayerRecursively(instance, WorldWeaponLayerName);
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

    private void CacheRestPoses()
    {
        if (weaponKick == null)
            return;

        kickRestLocalPosition = weaponKick.localPosition;
        kickRestLocalRotation = weaponKick.localRotation;
    }

    private void ResolveHierarchyFallbacks()
    {
        if (weaponHandAnchor == null)
            weaponHandAnchor = transform.Find("WeaponHandAnchor");

        if (worldWeaponRoot == null && weaponHandAnchor != null)
            worldWeaponRoot = weaponHandAnchor.Find("WorldWeaponRoot");

        if (weaponKick == null && worldWeaponRoot != null)
            weaponKick = worldWeaponRoot.Find("WeaponKick");

        if (muzzlePoint == null)
            muzzlePoint = FindChildByName(weaponKick != null ? weaponKick : worldWeaponRoot, "MuzzlePoint");

        if (weaponAnimator == null && weaponKick != null)
            weaponAnimator = weaponKick.GetComponentInChildren<Animator>(true);

        if (stanceFollow == null)
        {
            if (playerMovement != null)
                stanceFollow = playerMovement.BodyVisual;
            if (stanceFollow == null)
                stanceFollow = transform.Find("Capsule");
        }
    }

    private void PrepareAudioSource()
    {
        Transform host = weaponKick != null ? weaponKick : worldWeaponRoot;
        if (host == null)
            host = transform;

        if (audioSource == null)
            audioSource = host.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = host.gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.dopplerLevel = 0.15f;

        WeaponPresentationConfig config = Config;
        audioSource.minDistance = config != null ? Mathf.Max(0.1f, config.WorldAudioMinDistance) : 1.5f;
        audioSource.maxDistance = config != null ? Mathf.Max(audioSource.minDistance + 0.1f, config.WorldAudioMaxDistance) : 45f;
        PlayerGameSettings.RouteToSfx(audioSource);
    }

    private void PreparePresentationObject()
    {
        if (weaponHandAnchor == null)
            return;

        ApplyLayerRecursively(weaponHandAnchor.gameObject, WorldWeaponLayerName);
        DisableGameplayCollision(weaponHandAnchor.gameObject);
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
}
