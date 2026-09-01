using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// REQ-044: placeholder bullet-hole art, pooled HDRP decal prefab, and
/// per-weapon surface-impact configuration.
/// </summary>
public static class BulletImpactSetup
{
    public const string RootFolder = "Assets/VFX/BulletImpacts";
    public const string TextureFolder = RootFolder + "/Textures";
    public const string MaterialFolder = RootFolder + "/Materials";
    public const string PrefabFolder = RootFolder + "/Prefabs";
    public const string ResourcesFolder = RootFolder + "/Resources";
    public const string SettingsPath = ResourcesFolder + "/BulletImpactSettings.asset";
    public const string DecalSetPath = RootFolder + "/BulletImpactDecalSet.asset";
    public const string PrefabPath = PrefabFolder + "/BulletImpactDecal.prefab";

    private static readonly string[] WeaponDefinitionPaths =
    {
        "Assets/Scripts/Weapons/Ruger22Definition.asset",
        "Assets/Scripts/Weapons/AKDefinition.asset",
        "Assets/Scripts/Weapons/DMRDefinition.asset",
        "Assets/Scripts/Weapons/ShotgunDefinition.asset"
    };

    [MenuItem("Bullseye/VFX/Setup Bullet Impact Decals (REQ-044)")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        EnsureFolders();

        Texture2D[] textures = new Texture2D[4];
        Material[] materials = new Material[4];
        for (int i = 0; i < 4; i++)
        {
            string texturePath = $"{TextureFolder}/BulletHole_0{i + 1}.png";
            textures[i] = WritePlaceholderTexture(texturePath, i);
            if (textures[i] == null)
                return $"FAILED: could not write {texturePath}";

            string materialPath = $"{MaterialFolder}/BulletHole_0{i + 1}.mat";
            materials[i] = CreateDecalMaterial(materialPath, textures[i]);
            if (materials[i] == null)
                return $"FAILED: could not create {materialPath}";
        }

        GameObject prefab = CreateDecalPrefab(materials[0]);
        if (prefab == null)
            return "FAILED: could not create BulletImpactDecal.prefab";

        BulletImpactDecalSet set = CreateDecalSet(materials);
        if (set == null)
            return "FAILED: could not create BulletImpactDecalSet.asset";

        BulletImpactSettings settings = CreateSettings(prefab, set);
        if (settings == null)
            return "FAILED: could not create BulletImpactSettings.asset";

        ConfigureWeapons(set);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: bullet impact decals are configured";
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "VFX");
        CreateFolder("Assets/VFX", "BulletImpacts");
        CreateFolder(RootFolder, "Textures");
        CreateFolder(RootFolder, "Materials");
        CreateFolder(RootFolder, "Prefabs");
        CreateFolder(RootFolder, "Resources");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Texture2D WritePlaceholderTexture(string assetPath, int variantIndex)
    {
        string absolute = ToAbsolute(assetPath);
        if (!File.Exists(absolute))
        {
            Texture2D generated = BulletHolePlaceholderTextures.Create(variantIndex, 256);
            File.WriteAllBytes(absolute, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static Material CreateDecalMaterial(string assetPath, Texture2D texture)
    {
        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null)
            shader = Shader.Find("HDRP/Decal");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            if (shader == null)
                return null;

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
            material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        }

        if (shader != null && material.shader != shader)
            material.shader = shader;

        if (material.HasProperty("_UnlitColorMap"))
            material.SetTexture("_UnlitColorMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColorMap"))
            material.SetTexture("_BaseColorMap", texture);
        if (material.HasProperty("_UnlitColor"))
            material.SetColor("_UnlitColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);

        if (material.HasProperty("_AlphaCutoffEnable"))
            material.SetFloat("_AlphaCutoffEnable", 1f);
        if (material.HasProperty("_AlphaCutoff"))
            material.SetFloat("_AlphaCutoff", 0.18f);
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", 0.18f);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", 0f);
        if (material.HasProperty("_OpaqueCullMode"))
            material.SetFloat("_OpaqueCullMode", 0f);
        if (material.HasProperty("_TransparentCullMode"))
            material.SetFloat("_TransparentCullMode", 0f);

        material.EnableKeyword("_ALPHATEST_ON");
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateDecalPrefab(Material previewMaterial)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            ConfigurePrefabContents(PrefabPath, previewMaterial);
            return existing;
        }

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        try
        {
            temp.name = "BulletImpactDecal";
            Collider collider = temp.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            ConfigureQuad(temp, previewMaterial);
            temp.AddComponent<BulletImpactDecal>();
            return PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    private static void ConfigurePrefabContents(string prefabPath, Material previewMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            DecalProjector projector = root.GetComponent<DecalProjector>();
            if (projector != null)
                Object.DestroyImmediate(projector);

            if (root.GetComponent<MeshFilter>() == null || root.GetComponent<MeshRenderer>() == null)
            {
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                try
                {
                    Mesh mesh = quad.GetComponent<MeshFilter>().sharedMesh;
                    MeshFilter filter = root.GetComponent<MeshFilter>();
                    if (filter == null)
                        filter = root.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    if (root.GetComponent<MeshRenderer>() == null)
                        root.AddComponent<MeshRenderer>();
                    Collider extra = root.GetComponent<Collider>();
                    if (extra != null)
                        Object.DestroyImmediate(extra);
                }
                finally
                {
                    Object.DestroyImmediate(quad);
                }
            }

            if (root.GetComponent<BulletImpactDecal>() == null)
                root.AddComponent<BulletImpactDecal>();
            ConfigureQuad(root, previewMaterial);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureQuad(GameObject root, Material previewMaterial)
    {
        MeshRenderer renderer = root.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = previewMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        Collider collider = root.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);
    }

    private static BulletImpactDecalSet CreateDecalSet(Material[] materials)
    {
        BulletImpactDecalSet set = AssetDatabase.LoadAssetAtPath<BulletImpactDecalSet>(DecalSetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<BulletImpactDecalSet>();
            AssetDatabase.CreateAsset(set, DecalSetPath);
        }

        SerializedObject so = new SerializedObject(set);
        SerializedProperty variants = so.FindProperty("variants");
        variants.arraySize = materials.Length;
        for (int i = 0; i < materials.Length; i++)
            variants.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        return set;
    }

    private static BulletImpactSettings CreateSettings(GameObject prefab, BulletImpactDecalSet set)
    {
        BulletImpactSettings settings = AssetDatabase.LoadAssetAtPath<BulletImpactSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<BulletImpactSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        SerializedObject so = new SerializedObject(settings);
        so.FindProperty("decalPrefab").objectReferenceValue = prefab;
        so.FindProperty("defaultVariantSet").objectReferenceValue = set;
        so.FindProperty("baseSize").floatValue = 0.12f;
        so.FindProperty("lifetime").floatValue = 45f;
        so.FindProperty("fadeDuration").floatValue = 5f;
        so.FindProperty("maxActiveDecals").intValue = 200;
        so.FindProperty("prewarmCount").intValue = 32;
        so.FindProperty("surfaceOffset").floatValue = 0.004f;
        so.FindProperty("overlapDistance").floatValue = 0.05f;
        so.FindProperty("validLayers").intValue = ~0;
        so.FindProperty("debugImpacts").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static void ConfigureWeapons(BulletImpactDecalSet set)
    {
        ConfigureWeapon(WeaponDefinitionPaths[0], 1f, 50f, WeaponImpactPattern.Single, 1, set);
        ConfigureWeapon(WeaponDefinitionPaths[1], 0.75f, 60f, WeaponImpactPattern.Single, 1, set);
        ConfigureWeapon(WeaponDefinitionPaths[2], 1f, 100f, WeaponImpactPattern.Single, 1, set);
        ConfigureWeapon(WeaponDefinitionPaths[3], 0.7f, 25f, WeaponImpactPattern.PelletSpread, 8, set);
    }

    private static void ConfigureWeapon(
        string path,
        float scale,
        float maxDistance,
        WeaponImpactPattern pattern,
        int maxPerShot,
        BulletImpactDecalSet set)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (definition == null)
        {
            Debug.LogWarning($"BulletImpactSetup skipped missing weapon definition: {path}");
            return;
        }

        SerializedObject so = new SerializedObject(definition);
        SerializedProperty impact = so.FindProperty("impactDecalSettings");
        if (impact == null)
        {
            Debug.LogWarning($"BulletImpactSetup could not find impactDecalSettings on {path}");
            return;
        }

        impact.FindPropertyRelative("enabled").boolValue = true;
        impact.FindPropertyRelative("decalScale").floatValue = scale;
        impact.FindPropertyRelative("maximumDecalDistance").floatValue = maxDistance;
        impact.FindPropertyRelative("variantSet").objectReferenceValue = set;
        impact.FindPropertyRelative("impactPattern").enumValueIndex = (int)pattern;
        impact.FindPropertyRelative("maxDecalsPerShot").intValue = maxPerShot;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static string ToAbsolute(string assetPath)
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(project, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
