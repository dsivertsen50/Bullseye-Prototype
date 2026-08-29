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
    public const string ShotgunPath = PrefabFolder + "/ThirdPerson_Shotgun.prefab";

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
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Pistol));
        ConfigureDefinition(
            "Assets/Scripts/Weapons/RifleDefinition.asset",
            rifle,
            ThirdPersonWeaponClass.Rifle,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Rifle));
        ConfigureDefinition(
            "Assets/Scripts/Weapons/ShotgunDefinition.asset",
            shotgun,
            ThirdPersonWeaponClass.Shotgun,
            ThirdPersonWeaponPose.CreateDefault(ThirdPersonWeaponClass.Shotgun));

        CreateUpperBodyMask();
        EnsureWeaponPoseLayer();
        WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: third-person weapon alignment assets updated";
    }

    private static GameObject CreateWrapper(string name, GameObject source, string path, Vector3 leftHandLocal)
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

            GameObject left = new GameObject("LeftHandIKTarget");
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
        ThirdPersonWeaponPose pose)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(assetPath);
        if (definition == null)
            return;

        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("worldPrefab").objectReferenceValue = worldPrefab;
        so.FindProperty("thirdPersonClass").enumValueIndex = (int)weaponClass;
        SerializedProperty poseProp = so.FindProperty("thirdPersonPose");
        WriteVector(poseProp.FindPropertyRelative("rightHandLocalPosition"), pose.rightHandLocalPosition);
        WriteVector(poseProp.FindPropertyRelative("rightHandLocalEuler"), pose.rightHandLocalEuler);
        WriteVector(poseProp.FindPropertyRelative("rightHandLocalScale"), pose.rightHandLocalScale);
        WriteVector(poseProp.FindPropertyRelative("holdLocalPosition"), pose.holdLocalPosition);
        WriteVector(poseProp.FindPropertyRelative("holdLocalEuler"), pose.holdLocalEuler);
        WriteVector(poseProp.FindPropertyRelative("aimHoldLocalPosition"), pose.aimHoldLocalPosition);
        WriteVector(poseProp.FindPropertyRelative("aimHoldLocalEuler"), pose.aimHoldLocalEuler);
        WriteVector(poseProp.FindPropertyRelative("aimRightHandLocalPosition"), pose.aimRightHandLocalPosition);
        WriteVector(poseProp.FindPropertyRelative("aimRightHandLocalEuler"), pose.aimRightHandLocalEuler);
        poseProp.FindPropertyRelative("defaultWeight").floatValue = pose.defaultWeight;
        poseProp.FindPropertyRelative("sprintWeight").floatValue = pose.sprintWeight;
        poseProp.FindPropertyRelative("crouchWeight").floatValue = pose.crouchWeight;
        poseProp.FindPropertyRelative("proneWeight").floatValue = pose.proneWeight;
        poseProp.FindPropertyRelative("diveWeight").floatValue = pose.diveWeight;
        poseProp.FindPropertyRelative("jumpWeight").floatValue = pose.jumpWeight;
        poseProp.FindPropertyRelative("rightArmReach").floatValue = pose.rightArmReach;
        poseProp.FindPropertyRelative("leftArmReach").floatValue = pose.leftArmReach;
        poseProp.FindPropertyRelative("maxAimPitch").floatValue = pose.maxAimPitch;
        poseProp.FindPropertyRelative("spineAimWeight").floatValue = pose.spineAimWeight;
        poseProp.FindPropertyRelative("upperChestAimShare").floatValue = pose.upperChestAimShare;
        poseProp.FindPropertyRelative("aimRaisePitch").floatValue = pose.aimRaisePitch;
        poseProp.FindPropertyRelative("recoilPitch").floatValue = pose.recoilPitch;
        poseProp.FindPropertyRelative("recoilRightRoll").floatValue = pose.recoilRightRoll;
        poseProp.FindPropertyRelative("recoilYaw").floatValue = pose.recoilYaw;
        poseProp.FindPropertyRelative("recoilInTime").floatValue = pose.recoilInTime;
        poseProp.FindPropertyRelative("recoilOutTime").floatValue = pose.recoilOutTime;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
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

        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].name == "WeaponPose")
                return;
        }

        controller.AddLayer("WeaponPose");
        AnimatorControllerLayer[] layers = controller.layers;
        int index = layers.Length - 1;
        layers[index].defaultWeight = 0f;
        layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[index].avatarMask = mask;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
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
            Transform socket = FindChild(contents.transform, "RightHandWeaponSocket");
            Transform anchor = FindChild(contents.transform, "WeaponHandAnchor");
            Transform visual = contents.transform.Find("VisualRoot");
            so.FindProperty("thirdPersonAnimator").objectReferenceValue = animator;
            so.FindProperty("visualRig").objectReferenceValue = visualRig;
            so.FindProperty("animationState").objectReferenceValue = contents.GetComponent<PlayerAnimationState>();
            so.FindProperty("playerHealth").objectReferenceValue = contents.GetComponent<PlayerHealth>();
            so.FindProperty("worldWeapon").objectReferenceValue = contents.GetComponent<WorldWeaponView>();
            so.FindProperty("visualRoot").objectReferenceValue = visual;
            so.FindProperty("weaponSocket").objectReferenceValue = socket;
            so.FindProperty("weaponHandAnchor").objectReferenceValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject world = new SerializedObject(contents.GetComponent<WorldWeaponView>());
            world.FindProperty("thirdPersonRig").objectReferenceValue = rig;
            world.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
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
