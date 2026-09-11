using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// REQ-060: add the Bazooka as a Heavy projectile weapon using the existing
/// catalog, pose, pickup, and network-prefab pipelines.
/// </summary>
public static class BazookaWeaponSetup
{
    public const string WeaponFolder = "Assets/Weapons/Bazooka";
    public const string PrefabFolder = WeaponFolder + "/Prefabs";
    public const string AuthoredPrefabPath = PrefabFolder + "/Bazooka V1.prefab";
    public const string GameplayPath = PrefabFolder + "/Bazooka_Gameplay.prefab";
    public const string RocketPath = PrefabFolder + "/RocketProjectile.prefab";
    public const string PlaceholderModelPath = PrefabFolder + "/Bazooka_PlaceholderModel.prefab";
    public const string DefinitionPath = "Assets/Scripts/Weapons/BazookaDefinition.asset";
    public const string PresentationPath = "Assets/Scripts/Weapons/BazookaPresentation.asset";
    public const string CatalogPath = "Assets/Scripts/Weapons/WeaponCatalog.asset";
    public const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    public const string ScenePath = "Assets/ArenaPrototype.unity";
    public const string HeavyGunProfilePath =
        "Assets/Animations/ThirdPersonWeapons/Shared/HeavyGun/HeavyGunPoseProfile.asset";
    private const float TargetLength = 1.05f;

    [MenuItem("Bullseye/Weapons/Setup Bazooka (REQ-060)")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        EnsureFolder(PrefabFolder);

        NormalizeAuthoredPickupPrefab();
        GameObject modelSource = ResolveWeaponModel(out string modelNote);
        if (modelSource == null)
            return "FAILED: could not create a Bazooka model";

        GameObject gameplay = CreateGameplayPrefab(modelSource);
        if (gameplay == null)
            return "FAILED: could not create Bazooka_Gameplay.prefab";

        GameObject world = ThirdPersonWeaponSetup.CreateWrapper(
            "ThirdPerson_Bazooka",
            gameplay,
            ThirdPersonWeaponSetup.BazookaPath,
            new Vector3(0.04f, -0.03f, 0.38f));
        if (world == null)
            return "FAILED: could not create ThirdPerson_Bazooka.prefab";

        GameObject rocket = CreateRocketPrefab();
        if (rocket == null)
            return "FAILED: could not create RocketProjectile.prefab";

        if (!RegisterNetworkPrefab(rocket))
            return "FAILED: could not register RocketProjectile in DefaultNetworkPrefabs";

        WeaponPresentationConfig presentation = CreatePresentation();
        if (presentation == null)
            return "FAILED: could not create BazookaPresentation.asset";

        WeaponDefinition definition = CreateDefinition(gameplay, world, presentation, rocket);
        if (definition == null)
            return "FAILED: could not create BazookaDefinition.asset";

        if (!AddToCatalog(definition))
            return "FAILED: WeaponCatalog is missing";

        WirePlayerPrefab();
        AssignScenePickup(definition);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: Bazooka Heavy projectile weapon, rocket prefab, catalog, and pickups are configured. " + modelNote;
    }

    private static GameObject ResolveWeaponModel(out string note)
    {
        if (TryFindAuthoredModel(out GameObject authored, out string path))
        {
            note = "Using authored model at " + path;
            return authored;
        }

        note = "Authored Bazooka FBX was not found; a placeholder tube launcher was created. Re-run setup after importing the model.";
        return CreatePlaceholderModel();
    }

    private static bool TryFindAuthoredModel(out GameObject model, out string path)
    {
        model = null;
        path = FindPreferredAuthoredPath()
            ?? FindModel("Assets/Weapons/Bazooka")
            ?? FindModel("Assets/Weapons")
            ?? FindModelAnywhere();
        if (string.IsNullOrEmpty(path))
            return false;

        model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return model != null;
    }

    private static string FindPreferredAuthoredPath()
    {
        string[] preferred =
        {
            PrefabFolder + "/Bazooka V1.fbx",
            AuthoredPrefabPath,
            WeaponFolder + "/Bazooka V1.fbx"
        };

        for (int i = 0; i < preferred.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(preferred[i]) != null)
                return preferred[i];
        }

