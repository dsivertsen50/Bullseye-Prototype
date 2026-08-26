using System.Collections;
using UnityEngine;

/// <summary>
/// Cosmetic death presentation: shatter the intact bullseye, freeze the
/// corpse briefly, then hide body visuals. Gameplay death/respawn stay on
/// PlayerHealth. Fragment physics is local and is not networked.
/// </summary>
public class BullseyeShatterController : MonoBehaviour
{
    private const string DebrisLayerName = "BullseyeDebris";

    [Header("Shatter")]
    [SerializeField] private GameObject shatteredBullseyePrefab;
    [SerializeField] private Transform intactBullseye;
    [SerializeField] private float shatterForce = 2.5f;
    [SerializeField] private float shatterRadius = 1f;
    [SerializeField] private float upwardModifier = 0.2f;
    [SerializeField] private float fragmentLifetime = 4f;

    [Header("Death Presentation")]
    [SerializeField] private float deathFreezeDuration = 1.25f;
    [SerializeField] private Transform bodyVisual;
    [SerializeField] private GameObject worldHealthUi;

    [Header("Audio")]
    [SerializeField] private AudioClip[] bullseyeBreakSounds;
    [SerializeField, Range(0f, 1f)] private float breakVolume = 1f;

    [Header("Debug")]
    [SerializeField, Tooltip("Logs death shatter and corpse hide/restore. Leave off for normal play.")]
    private bool logDeathPresentation;

    private PlayerHealth playerHealth;
    private PlayerMovement playerMovement;
    private Renderer[] intactRenderers = System.Array.Empty<Renderer>();
    private Collider[] intactColliders = System.Array.Empty<Collider>();
    private Coroutine hideRoutine;
    private GameObject activeShatter;
    private bool shatteredThisLife;
    private bool corpseVisualsHidden;

