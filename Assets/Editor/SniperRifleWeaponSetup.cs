using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// REQ-057: add the Sniper Rifle as a long gun with a dedicated variable-zoom
/// scope, using the existing weapon definition architecture.
/// </summary>
public static class SniperRifleWeaponSetup
{
    public const string WeaponFolder = "Assets/Weapons/Sniper Rifle";
    public const string PrefabFolder = WeaponFolder + "/Prefabs";
    public const string ScopeFolder = WeaponFolder + "/Scope";
    public const string GameplayPath = PrefabFolder + "/SniperRifle_Gameplay.prefab";
    public const string DefinitionPath = "Assets/Scripts/Weapons/SniperRifleDefinition.asset";
    public const string PresentationPath = "Assets/Scripts/Weapons/SniperRiflePresentation.asset";
    public const string ScopeDefinitionPath = "Assets/Scripts/Weapons/SniperScopeDefinition.asset";
    public const string CatalogPath = "Assets/Scripts/Weapons/WeaponCatalog.asset";
    public const string ScenePath = "Assets/ArenaPrototype.unity";
    public const string LongGunProfilePath =
        "Assets/Animations/ThirdPersonWeapons/Shared/LongGun/LongGunPoseProfile.asset";
    private const string OverlayPath = ScopeFolder + "/Sniper_ScopeHousing.png";
    private const string ReticlePath = ScopeFolder + "/Sniper_ScopeReticle.png";
    private const string VignettePath = ScopeFolder + "/Sniper_ScopeVignette.png";
    private const string MaskPath = ScopeFolder + "/Sniper_ScopeMask.png";
    private const float TargetLength = 1.12f;

    [MenuItem("Bullseye/Weapons/Setup Sniper Rifle (REQ-057)")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        if (!TryFindFbx(out string fbxPath))
            return "FAILED: Sniper Rifle FBX was not found under Assets/Weapons/Sniper Rifle";

        EnsureFolder(PrefabFolder);
        EnsureFolder(ScopeFolder);

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null)
            return "FAILED: Sniper Rifle FBX did not import as a GameObject";

        Mesh mesh = FindPrimaryMesh(fbxPath);
        if (mesh == null)
            return "FAILED: Sniper Rifle FBX has no mesh";

        GameObject gameplay = CreateGameplayPrefab(fbx, mesh);
        if (gameplay == null)
            return "FAILED: could not create SniperRifle_Gameplay.prefab";

        GameObject world = ThirdPersonWeaponSetup.CreateWrapper(
            "ThirdPerson_Sniper",
            gameplay,
            ThirdPersonWeaponSetup.SniperPath,
            new Vector3(0.02f, -0.02f, 0.32f));
        if (world == null)
            return "FAILED: could not create ThirdPerson_Sniper.prefab";

        if (!CreateScopePresentation(out ScopeDefinition scope))
            return "FAILED: could not create Sniper scope presentation";

        WeaponPresentationConfig presentation = CreatePresentation();
        if (presentation == null)
            return "FAILED: could not create SniperRiflePresentation.asset";

        WeaponDefinition definition = CreateDefinition(gameplay, world, presentation, scope);
        if (definition == null)
            return "FAILED: could not create SniperRifleDefinition.asset";

        if (!AddToCatalog(definition))
            return "FAILED: WeaponCatalog is missing";

