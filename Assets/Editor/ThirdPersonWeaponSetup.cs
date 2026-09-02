using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds third-person weapon wrappers, pose defaults, and the upper-body mask.
/// </summary>
public static class ThirdPersonWeaponSetup
{
    public const string PrefabFolder = "Assets/Weapons/ThirdPerson";
    public const string MaskFolder = "Assets/Player/Masks";
    public const string MaskPath = MaskFolder + "/UpperBodyWeapon.mask";
    public const string PistolPath = PrefabFolder + "/ThirdPerson_Pistol.prefab";
    public const string RiflePath = PrefabFolder + "/ThirdPerson_AK.prefab";
    public const string DmrPath = PrefabFolder + "/ThirdPerson_DMR.prefab";
    public const string ShotgunPath = PrefabFolder + "/ThirdPerson_Shotgun.prefab";

    [MenuItem("Bullseye/Weapons/Apply REQ-047 Weapon Rig")]
    public static void ApplyReq047FromMenu()
    {
        Debug.Log(ApplyReq047());
    }

    public static string ApplyReq047()
    {
        RenameLeftHandGrips();
        CreateUpperBodyMask();
        EnsureWeaponPoseLayer();
        ConfigureDefinition(
            "Assets/Scripts/Weapons/Ruger22Definition.asset",
            AssetDatabase.LoadAssetAtPath<GameObject>(PistolPath),
            ThirdPersonWeaponClass.Pistol,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Pistol),
            overwritePose: false);
        ConfigureDefinition(
            ResolveAkDefinitionPath(),
            AssetDatabase.LoadAssetAtPath<GameObject>(RiflePath),
            ThirdPersonWeaponClass.Rifle,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Rifle),
            overwritePose: false);
        ConfigureDefinition(
            "Assets/Scripts/Weapons/ShotgunDefinition.asset",
            AssetDatabase.LoadAssetAtPath<GameObject>(ShotgunPath),
            ThirdPersonWeaponClass.Shotgun,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Shotgun),
            overwritePose: false);
        ConfigureDefinition(
            "Assets/Scripts/Weapons/DMRDefinition.asset",
            AssetDatabase.LoadAssetAtPath<GameObject>(DmrPath),
            ThirdPersonWeaponClass.Rifle,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Rifle),
            overwritePose: false);
        WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: REQ-047 weapon socket, left-hand grips, and upper-body layer applied";
    }

    private static void RenameLeftHandGrips()
    {
        string[] paths = { PistolPath, RiflePath, DmrPath, ShotgunPath };
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            if (prefab == null)
                continue;

            GameObject contents = PrefabUtility.LoadPrefabContents(paths[i]);
            try
            {
                Transform left = FindChild(contents.transform, "LeftHandIKTarget");
                if (left != null)
                    left.name = "LeftHandGrip";
                if (FindChild(contents.transform, "LeftHandGrip") == null)
                {
                    GameObject grip = new GameObject("LeftHandGrip");
                    grip.transform.SetParent(contents.transform, false);
                }

                ThirdPersonWeaponVisual visual = contents.GetComponent<ThirdPersonWeaponVisual>();
                if (visual != null)
                    visual.ResolveFallbacks();
                PrefabUtility.SaveAsPrefabAsset(contents, paths[i]);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }

    [MenuItem("Bullseye/Weapons/Setup Third-Person Weapon Alignment")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        EnsureFolder("Assets/Weapons");
        EnsureFolder(PrefabFolder);
        EnsureFolder("Assets/Player");
        EnsureFolder(MaskFolder);

        GameObject pistolSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Weapons/Pistol/Prefabs/Pistol_Gameplay.prefab");
        GameObject rifleSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Weapons/AK/Prefabs/AK_Gameplay.prefab");
        GameObject shotgunSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Weapons/Shotgun/Prefabs/Shotgun_Gameplay.prefab");
        if (pistolSource == null || rifleSource == null || shotgunSource == null)
            return "FAILED: missing gameplay weapon prefabs";

        GameObject pistol = CreateWrapper("ThirdPerson_Pistol", pistolSource, PistolPath, new Vector3(0.04f, -0.03f, 0.00f));
        GameObject rifle = CreateWrapper("ThirdPerson_AK", rifleSource, RiflePath, new Vector3(0.02f, -0.02f, 0.22f));
        GameObject shotgun = CreateWrapper("ThirdPerson_Shotgun", shotgunSource, ShotgunPath, new Vector3(0.02f, -0.02f, 0.18f));

        ConfigureDefinition(
            "Assets/Scripts/Weapons/Ruger22Definition.asset",
            pistol,
            ThirdPersonWeaponClass.Pistol,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Pistol),
            overwritePose: false);
        ConfigureDefinition(
            ResolveAkDefinitionPath(),
            rifle,
            ThirdPersonWeaponClass.Rifle,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Rifle),
            overwritePose: false);

        GameObject dmrSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Weapons/DMR/Prefabs/DMR_Gameplay.prefab");
        GameObject dmr = dmrSource != null
            ? CreateWrapper("ThirdPerson_DMR", dmrSource, DmrPath, new Vector3(0.02f, -0.02f, 0.22f))
            : AssetDatabase.LoadAssetAtPath<GameObject>(DmrPath);
        if (dmr != null)
        {
            ConfigureDefinition(
                "Assets/Scripts/Weapons/DMRDefinition.asset",
                dmr,
                ThirdPersonWeaponClass.Rifle,
                ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Rifle),
                overwritePose: false);
        }

            ConfigureDefinition(
                "Assets/Scripts/Weapons/ShotgunDefinition.asset",
                shotgun,
                ThirdPersonWeaponClass.Shotgun,
                ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Shotgun),
                overwritePose: false);

        CreateUpperBodyMask();
        EnsureWeaponPoseLayer();
        WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: third-person weapon alignment assets updated";
    }

    public static GameObject CreateWrapper(string name, GameObject source, string path, Vector3 leftHandLocal)
    {
        GameObject root = new GameObject(name);
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            GameObject left = new GameObject("LeftHandGrip");
            left.transform.SetParent(root.transform, false);
            left.transform.localPosition = leftHandLocal;
            left.transform.localRotation = Quaternion.identity;

            Transform muzzle = FindChild(root.transform, "Muzzle") ?? FindChild(root.transform, "MuzzlePoint");
            if (muzzle == null)
            {
                GameObject muzzleGo = new GameObject("Muzzle");
                muzzleGo.transform.SetParent(root.transform, false);
                muzzle = muzzleGo.transform;
            }

            Transform aim = FindChild(root.transform, "AimTarget") ?? FindChild(root.transform, "AimPoint");
            GameObject grip = new GameObject("RightHandGrip");
            grip.transform.SetParent(root.transform, false);

            ThirdPersonWeaponVisual visual = root.AddComponent<ThirdPersonWeaponVisual>();
            visual.Assign(left.transform, muzzle, grip.transform, aim);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void ConfigureDefinition(
        string assetPath,
        GameObject worldPrefab,
        ThirdPersonWeaponClass weaponClass,
        ThirdPersonWeaponPose pose,
        bool overwritePose = true)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(assetPath);
        if (definition == null)
            return;

        SerializedObject so = new SerializedObject(definition);
        if (worldPrefab != null)
            so.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
        so.FindProperty("thirdPersonClass").enumValueIndex = (int)weaponClass;
        WriteSocketDefaults(so, weaponClass);
        if (overwritePose)
            WritePose(so.FindProperty("thirdPersonPose"), pose);
        else
            WriteMissingPoseFields(so.FindProperty("thirdPersonPose"), pose);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void WriteSocketDefaults(SerializedObject so, ThirdPersonWeaponClass weaponClass)
    {
        Vector3 position = weaponClass == ThirdPersonWeaponClass.Pistol
            ? new Vector3(0f, 0.01f, 0.03f)
            : new Vector3(0f, 0.02f, 0.05f);
        SerializedProperty current = so.FindProperty("worldLocalPosition");
        if (current.vector3Value.sqrMagnitude > 0.2f)
            current.vector3Value = position;
        if (so.FindProperty("worldLocalScale").vector3Value.sqrMagnitude < 0.01f)
            so.FindProperty("worldLocalScale").vector3Value = Vector3.one;
    }

    private static void WritePose(SerializedProperty poseProp, ThirdPersonWeaponPose pose)
    {
        if (poseProp == null || pose == null)
            return;

        WriteVector(poseProp.FindPropertyRelative("gunEuler"), pose.gunEuler);
        WriteVector(poseProp.FindPropertyRelative("gunScale"), pose.gunScale);
        WriteVector(poseProp.FindPropertyRelative("rightHandPosition"), pose.rightHandPosition);
        WriteVector(poseProp.FindPropertyRelative("rightWristEuler"), pose.rightWristEuler);
        WriteVector(poseProp.FindPropertyRelative("aimRightHandPosition"), pose.aimRightHandPosition);
        WriteVector(poseProp.FindPropertyRelative("aimRightWristEuler"), pose.aimRightWristEuler);
        WriteVector(poseProp.FindPropertyRelative("sprintRightHandPosition"), pose.sprintRightHandPosition);
        WriteVector(poseProp.FindPropertyRelative("sprintRightWristEuler"), pose.sprintRightWristEuler);
        WriteVector(poseProp.FindPropertyRelative("proneRightHandPosition"), pose.proneRightHandPosition);
        WriteVector(poseProp.FindPropertyRelative("proneRightWristEuler"), pose.proneRightWristEuler);
        WriteVector(poseProp.FindPropertyRelative("leftWristEuler"), pose.leftWristEuler);
        WriteVector(poseProp.FindPropertyRelative("aimLeftWristEuler"), pose.aimLeftWristEuler);
        poseProp.FindPropertyRelative("rightElbowYaw").floatValue = pose.rightElbowYaw;
        poseProp.FindPropertyRelative("leftElbowYaw").floatValue = pose.leftElbowYaw;
        poseProp.FindPropertyRelative("defaultWeight").floatValue = pose.defaultWeight;
        poseProp.FindPropertyRelative("sprintWeight").floatValue = pose.sprintWeight;
        poseProp.FindPropertyRelative("crouchWeight").floatValue = pose.crouchWeight;
        poseProp.FindPropertyRelative("proneWeight").floatValue = pose.proneWeight;
        poseProp.FindPropertyRelative("diveWeight").floatValue = pose.diveWeight;
        poseProp.FindPropertyRelative("jumpWeight").floatValue = pose.jumpWeight;
        poseProp.FindPropertyRelative("rightArmReach").floatValue = pose.rightArmReach;
        poseProp.FindPropertyRelative("leftArmReach").floatValue = pose.leftArmReach;
        poseProp.FindPropertyRelative("sprintLeftIkWeight").floatValue = pose.sprintLeftIkWeight;
        poseProp.FindPropertyRelative("maxAimPitch").floatValue = pose.maxAimPitch;
        poseProp.FindPropertyRelative("maxAimPitchDown").floatValue = pose.maxAimPitchDown;
        poseProp.FindPropertyRelative("spineAimWeight").floatValue = pose.spineAimWeight;
        poseProp.FindPropertyRelative("upperChestAimShare").floatValue = pose.upperChestAimShare;
        poseProp.FindPropertyRelative("aimRaisePitch").floatValue = pose.aimRaisePitch;
        poseProp.FindPropertyRelative("proneBodyPitch").floatValue = pose.proneBodyPitch;
        poseProp.FindPropertyRelative("recoilPitch").floatValue = pose.recoilPitch;
        poseProp.FindPropertyRelative("recoilRightRoll").floatValue = pose.recoilRightRoll;
        poseProp.FindPropertyRelative("recoilYaw").floatValue = pose.recoilYaw;
        poseProp.FindPropertyRelative("recoilInTime").floatValue = pose.recoilInTime;
        poseProp.FindPropertyRelative("recoilOutTime").floatValue = pose.recoilOutTime;
    }

    private static void WriteMissingPoseFields(SerializedProperty poseProp, ThirdPersonWeaponPose pose)
    {
        if (poseProp == null || pose == null)
            return;

        WriteIfNearZero(poseProp, "sprintRightHandPosition", pose.sprintRightHandPosition);
        WriteIfNearZero(poseProp, "sprintRightWristEuler", pose.sprintRightWristEuler);
        WriteIfNearZero(poseProp, "proneRightHandPosition", pose.proneRightHandPosition);
        WriteIfNearZero(poseProp, "maxAimPitchDown", pose.maxAimPitchDown);
        WriteIfNearZero(poseProp, "proneBodyPitch", pose.proneBodyPitch);
        WriteIfNearZero(poseProp, "sprintLeftIkWeight", pose.sprintLeftIkWeight);
    }

    private static void WriteIfNearZero(SerializedProperty poseProp, string field, Vector3 value)
    {
        SerializedProperty property = poseProp.FindPropertyRelative(field);
        if (property != null && property.vector3Value.sqrMagnitude < 0.0001f)
            property.vector3Value = value;
    }

    private static void WriteIfNearZero(SerializedProperty poseProp, string field, float value)
    {
        SerializedProperty property = poseProp.FindPropertyRelative(field);
        if (property != null && Mathf.Abs(property.floatValue) < 0.01f)
            property.floatValue = value;
    }

    private static void CreateUpperBodyMask()
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
                || part == AvatarMaskBodyPart.Head
                || part == AvatarMaskBodyPart.LeftArm
                || part == AvatarMaskBodyPart.RightArm
                || part == AvatarMaskBodyPart.LeftFingers
                || part == AvatarMaskBodyPart.RightFingers;
            mask.SetHumanoidBodyPartActive(part, active);
        }

        EditorUtility.SetDirty(mask);
    }

    public static void EnsureWeaponPoseLayer()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            PlayerLocomotionAnimatorBuilder.ControllerPath);
        EnsureWeaponPoseLayer(controller);
    }

    public static void EnsureWeaponPoseLayer(AnimatorController controller)
    {
        if (controller == null)
            return;

        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null)
        {
            CreateUpperBodyMask();
            mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        }

        EnsureAnimatorFloat(controller, "AimWeight");
        EnsureAnimatorFloat(controller, "WeaponPoseWeight");

        int layerIndex = -1;
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].name == "WeaponPose")
            {
                layerIndex = i;
                break;
            }
        }

        if (layerIndex < 0)
        {
            controller.AddLayer("WeaponPose");
            layers = controller.layers;
            layerIndex = layers.Length - 1;
        }

        layers[layerIndex].defaultWeight = 0f;
        layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[layerIndex].avatarMask = mask;
        controller.layers = layers;

        AnimatorStateMachine machine = controller.layers[layerIndex].stateMachine;
        if (machine != null && machine.states.Length == 0)
        {
            AnimationClip ready = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/Player/Idle_PlayerThirdPerson.anim");
            AnimatorState state = machine.AddState("Weapon Ready", new Vector3(300f, 80f, 0f));
            state.motion = ready;
            state.writeDefaultValues = true;
            machine.defaultState = state;
        }

        EditorUtility.SetDirty(controller);
    }

    private static void EnsureAnimatorFloat(AnimatorController controller, string name)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
                return;
        }

        controller.AddParameter(name, AnimatorControllerParameterType.Float);
    }

    private static void WirePlayerPrefab()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
        if (player == null)
            return;

        string prefabPath = AssetDatabase.GetAssetPath(player);
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            ThirdPersonWeaponRig rig = contents.GetComponent<ThirdPersonWeaponRig>();
            if (rig == null)
                rig = contents.AddComponent<ThirdPersonWeaponRig>();

            SerializedObject so = new SerializedObject(rig);
            Animator animator = contents.GetComponentInChildren<Animator>(true);
            PlayerVisualRig visualRig = contents.GetComponentInChildren<PlayerVisualRig>(true);
            Transform socket = FindChild(contents.transform, "RightHandWeaponSocket")
                ?? FindChild(contents.transform, "WeaponSocket");
            Transform visual = contents.transform.Find("VisualRoot");
            so.FindProperty("thirdPersonAnimator").objectReferenceValue = animator;
            so.FindProperty("visualRig").objectReferenceValue = visualRig;
            so.FindProperty("animationState").objectReferenceValue = contents.GetComponent<PlayerAnimationState>();
            so.FindProperty("playerHealth").objectReferenceValue = contents.GetComponent<PlayerHealth>();
            so.FindProperty("worldWeapon").objectReferenceValue = contents.GetComponent<WorldWeaponView>();
            so.FindProperty("coordinator").objectReferenceValue = contents.GetComponent<WeaponPresentationCoordinator>();
            so.FindProperty("visualRoot").objectReferenceValue = visual;
            so.FindProperty("weaponSocket").objectReferenceValue = socket;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject world = new SerializedObject(contents.GetComponent<WorldWeaponView>());
            world.FindProperty("thirdPersonRig").objectReferenceValue = rig;
            world.FindProperty("weaponSocket").objectReferenceValue = socket;
            world.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    public static string ResolveAkDefinitionPath()
    {
        const string current = "Assets/Scripts/Weapons/AKDefinition.asset";
        const string legacy = "Assets/Scripts/Weapons/RifleDefinition.asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponDefinition>(current) != null)
            return current;
        return legacy;
    }

    private static void WriteVector(SerializedProperty property, Vector3 value)
    {
        if (property == null)
            return;
        property.vector3Value = value;
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
