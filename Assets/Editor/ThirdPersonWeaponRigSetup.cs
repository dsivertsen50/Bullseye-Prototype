using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Wires the REQ-048 Animation Rigging architecture, shared LongGun
/// upper-body poses, and AK grip/hint targets.
/// </summary>
public static class ThirdPersonWeaponRigSetup
{
    public const string ReadyClipPath = "Assets/Player/Animations/LongGunReady.anim";
    public const string SprintClipPath = "Assets/Player/Animations/LongGunSprint.anim";
    public const string ProneClipPath = "Assets/Player/Animations/LongGunProne.anim";

    [MenuItem("Bullseye/Weapons/Apply REQ-048 Weapon Rig")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        CreateUpperBodyPoses();
        ConfigureWeaponPoseLayer();
        ConfigureAkDefinition();
        ConfigureDefinitionCategory("Assets/Scripts/Weapons/DMRDefinition.asset", ThirdPersonPoseCategory.LongGun, true);
        ConfigureDefinitionCategory("Assets/Scripts/Weapons/ShotgunDefinition.asset", ThirdPersonPoseCategory.LongGun, true);
        ConfigureDefinitionCategory("Assets/Scripts/Weapons/Ruger22Definition.asset", ThirdPersonPoseCategory.Pistol, false);
        EnsureWeaponHints();
        WirePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: REQ-048 Animation Rigging weapon architecture applied (AK proof of concept).";
    }

    public static void CreateUpperBodyPoses()
    {
        EnsureFolder("Assets/Player/Animations");
        WritePoseClip(ReadyClipPath, LongGunReadyMuscles(), "LongGunReady");
        WritePoseClip(SprintClipPath, LongGunSprintMuscles(), "LongGunSprint");
        WritePoseClip(ProneClipPath, LongGunProneMuscles(), "LongGunProne");
    }

    public static void ConfigureWeaponPoseLayer()
    {
        ThirdPersonWeaponSetup.CreateUpperBodyMaskIfNeeded();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            PlayerLocomotionAnimatorBuilder.ControllerPath);
        if (controller == null)
            return;

        EnsureInt(controller, "WeaponPoseState");
        EnsureInt(controller, "PoseCategory");
        ThirdPersonWeaponSetup.EnsureWeaponPoseLayer(controller);

        int layerIndex = FindLayer(controller, "WeaponPose");
        if (layerIndex < 0)
            return;

        AnimatorControllerLayer[] layers = controller.layers;
        layers[layerIndex].defaultWeight = 0f;
        layers[layerIndex].blendingMode = AnimatorLayerBlendingMode.Override;
        layers[layerIndex].avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(ThirdPersonWeaponSetup.MaskPath);
        controller.layers = layers;

