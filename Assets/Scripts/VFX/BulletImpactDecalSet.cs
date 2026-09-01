using UnityEngine;

[CreateAssetMenu(
    fileName = "BulletImpactDecalSet",
    menuName = "Bullseye/VFX/Bullet Impact Decal Set")]
public class BulletImpactDecalSet : ScriptableObject
{
    [SerializeField, Tooltip("Shared bullet-hole materials. One is chosen at random per impact.")]
    private Material[] variants = System.Array.Empty<Material>();

    public int Count => variants != null ? variants.Length : 0;
    public bool HasVariants => Count > 0;

    public Material GetVariant(int randomValue)
    {
        if (!HasVariants)
            return null;

        int index = randomValue % variants.Length;
        if (index < 0)
            index += variants.Length;

        return variants[index];
    }

    public Material GetVariantOrDefault(int randomValue, Material fallback)
    {
        Material variant = GetVariant(randomValue);
        return variant != null ? variant : fallback;
    }
}