        return null;
    }

    private static string FindModelAnywhere()
    {
        string[] folders = { "Assets" };
        string[] nameHints = { "bazooka", "rocket launcher", "rocketlauncher" };
        string[] guids = AssetDatabase.FindAssets("t:Model", folders);
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            for (int h = 0; h < nameHints.Length; h++)
            {
                if (fileName.IndexOf(nameHints[h], System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return assetPath;
            }
        }

        return null;
    }

    private static string FindModel(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return null;

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            if (fileName.IndexOf("bazooka", System.StringComparison.OrdinalIgnoreCase) >= 0
                || fileName.IndexOf("rocket", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return assetPath;
        }

        return null;
    }

    private static void NormalizeAuthoredPickupPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AuthoredPrefabPath);
        if (prefab == null)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(AuthoredPrefabPath);
        try
        {
            contents.transform.localPosition = Vector3.zero;
            contents.transform.localRotation = Quaternion.identity;
            if (contents.transform.localScale.sqrMagnitude < 0.0001f)
                contents.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(contents, AuthoredPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static GameObject CreatePlaceholderModel()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderModelPath);
        if (existing != null)
            return existing;

        Material material = CreatePlaceholderMaterial();
        GameObject root = new GameObject("Bazooka_PlaceholderModel");
        try
        {
            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(tube.GetComponent<Collider>());
            tube.name = "Tube";
            tube.transform.SetParent(root.transform, false);
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tube.transform.localScale = new Vector3(0.12f, 0.48f, 0.12f);
            tube.GetComponent<MeshRenderer>().sharedMaterial = material;

            GameObject rear = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(rear.GetComponent<Collider>());
            rear.name = "Stock";
            rear.transform.SetParent(root.transform, false);
            rear.transform.localPosition = new Vector3(0f, -0.02f, -0.42f);
            rear.transform.localScale = new Vector3(0.08f, 0.1f, 0.22f);
            rear.GetComponent<MeshRenderer>().sharedMaterial = material;

            GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(grip.GetComponent<Collider>());
            grip.name = "Grip";
            grip.transform.SetParent(root.transform, false);
            grip.transform.localPosition = new Vector3(0f, -0.09f, -0.08f);
            grip.transform.localScale = new Vector3(0.05f, 0.12f, 0.05f);
            grip.GetComponent<MeshRenderer>().sharedMaterial = material;

            GameObject sight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(sight.GetComponent<Collider>());
            sight.name = "Sight";
            sight.transform.SetParent(root.transform, false);
            sight.transform.localPosition = new Vector3(0f, 0.08f, 0.18f);
            sight.transform.localScale = new Vector3(0.02f, 0.06f, 0.04f);
            sight.GetComponent<MeshRenderer>().sharedMaterial = material;

            PrefabUtility.SaveAsPrefabAsset(root, PlaceholderModelPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderModelPath);
    }

    private static Material CreatePlaceholderMaterial()
    {
        const string path = WeaponFolder + "/Materials/MAT_BazookaPlaceholder.mat";
        EnsureFolder(WeaponFolder + "/Materials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        material = new Material(shader);
        material.color = new Color(0.28f, 0.32f, 0.18f, 1f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreateGameplayPrefab(GameObject sourceModel)
    {
        RuntimeAnimatorController animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Scripts/Weapons/Animations/AKPresentation.overrideController");
        if (animator == null)
        {
            animator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Scripts/Weapons/Animations/RiflePresentation.overrideController");
        }

        Mesh mesh = FindPrimaryMesh(AssetDatabase.GetAssetPath(sourceModel));
        GameObject root = new GameObject("Bazooka_Gameplay");
        try
        {
            Animator gameplayAnimator = root.AddComponent<Animator>();
            gameplayAnimator.runtimeAnimatorController = animator;

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(sourceModel);
            if (model == null)
                model = Object.Instantiate(sourceModel);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            if (mesh != null)
                AlignLongAxisToForward(model.transform, mesh);

            Bounds local = GetLocalRendererBounds(root.transform);
            float longest = Mathf.Max(local.size.x, Mathf.Max(local.size.y, local.size.z));
            if (longest > 0.01f)
                model.transform.localScale = model.transform.localScale * (TargetLength / longest);

            local = GetLocalRendererBounds(root.transform);
            CreatePoint(root.transform, "MuzzlePoint", new Vector3(local.center.x, local.center.y, local.max.z));
            CreatePoint(
                root.transform,
                "AimPoint",
                new Vector3(local.center.x, local.max.y, Mathf.Lerp(local.min.z, local.max.z, 0.32f)));

            FittedWeaponModel fitted = root.AddComponent<FittedWeaponModel>();
            SerializedObject fittedSo = new SerializedObject(fitted);
            fittedSo.FindProperty("sourceModel").objectReferenceValue = sourceModel;
            fittedSo.FindProperty("targetLength").floatValue = TargetLength;
            fittedSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameplayPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPath);
    }

    private static GameObject CreateRocketPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(RocketPath);
        GameObject root = existing != null
            ? PrefabUtility.LoadPrefabContents(RocketPath)
            : new GameObject("RocketProjectile");

        try
        {
            root.name = "RocketProjectile";
            ConfigureRocketVisual(root);
            ConfigureRocketPhysics(root);
            ConfigureRocketNetwork(root);
            ConfigureRocketBehaviour(root);

            if (existing != null)
            {
                PrefabUtility.SaveAsPrefabAsset(root, RocketPath);
                return AssetDatabase.LoadAssetAtPath<GameObject>(RocketPath);
            }

            PrefabUtility.SaveAsPrefabAsset(root, RocketPath);
        }
        finally
        {
            if (existing != null)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(RocketPath);
    }

    private static void ConfigureRocketVisual(GameObject root)
    {
        Transform visual = root.transform.Find("Visual");
        if (visual == null)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Visual";
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.08f, 0.16f, 0.08f);
            visual = body.transform;

            Material material = CreateRocketMaterial();
            MeshRenderer renderer = body.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        if (root.GetComponentInChildren<TrailRenderer>(true) != null)
            return;

        GameObject trailGo = new GameObject("Trail");
        trailGo.transform.SetParent(root.transform, false);
        trailGo.transform.localPosition = new Vector3(0f, 0f, -0.12f);
        TrailRenderer trail = trailGo.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = 0.08f;
        trail.emitting = true;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        AnimationCurve width = new AnimationCurve();
        width.AddKey(0f, 1f);
        width.AddKey(1f, 0.15f);
        trail.widthCurve = width;
        Gradient color = new Gradient();
        color.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.65f, 0.15f), 0f),
                new GradientColorKey(new Color(1f, 0.2f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        trail.colorGradient = color;
        trail.sharedMaterial = CreateTrailMaterial();
    }

    private static Material CreateTrailMaterial()
    {
        const string path = WeaponFolder + "/Materials/MAT_RocketTrail.mat";
        EnsureFolder(WeaponFolder + "/Materials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");
        material = new Material(shader);
        material.color = new Color(1f, 0.55f, 0.12f, 0.85f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material CreateRocketMaterial()
    {
        const string path = WeaponFolder + "/Materials/MAT_RocketPlaceholder.mat";
        EnsureFolder(WeaponFolder + "/Materials");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        material = new Material(shader);
        material.color = new Color(0.55f, 0.18f, 0.08f, 1f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void ConfigureRocketPhysics(GameObject root)
    {
        SphereCollider sphere = root.GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = root.AddComponent<SphereCollider>();
        sphere.radius = 0.12f;
        sphere.isTrigger = false;

        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null)
            body = root.AddComponent<Rigidbody>();
        body.mass = 0.35f;
        body.linearDamping = 0f;
        body.angularDamping = 0.05f;
        body.useGravity = false;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        AudioSource audio = root.GetComponent<AudioSource>();
        if (audio == null)
            audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.loop = true;
        audio.spatialBlend = 1f;
        audio.dopplerLevel = 0f;
    }

    private static void ConfigureRocketNetwork(GameObject root)
    {
        if (root.GetComponent<NetworkObject>() == null)
            root.AddComponent<NetworkObject>();
        if (root.GetComponent<NetworkTransform>() == null)
            root.AddComponent<NetworkTransform>();
        if (root.GetComponent<NetworkRigidbody>() == null)
            root.AddComponent<NetworkRigidbody>();
    }

    private static void ConfigureRocketBehaviour(GameObject root)
    {
        RocketProjectile rocket = root.GetComponent<RocketProjectile>();
        if (rocket == null)
            rocket = root.AddComponent<RocketProjectile>();

        SerializedObject so = new SerializedObject(rocket);
        so.FindProperty("body").objectReferenceValue = root.GetComponent<Rigidbody>();
        so.FindProperty("flightAudio").objectReferenceValue = root.GetComponent<AudioSource>();
        so.FindProperty("trail").objectReferenceValue = root.GetComponentInChildren<TrailRenderer>(true);
        so.FindProperty("defaultCollisionRadius").floatValue = 0.12f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool RegisterNetworkPrefab(GameObject prefab)
    {
        Object list = AssetDatabase.LoadAssetAtPath<Object>(NetworkPrefabsPath);
        if (list == null)
            return false;

        SerializedObject so = new SerializedObject(list);
        SerializedProperty entries = so.FindProperty("List");
        if (entries == null)
            return false;

        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty prefabProp = entries.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
            if (prefabProp != null && prefabProp.objectReferenceValue == prefab)
                return true;
        }

        entries.arraySize++;
        SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        SerializedProperty overrideProp = added.FindPropertyRelative("Override");
        if (overrideProp != null)
            overrideProp.intValue = 0;
        added.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(list);
        return true;
    }

    private static WeaponPresentationConfig CreatePresentation()
    {
        if (AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(PresentationPath) == null)
        {
            string source = AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(
                "Assets/Scripts/Weapons/ShotgunPresentation.asset") != null
                ? "Assets/Scripts/Weapons/ShotgunPresentation.asset"
                : "Assets/Scripts/Weapons/AKPresentation.asset";
            if (!AssetDatabase.CopyAsset(source, PresentationPath))
                return null;
        }

        WeaponPresentationConfig presentation = AssetDatabase.LoadAssetAtPath<WeaponPresentationConfig>(PresentationPath);
        SerializedObject so = new SerializedObject(presentation);
        so.FindProperty("weaponName").stringValue = "Bazooka";
        so.FindProperty("fireSfxVolume").floatValue = 0.95f;
        so.FindProperty("fireKickLocalPosition").vector3Value = new Vector3(0f, 0.012f, -0.11f);
        so.FindProperty("fireKickLocalEuler").vector3Value = new Vector3(-8f, 0f, 0f);
        so.FindProperty("fireKickPositionVariance").vector3Value = new Vector3(0.012f, 0.01f, 0.016f);
        so.FindProperty("fireKickEulerVariance").vector3Value = new Vector3(3.5f, 4f, 3f);
        so.FindProperty("fireKickDuration").floatValue = 0.09f;
        so.FindProperty("fireRecoverDuration").floatValue = 0.28f;
        so.FindProperty("hipLocalPosition").vector3Value = new Vector3(0.14f, -0.22f, 0.62f);
        so.FindProperty("hipLocalEuler").vector3Value = new Vector3(-6f, 2f, -2f);
        so.FindProperty("aimDistance").floatValue = 0.42f;
        so.FindProperty("adsBlendDuration").floatValue = 0.2f;
        so.FindProperty("recoilPitch").floatValue = 6.8f;
        so.FindProperty("recoilYaw").floatValue = 1.4f;
        so.FindProperty("recoilPitchVariance").floatValue = 0.7f;
        so.FindProperty("recoilYawVariance").floatValue = 0.9f;
        so.FindProperty("worldFireKickLocalPosition").vector3Value = new Vector3(0f, 0.04f, -0.14f);
        so.FindProperty("worldFireKickLocalEuler").vector3Value = new Vector3(-28f, 6f, 5f);
        so.FindProperty("worldFireKickDuration").floatValue = 0.1f;
        so.FindProperty("worldFireRecoverDuration").floatValue = 0.28f;
        so.FindProperty("worldAudioMaxDistance").floatValue = 80f;
        AudioClip[] fireClips = FindAudioClips(new[] { "shotgun shoot", "shoot", "fire" }, "Assets/Sound Fx", 8);
        if (fireClips.Length > 0)
            AssignClips(so.FindProperty("fireSfx"), fireClips);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(presentation);
        return presentation;
    }

    private static WeaponDefinition CreateDefinition(
        GameObject gameplay,
        GameObject world,
        WeaponPresentationConfig presentation,
        GameObject rocket)
    {
        if (AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionPath) == null)
        {
            if (!AssetDatabase.CopyAsset("Assets/Scripts/Weapons/ShotgunDefinition.asset", DefinitionPath))
                return null;
        }

        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(DefinitionPath);
        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("weaponId").stringValue = "bazooka";
        so.FindProperty("displayName").stringValue = "Bazooka";
        so.FindProperty("inventoryRole").enumValueIndex = (int)WeaponInventoryRole.TemporaryPickup;
        so.FindProperty("automatic").boolValue = false;
        so.FindProperty("fireType").enumValueIndex = (int)WeaponFireType.Projectile;
        so.FindProperty("firstPersonPrefab").objectReferenceValue = gameplay;
        so.FindProperty("worldPrefab").objectReferenceValue = world;
        GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(AuthoredPrefabPath);
        so.FindProperty("pickupPrefab").objectReferenceValue = pickup != null ? pickup : gameplay;
        so.FindProperty("presentation").objectReferenceValue = presentation;
        so.FindProperty("magazineSize").intValue = 1;
        so.FindProperty("startingMagazineAmmo").intValue = 1;
        so.FindProperty("startingReserveAmmo").intValue = 4;
        so.FindProperty("maximumReserveAmmo").intValue = 8;
        so.FindProperty("unlimitedReserve").boolValue = false;
        so.FindProperty("fireRate").floatValue = 1.4f;
        so.FindProperty("reloadTime").floatValue = 2.8f;
        so.FindProperty("canRicochet").boolValue = false;
        so.FindProperty("usesMagnifiedAds").boolValue = true;
        so.FindProperty("adsMagnification").floatValue = 1.4f;
        so.FindProperty("adsHidesViewmodel").boolValue = false;
        so.FindProperty("usesVariableAdsMagnification").boolValue = false;
        so.FindProperty("locksHorizontalLocomotionWhileAds").boolValue = false;
        so.FindProperty("scopePresentation").objectReferenceValue = null;
        so.FindProperty("weaponPoseClass").enumValueIndex = (int)ThirdPersonWeaponPoseClass.HeavyGun;
        so.FindProperty("poseClassAssigned").boolValue = true;
        so.FindProperty("thirdPersonPoseCategory").enumValueIndex = (int)ThirdPersonPoseCategory.LongGun;
        so.FindProperty("useLeftHandGrip").boolValue = true;
        so.FindProperty("supportHandIkEnabled").boolValue = true;
        so.FindProperty("thirdPersonClass").enumValueIndex = (int)ThirdPersonWeaponClass.Shotgun;
        ThirdPersonWeaponPoseProfile heavy = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponPoseProfile>(HeavyGunProfilePath);
        if (heavy != null)
            so.FindProperty("thirdPersonPoseProfile").objectReferenceValue = heavy;

        SerializedProperty projectile = so.FindProperty("projectileSettings");
        projectile.FindPropertyRelative("projectilePrefab").objectReferenceValue = rocket;
        projectile.FindPropertyRelative("guidanceMode").enumValueIndex = (int)ProjectileGuidanceMode.Straight;
        projectile.FindPropertyRelative("projectileSpeed").floatValue = 35f;
        projectile.FindPropertyRelative("projectileLifetime").floatValue = 3.5f;
        projectile.FindPropertyRelative("aimDistance").floatValue = 500f;
        projectile.FindPropertyRelative("spawnForwardOffset").floatValue = 0.4f;
        projectile.FindPropertyRelative("gravity").floatValue = 0f;
        projectile.FindPropertyRelative("collisionRadius").floatValue = 0.12f;
        projectile.FindPropertyRelative("canRicochet").boolValue = true;
        projectile.FindPropertyRelative("maxRicochets").intValue = 1;
        projectile.FindPropertyRelative("directHitDamage").floatValue = 0f;
        projectile.FindPropertyRelative("explosionRadius").floatValue = 4.5f;
        projectile.FindPropertyRelative("maximumExplosionDamage").floatValue = 4f;
        projectile.FindPropertyRelative("minimumExplosionDamage").floatValue = 4f;
        projectile.FindPropertyRelative("dealHalfMaxHealthExplosionDamage").boolValue = true;
        projectile.FindPropertyRelative("detachesBullseyes").boolValue = true;
        projectile.FindPropertyRelative("explosionForce").floatValue = 14f;
        projectile.FindPropertyRelative("explosionUpwardModifier").floatValue = 0.35f;
        projectile.FindPropertyRelative("explosionVfx").objectReferenceValue = FindExplosionVfx();
        projectile.FindPropertyRelative("projectileTrail").objectReferenceValue = null;
        projectile.FindPropertyRelative("flightSfxVolume").floatValue = 0.5f;
        projectile.FindPropertyRelative("explosionSfxVolume").floatValue = 0.95f;
        projectile.FindPropertyRelative("debugProjectileTrajectory").boolValue = false;
        projectile.FindPropertyRelative("debugExplosionRadius").boolValue = false;
        projectile.FindPropertyRelative("debugAimTarget").boolValue = false;
        AudioClip[] fireClips = FindAudioClips(new[] { "shotgun shoot", "shoot", "fire" }, "Assets/Sound Fx", 8);
        if (fireClips.Length > 0)
            AssignClips(projectile.FindPropertyRelative("fireSfx"), fireClips);
        AudioClip[] explosionClips = FindAudioClips(new[] { "combustion grenade explosion", "explosion", "grenade" }, "Assets", 6);
        if (explosionClips.Length > 0)
            AssignClips(projectile.FindPropertyRelative("explosionSfx"), explosionClips);
        AudioClip[] flightClips = FindAudioClips(new[] { "throw", "whoosh" }, "Assets/Sound Fx", 2);
        if (flightClips.Length > 0)
            AssignClips(projectile.FindPropertyRelative("flightSfx"), flightClips);

        SerializedProperty pose = so.FindProperty("thirdPersonPose");
        if (pose != null)
        {
            pose.FindPropertyRelative("recoilPitch").floatValue = 12f;
            pose.FindPropertyRelative("recoilRightRoll").floatValue = 8f;
            pose.FindPropertyRelative("recoilYaw").floatValue = 3f;
            pose.FindPropertyRelative("recoilInTime").floatValue = 0.04f;
            pose.FindPropertyRelative("recoilOutTime").floatValue = 0.18f;
        }

        if (pickup != null)
        {
            so.FindProperty("pickupLocalPosition").vector3Value = new Vector3(0f, 0.08f, 0f);
            so.FindProperty("pickupLocalEuler").vector3Value = new Vector3(0f, 35f, 90f);
            so.FindProperty("pickupLocalScale").vector3Value = new Vector3(3.5f, 3.5f, 3.5f);
            so.FindProperty("fitPickupColliderToMesh").boolValue = true;
        }

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

    private static void WirePlayerPrefab()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (player == null)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerProjectileLauncher launcher = contents.GetComponent<PlayerProjectileLauncher>();
            if (launcher == null)
                launcher = contents.AddComponent<PlayerProjectileLauncher>();

            SerializedObject so = new SerializedObject(launcher);
            so.FindProperty("playerCamera").objectReferenceValue = contents.GetComponentInChildren<Camera>(true);
            so.FindProperty("firstPersonWeapon").objectReferenceValue = contents.GetComponent<WeaponPresentationController>();
            so.FindProperty("worldWeapon").objectReferenceValue = contents.GetComponent<WorldWeaponView>();
            so.FindProperty("inventory").objectReferenceValue = contents.GetComponent<PlayerWeaponInventory>();
            so.FindProperty("playerHealth").objectReferenceValue = contents.GetComponent<PlayerHealth>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void AssignScenePickup(WeaponDefinition definition)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        PrototypeGroundWeaponLayout layout = Object.FindAnyObjectByType<PrototypeGroundWeaponLayout>();
        if (layout == null)
        {
            EditorSceneManager.CloseScene(scene, true);
            return;
        }

        SerializedObject so = new SerializedObject(layout);
        SerializedProperty bazooka = so.FindProperty("bazooka");
        if (bazooka != null && bazooka.objectReferenceValue != definition)
        {
            bazooka.objectReferenceValue = definition;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static GameObject FindExplosionVfx()
    {
        GameObject combustion = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Effects/Grenade/Prefabs/vfx_CombustionGrenadeExplosion.prefab");
        if (combustion != null)
            return combustion;

        return AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Weapons/Grenades/VFX/GrenadeExplosionVFX.prefab");
    }

    private static AudioClip[] FindAudioClips(string[] nameHints, string folder, int max)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            folder = "Assets";

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
        var matches = new System.Collections.Generic.List<AudioClip>();
        for (int h = 0; h < nameHints.Length && matches.Count < max; h++)
        {
            for (int i = 0; i < guids.Length && matches.Count < max; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.IndexOf(nameHints[h], System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null && !matches.Contains(clip))
                    matches.Add(clip);
            }
        }

        return matches.ToArray();
    }

    private static void AssignClips(SerializedProperty property, AudioClip[] clips)
    {
        if (property == null || !property.isArray)
            return;

        property.arraySize = clips != null ? clips.Length : 0;
        for (int i = 0; i < property.arraySize; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
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

        return initialized ? bounds : new Bounds(Vector3.zero, new Vector3(0.12f, 0.12f, 1f));
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
        if (string.IsNullOrEmpty(assetPath))
            return null;

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
}
