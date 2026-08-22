using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds a temporary recognizable rifle/shotgun mesh from primitives.
/// Used until final weapon models exist.
/// </summary>
public class PrototypeWeaponVisual : MonoBehaviour
{
    public enum Shape
    {
        Rifle,
        Shotgun
    }

    [SerializeField] private Shape shape = Shape.Rifle;
    [SerializeField] private Color metalColor = new(0.18f, 0.2f, 0.22f, 1f);
    [SerializeField] private Color furnitureColor = new(0.28f, 0.18f, 0.1f, 1f);
    [SerializeField] private bool rebuildOnAwake = true;

    private static Material metalMaterial;
    private static Material furnitureMaterial;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        if (rebuildOnAwake)
            Build();
    }

    public void Build()
    {
        ClearGenerated();

        if (shape == Shape.Shotgun)
            BuildShotgun();
        else
            BuildRifle();
    }

    private void BuildRifle()
    {
        CreatePart("Receiver", new Vector3(0f, 0.02f, 0.04f), new Vector3(0.045f, 0.07f, 0.22f), metalColor, true);
        CreatePart("Barrel", new Vector3(0f, 0.03f, 0.28f), new Vector3(0.018f, 0.018f, 0.32f), metalColor, true);
        CreatePart("Handguard", new Vector3(0f, 0.018f, 0.16f), new Vector3(0.038f, 0.04f, 0.16f), furnitureColor, false);
        CreatePart("Stock", new Vector3(0f, 0.01f, -0.14f), new Vector3(0.03f, 0.055f, 0.16f), furnitureColor, false);
        CreatePart("Magazine", new Vector3(0f, -0.05f, 0.02f), new Vector3(0.028f, 0.09f, 0.05f), metalColor, true);
        CreatePart("Sight", new Vector3(0f, 0.055f, 0.08f), new Vector3(0.012f, 0.02f, 0.04f), metalColor, true);
        EnsurePoint("AimPoint", new Vector3(0f, 0.068f, 0.08f));
        EnsurePoint("MuzzlePoint", new Vector3(0f, 0.03f, 0.45f));
    }

    private void BuildShotgun()
    {
        CreatePart("Receiver", new Vector3(0f, 0.015f, 0.02f), new Vector3(0.055f, 0.075f, 0.2f), metalColor, true);
        CreatePart("Barrel", new Vector3(0f, 0.03f, 0.22f), new Vector3(0.032f, 0.032f, 0.26f), metalColor, true);
        CreatePart("Pump", new Vector3(0f, 0.0f, 0.14f), new Vector3(0.045f, 0.04f, 0.1f), furnitureColor, false);
        CreatePart("Stock", new Vector3(0f, 0.0f, -0.13f), new Vector3(0.035f, 0.06f, 0.16f), furnitureColor, false);
        CreatePart("Forend", new Vector3(0f, -0.02f, 0.08f), new Vector3(0.04f, 0.03f, 0.08f), furnitureColor, false);
        EnsurePoint("AimPoint", new Vector3(0f, 0.06f, 0.06f));
        EnsurePoint("MuzzlePoint", new Vector3(0f, 0.03f, 0.36f));
    }

    private void CreatePart(string partName, Vector3 localPosition, Vector3 localScale, Color color, bool metal)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(transform, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = localScale;
        part.layer = gameObject.layer;

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
                Destroy(collider);
            else
                DestroyImmediate(collider);
        }

        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetMaterial(color, metal);
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    private void EnsurePoint(string pointName, Vector3 localPosition)
    {
        Transform existing = transform.Find(pointName);
        if (existing != null)
        {
            existing.localPosition = localPosition;
            return;
        }

        GameObject point = new(pointName);
        point.transform.SetParent(transform, false);
        point.transform.localPosition = localPosition;
        point.transform.localRotation = Quaternion.identity;
        point.layer = gameObject.layer;
    }

    private void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "AimPoint" || child.name == "MuzzlePoint")
                continue;

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

    private static Material GetMaterial(Color color, bool metal)
    {
        if (metal)
        {
            if (metalMaterial == null)
                metalMaterial = CreateLitMaterial("PrototypeWeaponMetal", color, 0.65f);
            return metalMaterial;
        }

        if (furnitureMaterial == null)
            furnitureMaterial = CreateLitMaterial("PrototypeWeaponFurniture", color, 0.2f);
        return furnitureMaterial;
    }

    private static Material CreateLitMaterial(string materialName, Color color, float metallic)
    {
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new(shader)
        {
            name = materialName,
            hideFlags = HideFlags.HideAndDontSave
        };
        if (material.HasProperty(BaseColorId))
            material.SetColor(BaseColorId, color);
        else
            material.color = color;

        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);

        return material;
    }
}
