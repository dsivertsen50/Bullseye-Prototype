using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Attached bullseye representation: a world-space stamp on the skinned mesh
/// plus an HDRP decal projector. Hidden while the physical bullseye is out.
/// </summary>
public class BullseyeSurfaceVisual : MonoBehaviour
{
    private static readonly int PositionId = Shader.PropertyToID("_BullseyePosition");
    private static readonly int NormalId = Shader.PropertyToID("_BullseyeNormal");
    private static readonly int RadiusId = Shader.PropertyToID("_BullseyeRadius");
    private static readonly int EnabledId = Shader.PropertyToID("_BullseyeEnabled");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

    [SerializeField] private SkinnedMeshRenderer characterRenderer;
    [SerializeField] private Material stampMaterial;
    [SerializeField] private DecalProjector decalProjector;
    [SerializeField] private float stampRadius = 0.14f;
    [SerializeField] private float stampBrightness = 3.8f;
    [SerializeField] private float decalDepth = 0.18f;
    [SerializeField] private Color flashColor = new Color(2.5f, 2.5f, 2.5f, 1f);
    [SerializeField] private float flashDuration = 0.12f;

    private MaterialPropertyBlock propertyBlock;
    private int stampMaterialIndex = -1;
    private bool attachedVisible = true;
    private bool ownerSuppressed;
    private float flashUntil;
    private Color restDecalColor = Color.white;

    public float StampRadius
    {
        get => stampRadius;
        set => stampRadius = Mathf.Max(0.02f, value);
    }

    public void Configure(
        SkinnedMeshRenderer renderer,
        Material stamp,
        DecalProjector projector,
        float radius)
    {
        characterRenderer = renderer;
        stampMaterial = stamp;
        decalProjector = projector;
        stampRadius = Mathf.Max(0.02f, radius);
        EnsureStampSlot();
        CacheDecalColor();
    }

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        if (decalProjector != null && decalProjector.material != null)
            decalProjector.material = new Material(decalProjector.material);
        EnsureStampSlot();
        CacheDecalColor();
        ApplyEnabled(false);
    }

    private void CacheDecalColor()
    {
        if (decalProjector == null || decalProjector.material == null)
            return;

        if (decalProjector.material.HasProperty(EmissiveColorId))
            restDecalColor = decalProjector.material.GetColor(EmissiveColorId);
    }

    public void SetAttachedVisible(bool visible)
    {
        attachedVisible = visible;
        ApplyEnabled(IsShowing());
    }

    public void SetSuppressedForOwner(bool suppressed)
    {
        ownerSuppressed = suppressed;
        ApplyEnabled(IsShowing());
    }

    public void PlayHitFlash()
    {
        if (!IsShowing())
            return;

        flashUntil = Time.time + Mathf.Max(0.02f, flashDuration);
    }

    public void ApplyPose(Vector3 worldPosition, Vector3 worldNormal, Quaternion rotation)
    {
        bool show = IsShowing();
        ApplyEnabled(show);
        if (!show)
            return;

        if (characterRenderer != null && stampMaterialIndex >= 0)
        {
            characterRenderer.GetPropertyBlock(propertyBlock, stampMaterialIndex);
            propertyBlock.SetVector(PositionId, worldPosition);
            propertyBlock.SetVector(NormalId, worldNormal);
            propertyBlock.SetFloat(RadiusId, stampRadius);
            propertyBlock.SetFloat(EnabledId, 1f);
            propertyBlock.SetFloat(BrightnessId, stampBrightness);
            characterRenderer.SetPropertyBlock(propertyBlock, stampMaterialIndex);
        }

        if (decalProjector != null)
        {
            float size = stampRadius * 2.15f;
            decalProjector.size = new Vector3(size, size, Mathf.Max(0.04f, decalDepth));
            decalProjector.transform.SetPositionAndRotation(
                worldPosition + worldNormal * (decalDepth * 0.35f),
                rotation);
            decalProjector.fadeFactor = 1f;
            ApplyDecalFlash();
        }
    }

    private void LateUpdate()
    {
        ApplyDecalFlash();
    }

    private void ApplyDecalFlash()
    {
        if (decalProjector == null || decalProjector.material == null)
            return;

        float amount = 0f;
        if (Time.time < flashUntil)
            amount = 1f - Mathf.Clamp01((flashUntil - Time.time) / Mathf.Max(0.02f, flashDuration));

        if (decalProjector.material.HasProperty(EmissiveColorId))
            decalProjector.material.SetColor(EmissiveColorId, Color.Lerp(restDecalColor, flashColor, 1f - amount));
    }

    private bool IsShowing()
    {
        return attachedVisible && !ownerSuppressed;
    }

    private void ApplyEnabled(bool enabled)
    {
        if (characterRenderer != null && stampMaterialIndex >= 0)
        {
            characterRenderer.GetPropertyBlock(propertyBlock, stampMaterialIndex);
            propertyBlock.SetFloat(EnabledId, enabled ? 1f : 0f);
            characterRenderer.SetPropertyBlock(propertyBlock, stampMaterialIndex);
        }

        if (decalProjector != null)
        {
            decalProjector.enabled = enabled;
            decalProjector.fadeFactor = enabled ? 1f : 0f;
        }
    }

    private void EnsureStampSlot()
    {
        if (characterRenderer == null || stampMaterial == null)
        {
            stampMaterialIndex = -1;
            return;
        }

        Material[] materials = characterRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == stampMaterial)
            {
                stampMaterialIndex = i;
                return;
            }
        }

        var next = new Material[materials.Length + 1];
        for (int i = 0; i < materials.Length; i++)
            next[i] = materials[i];
        next[materials.Length] = stampMaterial;
        characterRenderer.sharedMaterials = next;
        stampMaterialIndex = materials.Length;
    }

    private void OnValidate()
    {
        stampRadius = Mathf.Max(0.02f, stampRadius);
        stampBrightness = Mathf.Max(1f, stampBrightness);
        decalDepth = Mathf.Max(0.03f, decalDepth);
        flashDuration = Mathf.Max(0.02f, flashDuration);
    }
}
