using UnityEngine;

[CreateAssetMenu(
    fileName = "BulletImpactSettings",
    menuName = "Bullseye/VFX/Bullet Impact Settings")]
public class BulletImpactSettings : ScriptableObject
{
    public const string ResourcesName = "BulletImpactSettings";

    [Header("Prefab")]
    [SerializeField, Tooltip("Pooled decal instance. Swap the materials on the variant set to replace placeholder art.")]
    private GameObject decalPrefab;

    [Header("Shared Visuals")]
    [SerializeField] private BulletImpactDecalSet defaultVariantSet;
    [SerializeField, Min(0.01f), Tooltip("World size of a 1.0-scale weapon mark, in meters.")]
    private float baseSize = 0.12f;

    [Header("Lifetime")]
    [SerializeField, Min(0.1f)] private float lifetime = 45f;
    [SerializeField, Min(0f), Tooltip("Fade during the last N seconds of lifetime. 0 removes instantly.")]
    private float fadeDuration = 5f;

    [Header("Pooling")]
    [SerializeField, Min(1)] private int maxActiveDecals = 200;
    [SerializeField, Min(0)] private int prewarmCount = 32;

    [Header("Placement")]
    [SerializeField, Min(0f), Tooltip("Pushes mesh-based marks off the surface to avoid z-fighting.")]
    private float surfaceOffset = 0.004f;
    [SerializeField, Min(0f), Tooltip("Skip an extra pellet mark if it lands this close to one already accepted this shot.")]
    private float overlapDistance = 0.05f;
    [SerializeField, Tooltip("Surfaces on other layers never receive marks. Player colliders are always excluded.")]
    private LayerMask validLayers = ~0;

    [Header("Debug")]
    [SerializeField, Tooltip("Draws impact normals and logs distance rejections. Editor / development builds only unless enabled.")]
    private bool debugImpacts;

    public GameObject DecalPrefab => decalPrefab;
    public BulletImpactDecalSet DefaultVariantSet => defaultVariantSet;
    public float BaseSize => Mathf.Max(0.01f, baseSize);
    public float Lifetime => Mathf.Max(0.1f, lifetime);
    public float FadeDuration => Mathf.Clamp(fadeDuration, 0f, Lifetime);
    public int MaxActiveDecals => Mathf.Max(1, maxActiveDecals);
    public int PrewarmCount => Mathf.Clamp(prewarmCount, 0, MaxActiveDecals);
    public float SurfaceOffset => Mathf.Max(0f, surfaceOffset);
    public float OverlapDistance => Mathf.Max(0f, overlapDistance);
    public LayerMask ValidLayers => validLayers;
    public bool DebugImpacts => debugImpacts;

    public static BulletImpactSettings Load()
    {
        return Resources.Load<BulletImpactSettings>(ResourcesName);
    }

    private void OnValidate()
    {
        baseSize = Mathf.Max(0.01f, baseSize);
        lifetime = Mathf.Max(0.1f, lifetime);
        fadeDuration = Mathf.Max(0f, fadeDuration);
        maxActiveDecals = Mathf.Max(1, maxActiveDecals);
        prewarmCount = Mathf.Max(0, prewarmCount);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        overlapDistance = Mathf.Max(0f, overlapDistance);
    }
}