        AssignScenePickup(definition);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: Sniper Rifle weapon, variable-zoom scope, catalog, and pickups are configured";
    }

    private static bool TryFindFbx(out string fbxPath)
    {
        fbxPath = FindAsset("t:Model", WeaponFolder, "Sniper Rifle V1.fbx");
        if (string.IsNullOrEmpty(fbxPath))
            fbxPath = FindAsset("t:Model", WeaponFolder, "Sniper Rifle.fbx");
        if (string.IsNullOrEmpty(fbxPath))
            fbxPath = FindAsset("t:Model", WeaponFolder, ".fbx");
        return !string.IsNullOrEmpty(fbxPath);
    }

    private static GameObject CreateGameplayPrefab(GameObject fbx, Mesh mesh)
    {
        RuntimeAnimatorController animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Scripts/Weapons/Animations/AKPresentation.overrideController");
        if (animator == null)
        {
            animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Scripts/Weapons/Animations/RiflePresentation.overrideController");
        }

        Material material = FindSniperMaterial();
        GameObject root = new GameObject("SniperRifle_Gameplay");
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
            Material[] shared = CollectFbxMaterials(fbx, material);
            if (shared != null && shared.Length > 0)
                renderer.sharedMaterials = shared;
            else if (material != null)
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

            FittedWeaponModel fitted = root.AddComponent<FittedWeaponModel>();
            SerializedObject fittedSo = new SerializedObject(fitted);
            fittedSo.FindProperty("sourceModel").objectReferenceValue = fbx;
            fittedSo.FindProperty("material").objectReferenceValue = material;
            fittedSo.FindProperty("targetLength").floatValue = TargetLength;
            fittedSo.ApplyModifiedPropertiesWithoutUndo();
            fitted.ForceFit();

            local = GetLocalRendererBounds(root.transform);
            Transform muzzle = root.transform.Find("MuzzlePoint");
            Transform aim = root.transform.Find("AimPoint");
            if (muzzle != null)
                muzzle.localPosition = new Vector3(local.center.x, local.center.y, local.max.z);
            if (aim != null)
                aim.localPosition = new Vector3(local.center.x, local.max.y, Mathf.Lerp(local.min.z, local.max.z, 0.28f));

            PrefabUtility.SaveAsPrefabAsset(root, GameplayPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath);
    }

    private static Material[] CollectFbxMaterials(GameObject fbx, Material fallback)
    {
        if (fbx == null)
            return fallback != null ? new[] { fallback } : null;

        Renderer[] renderers = fbx.GetComponentsInChildren<Renderer>(true);
        var materials = new System.Collections.Generic.List<Material>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] shared = renderers[i].sharedMaterials;
            for (int m = 0; m < shared.Length; m++)
            {
                if (shared[m] != null && !materials.Contains(shared[m]))
                    materials.Add(shared[m]);
            }
        }

        if (materials.Count == 0 && fallback != null)
            materials.Add(fallback);
        return materials.Count > 0 ? materials.ToArray() : null;
    }

    private static Material FindSniperMaterial()
    {
        string path = FindAsset("t:Material", WeaponFolder, "Material.009");
        if (string.IsNullOrEmpty(path))
            path = FindAsset("t:Material", WeaponFolder, "MAT_");
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static bool CreateScopePresentation(out ScopeDefinition scope)
    {
        Sprite mask = WriteSprite(MaskPath, ScopePlaceholderSprites.CreateHoleTexture(512));
        Sprite housing = WriteSprite(OverlayPath, ScopePlaceholderSprites.CreateSniperHousingTexture(512));
        Sprite vignette = WriteSprite(VignettePath, ScopePlaceholderSprites.CreateSniperVignetteTexture(512));
        Sprite reticle = WriteSprite(ReticlePath, ScopePlaceholderSprites.CreateSniperReticleTexture(512));
        if (mask == null || housing == null || vignette == null || reticle == null)
        {
            scope = null;
            return false;
        }

        scope = AssetDatabase.LoadAssetAtPath<ScopeDefinition>(ScopeDefinitionPath);
        if (scope == null)
        {
            scope = ScriptableObject.CreateInstance<ScopeDefinition>();
            AssetDatabase.CreateAsset(scope, ScopeDefinitionPath);
        }

        SerializedObject so = new SerializedObject(scope);
        so.FindProperty("style").enumValueIndex = (int)ScopePresentationType.Sniper;
        so.FindProperty("usesScopeOverlay").boolValue = true;
        so.FindProperty("lensRadius").floatValue = 0.74f;
        so.FindProperty("overlaySprite").objectReferenceValue = housing;
        so.FindProperty("reticleSprite").objectReferenceValue = reticle;
        so.FindProperty("vignetteSprite").objectReferenceValue = vignette;
        so.FindProperty("maskSprite").objectReferenceValue = mask;
        so.FindProperty("peripheralOpacity").floatValue = 1f;
        so.FindProperty("innerOpacity").floatValue = 0.05f;
        so.FindProperty("peripheralColor").colorValue = new Color(0f, 0f, 0f, 1f);
        so.FindProperty("housingColor").colorValue = Color.white;
        so.FindProperty("housingThickness").floatValue = 0.1f;
        so.FindProperty("vignetteStrength").floatValue = 0.48f;
        so.FindProperty("lensTint").colorValue = new Color(0.62f, 0.78f, 0.7f, 0.04f);
        so.FindProperty("hideHipFireReticle").boolValue = true;
        so.FindProperty("reticleColor").colorValue = new Color(0.92f, 0.93f, 0.88f, 0.95f);
        so.FindProperty("reticleScale").floatValue = 0.96f;
        so.FindProperty("reticleDotSize").floatValue = 0.012f;
        so.FindProperty("showMagnificationReadout").boolValue = true;
        so.FindProperty("showZoomBar").boolValue = true;
        so.FindProperty("zoomHudOffset").vector2Value = new Vector2(0.58f, 0.04f);
        so.FindProperty("zoomBadgeSize").floatValue = 0.26f;
        so.FindProperty("zoomBarHeight").floatValue = 0.16f;
        so.FindProperty("zoomBarWidth").floatValue = 0.02f;
        SerializedProperty curve = so.FindProperty("transitionCurve");
        if (curve != null)
            curve.animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scope);
        return true;
    }

    private static WeaponPresentationConfig CreatePresentation()
    {
        if (AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(PresentationPath) == null)
        {
            string source = AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(
                "Assets/Scripts/Weapons/DMRPresentation.asset") != null
                ? "Assets/Scripts/Weapons/DMRPresentation.asset"
                : "Assets/Scripts/Weapons/AKPresentation.asset";
            if (!AssetDatabase.CopyAsset(source, PresentationPath))
                return null;
        }

        WeaponPresentationConfig presentation = AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(PresentationPath);
        SerializedObject so = new SerializedObject(presentation);
        so.FindProperty("weaponName").stringValue = "Sniper Rifle";
        so.FindProperty("fireSfxVolume").floatValue = 0.9f;
        so.FindProperty("fireKickLocalPosition").vector3Value = new Vector3(0f, 0.006f, -0.07f);
        so.FindProperty("fireKickLocalEuler").vector3Value = new Vector3(-3.5f, 0f, 0f);
        so.FindProperty("fireKickPositionVariance").vector3Value = new Vector3(0.01f, 0.006f, 0.01f);
        so.FindProperty("fireKickEulerVariance").vector3Value = new Vector3(2.2f, 3.5f, 2.4f);
        so.FindProperty("fireKickDuration").floatValue = 0.07f;
        so.FindProperty("fireRecoverDuration").floatValue = 0.22f;
        so.FindProperty("hipLocalPosition").vector3Value = new Vector3(0.1f, -0.19f, 0.74f);
        so.FindProperty("hipLocalEuler").vector3Value = new Vector3(-4f, 1f, -1f);
        so.FindProperty("aimDistance").floatValue = 0.34f;
        so.FindProperty("adsBlendDuration").floatValue = 0.22f;
        so.FindProperty("aimInSpeed").floatValue = 4.6f;
        so.FindProperty("aimOutSpeed").floatValue = 6.2f;
        so.FindProperty("recoilPitch").floatValue = 3.6f;
        so.FindProperty("recoilYaw").floatValue = 0.85f;
        so.FindProperty("recoilPitchVariance").floatValue = 0.45f;
        so.FindProperty("recoilYawVariance").floatValue = 0.55f;
        so.FindProperty("worldFireKickLocalPosition").vector3Value = new Vector3(0f, 0.03f, -0.1f);
        so.FindProperty("worldFireKickLocalEuler").vector3Value = new Vector3(-24f, 5f, 4f);
        so.FindProperty("worldFireKickDuration").floatValue = 0.08f;
        so.FindProperty("worldFireRecoverDuration").floatValue = 0.22f;
        so.FindProperty("worldAudioMaxDistance").floatValue = 70f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);
        return presentation;
    }

    private static WeaponDefinition CreateDefinition(
        GameObject gameplay,
        GameObject world,
        WeaponPresentationConfig presentation,
        ScopeDefinition scope)
    {
        if (AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionPath) == null)
        {
            if (!AssetDatabase.CopyAsset("Assets/Scripts/Weapons/DMRDefinition.asset", DefinitionPath))
                return null;
        }

        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionPath);
        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("weaponId").stringValue = "sniper";
        so.FindProperty("displayName").stringValue = "Sniper Rifle";
        so.FindProperty("inventoryRole").enumValueIndex = (int)WeaponInventoryRole.TemporaryPickup;
        so.FindProperty("automatic").boolValue = false;
        so.FindProperty("firstPersonPrefab").objectReferenceValue = gameplay;
        so.FindProperty("worldPrefab").objectReferenceValue = world;
        so.FindProperty("pickupPrefab").objectReferenceValue = gameplay;
        so.FindProperty("presentation").objectReferenceValue = presentation;
        so.FindProperty("magazineSize").intValue = 5;
        so.FindProperty("startingMagazineAmmo").intValue = 5;
        so.FindProperty("startingReserveAmmo").intValue = 15;
        so.FindProperty("maximumReserveAmmo").intValue = 15;
        so.FindProperty("unlimitedReserve").boolValue = false;
        so.FindProperty("fireRate").floatValue = 1.25f;
        so.FindProperty("reloadTime").floatValue = 2.6f;
        so.FindProperty("usesMagnifiedAds").boolValue = true;
        so.FindProperty("adsMagnification").floatValue = 4f;
        so.FindProperty("adsEnterDuration").floatValue = 0.22f;
        so.FindProperty("adsExitDuration").floatValue = 0.16f;
        so.FindProperty("adsSensitivityMultiplier").floatValue = 0.28f;
        so.FindProperty("adsHidesViewmodel").boolValue = true;
        so.FindProperty("usesVariableAdsMagnification").boolValue = true;
        so.FindProperty("minAdsMagnification").floatValue = 4f;
        so.FindProperty("maxAdsMagnification").floatValue = 8f;
        so.FindProperty("defaultAdsMagnification").floatValue = 4f;
        so.FindProperty("adsZoomAdjustmentSpeed").floatValue = 2.2f;
        so.FindProperty("adsZoomInputDeadzone").floatValue = 0.2f;
        so.FindProperty("preserveAdsMagnificationOnExit").boolValue = false;
        so.FindProperty("adsSensitivityScalesWithMagnification").boolValue = true;
        so.FindProperty("locksHorizontalLocomotionWhileAds").boolValue = true;
        so.FindProperty("exitAdsWhenAirborne").boolValue = true;
        so.FindProperty("scopePresentation").objectReferenceValue = scope;
        so.FindProperty("weaponPoseClass").enumValueIndex = (int)ThirdPersonWeaponPoseClass.LongGun;
        so.FindProperty("poseClassAssigned").boolValue = true;
        so.FindProperty("thirdPersonPoseCategory").enumValueIndex = (int)ThirdPersonPoseCategory.LongGun;
        so.FindProperty("useLeftHandGrip").boolValue = true;
        so.FindProperty("supportHandIkEnabled").boolValue = true;
        so.FindProperty("thirdPersonClass").enumValueIndex = (int)ThirdPersonWeaponClass.Rifle;
        ThirdPersonWeaponPoseProfile longGun = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponPoseProfile>(LongGunProfilePath);
        if (longGun != null)
            so.FindProperty("thirdPersonPoseProfile").objectReferenceValue = longGun;

        SerializedProperty damage = so.FindProperty("damageSettings");
        damage.FindPropertyRelative("hitscanMode").enumValueIndex = (int)WeaponHitscanMode.Single;
        damage.FindPropertyRelative("pelletCount").intValue = 1;
        damage.FindPropertyRelative("baseDamage").floatValue = 8f;
        damage.FindPropertyRelative("damagePerPellet").floatValue = 8f;
        damage.FindPropertyRelative("guaranteeLethalHeadshot").boolValue = false;
        damage.FindPropertyRelative("falloffStart").floatValue = 220f;
        damage.FindPropertyRelative("falloffEnd").floatValue = 250f;
        damage.FindPropertyRelative("maximumRange").floatValue = 250f;
        damage.FindPropertyRelative("beyondMaxRange").enumValueIndex = (int)BeyondMaxRangeDamage.MinimumDamage;
        damage.FindPropertyRelative("minimumDamageMultiplier").floatValue = 1f;

        SerializedProperty accuracy = so.FindProperty("accuracy");
        accuracy.FindPropertyRelative("baseSpread").floatValue = 28f;
        accuracy.FindPropertyRelative("maxSpread").floatValue = 72f;
        accuracy.FindPropertyRelative("bloomPerShot").floatValue = 10f;
        accuracy.FindPropertyRelative("bloomRecoveryDelay").floatValue = 0.12f;
        accuracy.FindPropertyRelative("bloomRecoverySpeed").floatValue = 28f;
        accuracy.FindPropertyRelative("sprintSpread").floatValue = 52f;
        accuracy.FindPropertyRelative("sprintSpreadIncreaseSpeed").floatValue = 90f;
        accuracy.FindPropertyRelative("sprintSpreadRecoverySpeed").floatValue = 50f;
        accuracy.FindPropertyRelative("adsSpreadMultiplier").floatValue = 0f;
        accuracy.FindPropertyRelative("reticleElementLength").floatValue = 14f;
        accuracy.FindPropertyRelative("reticleElementThickness").floatValue = 2f;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
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
        SerializedProperty sniper = so.FindProperty("sniper");
        if (sniper != null && sniper.objectReferenceValue != definition)
        {
            sniper.objectReferenceValue = definition;
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

        return initialized ? bounds : new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, 1.1f));
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

    private static Sprite WriteSprite(string assetPath, Texture2D texture)
    {
        string absolute = ToAbsolute(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute));
        File.WriteAllBytes(absolute, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
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
        string project = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(project, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
