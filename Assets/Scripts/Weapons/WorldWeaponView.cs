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
    [SerializeField] private Transform weaponSocket;
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
    [SerializeField] private ThirdPersonWeaponRig thirdPersonRig;

    private readonly List<GameObject> activeMuzzleEffects = new();
    private bool remotePresentationEnabled;
    private bool wasDead;
    private Coroutine kickRoutine;
    private Vector3 kickRestLocalPosition;
    private Quaternion kickRestLocalRotation;
    private WeaponPresentationCoordinator coordinator;
    private ThirdPersonWeaponVisual currentVisual;

    public Transform WorldWeaponRoot => worldWeaponRoot;
    public Transform WeaponSocket => weaponSocket;
    public Transform WeaponHandAnchor => weaponHandAnchor;
    public WeaponDefinition Definition => definition;
    public bool IsRemotePresentationActive => remotePresentationEnabled;
    public ThirdPersonWeaponVisual CurrentVisual => currentVisual;
    public Transform RightHandIkTarget => currentVisual != null ? currentVisual.GripR : null;
    public Transform LeftHandIkTarget => currentVisual != null ? currentVisual.GripL : null;
    public Transform RightElbowHint => currentVisual != null ? currentVisual.RightElbowHint : null;
    public Transform LeftElbowHint => currentVisual != null ? currentVisual.LeftElbowHint : null;
    public Transform AimMarker => currentVisual != null ? currentVisual.Aim : null;
    public ThirdPersonWeaponPose ActiveThirdPersonPose =>
        definition != null ? definition.ThirdPersonPose : null;

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
        if (thirdPersonRig == null)
            thirdPersonRig = GetComponent<ThirdPersonWeaponRig>();

        ResolveHierarchyFallbacks();
        CacheRestPoses();
        PrepareAudioSource();
        PreparePresentationObject();
        SetWorldWeaponActive(false);
    }

    public override void OnNetworkSpawn()
    {
        remotePresentationEnabled = !IsOwner;
        if (!remotePresentationEnabled)
        {
            SetWorldWeaponActive(false);
            enabled = false;
            return;
        }

        if (weaponKick != null && weaponKick.childCount == 0 && definition != null)
            RebuildWorldModel(definition);

        AttachToAnchor();
        BindThirdPersonVisual();
        RefreshRemoteVisibility();
        ResetPresentation();
        thirdPersonRig?.NotifyWeaponChanged();
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
    }

    public void PrepareEditorPreview(WeaponDefinition next)
    {
        remotePresentationEnabled = true;
        enabled = true;
        ApplyDefinition(next);
        SetWorldWeaponActive(true);
        BindThirdPersonVisual();
        AttachToAnchor();
    }

    public void ApplyDefinition(WeaponDefinition next)
    {
        bool sameWeapon = definition == next && currentVisual != null;
        definition = next;
        if (IsSpawned && IsOwner)
            return;
        if (sameWeapon)
            return;

        RebuildWorldModel(next);
        AttachToAnchor();
        CacheRestPoses();
        PrepareAudioSource();
        PreparePresentationObject();
        BindThirdPersonVisual();
        ResetPresentation();
        thirdPersonRig?.NotifyWeaponChanged();
    }

    public void PlayFirePresentation()
    {
        if (!CanPresent())
            return;

        WeaponPresentationConfig config = Config;
        PlayClip(config != null ? config.FireSfx : null, config != null ? config.WorldFireSfxVolume : 1f);
        SpawnMuzzleEffect();
        // REQ-049: do not kick the world weapon. Grip_R / Grip_L live on that
        // transform, so a kick yanks both arms outward through IK.
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
        currentVisual = null;

        if (next == null || next.WorldPrefab == null)
            return;

        GameObject instance = Instantiate(next.WorldPrefab, weaponKick);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        FittedWeaponModel fit = instance.GetComponentInChildren<FittedWeaponModel>(true);
        if (fit != null)
            fit.ForceFit();
        ApplyLayerRecursively(instance, WorldWeaponLayerName);
        DisableGameplayCollision(instance);
        currentVisual = instance.GetComponent<ThirdPersonWeaponVisual>();
        if (currentVisual != null)
            currentVisual.ResolveFallbacks();
        muzzlePoint = currentVisual != null && currentVisual.Muzzle != null
            ? currentVisual.Muzzle
            : ThirdPersonWeaponMarkers.Find(instance.transform, ThirdPersonWeaponMarkers.MuzzleAliases);
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
        thirdPersonRig?.ResetAfterRespawn();
        thirdPersonRig?.NotifyWeaponChanged();
    }

    public void BindThirdPersonVisual()
    {
        if (weaponKick == null)
            ResolveHierarchyFallbacks();
        AttachToAnchor();
        if (currentVisual == null && weaponKick != null)
            currentVisual = weaponKick.GetComponentInChildren<ThirdPersonWeaponVisual>(true);
        if (currentVisual != null)
            currentVisual.ResolveFallbacks();
        if (muzzlePoint == null && currentVisual != null)
            muzzlePoint = currentVisual.Muzzle;
        if (muzzlePoint == null)
            muzzlePoint = FindChildByName(weaponKick != null ? weaponKick : worldWeaponRoot, "Muzzle")
                ?? FindChildByName(weaponKick != null ? weaponKick : worldWeaponRoot, "MuzzlePoint");
    }

    public void AttachToSocket()
    {
        AttachToAnchor();
    }

    public void AttachToAnchor()
    {
        if (IsSpawned && IsOwner)
            return;

        ResolveHierarchyFallbacks();
        Transform anchor = ResolveWeaponAnchor();
        if (worldWeaponRoot == null || anchor == null)
            return;

        // Mixamo bones are scaled 0.01. Parenting the gun under the chest
        // shrinks it. Keep the mesh under the player-scale hand anchor and
        // copy the independent weapon-anchor pose in world space.
        if (weaponHandAnchor != null && worldWeaponRoot.parent != weaponHandAnchor)
            worldWeaponRoot.SetParent(weaponHandAnchor, true);

        Vector3 localScale = definition != null ? definition.WorldLocalScale : Vector3.one;
        worldWeaponRoot.SetPositionAndRotation(anchor.position, anchor.rotation);
        AlignWeaponToAim(anchor.rotation);
        worldWeaponRoot.localScale = CounterParentScale(localScale, worldWeaponRoot.parent);

        if (weaponKick != null && kickRoutine == null)
        {
            kickRestLocalPosition = Vector3.zero;
            kickRestLocalRotation = Quaternion.identity;
            weaponKick.localPosition = kickRestLocalPosition;
            weaponKick.localRotation = kickRestLocalRotation;
        }
    }

    private void AlignWeaponToAim(Quaternion desiredAimRotation)
    {
        if (currentVisual == null)
            currentVisual = weaponKick != null
                ? weaponKick.GetComponentInChildren<ThirdPersonWeaponVisual>(true)
                : null;
        currentVisual?.ResolveFallbacks();
        Transform aim = currentVisual != null ? currentVisual.Aim : null;
        if (aim == null)
            return;

        Quaternion aimLocal = Quaternion.Inverse(worldWeaponRoot.rotation) * aim.rotation;
        worldWeaponRoot.rotation = desiredAimRotation * Quaternion.Inverse(aimLocal);
    }

    private static Vector3 CounterParentScale(Vector3 desiredWorldScale, Transform parent)
    {
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        return new Vector3(
            desiredWorldScale.x / ScaleComponent(parentScale.x),
            desiredWorldScale.y / ScaleComponent(parentScale.y),
            desiredWorldScale.z / ScaleComponent(parentScale.z));
    }

    private static float ScaleComponent(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : value;
    }

    private Transform ResolveWeaponAnchor()
    {
        if (thirdPersonRig != null && thirdPersonRig.WeaponAnchor != null)
            return thirdPersonRig.WeaponAnchor;

        PlayerVisualRig visualRig = GetComponentInChildren<PlayerVisualRig>(true);
        if (visualRig != null && visualRig.ThirdPersonWeaponAnchor != null)
            return visualRig.ThirdPersonWeaponAnchor;

        return FindChildByName(transform, "ThirdPersonWeaponAnchor") ?? ResolveSocket();
    }

    private Transform ResolveSocket()
    {
        if (weaponSocket != null)
            return weaponSocket;
        if (thirdPersonRig != null)
            weaponSocket = thirdPersonRig.WeaponSocket;
        if (weaponSocket == null)
        {
            PlayerVisualRig visualRig = GetComponentInChildren<PlayerVisualRig>(true);
            if (visualRig != null)
                weaponSocket = visualRig.RightHandWeaponSocket;
        }

        if (weaponSocket == null)
            weaponSocket = FindChildByName(transform, "RightHandWeaponSocket")
                ?? FindChildByName(transform, "WeaponSocket");
        return weaponSocket;
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
        if (worldWeaponRoot != null && worldWeaponRoot.gameObject.activeSelf != active)
            worldWeaponRoot.gameObject.SetActive(active);
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
        {
            Transform search = weaponKick != null ? weaponKick : worldWeaponRoot;
            muzzlePoint = FindChildByName(search, "Muzzle") ?? FindChildByName(search, "MuzzlePoint");
        }

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
        Transform root = worldWeaponRoot != null ? worldWeaponRoot : weaponHandAnchor;
        if (root == null)
            return;

        ApplyLayerRecursively(root.gameObject, WorldWeaponLayerName);
        DisableGameplayCollision(root.gameObject);
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
