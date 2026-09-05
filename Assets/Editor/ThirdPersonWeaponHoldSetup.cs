using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates shared hold profiles, wires the player dual-arm IK rig, and
/// applies the Grip_R / Grip_L / Aim / Muzzle marker contract.
/// </summary>
public static class ThirdPersonWeaponHoldSetup
{
    public const string HoldFolder = "Assets/Animations/ThirdPersonWeapons/Holds";
    public const string ResourcesFolder = "Assets/Resources";
    public const string LibraryPath = HoldFolder + "/ThirdPersonWeaponHoldLibrary.asset";
    public const string ResourcesLibraryPath = ResourcesFolder + "/ThirdPersonWeaponHoldLibrary.asset";
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    public const string PreviewScenePath = "Assets/AnimationAuthoringTest.unity";

    [MenuItem("Bullseye/Weapons/Apply REQ-049 Procedural Hold Rig")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        ReleaseLeakedPreviewScenes();
        ThirdPersonWeaponHoldLibrary library = EnsureProfilesAndLibrary();
        WirePlayerPrefab();
        ConfigureExistingDefinitions();
        string validation = ValidateAllWeapons();
        AssetDatabase.SaveAssets();
        int released = ReleaseLeakedPreviewScenes();
        return "OK: REQ-049 procedural weapon-hold architecture applied.\n" + validation +
               (released > 0 ? "\nReleased " + released + " leftover preview scenes." : string.Empty);
    }

    [MenuItem("Bullseye/Weapons/Release Leftover Preview Scenes")]
    public static void ReleaseLeakedPreviewScenesFromMenu()
    {
        Debug.Log("Released " + ReleaseLeakedPreviewScenes() + " leftover preview scenes.");
    }

    public static int ReleaseLeakedPreviewScenes()
    {
        Scene prefabStageScene = default;
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
            prefabStageScene = stage.scene;

        int closed = 0;
        for (int i = EditorSceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;
            if (!EditorSceneManager.IsPreviewScene(scene))
                continue;
            if (scene == prefabStageScene)
                continue;

            EditorSceneManager.CloseScene(scene, true);
            closed++;
        }

        return closed;
    }

    private static void UnloadPrefabEdit(GameObject contents)
    {
        if (contents == null)
            return;

        PrefabUtility.UnloadPrefabContents(contents);
    }

