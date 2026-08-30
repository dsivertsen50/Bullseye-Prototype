using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// REQ-039: rename AK-specific Rifle assets and add the DMR as a semi-automatic
/// secondary weapon using the existing weapon architecture.
/// </summary>
public static class DmrWeaponSetup
{
    private const string LegacyWeaponFolder = "Assets/Weapons/Semi-automatic Rifle";
    private const string WeaponFolder = "Assets/Weapons/DMR";
    private const string GameplayPath = "Assets/Weapons/DMR/Prefabs/DMR_Gameplay.prefab";
    private const string VisualPrefabPath = "Assets/Weapons/DMR/Prefabs/DMR Model V1.prefab";
    private const string DefinitionPath = "Assets/Scripts/Weapons/DMRDefinition.asset";
    private const string PresentationPath = "Assets/Scripts/Weapons/DMRPresentation.asset";
    private const string CatalogPath = "Assets/Scripts/Weapons/WeaponCatalog.asset";
    private const string ScenePath = "Assets/ArenaPrototype.unity";
    private const string MaskTexturePath = WeaponFolder + "/Textures/DMR_Mask.png";
    private const float TargetLength = 0.85f;

    [InitializeOnLoadMethod]
    private static void AutoSetupIfNeeded()
    {
        // Runtime FittedWeaponModel now owns DMR sizing. Do not auto-overwrite the prefab.
    }

    [MenuItem("Bullseye/Weapons/Setup DMR Weapon (REQ-039)")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static void SetupBatch()
    {
        string result = SetupInternal();
        Debug.Log(result);
        if (result.StartsWith("FAILED"))
            EditorApplication.Exit(1);
    }

    public static string SetupInternal()
    {
        RenameAkSpecificAssets();
        if (!OrganizeDmrFolder(out string fbxPath, out string materialPath, out string diffusePath, out string roughnessPath))
            return "FAILED: DMR model or textures were not found under Assets/Weapons";

        ConfigureTextures(diffusePath, roughnessPath);
        Material material = ConfigureMaterial(materialPath, diffusePath, roughnessPath);
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null)
            return "FAILED: DMR FBX did not import as a GameObject";

        Mesh mesh = FindPrimaryMesh(fbxPath);
        if (mesh == null)
            return "FAILED: DMR FBX has no mesh";

        CreateVisualPrefab(mesh, material);
        GameObject gameplay = CreateGameplayPrefab(mesh);
        if (gameplay == null)
            return "FAILED: could not create DMR_Gameplay.prefab";

        GameObject world = ThirdPersonWeaponSetup.CreateWrapper(
            "ThirdPerson_DMR",
            gameplay,
            ThirdPersonWeaponSetup.DmrPath,
            new Vector3(0.02f, -0.02f, 0.22f));
        if (world == null)
            return "FAILED: could not create ThirdPerson_DMR.prefab";

        WeaponPresentationConfig presentation = CreatePresentation();
        WeaponDefinition definition = CreateDefinition(gameplay, world, presentation);
        if (definition == null)
            return "FAILED: could not create DMRDefinition.asset";

        if (!AddToCatalog(definition))
            return "FAILED: WeaponCatalog is missing";

        AssignScenePickup(definition);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: DMR weapon assets created and AK Rifle naming cleaned up";
    }

