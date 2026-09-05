using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Creates writable third-person weapon pose assets and the shared
/// ThirdPersonWeaponPose Animator layer. Does not call Very Animation.
/// </summary>
public static class ThirdPersonWeaponPoseAuthoringSetup
{
    public const string RootFolder = "Assets/Animations/ThirdPersonWeapons";
    public const string SharedFolder = RootFolder + "/Shared";
    public const string LongGunFolder = SharedFolder + "/LongGun";
    public const string ShortGunFolder = SharedFolder + "/ShortGun";
    public const string HeavyGunFolder = SharedFolder + "/HeavyGun";
    public const string SlotFolder = SharedFolder + "/Slots";
    public const string ResourcesFolder = "Assets/Resources";
    public const string LibraryPath = SharedFolder + "/ThirdPersonWeaponClassPoseLibrary.asset";
    public const string ResourcesLibraryPath = ResourcesFolder + "/ThirdPersonWeaponClassPoseLibrary.asset";
    public const string PreviewScenePath = "Assets/AnimationAuthoringTest.unity";
    public const string MaskPath = ThirdPersonWeaponSetup.MaskPath;
    public const string LayerName = ThirdPersonWeaponPoseBinder.LayerName;

    public const string LongGunHoldPath = LongGunFolder + "/LongGun_Hold.anim";
    public const string LongGunSprintPath = LongGunFolder + "/LongGun_Sprint.anim";
    public const string LongGunPronePath = LongGunFolder + "/LongGun_Prone.anim";
    public const string LongGunAimPath = LongGunFolder + "/LongGun_Aim.anim";
    public const string ShortGunHoldPath = ShortGunFolder + "/ShortGun_Hold.anim";
    public const string ShortGunSprintPath = ShortGunFolder + "/ShortGun_Sprint.anim";
    public const string ShortGunPronePath = ShortGunFolder + "/ShortGun_Prone.anim";
    public const string ShortGunAimPath = ShortGunFolder + "/ShortGun_Aim.anim";
    public const string HeavyGunHoldPath = HeavyGunFolder + "/HeavyGun_Hold.anim";
    public const string HeavyGunSprintPath = HeavyGunFolder + "/HeavyGun_Sprint.anim";
    public const string HeavyGunPronePath = HeavyGunFolder + "/HeavyGun_Prone.anim";
    public const string HeavyGunAimPath = HeavyGunFolder + "/HeavyGun_Aim.anim";
    public const string AkHoldPath = RootFolder + "/AK/AK_Hold.anim";

    public const string SlotHoldPath = SlotFolder + "/TP_WeaponPose_Hold.anim";
    public const string SlotSprintPath = SlotFolder + "/TP_WeaponPose_Sprint.anim";
    public const string SlotPronePath = SlotFolder + "/TP_WeaponPose_Prone.anim";
    public const string SlotAimPath = SlotFolder + "/TP_WeaponPose_Aim.anim";
    public const string SlotCrouchPath = SlotFolder + "/TP_WeaponPose_Crouch.anim";

    public const string LongGunProfilePath = LongGunFolder + "/LongGunPoseProfile.asset";
    public const string ShortGunProfilePath = ShortGunFolder + "/ShortGunPoseProfile.asset";
    public const string HeavyGunProfilePath = HeavyGunFolder + "/HeavyGunPoseProfile.asset";
    public const string AkProfilePath = RootFolder + "/AK/AKPoseProfile.asset";
    public const string AuthoringControllerPath = SharedFolder + "/AC_ThirdPersonPoseAuthoring.controller";
    public const string AuthoringCharacterName = "TP_WeaponPoseAuthoringCharacter";

    [MenuItem("Bullseye/Weapons/Apply REQ-048 Pose Pipeline (Deprecated)")]
    public static void ApplyFromMenu()
    {
        Debug.Log(ApplyInfrastructure());
    }

    public static string ApplyInfrastructure()
    {
        EnsureFolders();
        CreateUpperBodyMask();
        CreateSlotClips();
        CreateSharedClassClips();
        CreateAkHoldIfMissing();
        ThirdPersonWeaponClassPoseLibrary library = CreateProfilesAndLibrary();
        ConfigureAnimatorLayer();
        AssignWeaponProfiles(library);
        EnsureWeaponGripTargets();
        ThirdPersonWeaponRigSetup.WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: REQ-048 authored-pose pipeline is in place. AK uses AK_Hold over shared LongGun states.";
    }