    public static ThirdPersonWeaponHoldLibrary EnsureProfilesAndLibrary()
    {
        EnsureFolder(HoldFolder);
        EnsureFolder(ResourcesFolder);

        ThirdPersonWeaponHoldProfile longHold = CreateProfile(
            HoldFolder + "/LongGun_Hold.asset",
            "LongGun_Hold",
            ThirdPersonWeaponPoseClass.LongGun,
            ThirdPersonWeaponPoseKind.Hold,
            new Vector3(0.16f, 0.06f, 0.26f),
            Vector3.zero,
            new Vector3(0.32f, -0.04f, 0.06f),
            new Vector3(-0.16f, 0.02f, 0.18f),
            true);
        ThirdPersonWeaponHoldProfile shortHold = CreateProfile(
            HoldFolder + "/ShortGun_Hold.asset",
            "ShortGun_Hold",
            ThirdPersonWeaponPoseClass.ShortGun,
            ThirdPersonWeaponPoseKind.Hold,
            new Vector3(0.10f, 0.08f, 0.30f),
            new Vector3(-4f, 0f, 0f),
            new Vector3(0.24f, -0.02f, 0.04f),
            new Vector3(-0.08f, 0.04f, 0.16f),
            false);
        ThirdPersonWeaponHoldProfile heavyHold = CreateProfile(
            HoldFolder + "/HeavyGun_Hold.asset",
            "HeavyGun_Hold",
            ThirdPersonWeaponPoseClass.HeavyGun,
            ThirdPersonWeaponPoseKind.Hold,
            new Vector3(0.22f, 0.10f, 0.20f),
            new Vector3(8f, 6f, -4f),
            new Vector3(0.36f, 0.02f, 0.02f),
            new Vector3(-0.12f, 0.06f, 0.22f),
            true);

        ThirdPersonWeaponHoldProfile longSprint = CreateVariant(
            HoldFolder + "/LongGun_SprintHold.asset",
            "LongGun_SprintHold",
            longHold,
            ThirdPersonWeaponPoseKind.Sprint,
            new Vector3(0.18f, -0.04f, 0.16f),
            new Vector3(12f, 8f, -6f));
        ThirdPersonWeaponHoldProfile longProne = CreateVariant(
            HoldFolder + "/LongGun_ProneHold.asset",
            "LongGun_ProneHold",
            longHold,
            ThirdPersonWeaponPoseKind.Prone,
            new Vector3(0.12f, 0.02f, 0.34f),
            new Vector3(6f, 0f, 0f));
        ThirdPersonWeaponHoldProfile longAim = CreateVariant(
            HoldFolder + "/LongGun_AimHold.asset",
            "LongGun_AimHold",
            longHold,
            ThirdPersonWeaponPoseKind.Aim,
            new Vector3(0.10f, 0.10f, 0.28f),
            new Vector3(-2f, 0f, 0f));

        ThirdPersonWeaponHoldProfile shortSprint = CreateVariant(
            HoldFolder + "/ShortGun_SprintHold.asset",
            "ShortGun_SprintHold",
            shortHold,
            ThirdPersonWeaponPoseKind.Sprint,
            new Vector3(0.14f, -0.02f, 0.22f),
            new Vector3(10f, 6f, -4f));
        ThirdPersonWeaponHoldProfile shortProne = CreateVariant(
            HoldFolder + "/ShortGun_ProneHold.asset",
            "ShortGun_ProneHold",
            shortHold,
            ThirdPersonWeaponPoseKind.Prone,
            new Vector3(0.08f, 0.04f, 0.32f),
            new Vector3(4f, 0f, 0f));
        ThirdPersonWeaponHoldProfile shortAim = CreateVariant(
            HoldFolder + "/ShortGun_AimHold.asset",
            "ShortGun_AimHold",
            shortHold,
            ThirdPersonWeaponPoseKind.Aim,
            new Vector3(0.08f, 0.12f, 0.30f),
            new Vector3(-6f, 0f, 0f));

        ThirdPersonWeaponHoldProfile heavySprint = CreateVariant(
            HoldFolder + "/HeavyGun_SprintHold.asset",
            "HeavyGun_SprintHold",
            heavyHold,
            ThirdPersonWeaponPoseKind.Sprint,
            new Vector3(0.24f, 0.02f, 0.14f),
            new Vector3(14f, 10f, -8f));
        ThirdPersonWeaponHoldProfile heavyProne = CreateVariant(
            HoldFolder + "/HeavyGun_ProneHold.asset",
            "HeavyGun_ProneHold",
            heavyHold,
            ThirdPersonWeaponPoseKind.Prone,
            new Vector3(0.18f, 0.04f, 0.28f),
            new Vector3(10f, 4f, -2f));
        ThirdPersonWeaponHoldProfile heavyAim = CreateVariant(
            HoldFolder + "/HeavyGun_AimHold.asset",
            "HeavyGun_AimHold",
            heavyHold,
            ThirdPersonWeaponPoseKind.Aim,
            new Vector3(0.16f, 0.12f, 0.22f),
            new Vector3(4f, 2f, -2f));

        ThirdPersonWeaponHoldLibrary library = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponHoldLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<ThirdPersonWeaponHoldLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            library = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponHoldLibrary>(LibraryPath);
        }

        library.Assign(
            longHold, shortHold, heavyHold,
            longSprint, longProne, longAim,
            shortSprint, shortProne, shortAim,
            heavySprint, heavyProne, heavyAim);
        EditorUtility.SetDirty(library);

