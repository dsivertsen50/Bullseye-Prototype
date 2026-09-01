using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// One pooled cosmetic bullet mark. Uses an HDRP Decal Projector when present,
/// otherwise a mesh quad.
/// </summary>
public class BulletImpactDecal : MonoBehaviour
{
    private static readonly int UnlitColorId = Shader.PropertyToID("_UnlitColor");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private DecalProjector projector;
    [SerializeField] private MeshRenderer meshRenderer;

    private MaterialPropertyBlock propertyBlock;
    private float expireTime;
    private float fadeStartTime;
    private Color meshBaseColor = Color.white;
    private int meshColorProperty = 0;

    public bool IsExpired => Time.unscaledTime >= expireTime;
    public bool UsesProjector => projector != null;

    private void Awake()
    {
        CacheComponents();
    }

    public void Play(
        Vector3 point,
        Vector3 normal,
        float size,
        Material material,
        float lifetime,
        float fadeDuration,
        float surfaceOffset,
        float rotationDegrees)
    {
        CacheComponents();

        Vector3 n = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        bool projectorMode = projector != null;
        Vector3 position = point + n * (projectorMode ? 0.001f : surfaceOffset);
        Quaternion facing = projectorMode
            ? Quaternion.LookRotation(-n)
            : Quaternion.LookRotation(n);
        transform.SetPositionAndRotation(position, facing * Quaternion.Euler(0f, 0f, rotationDegrees));

        if (projector != null)
        {
            float depth = Mathf.Max(0.08f, size * 0.7f);
            projector.size = new Vector3(size, size, depth);
            projector.pivot = new Vector3(0f, 0f, 0.5f);
            projector.fadeFactor = 1f;
            if (material != null)
                projector.material = material;
        }

        if (meshRenderer != null)
        {
            transform.localScale = new Vector3(size, size, 1f);
            if (material != null)
                meshRenderer.sharedMaterial = material;
            CacheMeshColor(material);
            ApplyMeshFade(1f);
        }

        float duration = Mathf.Max(0.1f, lifetime);
        float fade = Mathf.Clamp(fadeDuration, 0f, duration);
        expireTime = Time.unscaledTime + duration;
        fadeStartTime = expireTime - fade;
        gameObject.SetActive(true);
    }

    public void Tick()
    {
        if (Time.unscaledTime <= fadeStartTime)
            return;

        float fade = Mathf.InverseLerp(expireTime, fadeStartTime, Time.unscaledTime);
        fade = Mathf.Clamp01(fade);
        if (projector != null)
            projector.fadeFactor = fade;
        if (meshRenderer != null)
            ApplyMeshFade(fade);
    }

    public void Sleep()
    {
        if (projector != null)
            projector.fadeFactor = 1f;
        if (meshRenderer != null)
            ApplyMeshFade(1f);

        gameObject.SetActive(false);
    }

    private void CacheComponents()
    {
        if (projector == null)
            projector = GetComponent<DecalProjector>();
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    private void CacheMeshColor(Material material)
    {
        meshColorProperty = 0;
        meshBaseColor = Color.white;
        if (material == null)
            return;

        if (material.HasProperty(UnlitColorId))
        {
            meshColorProperty = UnlitColorId;
            meshBaseColor = material.GetColor(UnlitColorId);
        }
        else if (material.HasProperty(BaseColorId))
        {
            meshColorProperty = BaseColorId;
            meshBaseColor = material.GetColor(BaseColorId);
        }
        else if (material.HasProperty(ColorId))
        {
            meshColorProperty = ColorId;
            meshBaseColor = material.GetColor(ColorId);
        }
    }

    private void ApplyMeshFade(float fade)
    {
        if (meshRenderer == null || meshColorProperty == 0)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propertyBlock);
        Color color = meshBaseColor;
        color.a *= fade;
        propertyBlock.SetColor(meshColorProperty, color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