    public static string CreateOrRepairWeapon(WeaponDefinition definition)
    {
        if (definition == null)
            return "No weapon selected.";

        EnsureFolders();
        CreateSharedClassClips();
        ThirdPersonWeaponClassPoseLibrary library = CreateProfilesAndLibrary();
        ConfigureAnimatorLayer();

        ThirdPersonWeaponPoseClass poseClass = definition.PoseClassWasAssigned()
            ? definition.WeaponPoseClass
            : GuessPoseClass(definition);

        string folder = RootFolder + "/" + SanitizeName(definition.DisplayName);
        EnsureFolder(folder);

        ThirdPersonWeaponPoseProfile profile = definition.PoseProfile;
        if (profile == null)
        {
            if (poseClass == ThirdPersonWeaponPoseClass.LongGun && definition.WeaponId == "ak")
                profile = CreateWeaponProfile(AkProfilePath, "AKPoseProfile", poseClass, library, LoadClip(AkHoldPath));
            else
            {
                string path = folder + "/" + SanitizeName(definition.DisplayName) + "PoseProfile.asset";
                profile = CreateWeaponProfile(path, SanitizeName(definition.DisplayName) + "PoseProfile", poseClass, library, null);
            }
        }

        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("thirdPersonPoseProfile").objectReferenceValue = profile;
        so.FindProperty("weaponPoseClass").enumValueIndex = (int)poseClass;
        so.FindProperty("poseClassAssigned").boolValue = true;
        so.FindProperty("thirdPersonPoseCategory").enumValueIndex =
            poseClass == ThirdPersonWeaponPoseClass.ShortGun ? 0 : 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        definition.AssignPoseProfile(profile);
        definition.AssignWeaponPoseClass(poseClass);
        EditorUtility.SetDirty(definition);
        EnsureGripOnPrefab(definition);
        AssetDatabase.SaveAssets();
        return "Created or repaired pose setup for " + definition.DisplayName + ".";
    }

    public static AnimationClip CreateWeaponPoseClip(
        WeaponDefinition definition,
        ThirdPersonWeaponPoseKind kind,
        bool overwrite)
    {
        if (definition == null)
            return null;

        string folder = RootFolder + "/" + SanitizeName(definition.DisplayName);
        EnsureFolder(folder);
        string path = folder + "/" + SanitizeName(definition.DisplayName) + "_" + kind + ".anim";
        if (definition.WeaponId == "ak" && kind == ThirdPersonWeaponPoseKind.Hold)
            path = AkHoldPath;

        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null && !overwrite)
            return existing;

        ThirdPersonWeaponPoseProfile classProfile = ThirdPersonWeaponClassPoseLibraryCache.Get(definition.WeaponPoseClass);
        AnimationClip template = classProfile != null ? classProfile.GetOwnClip(kind) : null;
        if (template == null && classProfile != null)
            template = classProfile.DefaultHoldPose;