        if (AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponHoldLibrary>(ResourcesLibraryPath) == null)
            AssetDatabase.CopyAsset(LibraryPath, ResourcesLibraryPath);
        else
        {
            ThirdPersonWeaponHoldLibrary resourcesCopy =
                AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponHoldLibrary>(ResourcesLibraryPath);
            if (resourcesCopy != null)
            {
                resourcesCopy.Assign(
                    longHold, shortHold, heavyHold,
                    longSprint, longProne, longAim,
                    shortSprint, shortProne, shortAim,
                    heavySprint, heavyProne, heavyAim);
                EditorUtility.SetDirty(resourcesCopy);
            }
        }

        ThirdPersonWeaponHoldLibraryCache.SetEditorLibrary(library);
        return library;
    }

    public static string AutoSetupWeapon(WeaponDefinition definition, bool overwriteCustom)
    {
        if (definition == null)
            return "No weapon selected.";

        EnsureProfilesAndLibrary();
        if (definition.WorldPrefab == null)
            return definition.DisplayName + ": missing world prefab.";

        string path = AssetDatabase.GetAssetPath(definition.WorldPrefab);
        if (string.IsNullOrEmpty(path))
            return definition.DisplayName + ": world prefab is not an asset.";

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnsureMarkerContract(contents.transform, definition, overwriteCustom);
            ThirdPersonWeaponVisual visual = contents.GetComponent<ThirdPersonWeaponVisual>()
                ?? contents.AddComponent<ThirdPersonWeaponVisual>();
            visual.ResolveFallbacks();
            visual.AssignMarkers(
                visual.GripR,
                visual.GripL,
                visual.Aim,
                visual.Muzzle,
                visual.RightElbowHint,
                visual.LeftElbowHint);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            UnloadPrefabEdit(contents);
        }

        SerializedObject so = new SerializedObject(definition);
        if (!definition.PoseClassWasAssigned())
        {
            ThirdPersonWeaponPoseClass guessed = GuessHoldClass(definition);
            so.FindProperty("weaponPoseClass").enumValueIndex = (int)guessed;
            so.FindProperty("poseClassAssigned").boolValue = true;
            so.FindProperty("useLeftHandGrip").boolValue = guessed != ThirdPersonWeaponPoseClass.ShortGun;
            so.FindProperty("supportHandIkEnabled").boolValue = guessed != ThirdPersonWeaponPoseClass.ShortGun;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return "Configured " + definition.DisplayName + " as " + definition.ThirdPersonHoldClass + ".";
    }

    public static string AutoConfigureAllValidWeapons()
    {
        EnsureProfilesAndLibrary();
        StringBuilder log = new StringBuilder();
        WeaponDefinition[] definitions = LoadAllDefinitions();
        int configured = 0;
        for (int i = 0; i < definitions.Length; i++)
        {
            WeaponDefinition definition = definitions[i];
            if (definition == null || definition.WorldPrefab == null)
                continue;

            log.AppendLine(AutoSetupWeapon(definition, false));
            configured++;
        }

        AssetDatabase.SaveAssets();
        return "Auto-configured " + configured + " weapons.\n" + log;
    }

    public static string ValidateAllWeapons()
    {
        WeaponDefinition[] definitions = LoadAllDefinitions();
        StringBuilder log = new StringBuilder();
        log.AppendLine("Third-person weapon hold validation");
        for (int i = 0; i < definitions.Length; i++)
        {
            WeaponDefinition definition = definitions[i];
            if (definition == null)
                continue;

            ThirdPersonWeaponMarkerReport report = InspectDefinition(definition);
            log.AppendLine();
            log.AppendLine(report.weaponName);
            log.AppendLine("Class: " + report.holdClass);
            log.AppendLine(Mark(report.hasGripR, "Grip_R"));
            log.AppendLine(Mark(report.hasGripL || !report.usesLeftHand, "Grip_L"));
            log.AppendLine(Mark(report.hasAim, "Aim"));
            log.AppendLine(Mark(report.hasMuzzle, "Muzzle"));
            log.AppendLine(Mark(report.hasHoldProfile, "Hold Profile " + report.holdProfileName));
            if (!string.IsNullOrEmpty(report.issues))
                log.AppendLine("Issues: " + report.issues);
        }

        return log.ToString();
    }

    public static ThirdPersonWeaponMarkerReport InspectDefinition(WeaponDefinition definition)
    {
        var empty = new ThirdPersonWeaponMarkerReport
        {
            weaponName = definition != null ? definition.DisplayName : "None",
            holdClass = definition != null ? definition.ThirdPersonHoldClass : ThirdPersonWeaponPoseClass.LongGun,
            usesLeftHand = definition != null && definition.UseLeftHandGrip,
            hasHoldProfile = ThirdPersonWeaponHoldResolver.Resolve(definition, ThirdPersonWeaponPoseKind.Hold) != null,
            holdProfileName = ThirdPersonWeaponHoldResolver.Describe(definition, ThirdPersonWeaponPoseKind.Hold)
        };
        if (definition == null || definition.WorldPrefab == null)
        {
            empty.issues = "Missing world prefab.";
            return empty;
        }

        GameObject prefab = definition.WorldPrefab;
        ThirdPersonWeaponVisual visual = prefab.GetComponent<ThirdPersonWeaponVisual>();
        if (visual == null)
            visual = prefab.GetComponentInChildren<ThirdPersonWeaponVisual>(true);
        if (visual != null)
            return visual.BuildReport(definition);

        empty.hasGripR = ThirdPersonWeaponMarkers.Find(prefab.transform, ThirdPersonWeaponMarkers.GripRAliases) != null;
        empty.hasGripL = ThirdPersonWeaponMarkers.Find(prefab.transform, ThirdPersonWeaponMarkers.GripLAliases) != null;
        empty.hasAim = ThirdPersonWeaponMarkers.Find(prefab.transform, ThirdPersonWeaponMarkers.AimAliases) != null;
        empty.hasMuzzle = ThirdPersonWeaponMarkers.Find(prefab.transform, ThirdPersonWeaponMarkers.MuzzleAliases) != null;
        StringBuilder issues = new StringBuilder();
        if (!empty.hasGripR)
            issues.Append("Missing Grip_R. ");
        if (empty.usesLeftHand && !empty.hasGripL)
            issues.Append("Missing Grip_L. ");
        if (!empty.hasAim)
            issues.Append("Missing Aim. ");
        if (!empty.hasMuzzle)
            issues.Append("Missing Muzzle. ");
        empty.issues = issues.ToString().Trim();
        return empty;
    }

    public static Transform CreateMissingMarker(WeaponDefinition definition, string markerName)
    {
        if (definition == null || definition.WorldPrefab == null)
            return null;

        string path = AssetDatabase.GetAssetPath(definition.WorldPrefab);
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        Transform created = null;
        try
        {
            created = EnsureNamedMarker(contents.transform, markerName, DefaultMarkerLocal(markerName), Quaternion.identity);
            ThirdPersonWeaponVisual visual = contents.GetComponent<ThirdPersonWeaponVisual>()
                ?? contents.AddComponent<ThirdPersonWeaponVisual>();
            visual.ResolveFallbacks();
            visual.AssignMarkers(
                visual.GripR,
                visual.GripL,
                visual.Aim,
                visual.Muzzle,
                visual.RightElbowHint,
                visual.LeftElbowHint);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            UnloadPrefabEdit(contents);
        }

        return created;
    }

    public static void SaveMarkerLocal(WeaponDefinition definition, string markerName, Vector3 localPosition, Vector3 localEuler)
    {
        if (definition == null || definition.WorldPrefab == null)
            return;

        string path = AssetDatabase.GetAssetPath(definition.WorldPrefab);
        if (string.IsNullOrEmpty(path))
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform target = ThirdPersonWeaponMarkers.Find(contents.transform, markerName)
                ?? EnsureNamedMarker(contents.transform, markerName, localPosition, Quaternion.Euler(localEuler));
            target.localPosition = localPosition;
            target.localEulerAngles = localEuler;
            ThirdPersonWeaponVisual visual = contents.GetComponent<ThirdPersonWeaponVisual>();
            visual?.ResolveFallbacks();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            UnloadPrefabEdit(contents);
        }
    }

    public static void WirePlayerPrefab()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (player == null)
            return;

        string prefabPath = AssetDatabase.GetAssetPath(player);
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = FindCharacterAnimator(contents);
            ThirdPersonWeaponRig controller = contents.GetComponent<ThirdPersonWeaponRig>();
            if (controller == null)
                controller = contents.AddComponent<ThirdPersonWeaponRig>();

            Transform host = EnsureChild(contents.transform, "WorldWeaponRig");
            Transform weaponAnchor = EnsureChild(host, "ThirdPersonWeaponAnchor");
            Transform aimTarget = EnsureChild(host, "AimTarget");
            Transform rightTarget = EnsureChild(host, "RightHandIKTarget");
            Transform leftTarget = EnsureChild(host, "LeftHandIKTarget");
            Transform rightHint = EnsureChild(host, "RightElbowHint");
            Transform leftHint = EnsureChild(host, "LeftElbowHint");

            PlayerVisualRig visualRig = contents.GetComponentInChildren<PlayerVisualRig>(true);
            if (visualRig != null)
                visualRig.AssignAnchor(weaponAnchor);

            RigBuilder builder = null;
            Rig rig = null;
            TwoBoneIKConstraint rightIk = null;
            TwoBoneIKConstraint leftIk = null;
            MultiAimConstraint aim = null;
            if (animator != null)
            {
                builder = animator.GetComponent<RigBuilder>();
                if (builder == null)
                    builder = animator.gameObject.AddComponent<RigBuilder>();

                Transform rigTransform = animator.transform.Find("ThirdPersonWeaponRig");
                if (rigTransform == null)
                {
                    GameObject rigGo = new GameObject("ThirdPersonWeaponRig");
                    rigGo.transform.SetParent(animator.transform, false);
                    rigTransform = rigGo.transform;
                }

                rig = rigTransform.GetComponent<Rig>() ?? rigTransform.gameObject.AddComponent<Rig>();
                rightIk = EnsureTwoBone(
                    rigTransform,
                    "RightHandIK",
                    animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                    animator.GetBoneTransform(HumanBodyBones.RightHand),
                    rightTarget,
                    rightHint);
                leftIk = EnsureTwoBone(
                    rigTransform,
                    "LeftHandIK",
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                    animator.GetBoneTransform(HumanBodyBones.LeftHand),
                    leftTarget,
                    leftHint);

                Transform aimTransform = rigTransform.Find("AimRig");
                if (aimTransform == null)
                {
                    GameObject aimGo = new GameObject("AimRig");
                    aimGo.transform.SetParent(rigTransform, false);
                    aimTransform = aimGo.transform;
                }

                aim = aimTransform.GetComponent<MultiAimConstraint>()
                    ?? aimTransform.gameObject.AddComponent<MultiAimConstraint>();
                MultiAimConstraintData aimData = aim.data;
                aimData.constrainedObject = animator.GetBoneTransform(HumanBodyBones.Chest)
                    ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                WeightedTransformArray sources = aimData.sourceObjects;
                if (sources.Count == 0)
                    sources.Add(new WeightedTransform(aimTarget, 1f));
                else
                    sources[0] = new WeightedTransform(aimTarget, 1f);
                aimData.sourceObjects = sources;
                aim.data = aimData;
                aim.weight = 0f;

                bool hasLayer = false;
                for (int i = 0; i < builder.layers.Count; i++)
                {
                    if (builder.layers[i].rig == rig)
                    {
                        hasLayer = true;
                        break;
                    }
                }

                if (!hasLayer)
                    builder.layers.Add(new RigLayer(rig, true));
            }

            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("thirdPersonAnimator").objectReferenceValue = animator;
            so.FindProperty("visualRig").objectReferenceValue = visualRig;
            so.FindProperty("animationState").objectReferenceValue = contents.GetComponent<PlayerAnimationState>();
            so.FindProperty("playerHealth").objectReferenceValue = contents.GetComponent<PlayerHealth>();
            so.FindProperty("worldWeapon").objectReferenceValue = contents.GetComponent<WorldWeaponView>();
            so.FindProperty("coordinator").objectReferenceValue = contents.GetComponent<WeaponPresentationCoordinator>();
            so.FindProperty("visualRoot").objectReferenceValue = contents.transform.Find("VisualRoot");
            so.FindProperty("weaponAnchor").objectReferenceValue = weaponAnchor;
            so.FindProperty("aimTarget").objectReferenceValue = aimTarget;
            so.FindProperty("rightHandIkTarget").objectReferenceValue = rightTarget;
            so.FindProperty("leftHandIkTarget").objectReferenceValue = leftTarget;
            so.FindProperty("rightElbowHint").objectReferenceValue = rightHint;
            so.FindProperty("leftElbowHint").objectReferenceValue = leftHint;
            so.FindProperty("rigBuilder").objectReferenceValue = builder;
            so.FindProperty("weaponRig").objectReferenceValue = rig;
            so.FindProperty("rightHandIk").objectReferenceValue = rightIk;
            so.FindProperty("leftHandIk").objectReferenceValue = leftIk;
            so.FindProperty("spineAim").objectReferenceValue = aim;
            so.ApplyModifiedPropertiesWithoutUndo();

            WorldWeaponView world = contents.GetComponent<WorldWeaponView>();
            if (world != null)
            {
                SerializedObject worldSo = new SerializedObject(world);
                worldSo.FindProperty("thirdPersonRig").objectReferenceValue = controller;
                worldSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            UnloadPrefabEdit(contents);
        }
    }

    private static void ConfigureExistingDefinitions()
    {
        AssignClass("Assets/Scripts/Weapons/AKDefinition.asset", ThirdPersonWeaponPoseClass.LongGun, true);
        AssignClass("Assets/Scripts/Weapons/DMRDefinition.asset", ThirdPersonWeaponPoseClass.LongGun, true);
        AssignClass("Assets/Scripts/Weapons/ShotgunDefinition.asset", ThirdPersonWeaponPoseClass.LongGun, true);
        AssignClass("Assets/Scripts/Weapons/Ruger22Definition.asset", ThirdPersonWeaponPoseClass.ShortGun, false);
        AutoSetupWeapon(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Scripts/Weapons/AKDefinition.asset"), false);
        AutoSetupWeapon(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Scripts/Weapons/DMRDefinition.asset"), false);
        AutoSetupWeapon(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Scripts/Weapons/ShotgunDefinition.asset"), false);
        AutoSetupWeapon(AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Scripts/Weapons/Ruger22Definition.asset"), false);
    }

    private static void AssignClass(string path, ThirdPersonWeaponPoseClass holdClass, bool useLeft)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (definition == null)
            return;

        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("weaponPoseClass").enumValueIndex = (int)holdClass;
        so.FindProperty("poseClassAssigned").boolValue = true;
        so.FindProperty("useLeftHandGrip").boolValue = useLeft;
        so.FindProperty("supportHandIkEnabled").boolValue = useLeft;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void EnsureMarkerContract(Transform root, WeaponDefinition definition, bool overwriteCustom)
    {
        PromoteAlias(root, ThirdPersonWeaponMarkers.GripRAliases, ThirdPersonWeaponMarkers.GripR);
        if (definition == null || definition.UseLeftHandGrip || definition.ThirdPersonHoldClass != ThirdPersonWeaponPoseClass.ShortGun)
            PromoteAlias(root, ThirdPersonWeaponMarkers.GripLAliases, ThirdPersonWeaponMarkers.GripL);
        PromoteAlias(root, ThirdPersonWeaponMarkers.AimAliases, ThirdPersonWeaponMarkers.Aim);
        PromoteAlias(root, ThirdPersonWeaponMarkers.MuzzleAliases, ThirdPersonWeaponMarkers.Muzzle);

        if (ThirdPersonWeaponMarkers.Find(root, ThirdPersonWeaponMarkers.GripR) == null)
            EnsureNamedMarker(root, ThirdPersonWeaponMarkers.GripR, new Vector3(0f, -0.04f, 0.02f), Quaternion.identity);
        if ((definition == null || definition.UseLeftHandGrip || definition.ThirdPersonHoldClass != ThirdPersonWeaponPoseClass.ShortGun)
            && ThirdPersonWeaponMarkers.Find(root, ThirdPersonWeaponMarkers.GripL) == null)
            EnsureNamedMarker(root, ThirdPersonWeaponMarkers.GripL, new Vector3(0f, -0.02f, 0.18f), Quaternion.identity);
        if (ThirdPersonWeaponMarkers.Find(root, ThirdPersonWeaponMarkers.Aim) == null)
            EnsureNamedMarker(root, ThirdPersonWeaponMarkers.Aim, Vector3.zero, Quaternion.identity);
        if (ThirdPersonWeaponMarkers.Find(root, ThirdPersonWeaponMarkers.Muzzle) == null)
            EnsureNamedMarker(root, ThirdPersonWeaponMarkers.Muzzle, new Vector3(0f, 0f, 0.35f), Quaternion.identity);

        if (overwriteCustom)
        {
            Transform aim = ThirdPersonWeaponMarkers.Find(root, ThirdPersonWeaponMarkers.Aim);
            if (aim != null && aim.localRotation == Quaternion.identity)
                aim.localRotation = Quaternion.identity;
        }
    }

    private static void PromoteAlias(Transform root, string[] aliases, string preferred)
    {
        Transform existing = ThirdPersonWeaponMarkers.Find(root, preferred);
        if (existing != null)
            return;

        Transform alias = ThirdPersonWeaponMarkers.Find(root, aliases);
        if (alias != null && alias.name != preferred)
            alias.name = preferred;
    }

    private static Transform EnsureNamedMarker(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        return ThirdPersonWeaponMarkers.FindOrCreate(parent, name, localPosition, localRotation);
    }

    private static Vector3 DefaultMarkerLocal(string markerName)
    {
        return markerName switch
        {
            ThirdPersonWeaponMarkers.GripR => new Vector3(0f, -0.04f, 0.02f),
            ThirdPersonWeaponMarkers.GripL => new Vector3(0f, -0.02f, 0.18f),
            ThirdPersonWeaponMarkers.Muzzle => new Vector3(0f, 0f, 0.35f),
            _ => Vector3.zero
        };
    }

    private static ThirdPersonWeaponHoldProfile CreateProfile(
        string path,
        string assetName,
        ThirdPersonWeaponPoseClass holdClass,
        ThirdPersonWeaponPoseKind kind,
        Vector3 weaponPos,
        Vector3 weaponEuler,
        Vector3 rightHint,
        Vector3 leftHint,
        bool useLeft)
    {
        ThirdPersonWeaponHoldProfile profile = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponHoldProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ThirdPersonWeaponHoldProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponHoldProfile>(path);
        }

        SerializedObject so = new SerializedObject(profile);
        so.FindProperty("holdClass").enumValueIndex = (int)holdClass;
        so.FindProperty("holdKind").enumValueIndex = (int)kind;
        so.FindProperty("weaponAnchorLocalPosition").vector3Value = weaponPos;
        so.FindProperty("weaponAnchorLocalEuler").vector3Value = weaponEuler;
        so.FindProperty("rightElbowHintLocalPosition").vector3Value = rightHint;
        so.FindProperty("leftElbowHintLocalPosition").vector3Value = leftHint;
        so.FindProperty("useLeftHand").boolValue = useLeft;
        so.FindProperty("rightArmIkWeight").floatValue = 1f;
        so.FindProperty("leftArmIkWeight").floatValue = useLeft ? 1f : 0f;
        so.FindProperty("hintWeight").floatValue = 1f;
        so.FindProperty("blendDuration").floatValue = 0.14f;
        so.ApplyModifiedPropertiesWithoutUndo();
        profile.name = assetName;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static ThirdPersonWeaponHoldProfile CreateVariant(
        string path,
        string assetName,
        ThirdPersonWeaponHoldProfile source,
        ThirdPersonWeaponPoseKind kind,
        Vector3 weaponPos,
        Vector3 weaponEuler)
    {
        ThirdPersonWeaponHoldProfile profile = CreateProfile(
            path,
            assetName,
            source.HoldClass,
            kind,
            weaponPos,
            weaponEuler,
            source.RightElbowHintLocalPosition,
            source.LeftElbowHintLocalPosition,
            source.UseLeftHand);
        SerializedObject so = new SerializedObject(profile);
        so.FindProperty("shoulderInfluence").floatValue = source.ShoulderInfluence;
        so.FindProperty("chestInfluence").floatValue = source.ChestInfluence;
        so.FindProperty("maxArmReach").floatValue = source.MaxArmReach;
        so.ApplyModifiedPropertiesWithoutUndo();
        return profile;
    }

    private static TwoBoneIKConstraint EnsureTwoBone(
        Transform rigTransform,
        string childName,
        Transform root,
        Transform mid,
        Transform tip,
        Transform target,
        Transform hint)
    {
        Transform ikTransform = rigTransform.Find(childName);
        if (ikTransform == null)
        {
            GameObject ikGo = new GameObject(childName);
            ikGo.transform.SetParent(rigTransform, false);
            ikTransform = ikGo.transform;
        }

        TwoBoneIKConstraint constraint = ikTransform.GetComponent<TwoBoneIKConstraint>()
            ?? ikTransform.gameObject.AddComponent<TwoBoneIKConstraint>();
        TwoBoneIKConstraintData data = constraint.data;
        data.root = root;
        data.mid = mid;
        data.tip = tip;
        data.target = target;
        data.hint = hint;
        data.targetPositionWeight = 1f;
        data.targetRotationWeight = 1f;
        data.hintWeight = 1f;
        constraint.data = data;
        constraint.weight = 1f;
        return constraint;
    }

    public static WeaponDefinition[] LoadAllDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:WeaponDefinition");
        List<WeaponDefinition> list = new List<WeaponDefinition>();
        for (int i = 0; i < guids.Length; i++)
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (definition != null)
                list.Add(definition);
        }

        return list.ToArray();
    }

    private static ThirdPersonWeaponPoseClass GuessHoldClass(WeaponDefinition definition)
    {
        string id = definition.WeaponId.ToLowerInvariant();
        if (id.Contains("pistol") || id.Contains("ruger"))
            return ThirdPersonWeaponPoseClass.ShortGun;
        if (id.Contains("rocket") || id.Contains("launcher"))
            return ThirdPersonWeaponPoseClass.HeavyGun;
        return ThirdPersonWeaponPoseClass.LongGun;
    }

    private static string Mark(bool ok, string label)
    {
        return (ok ? "✓ " : "✗ ") + label;
    }

    private static Animator FindCharacterAnimator(GameObject root)
    {
        Transform visual = root.transform.Find("VisualRoot");
        if (visual != null)
        {
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator != null)
                return animator;
        }

        return root.GetComponentInChildren<Animator>(true);
    }

    private static Transform EnsureChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing;

        GameObject child = new GameObject(childName);
        child.transform.SetParent(parent, false);
        return child.transform;
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