    private static void RenameAkSpecificAssets()
    {
        RenameAsset("Assets/Scripts/Weapons/RifleDefinition.asset", "Assets/Scripts/Weapons/AKDefinition.asset");
        RenameAsset("Assets/Scripts/Weapons/RiflePresentation.asset", "Assets/Scripts/Weapons/AKPresentation.asset");
        RenameAsset(
            "Assets/Scripts/Weapons/Animations/RiflePresentation.overrideController",
            "Assets/Scripts/Weapons/Animations/AKPresentation.overrideController");

        WeaponDefinition ak = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(ThirdPersonWeaponSetup.ResolveAkDefinitionPath());
        if (ak == null)
            return;

        SerializedObject so = new SerializedObject(ak);
        so.FindProperty("weaponId").stringValue = "ak";
        so.FindProperty("displayName").stringValue = "AK";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ak);
    }

    private static bool OrganizeDmrFolder(
        out string fbxPath,
        out string materialPath,
        out string diffusePath,
        out string roughnessPath)
    {
        fbxPath = null;
        materialPath = null;
        diffusePath = null;
        roughnessPath = null;

        string sourceFolder = AssetDatabase.IsValidFolder(LegacyWeaponFolder)
            ? LegacyWeaponFolder
            : WeaponFolder;
        if (!AssetDatabase.IsValidFolder(sourceFolder))
            return false;

        EnsureFolder(WeaponFolder);
        EnsureFolder(WeaponFolder + "/Prefabs");
        EnsureFolder(WeaponFolder + "/Materials");
        EnsureFolder(WeaponFolder + "/Textures");

        fbxPath = FindAsset("t:Model", sourceFolder, "DMR Model V1.fbx");
        if (string.IsNullOrEmpty(fbxPath))
            fbxPath = FindAsset("t:Model", sourceFolder, ".fbx");
        if (string.IsNullOrEmpty(fbxPath))
            return false;

        materialPath = FindAsset("t:Material", sourceFolder, "MAT_");
        if (string.IsNullOrEmpty(materialPath))
            materialPath = WeaponFolder + "/Materials/MAT_DMR.mat";

        string desiredMat = WeaponFolder + "/Materials/MAT_DMR.mat";
        if (materialPath != desiredMat && File.Exists(ToAbsolute(materialPath)))
        {
            RenameAsset(materialPath, desiredMat);
            materialPath = desiredMat;
        }

        diffusePath = FindAsset("t:Texture2D", sourceFolder, "Diffuse");
        roughnessPath = FindAsset("t:Texture2D", sourceFolder, "Roughness");
        return !string.IsNullOrEmpty(diffusePath) && !string.IsNullOrEmpty(roughnessPath);
    }

    private static void ConfigureTextures(string diffusePath, string roughnessPath)
    {
        TextureImporter diffuseImporter = AssetImporter.GetAtPath(diffusePath) as TextureImporter;
        if (diffuseImporter != null && !diffuseImporter.sRGBTexture)
        {
            diffuseImporter.sRGBTexture = true;
            diffuseImporter.SaveAndReimport();
        }

        TextureImporter roughnessImporter = AssetImporter.GetAtPath(roughnessPath) as TextureImporter;
        if (roughnessImporter == null)
            return;

        bool dirty = roughnessImporter.sRGBTexture || !roughnessImporter.isReadable;
        roughnessImporter.sRGBTexture = false;
        roughnessImporter.isReadable = true;
        if (dirty)
            roughnessImporter.SaveAndReimport();
    }

    private static Material ConfigureMaterial(string materialPath, string diffusePath, string roughnessPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(roughnessPath);
        Texture2D mask = BuildSmoothnessMask(roughness);

        material.name = "MAT_DMR";
        material.SetTexture("_BaseColorMap", diffuse);
        material.SetTexture("_MainTex", diffuse);
        if (mask != null)
        {
            material.SetTexture("_MaskMap", mask);
            material.EnableKeyword("_MASKMAP");
            material.SetFloat("_MetallicRemapMin", 0f);
            material.SetFloat("_MetallicRemapMax", 0f);
            material.SetFloat("_SmoothnessRemapMin", 0f);
            material.SetFloat("_SmoothnessRemapMax", 1f);
            material.SetFloat("_AORemapMin", 1f);
            material.SetFloat("_AORemapMax", 1f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D BuildSmoothnessMask(Texture2D roughness)
    {
        if (roughness == null)
            return null;

        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskTexturePath);
        if (existing != null)
            return existing;

        Color[] source;
        try
        {
            source = roughness.GetPixels();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Could not read DMR roughness pixels for mask conversion: " + exception.Message);
            return roughness;
        }

        var mask = new Texture2D(roughness.width, roughness.height, TextureFormat.RGBA32, true, true);
        var pixels = new Color[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            float roughnessValue = source[i].r;
            pixels[i] = new Color(0f, 1f, 0f, 1f - roughnessValue);
        }

        mask.SetPixels(pixels);
        mask.Apply();
        File.WriteAllBytes(MaskTexturePath, mask.EncodeToPNG());
        Object.DestroyImmediate(mask);
        AssetDatabase.ImportAsset(MaskTexturePath);

        TextureImporter importer = AssetImporter.GetAtPath(MaskTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(MaskTexturePath);
    }

    private static GameObject CreateVisualPrefab(Mesh mesh, Material material)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
        if (existing != null)
            return existing;

        GameObject root = new GameObject("DMR Model V1");
        try
        {
            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, VisualPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
    }

    private static bool NeedsDirectMeshRebuild()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath);
        if (existing == null)
            return true;

        Transform model = existing.transform.Find("Model");
        if (model == null || model.GetComponent<MeshFilter>() == null)
            return true;

        return false;
    }

    private static GameObject CreateGameplayPrefab(Mesh mesh)
    {
        RuntimeAnimatorController animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Scripts/Weapons/Animations/AKPresentation.overrideController");
        if (animator == null)
        {
            animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Scripts/Weapons/Animations/RiflePresentation.overrideController");
        }

        Material material = FindDmrMaterial();
        GameObject root = new GameObject("DMR_Gameplay");
        try
        {
            Animator gameplayAnimator = root.AddComponent<Animator>();
            gameplayAnimator.runtimeAnimatorController = animator;

            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = new Quaternion(0.5f, -0.5f, -0.5f, -0.5f);
            model.transform.localScale = Vector3.one;

            MeshFilter filter = model.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = model.AddComponent<MeshRenderer>();
            if (material != null)
                renderer.sharedMaterial = material;

            AlignLongAxisToForward(model.transform, mesh);
            Bounds local = GetLocalRendererBounds(root.transform);
            if (local.size.z < local.size.x * 0.85f)
            {
                model.transform.localRotation = new Quaternion(0.5f, -0.5f, -0.5f, -0.5f);
                local = GetLocalRendererBounds(root.transform);
            }

            float longest = Mathf.Max(local.size.x, Mathf.Max(local.size.y, local.size.z));
            if (longest > 0.01f)
                model.transform.localScale = Vector3.one * (TargetLength / longest);

            local = GetLocalRendererBounds(root.transform);
            CreatePoint(root.transform, "MuzzlePoint", new Vector3(local.center.x, local.center.y, local.max.z));
            CreatePoint(
                root.transform,
                "AimPoint",
                new Vector3(local.center.x, local.max.y, Mathf.Lerp(local.min.z, local.max.z, 0.28f)));

            PrefabUtility.SaveAsPrefabAsset(root, GameplayPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath);
    }

    private static Material FindDmrMaterial()
    {
        string[] folders = { WeaponFolder, LegacyWeaponFolder };
        for (int i = 0; i < folders.Length; i++)
        {
            if (!AssetDatabase.IsValidFolder(folders[i]))
                continue;

            string path = FindAsset("t:Material", folders[i], "MAT_DMR");
            if (string.IsNullOrEmpty(path))
                path = FindAsset("t:Material", folders[i], "MAT_");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
        }

        return null;
    }

    private static WeaponPresentationConfig CreatePresentation()
    {
        WeaponPresentationConfig existing = AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(PresentationPath);
        if (existing != null)
            return existing;

        string akPresentation = AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(
            "Assets/Scripts/Weapons/AKPresentation.asset") != null
            ? "Assets/Scripts/Weapons/AKPresentation.asset"
            : "Assets/Scripts/Weapons/RiflePresentation.asset";

        if (!AssetDatabase.CopyAsset(akPresentation, PresentationPath))
            return null;

        WeaponPresentationConfig presentation = AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(PresentationPath);
        SerializedObject so = new SerializedObject(presentation);
        so.FindProperty("weaponName").stringValue = "DMR";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);
        return presentation;
    }

    private static WeaponDefinition CreateDefinition(
        GameObject gameplay,
        GameObject world,
        WeaponPresentationConfig presentation)
    {
        WeaponDefinition existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionPath);
        if (existing == null)
        {
            if (!AssetDatabase.CopyAsset(ThirdPersonWeaponSetup.ResolveAkDefinitionPath(), DefinitionPath))
                return null;
            existing = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionPath);
        }

        SerializedObject so = new SerializedObject(existing);
        so.FindProperty("weaponId").stringValue = "dmr";
        so.FindProperty("displayName").stringValue = "DMR";
        so.FindProperty("automatic").boolValue = false;
        so.FindProperty("firstPersonPrefab").objectReferenceValue = gameplay;
        so.FindProperty("worldPrefab").objectReferenceValue = world;
        so.FindProperty("pickupPrefab").objectReferenceValue = gameplay;
        so.FindProperty("presentation").objectReferenceValue = presentation;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static bool AddToCatalog(WeaponDefinition definition)
    {
        WeaponCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
        if (catalog == null)
            return false;

        SerializedObject so = new SerializedObject(catalog);
        SerializedProperty weapons = so.FindProperty("weapons");
        for (int i = 0; i < weapons.arraySize; i++)
        {
            if (weapons.GetArrayElementAtIndex(i).objectReferenceValue == definition)
                return true;
        }

        weapons.arraySize++;
        weapons.GetArrayElementAtIndex(weapons.arraySize - 1).objectReferenceValue = definition;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return true;
    }

    private static void AssignScenePickup(WeaponDefinition definition)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        PrototypeGroundWeaponLayout layout = Object.FindFirstObjectByType<PrototypeGroundWeaponLayout>();
        if (layout == null)
        {
            EditorSceneManager.CloseScene(scene, true);
            return;
        }

        SerializedObject so = new SerializedObject(layout);
        SerializedProperty dmr = so.FindProperty("dmr");
        if (dmr != null && dmr.objectReferenceValue != definition)
        {
            dmr.objectReferenceValue = definition;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static void AlignLongAxisToForward(Transform model, Mesh mesh)
    {
        Vector3 size = mesh.bounds.size;
        if (size.z >= size.x && size.z >= size.y)
            return;

        if (size.x >= size.y && size.x >= size.z)
            model.localRotation = Quaternion.Euler(0f, -90f, 0f);
    }

    private static Bounds GetLocalRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        bool initialized = false;
        Bounds bounds = new Bounds();
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshFilter filter = renderers[i].GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;

            Vector3[] corners = GetBoundsCorners(filter.sharedMesh.bounds);
            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corners[c]));
                if (!initialized)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return initialized ? bounds : new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, 0.8f));
    }

    private static Vector3[] GetBoundsCorners(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };
    }

    private static void CreatePoint(Transform parent, string pointName, Vector3 localPosition)
    {
        GameObject point = new GameObject(pointName);
        point.transform.SetParent(parent, false);
        point.transform.localPosition = localPosition;
        point.transform.localRotation = Quaternion.identity;
    }

    private static Mesh FindPrimaryMesh(string assetPath)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        Mesh best = null;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is not Mesh mesh)
                continue;
            if (best == null || mesh.vertexCount > best.vertexCount)
                best = mesh;
        }

        return best;
    }

    private static string FindAsset(string filter, string folder, string nameContains)
    {
        string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return path;
        }

        return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
    }

    private static void RenameAsset(string from, string to)
    {
        if (from == to || !File.Exists(ToAbsolute(from)))
            return;
        if (File.Exists(ToAbsolute(to)))
            return;

        string error = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(error))
            Debug.LogError(error);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static string ToAbsolute(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }
}