        AnimationClip clip = CreateWritableClip(path, SanitizeName(definition.DisplayName) + "_" + kind, template);
        AssignClipToProfile(definition.PoseProfile, kind, clip);
        return clip;
    }

    public static bool PoseClassWasAssigned(this WeaponDefinition definition)
    {
        if (definition == null)
            return false;
        SerializedObject so = new SerializedObject(definition);
        SerializedProperty assigned = so.FindProperty("poseClassAssigned");
        return assigned != null && assigned.boolValue;
    }

    public static void EnsureFolders()
    {
        EnsureFolder("Assets/Animations");
        EnsureFolder(RootFolder);
        EnsureFolder(SharedFolder);
        EnsureFolder(LongGunFolder);
        EnsureFolder(ShortGunFolder);
        EnsureFolder(HeavyGunFolder);
        EnsureFolder(SlotFolder);
        EnsureFolder(RootFolder + "/AK");
        EnsureFolder(ResourcesFolder);
        EnsureFolder("Assets/Player/Masks");
    }

    public static void CreateUpperBodyMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, MaskPath);
        }

        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            AvatarMaskBodyPart part = (AvatarMaskBodyPart)i;
            bool active = part == AvatarMaskBodyPart.Body
                || part == AvatarMaskBodyPart.LeftArm
                || part == AvatarMaskBodyPart.RightArm
                || part == AvatarMaskBodyPart.LeftFingers
                || part == AvatarMaskBodyPart.RightFingers;
            mask.SetHumanoidBodyPartActive(part, active);
        }

        EditorUtility.SetDirty(mask);
    }

    public static void CreateSlotClips()
    {
        CreateMuscleClip(SlotHoldPath, "TP_WeaponPose_Hold", LongGunHoldMuscles(), overwrite: false);
        CreateMuscleClip(SlotSprintPath, "TP_WeaponPose_Sprint", LongGunSprintMuscles(), overwrite: false);
        CreateMuscleClip(SlotPronePath, "TP_WeaponPose_Prone", LongGunProneMuscles(), overwrite: false);
        CreateMuscleClip(SlotAimPath, "TP_WeaponPose_Aim", LongGunAimMuscles(), overwrite: false);
        CreateMuscleClip(SlotCrouchPath, "TP_WeaponPose_Crouch", LongGunHoldMuscles(), overwrite: false);
        CopyToResources(SlotHoldPath, "TP_WeaponPose_Hold.anim");
        CopyToResources(SlotSprintPath, "TP_WeaponPose_Sprint.anim");
        CopyToResources(SlotPronePath, "TP_WeaponPose_Prone.anim");
        CopyToResources(SlotAimPath, "TP_WeaponPose_Aim.anim");
        CopyToResources(SlotCrouchPath, "TP_WeaponPose_Crouch.anim");
    }

    private static void CopyToResources(string sourcePath, string fileName)
    {
        EnsureFolder(ResourcesFolder);
        string dest = ResourcesFolder + "/" + fileName;
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(dest) != null)
            return;
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath) != null)
            AssetDatabase.CopyAsset(sourcePath, dest);
    }

    public static void CreateSharedClassClips()
    {
        CreateMuscleClip(LongGunHoldPath, "LongGun_Hold", LongGunHoldMuscles(), overwrite: false);
        CreateMuscleClip(LongGunSprintPath, "LongGun_Sprint", LongGunSprintMuscles(), overwrite: false);
        CreateMuscleClip(LongGunPronePath, "LongGun_Prone", LongGunProneMuscles(), overwrite: false);
        CreateMuscleClip(LongGunAimPath, "LongGun_Aim", LongGunAimMuscles(), overwrite: false);

        CreateMuscleClip(ShortGunHoldPath, "ShortGun_Hold", ShortGunHoldMuscles(), overwrite: false);
        CreateMuscleClip(ShortGunSprintPath, "ShortGun_Sprint", ShortGunSprintMuscles(), overwrite: false);
        CreateMuscleClip(ShortGunPronePath, "ShortGun_Prone", ShortGunProneMuscles(), overwrite: false);
        CreateMuscleClip(ShortGunAimPath, "ShortGun_Aim", ShortGunAimMuscles(), overwrite: false);

        CreateMuscleClip(HeavyGunHoldPath, "HeavyGun_Hold", HeavyGunHoldMuscles(), overwrite: false);
        CreateMuscleClip(HeavyGunSprintPath, "HeavyGun_Sprint", LongGunSprintMuscles(), overwrite: false);
        CreateMuscleClip(HeavyGunPronePath, "HeavyGun_Prone", LongGunProneMuscles(), overwrite: false);
        CreateMuscleClip(HeavyGunAimPath, "HeavyGun_Aim", LongGunAimMuscles(), overwrite: false);
    }

    public static void CreateAkHoldIfMissing()
    {
        EnsureFolder(RootFolder + "/AK");
        AnimationClip template = AssetDatabase.LoadAssetAtPath<AnimationClip>(LongGunHoldPath);
        CreateWritableClip(AkHoldPath, "AK_Hold", template);
    }

    public static ThirdPersonWeaponClassPoseLibrary CreateProfilesAndLibrary()
    {
        ThirdPersonWeaponPoseProfile longGun = CreateClassProfile(
            LongGunProfilePath,
            "LongGunPoseProfile",
            ThirdPersonWeaponPoseClass.LongGun,
            LoadClip(LongGunHoldPath),
            LoadClip(LongGunSprintPath),
            LoadClip(LongGunAimPath),
            LoadClip(LongGunPronePath),
            supportIk: true);
        ThirdPersonWeaponPoseProfile shortGun = CreateClassProfile(
            ShortGunProfilePath,
            "ShortGunPoseProfile",
            ThirdPersonWeaponPoseClass.ShortGun,
            LoadClip(ShortGunHoldPath),
            LoadClip(ShortGunSprintPath),
            LoadClip(ShortGunAimPath),
            LoadClip(ShortGunPronePath),
            supportIk: false);
        ThirdPersonWeaponPoseProfile heavyGun = CreateClassProfile(
            HeavyGunProfilePath,
            "HeavyGunPoseProfile",
            ThirdPersonWeaponPoseClass.HeavyGun,
            LoadClip(HeavyGunHoldPath),
            LoadClip(HeavyGunSprintPath),
            LoadClip(HeavyGunAimPath),
            LoadClip(HeavyGunPronePath),
            supportIk: true);

        ThirdPersonWeaponClassPoseLibrary library = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponClassPoseLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<ThirdPersonWeaponClassPoseLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
            library = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponClassPoseLibrary>(LibraryPath);
        }

        SerializedObject so = new SerializedObject(library);
        so.FindProperty("longGun").objectReferenceValue = longGun;
        so.FindProperty("shortGun").objectReferenceValue = shortGun;
        so.FindProperty("heavyGun").objectReferenceValue = heavyGun;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);

        if (AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponClassPoseLibrary>(ResourcesLibraryPath) == null)
            AssetDatabase.CopyAsset(LibraryPath, ResourcesLibraryPath);
        else
        {
            ThirdPersonWeaponClassPoseLibrary resourcesCopy =
                AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponClassPoseLibrary>(ResourcesLibraryPath);
            SerializedObject copy = new SerializedObject(resourcesCopy);
            copy.FindProperty("longGun").objectReferenceValue = longGun;
            copy.FindProperty("shortGun").objectReferenceValue = shortGun;
            copy.FindProperty("heavyGun").objectReferenceValue = heavyGun;
            copy.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(resourcesCopy);
        }

        ThirdPersonWeaponClassPoseLibraryCache.SetEditorLibrary(library);
        CreateWeaponProfile(AkProfilePath, "AKPoseProfile", ThirdPersonWeaponPoseClass.LongGun, library, LoadClip(AkHoldPath));
        return library;
    }

    public static void ConfigureAnimatorLayer()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            PlayerLocomotionAnimatorBuilder.ControllerPath);
        if (controller == null)
            return;

        CreateUpperBodyMask();
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        EnsureInt(controller, "WeaponPoseState");
        EnsureInt(controller, "PoseClass");
        EnsureInt(controller, "PoseCategory");
        EnsureFloat(controller, "WeaponPoseWeight");
        EnsureFloat(controller, "AimWeight");

        int layerIndex = FindLayer(controller, LayerName);
        if (layerIndex < 0)
            layerIndex = FindLayer(controller, ThirdPersonWeaponPoseBinder.LegacyLayerName);
        if (layerIndex < 0)
        {
            controller.AddLayer(LayerName);
            layerIndex = controller.layers.Length - 1;
        }

        AnimatorControllerLayer[] layers = controller.layers;
        layers[layerIndex].name = LayerName;
        layers[layerIndex].defaultWeight = 0f;
        // Override keeps authored hold/sprint/prone/aim as absolute upper-body
        // poses. Partial runtime weight lets locomotion still move the arms.
        // Additive was rejected because these clips are absolute Humanoid
        // poses, not additive deltas, and would stack incorrectly.
        layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[layerIndex].avatarMask = mask;
        controller.layers = layers;

        AnimatorStateMachine machine = controller.layers[layerIndex].stateMachine;
        AnimatorState hold = EnsureState(machine, "WeaponPose_Hold", SlotHoldPath, new Vector3(280f, 40f, 0f));
        AnimatorState sprint = EnsureState(machine, "WeaponPose_Sprint", SlotSprintPath, new Vector3(280f, 140f, 0f));
        AnimatorState prone = EnsureState(machine, "WeaponPose_Prone", SlotPronePath, new Vector3(280f, 240f, 0f));
        AnimatorState aim = EnsureState(machine, "WeaponPose_Aim", SlotAimPath, new Vector3(520f, 40f, 0f));
        AnimatorState crouch = EnsureState(machine, "WeaponPose_Crouch", SlotCrouchPath, new Vector3(520f, 140f, 0f));
        machine.defaultState = hold;
        EnsureAnyState(machine, hold, 0, 0.14f);
        EnsureAnyState(machine, sprint, 1, 0.16f);
        EnsureAnyState(machine, prone, 2, 0.18f);
        EnsureAnyState(machine, aim, 3, 0.14f);
        EnsureAnyState(machine, crouch, 4, 0.14f);
        EditorUtility.SetDirty(controller);
    }

    public static AnimatorController EnsureAuthoringController(AnimationClip clip)
    {
        EnsureFolder(SharedFolder);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AuthoringControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(AuthoringControllerPath);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AuthoringControllerPath);
        }

        if (controller == null)
            return null;

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = null;
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == "AuthoringPose")
            {
                state = states[i].state;
                break;
            }
        }

        if (state == null)
            state = machine.AddState("AuthoringPose", new Vector3(300f, 80f, 0f));

        if (clip != null)
            state.motion = clip;
        state.writeDefaultValues = true;
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    public static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    public static AnimationClip SlotHold => LoadClip(SlotHoldPath);
    public static AnimationClip SlotSprint => LoadClip(SlotSprintPath);
    public static AnimationClip SlotProne => LoadClip(SlotPronePath);
    public static AnimationClip SlotAim => LoadClip(SlotAimPath);
    public static AnimationClip SlotCrouch => LoadClip(SlotCrouchPath);

    private static void AssignWeaponProfiles(ThirdPersonWeaponClassPoseLibrary library)
    {
        ThirdPersonWeaponPoseProfile ak = CreateWeaponProfile(
            AkProfilePath,
            "AKPoseProfile",
            ThirdPersonWeaponPoseClass.LongGun,
            library,
            LoadClip(AkHoldPath));

        AssignDefinition(
            ThirdPersonWeaponSetup.ResolveAkDefinitionPath(),
            ThirdPersonWeaponPoseClass.LongGun,
            ak,
            true);
        AssignDefinition(
            "Assets/Scripts/Weapons/DMRDefinition.asset",
            ThirdPersonWeaponPoseClass.LongGun,
            library.LongGun,
            true);
        AssignDefinition(
            "Assets/Scripts/Weapons/ShotgunDefinition.asset",
            ThirdPersonWeaponPoseClass.LongGun,
            library.LongGun,
            true);
        AssignDefinition(
            "Assets/Scripts/Weapons/Ruger22Definition.asset",
            ThirdPersonWeaponPoseClass.ShortGun,
            library.ShortGun,
            false);
    }

    private static void AssignDefinition(
        string path,
        ThirdPersonWeaponPoseClass poseClass,
        ThirdPersonWeaponPoseProfile profile,
        bool supportIk)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (definition == null)
            return;

        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("weaponPoseClass").enumValueIndex = (int)poseClass;
        so.FindProperty("poseClassAssigned").boolValue = true;
        so.FindProperty("thirdPersonPoseProfile").objectReferenceValue = profile;
        so.FindProperty("thirdPersonPoseCategory").enumValueIndex =
            poseClass == ThirdPersonWeaponPoseClass.ShortGun ? 0 : 1;
        so.FindProperty("supportHandIkEnabled").boolValue = supportIk;
        so.ApplyModifiedPropertiesWithoutUndo();
        definition.AssignPoseProfile(profile);
        definition.AssignWeaponPoseClass(poseClass);
        EditorUtility.SetDirty(definition);
    }

    private static ThirdPersonWeaponPoseProfile CreateClassProfile(
        string path,
        string assetName,
        ThirdPersonWeaponPoseClass poseClass,
        AnimationClip hold,
        AnimationClip sprint,
        AnimationClip aim,
        AnimationClip prone,
        bool supportIk)
    {
        ThirdPersonWeaponPoseProfile profile = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponPoseProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ThirdPersonWeaponPoseProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponPoseProfile>(path);
        }

        SerializedObject so = new SerializedObject(profile);
        so.FindProperty("weaponPoseClass").enumValueIndex = (int)poseClass;
        so.FindProperty("classDefaults").objectReferenceValue = null;
        so.FindProperty("defaultHoldPose").objectReferenceValue = hold;
        so.FindProperty("sprintPose").objectReferenceValue = sprint;
        so.FindProperty("adsOrAimPose").objectReferenceValue = aim;
        so.FindProperty("pronePose").objectReferenceValue = prone;
        so.FindProperty("supportHandIkEnabled").boolValue = supportIk;
        if (so.FindProperty("weaponPoseBlendDuration").floatValue < 0.01f)
            so.FindProperty("weaponPoseBlendDuration").floatValue = 0.14f;
        if (so.FindProperty("ikBlendDuration").floatValue < 0.01f)
            so.FindProperty("ikBlendDuration").floatValue = 0.12f;
        if (so.FindProperty("overrideLayerWeight").floatValue < 0.15f)
            so.FindProperty("overrideLayerWeight").floatValue = 0.78f;
        so.ApplyModifiedPropertiesWithoutUndo();
        profile.name = assetName;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static ThirdPersonWeaponPoseProfile CreateWeaponProfile(
        string path,
        string assetName,
        ThirdPersonWeaponPoseClass poseClass,
        ThirdPersonWeaponClassPoseLibrary library,
        AnimationClip holdOverride)
    {
        EnsureFolder(Path.GetDirectoryName(path)?.Replace("\\", "/"));
        ThirdPersonWeaponPoseProfile profile = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponPoseProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<ThirdPersonWeaponPoseProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile = AssetDatabase.LoadAssetAtPath<ThirdPersonWeaponPoseProfile>(path);
        }

        ThirdPersonWeaponPoseProfile classProfile = library != null ? library.GetClassProfile(poseClass) : null;
        SerializedObject so = new SerializedObject(profile);
        so.FindProperty("weaponPoseClass").enumValueIndex = (int)poseClass;
        so.FindProperty("classDefaults").objectReferenceValue = classProfile;
        if (holdOverride != null)
            so.FindProperty("defaultHoldPose").objectReferenceValue = holdOverride;
        so.FindProperty("supportHandIkEnabled").boolValue = poseClass != ThirdPersonWeaponPoseClass.ShortGun;
        so.ApplyModifiedPropertiesWithoutUndo();
        profile.name = assetName;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void AssignClipToProfile(
        ThirdPersonWeaponPoseProfile profile,
        ThirdPersonWeaponPoseKind kind,
        AnimationClip clip)
    {
        if (profile == null || clip == null)
            return;

        SerializedObject so = new SerializedObject(profile);
        string field = kind switch
        {
            ThirdPersonWeaponPoseKind.Sprint => "sprintPose",
            ThirdPersonWeaponPoseKind.Prone => "pronePose",
            ThirdPersonWeaponPoseKind.Aim => "adsOrAimPose",
            ThirdPersonWeaponPoseKind.Crouch => "optionalCrouchPose",
            _ => "defaultHoldPose"
        };
        so.FindProperty(field).objectReferenceValue = clip;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void EnsureWeaponGripTargets()
    {
        EnsureGripOnPrefabPath(ThirdPersonWeaponSetup.RiflePath, new Vector3(-0.12f, -0.08f, 0.10f));
        EnsureGripOnPrefabPath(ThirdPersonWeaponSetup.DmrPath, new Vector3(-0.12f, -0.08f, 0.12f));
        EnsureGripOnPrefabPath(ThirdPersonWeaponSetup.ShotgunPath, new Vector3(-0.10f, -0.08f, 0.10f));
        EnsureGripOnPrefabPath(ThirdPersonWeaponSetup.PistolPath, new Vector3(-0.04f, -0.02f, 0.04f), createHint: false);
    }

    public static void EnsureGripOnPrefab(WeaponDefinition definition)
    {
        if (definition == null || definition.WorldPrefab == null)
            return;
        string path = AssetDatabase.GetAssetPath(definition.WorldPrefab);
        if (string.IsNullOrEmpty(path))
            return;
        bool twoHanded = definition.WeaponPoseClass != ThirdPersonWeaponPoseClass.ShortGun;
        EnsureGripOnPrefabPath(path, new Vector3(-0.10f, -0.08f, 0.10f), twoHanded);
    }

    private static void EnsureGripOnPrefabPath(string path, Vector3 hintLocal, bool createHint = true)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (FindChild(contents.transform, "LeftHandGrip") == null)
            {
                GameObject grip = new GameObject("LeftHandGrip");
                grip.transform.SetParent(contents.transform, false);
            }

            Transform hint = FindChild(contents.transform, "LeftElbowHint");
            if (createHint && hint == null)
            {
                GameObject hintGo = new GameObject("LeftElbowHint");
                hintGo.transform.SetParent(contents.transform, false);
                hintGo.transform.localPosition = hintLocal;
                hint = hintGo.transform;
            }

            ThirdPersonWeaponVisual visual = contents.GetComponent<ThirdPersonWeaponVisual>();
            if (visual != null)
            {
                visual.ResolveFallbacks();
                visual.Assign(
                    visual.LeftHandGrip,
                    visual.Muzzle,
                    visual.RightHandGrip,
                    visual.AimTarget,
                    hint);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            Object.DestroyImmediate(contents);
        }
    }

    private static AnimationClip CreateWritableClip(string path, string clipName, AnimationClip template)
    {
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
            return existing;

        AnimationClip clip = new AnimationClip();
        if (template != null)
            EditorUtility.CopySerialized(template, clip);
        clip.name = clipName;
        AssetDatabase.CreateAsset(clip, path);
        clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        FlattenPoseClip(clip);
        return clip;
    }

    private static void CreateMuscleClip(
        string path,
        string clipName,
        Dictionary<string, float> muscles,
        bool overwrite)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null && !overwrite)
            return;

        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        clip.name = clipName;
        clip.ClearCurves();
        string[] names = HumanTrait.MuscleName;
        foreach (KeyValuePair<string, float> muscle in muscles)
        {
            bool found = false;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == muscle.Key)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
                continue;

            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle.Key);
            AnimationUtility.SetEditorCurve(clip, binding, SinglePoseCurve(muscle.Value));
        }

        FlattenPoseClip(clip);
    }

    public static void FlattenPoseClip(AnimationClip clip)
    {
        if (clip == null)
            return;

        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        for (int i = 0; i < bindings.Length; i++)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, bindings[i]);
            if (curve == null || curve.length == 0)
                continue;

            AnimationUtility.SetEditorCurve(clip, bindings[i], SinglePoseCurve(curve.Evaluate(0f)));
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.startTime = 0f;
        settings.stopTime = 0f;
        settings.loopTime = true;
        settings.loopBlend = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    public static void FlattenAllWeaponPoseClips()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { RootFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (clip != null)
                FlattenPoseClip(clip);
        }

        AssetDatabase.SaveAssets();
    }

    private static AnimationCurve SinglePoseCurve(float value)
    {
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(new Keyframe(0f, value, 0f, 0f));
        return curve;
    }

    private static AnimatorState EnsureState(
        AnimatorStateMachine machine,
        string stateName,
        string clipPath,
        Vector3 position)
    {
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state != null && states[i].state.name == stateName)
            {
                states[i].state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                states[i].state.writeDefaultValues = false;
                return states[i].state;
            }
        }

        AnimatorState state = machine.AddState(stateName, position);
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        state.writeDefaultValues = false;
        return state;
    }

    private static void EnsureAnyState(AnimatorStateMachine machine, AnimatorState to, int poseState, float duration)
    {
        AnimatorStateTransition[] transitions = machine.anyStateTransitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            if (transitions[i].destinationState == to)
                return;
        }

        AnimatorStateTransition transition = machine.AddAnyStateTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.Equals, poseState, "WeaponPoseState");
    }

    private static int FindLayer(AnimatorController controller, string layerName)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == layerName)
                return i;
        }

        return -1;
    }

    private static void EnsureInt(AnimatorController controller, string name)
    {
        if (HasParameter(controller, name))
            return;
        controller.AddParameter(name, AnimatorControllerParameterType.Int);
    }

    private static void EnsureFloat(AnimatorController controller, string name)
    {
        if (HasParameter(controller, name))
            return;
        controller.AddParameter(name, AnimatorControllerParameterType.Float);
    }

    private static bool HasParameter(AnimatorController controller, string name)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
                return true;
        }

        return false;
    }

    private static ThirdPersonWeaponPoseClass GuessPoseClass(WeaponDefinition definition)
    {
        SerializedObject so = new SerializedObject(definition);
        SerializedProperty assigned = so.FindProperty("poseClassAssigned");
        if (assigned != null && assigned.boolValue)
            return (ThirdPersonWeaponPoseClass)so.FindProperty("weaponPoseClass").enumValueIndex;

        SerializedProperty legacy = so.FindProperty("thirdPersonPoseCategory");
        if (legacy != null && legacy.enumValueIndex == (int)ThirdPersonPoseCategory.LongGun)
            return ThirdPersonWeaponPoseClass.LongGun;
        return ThirdPersonWeaponPoseClass.ShortGun;
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Weapon";
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }

    public static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Dictionary<string, float> LongGunHoldMuscles()
    {
        return new Dictionary<string, float>
        {
            { "Spine Front-Back", 0.08f },
            { "Spine Twist Left-Right", 0.18f },
            { "Chest Front-Back", 0.06f },
            { "Chest Twist Left-Right", 0.12f },
            { "Right Shoulder Down-Up", 0.22f },
            { "Right Shoulder Front-Back", 0.10f },
            { "Right Arm Down-Up", 0.58f },
            { "Right Arm Front-Back", 0.42f },
            { "Right Arm Twist In-Out", -0.18f },
            { "Right Forearm Stretch", -0.38f },
            { "Right Hand Down-Up", 0.12f },
            { "Left Shoulder Down-Up", 0.18f },
            { "Left Arm Down-Up", 0.46f },
            { "Left Arm Front-Back", 0.52f },
            { "Left Arm Twist In-Out", 0.10f },
            { "Left Forearm Stretch", -0.42f }
        };
    }

    private static Dictionary<string, float> LongGunSprintMuscles()
    {
        return new Dictionary<string, float>
        {
            { "Spine Front-Back", 0.16f },
            { "Spine Twist Left-Right", 0.08f },
            { "Chest Front-Back", 0.10f },
            { "Right Shoulder Down-Up", 0.08f },
            { "Right Arm Down-Up", 0.28f },
            { "Right Arm Front-Back", 0.18f },
            { "Right Arm Twist In-Out", -0.08f },
            { "Right Forearm Stretch", -0.22f },
            { "Left Shoulder Down-Up", 0.06f },
            { "Left Arm Down-Up", 0.22f },
            { "Left Arm Front-Back", 0.20f },
            { "Left Forearm Stretch", -0.18f }
        };
    }

    private static Dictionary<string, float> LongGunProneMuscles()
    {
        return new Dictionary<string, float>
        {
            { "Spine Front-Back", -0.08f },
            { "Chest Front-Back", -0.04f },
            { "Right Shoulder Down-Up", 0.12f },
            { "Right Arm Down-Up", 0.36f },
            { "Right Arm Front-Back", 0.48f },
            { "Right Forearm Stretch", -0.28f },
            { "Left Arm Down-Up", 0.32f },
            { "Left Arm Front-Back", 0.50f },
            { "Left Forearm Stretch", -0.30f }
        };
    }

    private static Dictionary<string, float> LongGunAimMuscles()
    {
        Dictionary<string, float> muscles = LongGunHoldMuscles();
        muscles["Right Arm Down-Up"] = 0.64f;
        muscles["Right Arm Front-Back"] = 0.50f;
        muscles["Left Arm Down-Up"] = 0.52f;
        muscles["Chest Front-Back"] = 0.10f;
        return muscles;
    }

    private static Dictionary<string, float> ShortGunHoldMuscles()
    {
        return new Dictionary<string, float>
        {
            { "Spine Front-Back", 0.04f },
            { "Chest Front-Back", 0.03f },
            { "Right Shoulder Down-Up", 0.16f },
            { "Right Arm Down-Up", 0.48f },
            { "Right Arm Front-Back", 0.28f },
            { "Right Forearm Stretch", -0.22f },
            { "Left Shoulder Down-Up", 0.10f },
            { "Left Arm Down-Up", 0.20f },
            { "Left Arm Front-Back", 0.12f },
            { "Left Forearm Stretch", -0.12f }
        };
    }

    private static Dictionary<string, float> ShortGunSprintMuscles()
    {
        return new Dictionary<string, float>
        {
            { "Right Arm Down-Up", 0.22f },
            { "Right Arm Front-Back", 0.12f },
            { "Right Forearm Stretch", -0.10f },
            { "Left Arm Down-Up", 0.16f },
            { "Left Arm Front-Back", 0.10f }
        };
    }

    private static Dictionary<string, float> ShortGunProneMuscles()
    {
        return new Dictionary<string, float>
        {
            { "Right Arm Down-Up", 0.30f },
            { "Right Arm Front-Back", 0.40f },
            { "Left Arm Down-Up", 0.18f },
            { "Left Arm Front-Back", 0.22f }
        };
    }

    private static Dictionary<string, float> ShortGunAimMuscles()
    {
        Dictionary<string, float> muscles = ShortGunHoldMuscles();
        muscles["Right Arm Down-Up"] = 0.56f;
        muscles["Right Arm Front-Back"] = 0.36f;
        return muscles;
    }

    private static Dictionary<string, float> HeavyGunHoldMuscles()
    {
        Dictionary<string, float> muscles = LongGunHoldMuscles();
        muscles["Spine Front-Back"] = 0.14f;
        muscles["Right Arm Down-Up"] = 0.50f;
        muscles["Left Arm Down-Up"] = 0.40f;
        return muscles;
    }
}
