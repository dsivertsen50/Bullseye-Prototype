using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class BullseyeTarget : MonoBehaviour
{
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    [SerializeField] private float flashDuration = 0.12f;
    [SerializeField] private float flashScaleMultiplier = 1.35f;
    [SerializeField] private Color flashColor = new Color(2.5f, 2.5f, 2.5f, 1f);

    private PlayerHealth playerHealth;
    private Renderer cachedRenderer;
    private Vector3 baseScale;
    private MaterialPropertyBlock propertyBlock;
    private Color restEmissiveColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        playerHealth = GetComponentInParent<PlayerHealth>();
        cachedRenderer = GetComponent<Renderer>();
        baseScale = transform.localScale;
        propertyBlock = new MaterialPropertyBlock();
        ConfigureNonPhysicalTarget();

        if (cachedRenderer != null &&
            cachedRenderer.sharedMaterial != null &&
            cachedRenderer.sharedMaterial.HasProperty(EmissiveColorId))
        {
            restEmissiveColor = cachedRenderer.sharedMaterial.GetColor(EmissiveColorId);
        }
    }

    public void SetVisibleToLocalViewer(bool visible)
    {
        if (cachedRenderer != null)
            cachedRenderer.forceRenderingOff = !visible;
    }

    private void ConfigureNonPhysicalTarget()
    {
        if (TryGetComponent(out Rigidbody body))
            Destroy(body);

        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;
    }

    public bool IsOwnedBy(ulong clientId)
    {
        NetworkObject ownerObject = ResolveOwnerNetworkObject();
        return ownerObject != null && ownerObject.OwnerClientId == clientId;
    }

    public bool TryRegisterHit(ulong shooterClientId)
    {
        if (IsOwnedBy(shooterClientId))
            return false;

        if (playerHealth == null)
            return false;

        if (playerHealth.IsDead || playerHealth.CurrentHealth <= 0)
            return false;

        playerHealth.RegisterBullseyeHit();
        return true;
    }

    private NetworkObject ResolveOwnerNetworkObject()
    {
        if (playerHealth != null)
            return playerHealth.NetworkObject;

        return GetComponentInParent<NetworkObject>();
    }

    public void PlayHitFlash()
    {
        if (!isActiveAndEnabled)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        float elapsed = 0f;
        Vector3 peakScale = baseScale * flashScaleMultiplier;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float amount = 1f - Mathf.Clamp01(elapsed / flashDuration);
            transform.localScale = Vector3.Lerp(baseScale, peakScale, amount);
            ApplyFlashColor(amount);
            yield return null;
        }

        transform.localScale = baseScale;
        ApplyFlashColor(0f);
        flashRoutine = null;
    }

    private void ApplyFlashColor(float amount)
    {
        if (cachedRenderer == null ||
            cachedRenderer.sharedMaterial == null ||
            !cachedRenderer.sharedMaterial.HasProperty(EmissiveColorId))
        {
            return;
        }

        cachedRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissiveColorId, Color.Lerp(restEmissiveColor, flashColor, amount));
        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnValidate()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.isTrigger = true;
    }
}