    public bool AreCorpseVisualsHidden => corpseVisualsHidden;
    public float DeathFreezeDuration => Mathf.Max(0f, deathFreezeDuration);

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMovement = GetComponent<PlayerMovement>();
        ResolveReferences();
        CacheIntactBullseye();
    }

    public void HandleDeadChanged(bool dead)
    {
        if (!dead)
        {
            RestoreAfterRespawn();
            return;
        }

        BeginDeathPresentation();
    }

    public void CleanupForDespawn()
    {
        StopHideRoutine();
        DestroyActiveShatter();
        shatteredThisLife = false;
        corpseVisualsHidden = false;
    }

    private void BeginDeathPresentation()
    {
        if (shatteredThisLife)
            return;

        shatteredThisLife = true;
        corpseVisualsHidden = false;

        if (playerMovement != null)
            playerMovement.FreezeForDeath();

        CaptureBullseyePose(out Vector3 position, out Quaternion rotation, out Vector3 scale);
        LogDeath($"[Death] Player {ResolveOwnerId()} entered death state.");
        ShatterBullseye(position, rotation, scale);
        ScheduleCorpseHide();
    }

    public void ShatterBullseye()
    {
        CaptureBullseyePose(out Vector3 position, out Quaternion rotation, out Vector3 scale);
        ShatterBullseye(position, rotation, scale);
    }

    public void ShatterBullseye(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        HideIntactBullseye();
        PlayBreakSound(position);
        SpawnFragments(position, rotation, scale);
    }

    private void ScheduleCorpseHide()
    {
        StopHideRoutine();

        float delay = DeathFreezeDuration;
        if (playerHealth != null)
            delay = Mathf.Max(0f, DeathFreezeDuration - playerHealth.GetElapsedDeathTime());

        if (delay <= 0f)
        {
            HideCorpseVisuals();
            return;
        }

        hideRoutine = StartCoroutine(HideCorpseAfterDelay(delay));
    }

    private IEnumerator HideCorpseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        hideRoutine = null;
        HideCorpseVisuals();
    }

    private void HideCorpseVisuals()
    {
        if (!shatteredThisLife)
            return;

        corpseVisualsHidden = true;
        SetBodyVisible(false);
        LogDeath($"[Death] Hiding Player {ResolveOwnerId()} visuals.");
    }

    private void RestoreAfterRespawn()
    {
        StopHideRoutine();
        shatteredThisLife = false;
        corpseVisualsHidden = false;
        activeShatter = null;
        RestoreIntactBullseye();
        SetBodyVisible(true);
        LogDeath($"[Respawn] Restoring Player {ResolveOwnerId()}.");
    }

    private void HideIntactBullseye()
    {
        for (int i = 0; i < intactRenderers.Length; i++)
        {
            if (intactRenderers[i] == null)
                continue;
            intactRenderers[i].enabled = false;
            intactRenderers[i].forceRenderingOff = true;
        }

        for (int i = 0; i < intactColliders.Length; i++)
        {
            if (intactColliders[i] != null)
                intactColliders[i].enabled = false;
        }
    }

    private void RestoreIntactBullseye()
    {
        for (int i = 0; i < intactRenderers.Length; i++)
        {
            if (intactRenderers[i] == null)
                continue;
            intactRenderers[i].enabled = true;
            intactRenderers[i].forceRenderingOff = false;
        }

        for (int i = 0; i < intactColliders.Length; i++)
        {
            if (intactColliders[i] != null)
                intactColliders[i].enabled = true;
        }

        if (intactBullseye != null && !intactBullseye.gameObject.activeSelf)
            intactBullseye.gameObject.SetActive(true);
    }

    private void SetBodyVisible(bool visible)
    {
        Transform body = bodyVisual;
        if (body == null && playerMovement != null)
            body = playerMovement.BodyVisual;

        if (body != null && body.gameObject.activeSelf != visible)
            body.gameObject.SetActive(visible);

        if (worldHealthUi != null && worldHealthUi.activeSelf != visible)
            worldHealthUi.SetActive(visible);
    }

    private void SpawnFragments(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        DestroyActiveShatter();

        if (shatteredBullseyePrefab == null)
        {
            Debug.LogWarning(
                "[Bullseye] Shattered prefab is not assigned. Death continues without fragments.",
                this);
            return;
        }

        activeShatter = Instantiate(shatteredBullseyePrefab, position, rotation);
        activeShatter.transform.localScale = SanitizeScale(scale);
        StripNetworking(activeShatter);
        ApplyDebrisLayer(activeShatter);
        IgnoreGameplayCollisions(activeShatter);
        ApplyShatterForces(activeShatter, position);
        Destroy(activeShatter, Mathf.Max(0.1f, fragmentLifetime));
        LogDeath($"[Bullseye] Shatter triggered at ({position.x:0.00}, {position.y:0.00}, {position.z:0.00}).");
    }

    private void ApplyShatterForces(GameObject root, Vector3 center)
    {
        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null)
                continue;

            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.AddExplosionForce(
                Mathf.Max(0f, shatterForce),
                center,
                Mathf.Max(0.1f, shatterRadius),
                Mathf.Max(0f, upwardModifier),
                ForceMode.Impulse);
        }
    }

    private void PlayBreakSound(Vector3 position)
    {
        AudioClip clip = PickRandomClip(bullseyeBreakSounds);
        if (clip == null)
            return;

        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(breakVolume));
        LogDeath($"[DeathAudio] Playing {clip.name}.");
    }

    private void CaptureBullseyePose(out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        if (intactBullseye != null)
        {
            position = intactBullseye.position;
            rotation = intactBullseye.rotation;
            scale = intactBullseye.lossyScale;
            return;
        }

        position = transform.position + Vector3.up;
        rotation = transform.rotation;
        scale = Vector3.one;
    }

    private void ResolveReferences()
    {
        if (intactBullseye == null)
        {
            BullseyeTarget target = GetComponentInChildren<BullseyeTarget>(true);
            if (target != null)
                intactBullseye = target.transform;
        }

        if (bodyVisual == null && playerMovement != null)
            bodyVisual = playerMovement.BodyVisual;

        if (worldHealthUi == null)
        {
            Transform existing = transform.Find("WorldHealthUI");
            if (existing != null)
                worldHealthUi = existing.gameObject;
        }
    }

    private void CacheIntactBullseye()
    {
        if (intactBullseye == null)
        {
            intactRenderers = System.Array.Empty<Renderer>();
            intactColliders = System.Array.Empty<Collider>();
            return;
        }

        intactRenderers = intactBullseye.GetComponentsInChildren<Renderer>(true);
        intactColliders = intactBullseye.GetComponentsInChildren<Collider>(true);
    }

    private void IgnoreGameplayCollisions(GameObject root)
    {
        Collider[] fragmentColliders = root.GetComponentsInChildren<Collider>(true);
        Collider[] ownerColliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < fragmentColliders.Length; i++)
        {
            Collider fragment = fragmentColliders[i];
            if (fragment == null)
                continue;

            fragment.isTrigger = false;
            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider == null)
                    continue;
                Physics.IgnoreCollision(fragment, ownerCollider, true);
            }
        }

        PlayerMovement[] movers = FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude);
        for (int i = 0; i < movers.Length; i++)
        {
            PlayerMovement mover = movers[i];
            if (mover == null || mover.transform == transform)
                continue;

            Collider[] otherColliders = mover.GetComponentsInChildren<Collider>(true);
            for (int f = 0; f < fragmentColliders.Length; f++)
            {
                Collider fragment = fragmentColliders[f];
                if (fragment == null)
                    continue;
                for (int c = 0; c < otherColliders.Length; c++)
                {
                    if (otherColliders[c] != null)
                        Physics.IgnoreCollision(fragment, otherColliders[c], true);
                }
            }
        }
    }

    private static void ApplyDebrisLayer(GameObject root)
    {
        int layer = LayerMask.NameToLayer(DebrisLayerName);
        if (layer < 0)
            layer = LayerMask.NameToLayer("Ignore Raycast");
        if (layer < 0)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null)
                transforms[i].gameObject.layer = layer;
        }
    }

    private static void StripNetworking(GameObject root)
    {
        Unity.Netcode.NetworkObject[] networkObjects =
            root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true);
        for (int i = 0; i < networkObjects.Length; i++)
        {
            if (networkObjects[i] != null)
                Destroy(networkObjects[i]);
        }

        Unity.Netcode.Components.NetworkTransform[] networkTransforms =
            root.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true);
        for (int i = 0; i < networkTransforms.Length; i++)
        {
            if (networkTransforms[i] != null)
                Destroy(networkTransforms[i]);
        }
    }

    private void DestroyActiveShatter()
    {
        if (activeShatter == null)
            return;

        Destroy(activeShatter);
        activeShatter = null;
    }

    private void StopHideRoutine()
    {
        if (hideRoutine == null)
            return;

        StopCoroutine(hideRoutine);
        hideRoutine = null;
    }

    private static AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int valid = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                valid++;
        }

        if (valid <= 0)
            return null;

        int chosen = Random.Range(0, valid);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;
            if (chosen == 0)
                return clips[i];
            chosen--;
        }

        return null;
    }

    private static Vector3 SanitizeScale(Vector3 scale)
    {
        return new Vector3(
            Mathf.Abs(scale.x) < 0.0001f ? 1f : scale.x,
            Mathf.Abs(scale.y) < 0.0001f ? 1f : scale.y,
            Mathf.Abs(scale.z) < 0.0001f ? 1f : scale.z);
    }

    private ulong ResolveOwnerId()
    {
        if (playerHealth != null && playerHealth.IsSpawned)
            return playerHealth.OwnerClientId;
        return 0;
    }

    private void LogDeath(string message)
    {
        if (logDeathPresentation)
            Debug.Log(message, this);
    }

    private void OnDestroy()
    {
        StopHideRoutine();
        DestroyActiveShatter();
    }

    private void OnValidate()
    {
        shatterForce = Mathf.Max(0f, shatterForce);
        shatterRadius = Mathf.Max(0.1f, shatterRadius);
        upwardModifier = Mathf.Max(0f, upwardModifier);
        fragmentLifetime = Mathf.Max(0.1f, fragmentLifetime);
        deathFreezeDuration = Mathf.Max(0f, deathFreezeDuration);
        breakVolume = Mathf.Clamp01(breakVolume);
    }
}