        AnimatorStateMachine machine = controller.layers[layerIndex].stateMachine;
        AnimatorState ready = EnsureState(machine, "LongGunReady", ReadyClipPath, new Vector3(280f, 40f, 0f));
        AnimatorState sprint = EnsureState(machine, "LongGunSprint", SprintClipPath, new Vector3(280f, 140f, 0f));
        AnimatorState prone = EnsureState(machine, "LongGunProne", ProneClipPath, new Vector3(280f, 240f, 0f));
        machine.defaultState = ready;
        EnsureIntTransition(machine, ready, sprint, "WeaponPoseState", 1, 0.16f);
        EnsureIntTransition(machine, sprint, ready, "WeaponPoseState", 0, 0.16f);
        EnsureIntTransition(machine, ready, prone, "WeaponPoseState", 2, 0.18f);
        EnsureIntTransition(machine, sprint, prone, "WeaponPoseState", 2, 0.18f);
        EnsureIntTransition(machine, prone, ready, "WeaponPoseState", 0, 0.18f);
        EnsureIntTransition(machine, prone, sprint, "WeaponPoseState", 1, 0.18f);
        EditorUtility.SetDirty(controller);
    }

    private static void ConfigureAkDefinition()
    {
        ConfigureDefinitionCategory(
            ThirdPersonWeaponSetup.ResolveAkDefinitionPath(),
            ThirdPersonPoseCategory.LongGun,
            true);
    }

    private static void ConfigureDefinitionCategory(
        string path,
        ThirdPersonPoseCategory category,
        bool supportIk)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (definition == null)
            return;

        SerializedObject so = new SerializedObject(definition);
        so.FindProperty("thirdPersonPoseCategory").enumValueIndex = (int)category;
        so.FindProperty("supportHandIkEnabled").boolValue = supportIk;
        if (so.FindProperty("ikBlendDuration").floatValue < 0.01f)
            so.FindProperty("ikBlendDuration").floatValue = 0.12f;
        if (so.FindProperty("sprintSupportIkWeight").floatValue < 0.01f)
            so.FindProperty("sprintSupportIkWeight").floatValue = 0.55f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private static void EnsureWeaponHints()
    {
        EnsureHintOnPrefab(ThirdPersonWeaponSetup.RiflePath, new Vector3(-0.12f, -0.08f, 0.10f));
        EnsureHintOnPrefab(ThirdPersonWeaponSetup.DmrPath, new Vector3(-0.12f, -0.08f, 0.12f));
        EnsureHintOnPrefab(ThirdPersonWeaponSetup.ShotgunPath, new Vector3(-0.10f, -0.08f, 0.10f));
    }

    private static void EnsureHintOnPrefab(string path, Vector3 localPosition)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform hint = FindChild(contents.transform, "LeftElbowHint");
            if (hint == null)
            {
                GameObject hintGo = new GameObject("LeftElbowHint");
                hintGo.transform.SetParent(contents.transform, false);
                hintGo.transform.localPosition = localPosition;
                hint = hintGo.transform;
            }

            if (FindChild(contents.transform, "LeftHandGrip") == null)
            {
                GameObject grip = new GameObject("LeftHandGrip");
                grip.transform.SetParent(contents.transform, false);
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

    public static void WirePlayerPrefab()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player.prefab");
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

            Transform socket = FindChild(contents.transform, "RightHandWeaponSocket")
                ?? FindChild(contents.transform, "WeaponSocket");
            Transform visual = contents.transform.Find("VisualRoot");
            Transform host = EnsureChild(contents.transform, "WorldWeaponRig");
            Transform aimTarget = EnsureChild(host, "AimTarget");
            Transform leftTarget = EnsureChild(host, "LeftHandIKTarget");
            Transform leftHint = EnsureChild(host, "LeftElbowHint");

            RigBuilder builder = null;
            Rig rig = null;
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

                rig = rigTransform.GetComponent<Rig>();
                if (rig == null)
                    rig = rigTransform.gameObject.AddComponent<Rig>();

                Transform leftIkTransform = rigTransform.Find("LeftHandIK");
                if (leftIkTransform == null)
                {
                    GameObject ikGo = new GameObject("LeftHandIK");
                    ikGo.transform.SetParent(rigTransform, false);
                    leftIkTransform = ikGo.transform;
                }

                leftIk = leftIkTransform.GetComponent<TwoBoneIKConstraint>();
                if (leftIk == null)
                    leftIk = leftIkTransform.gameObject.AddComponent<TwoBoneIKConstraint>();

                Transform aimTransform = rigTransform.Find("AimRig");
                if (aimTransform == null)
                {
                    GameObject aimGo = new GameObject("AimRig");
                    aimGo.transform.SetParent(rigTransform, false);
                    aimTransform = aimGo.transform;
                }

                aim = aimTransform.GetComponent<MultiAimConstraint>();
                if (aim == null)
                    aim = aimTransform.gameObject.AddComponent<MultiAimConstraint>();

                TwoBoneIKConstraintData ikData = leftIk.data;
                ikData.root = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                ikData.mid = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                ikData.tip = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                ikData.target = leftTarget;
                ikData.hint = leftHint;
                ikData.targetPositionWeight = 1f;
                ikData.targetRotationWeight = 1f;
                ikData.hintWeight = 1f;
                leftIk.data = ikData;
                leftIk.weight = 1f;

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
            so.FindProperty("visualRig").objectReferenceValue = contents.GetComponentInChildren<PlayerVisualRig>(true);
            so.FindProperty("animationState").objectReferenceValue = contents.GetComponent<PlayerAnimationState>();
            so.FindProperty("playerHealth").objectReferenceValue = contents.GetComponent<PlayerHealth>();
            so.FindProperty("worldWeapon").objectReferenceValue = contents.GetComponent<WorldWeaponView>();
            so.FindProperty("coordinator").objectReferenceValue = contents.GetComponent<WeaponPresentationCoordinator>();
            so.FindProperty("visualRoot").objectReferenceValue = visual;
            so.FindProperty("weaponSocket").objectReferenceValue = socket;
            so.FindProperty("aimTarget").objectReferenceValue = aimTarget;
            so.FindProperty("leftHandIkTarget").objectReferenceValue = leftTarget;
            so.FindProperty("leftElbowHint").objectReferenceValue = leftHint;
            so.FindProperty("rigBuilder").objectReferenceValue = builder;
            so.FindProperty("weaponRig").objectReferenceValue = rig;
            so.FindProperty("leftHandIk").objectReferenceValue = leftIk;
            so.FindProperty("spineAim").objectReferenceValue = aim;
            so.ApplyModifiedPropertiesWithoutUndo();

            WorldWeaponView world = contents.GetComponent<WorldWeaponView>();
            if (world != null)
            {
                SerializedObject worldSo = new SerializedObject(world);
                worldSo.FindProperty("thirdPersonRig").objectReferenceValue = controller;
                worldSo.FindProperty("weaponSocket").objectReferenceValue = socket;
                worldSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            Object.DestroyImmediate(contents);
        }
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
                return states[i].state;
            }
        }

        AnimatorState state = machine.AddState(stateName, position);
        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        state.writeDefaultValues = true;
        return state;
    }

    private static void EnsureIntTransition(
        AnimatorStateMachine machine,
        AnimatorState from,
        AnimatorState to,
        string parameter,
        int value,
        float duration)
    {
        AnimatorStateTransition[] transitions = from.transitions;
        for (int i = 0; i < transitions.Length; i++)
        {
            if (transitions[i].destinationState == to)
                return;
        }

        AnimatorStateTransition transition = from.AddTransition(to);
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.AddCondition(AnimatorConditionMode.Equals, value, parameter);
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
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name)
                return;
        }

        controller.AddParameter(name, AnimatorControllerParameterType.Int);
    }

    private static void WritePoseClip(string path, Dictionary<string, float> muscles, string clipName)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        clip.name = clipName;
        clip.ClearCurves();
        foreach (KeyValuePair<string, float> muscle in muscles)
        {
            if (!HasMuscle(muscle.Key))
                continue;

            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Animator), muscle.Key);
            AnimationCurve curve = AnimationCurve.Constant(0f, 1f, muscle.Value);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static bool HasMuscle(string muscleName)
    {
        string[] names = HumanTrait.MuscleName;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == muscleName)
                return true;
        }

        return false;
    }

    private static Dictionary<string, float> LongGunReadyMuscles()
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
