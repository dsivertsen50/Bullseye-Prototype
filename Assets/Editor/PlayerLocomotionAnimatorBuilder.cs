using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Imports Mixamo locomotion clips and rebuilds AC_PlayerThirdPerson
/// around stance state machines and directional blend trees.
/// </summary>
public static class PlayerLocomotionAnimatorBuilder
{
    public const string ControllerPath = "Assets/Player/AC_PlayerThirdPerson.controller";
    public const string TPosePath = "Assets/Player/Player Character - Rigged - T-Pose.fbx";
    public const string AnimationFolder = "Assets/Player/Animations";

    private const float LocomotionBlend = 0.2f;
    private const float SprintBlend = 0.18f;
    private const float StanceBlend = 0.16f;
    private const float ProneExitBlend = 0.28f;
    private const float TransitionExit = 0.88f;
    private const float WalkingForwardPlayback = 1.2f;
    private const float WalkingBackwardPlayback = 1.3f;
    private const float WalkingStrafePlayback = 1.2f;
    private const float SprintBackwardFallbackPlayback = 1.8f;
    private const float ProneCrawlPlayback = 1.5f;
    private const string Req040AppliedPref = "Bullseye.REQ040.StrafeIntegrationApplied";
    private const string IdleJumpTakeoffPath = "Assets/Player/Animations/IdleJumpTakeoff.anim";
    private const string SprintJumpTakeoffPath = "Assets/Player/Animations/SprintJumpTakeoff.anim";

    public static void BuildBatch()
    {
        string result = Build();
        Debug.Log(result);
        if (result.StartsWith("FAILED"))
            EditorApplication.Exit(1);
    }

    [InitializeOnLoadMethod]
    private static void AutoApplyReq040IfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (EditorPrefs.GetBool(Req040AppliedPref, false))
                return;
            if (!StrafeClipFilesExist())
                return;

            if (ControllerHasSprintLocomotionTree())
            {
                Avatar sourceAvatar = LoadAvatar(TPosePath);
                if (sourceAvatar != null)
                    ConfigureClipImports(sourceAvatar, StrafeClipFileNames);
                EditorPrefs.SetBool(Req040AppliedPref, true);
                Debug.Log("REQ-040: strafe clips already wired; configured Humanoid loop/root import settings.");
                return;
            }

            string result = Build();
            Debug.Log("REQ-040 strafe integration: " + result);
            if (!result.StartsWith("FAILED"))
                EditorPrefs.SetBool(Req040AppliedPref, true);
        };
    }

    public static string Build()
    {
        Avatar sourceAvatar = LoadAvatar(TPosePath);
        if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
            return "FAILED: T-Pose Humanoid Avatar missing or invalid.";

        string importLog = ConfigureClipImports(sourceAvatar);
        Dictionary<string, AnimationClip> clips = LoadConfiguredClips();
        string missing = ValidateClips(clips);
        if (!string.IsNullOrEmpty(missing))
            return "FAILED: missing clips: " + missing + "\n" + importLog;

        clips["IdleToJumpTakeoff"] = CreateTakeoffClip(clips["IdleToJump"], IdleJumpTakeoffPath, 0.42f);
        clips["SprintToJumpTakeoff"] = CreateTakeoffClip(clips["SprintToJump"], SprintJumpTakeoffPath, 0.5f);

        AnimatorController controller = LoadOrCreateController();
        ResetController(controller);
        AddParameters(controller);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        LocomotionGraph graph = BuildGraph(root, clips);
        WireTransitions(root, graph);

        ThirdPersonWeaponSetup.EnsureWeaponPoseLayer(controller);
        ThirdPersonWeaponRigSetup.ConfigureWeaponPoseLayer();
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return
            "Locomotion animator rebuilt.\n" +
            importLog + "\n" +
            "Controller=" + ControllerPath + "\n" +
            "States: Standing 2D walk/strafe, Sprint 2D loco, Crouch, Prone, Idle/Sprint jump.\n" +
            "Walk forward=" + WalkingForwardPlayback.ToString("0.00") +
            "x back=" + WalkingBackwardPlayback.ToString("0.00") +
            "x strafe=" + WalkingStrafePlayback.ToString("0.00") +
            "x prone crawl=" + ProneCrawlPlayback.ToString("0.00") + "x";
    }

    private static readonly string[] StrafeClipFileNames =
    {
        "Walking Left Strafe.fbx",
        "Walking Right Strafe.fbx",
        "Sprinting Left Strafe.fbx",
        "Sprinting Right Strafe.fbx"
    };

    private static string ConfigureClipImports(Avatar sourceAvatar, string[] onlyFileNames = null)
    {
        int configured = 0;
        foreach (ClipImport clip in ClipImports)
        {
            if (onlyFileNames != null && !onlyFileNames.Contains(clip.fileName))
                continue;

            string path = AnimationFolder + "/" + clip.fileName;
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = sourceAvatar;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0)
                continue;

            ModelImporterClipAnimation imported = defaults[0];
            imported.name = clip.clipName;
            imported.loopTime = clip.loop;
            imported.loopPose = clip.loop;
            imported.lockRootRotation = true;
            imported.lockRootHeightY = true;
            imported.lockRootPositionXZ = true;
            imported.keepOriginalOrientation = true;
            imported.keepOriginalPositionY = true;
            imported.keepOriginalPositionXZ = true;
            imported.heightFromFeet = false;
            importer.clipAnimations = new[] { imported };
            importer.SaveAndReimport();
            configured++;
        }

        return "Configured " + configured + " Mixamo clip imports (Humanoid, baked root, shared T-Pose Avatar).";
    }

    private static Dictionary<string, AnimationClip> LoadConfiguredClips()
    {
        var clips = new Dictionary<string, AnimationClip>();
        foreach (ClipImport clip in ClipImports)
        {
            string path = AnimationFolder + "/" + clip.fileName;
            AnimationClip loaded = AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (loaded != null)
                clips[clip.clipName] = loaded;
        }

        return clips;
    }

    private static string ValidateClips(Dictionary<string, AnimationClip> clips)
    {
        var missing = new List<string>();
        foreach (ClipImport clip in ClipImports)
        {
            if (!clips.ContainsKey(clip.clipName))
                missing.Add(clip.clipName);
        }

        return string.Join(", ", missing);
    }

    private static AnimatorController LoadOrCreateController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            return controller;

        return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
    }

    private static void ResetController(AnimatorController controller)
    {
        while (controller.layers.Length > 1)
            controller.RemoveLayer(1);

        foreach (AnimatorControllerParameter parameter in controller.parameters.ToArray())
            controller.RemoveParameter(parameter);

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in root.states.ToArray())
            root.RemoveState(child.state);
        foreach (ChildAnimatorStateMachine child in root.stateMachines.ToArray())
            root.RemoveStateMachine(child.stateMachine);
        foreach (AnimatorStateTransition transition in root.anyStateTransitions.ToArray())
            root.RemoveAnyStateTransition(transition);
        foreach (AnimatorTransition transition in root.entryTransitions.ToArray())
            root.RemoveEntryTransition(transition);

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree)
                Object.DestroyImmediate(asset, true);
        }
    }

    private static void AddParameters(AnimatorController controller)
    {
        AddFloat(controller, "MoveX");
        AddFloat(controller, "MoveY");
        AddFloat(controller, "MoveSpeed");
        AddFloat(controller, "LocomotionPlaySpeed", 1f);
        AddFloat(controller, "Speed");
        AddFloat(controller, "ForwardSpeed");
        AddFloat(controller, "StrafeSpeed");
        AddFloat(controller, "VerticalVelocity");
        AddFloat(controller, "AimPitch");
        AddFloat(controller, "ProneMoveSpeed");
        AddBool(controller, "IsMoving");
        AddBool(controller, "IsGrounded", true);
        AddBool(controller, "IsCrouching");
        AddBool(controller, "IsSprinting");
        AddBool(controller, "IsProne");
        AddBool(controller, "IsDolphinDiving");
        AddBool(controller, "IsAiming");
        AddBool(controller, "IsReloading");
        AddBool(controller, "IsFiring");
        AddBool(controller, "IsThrowingGrenade");
        AddBool(controller, "IsDead");
        AddInt(controller, "CurrentWeapon");
        controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("DolphinDive", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("DiveTrigger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("EnterCrouch", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ExitCrouch", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("EnterProne", AnimatorControllerParameterType.Trigger);
        AddFloat(controller, "TurnSpeed");
        AddBool(controller, "IsTurningLeft");
        AddBool(controller, "IsTurningRight");
        AddBool(controller, "IsAirborne");
        AddBool(controller, "JumpFromSprint");
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
    }

    private static LocomotionGraph BuildGraph(AnimatorStateMachine root, Dictionary<string, AnimationClip> clips)
    {
        var graph = new LocomotionGraph();

        AnimatorStateMachine standing = root.AddStateMachine("Standing", new Vector3(320f, 80f, 0f));
        graph.StandingIdle = AddState(standing, "Standing Idle", clips["StandingIdle"], new Vector3(200f, 80f, 0f));
        graph.StandingLocomotion = AddState(
            standing,
            "Standing Locomotion",
            CreateStandingLocomotionTree(clips),
            new Vector3(480f, 80f, 0f),
            true);
        graph.Sprint = AddState(
            standing,
            "Sprint Locomotion",
            CreateSprintLocomotionTree(clips),
            new Vector3(480f, -40f, 0f),
            true);
        AddState(standing, "Sprinting Backward (pending)", clips["WalkingBackward"], new Vector3(720f, -40f, 0f));
        standing.defaultState = graph.StandingIdle;

        graph.StandingToCrouching = AddState(root, "Standing to Crouching", clips["StandingToCrouching"], new Vector3(320f, 230f, 0f));
        graph.CrouchingToStanding = AddState(root, "Crouching to Standing", clips["CrouchingToStanding"], new Vector3(560f, 230f, 0f));

        AnimatorStateMachine crouching = root.AddStateMachine("Crouching", new Vector3(320f, 380f, 0f));
        graph.CrouchingIdle = AddState(crouching, "Crouching Idle", clips["CrouchingIdle"], new Vector3(200f, 80f, 0f));
        graph.CrouchingLocomotion = AddState(
            crouching,
            "Crouching Locomotion",
            CreateCrouchLocomotionTree(clips),
            new Vector3(480f, 80f, 0f),
            true);
        crouching.defaultState = graph.CrouchingIdle;

        graph.CrouchingToProne = AddState(root, "Crouching to Prone", clips["CrouchToProne"], new Vector3(320f, 530f, 0f));
        graph.ProneToCrouching = AddState(root, "Prone to Crouching", clips["ProneToCrouching"], new Vector3(80f, 680f, 0f));

        AnimatorStateMachine prone = root.AddStateMachine("Prone", new Vector3(320f, 680f, 0f));
        graph.ProneIdle = AddState(prone, "Prone Idle", clips["ProneIdle"], new Vector3(200f, 80f, 0f));
        graph.ProneForward = AddState(prone, "Prone Forward", clips["ProneForward"], new Vector3(480f, 0f, 0f), true);
        graph.ProneForward.speed = ProneCrawlPlayback;
        graph.ProneBackward = AddState(prone, "Prone Backward", clips["ProneBackward"], new Vector3(480f, 160f, 0f), true);
        graph.ProneBackward.speed = ProneCrawlPlayback;
        graph.ProneLeftTurn = AddState(prone, "Prone Left Turn", clips["ProneLeftTurn"], new Vector3(40f, 0f, 0f));
        graph.ProneRightTurn = AddState(prone, "Prone Right Turn", clips["ProneRightTurn"], new Vector3(40f, 160f, 0f));
        graph.ProneLocomotion = AddState(
            prone,
            "Prone Locomotion",
            CreateProneLocomotionTree(clips),
            new Vector3(700f, 80f, 0f));
        AddState(prone, "Prone Crawl Left (pending)", clips["ProneIdle"], new Vector3(700f, 160f, 0f));
        AddState(prone, "Prone Crawl Right (pending)", clips["ProneIdle"], new Vector3(700f, 220f, 0f));
        AddState(prone, "Prone to Standing (pending)", clips["ProneToCrouching"], new Vector3(200f, 220f, 0f));
        prone.defaultState = graph.ProneIdle;

        graph.DolphinDive = AddState(root, "Dolphin Dive (pending)", clips["ProneIdle"], new Vector3(40f, 530f, 0f));

        graph.IdleToJump = AddState(root, "Idle to Jump", clips["IdleToJumpTakeoff"], new Vector3(760f, 40f, 0f));
        graph.SprintToJump = AddState(root, "Sprint to Jump", clips["SprintToJumpTakeoff"], new Vector3(760f, 140f, 0f));
        graph.Airborne = AddState(root, "Airborne (pending)", clips["StandingIdle"], new Vector3(980f, 90f, 0f));
        AddState(root, "Falling (pending)", clips["StandingIdle"], new Vector3(980f, 170f, 0f));
        AddState(root, "Landing (pending)", clips["StandingIdle"], new Vector3(980f, 250f, 0f));

        root.defaultState = graph.StandingIdle;
        graph.Standing = standing;
        graph.Crouching = crouching;
        graph.Prone = prone;
        return graph;
    }

    private static void WireTransitions(AnimatorStateMachine root, LocomotionGraph graph)
    {
        AddBoolTransition(graph.StandingIdle, graph.StandingLocomotion, LocomotionBlend, false, "IsMoving", true);
        AddBoolTransition(graph.StandingLocomotion, graph.StandingIdle, 0.15f, false, "IsMoving", false);
        AnimatorStateTransition idleToSprint = AddBoolTransition(graph.StandingIdle, graph.Sprint, SprintBlend, false, "IsSprinting", true);
        idleToSprint.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        AnimatorStateTransition locoToSprint = AddBoolTransition(graph.StandingLocomotion, graph.Sprint, SprintBlend, false, "IsSprinting", true);
        locoToSprint.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        AnimatorStateTransition sprintToLoco = AddBoolTransition(graph.Sprint, graph.StandingLocomotion, SprintBlend, false, "IsSprinting", false);
        sprintToLoco.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        AddBoolTransition(graph.Sprint, graph.StandingIdle, 0.12f, false, "IsMoving", false);

        AddBoolTransition(graph.CrouchingIdle, graph.CrouchingLocomotion, 0.18f, false, "IsMoving", true);
        AddBoolTransition(graph.CrouchingLocomotion, graph.CrouchingIdle, 0.2f, false, "IsMoving", false);

        With(AddFloatTransition(graph.ProneIdle, graph.ProneForward, 0.2f, "MoveY", 0.25f, true), AnimatorConditionMode.If, 0, "IsMoving");
        With(AddFloatTransition(graph.ProneIdle, graph.ProneBackward, 0.2f, "MoveY", -0.25f, false), AnimatorConditionMode.If, 0, "IsMoving");
        With(AddBoolTransition(graph.ProneIdle, graph.ProneLeftTurn, 0.2f, false, "IsTurningLeft", true), AnimatorConditionMode.IfNot, 0, "IsMoving");
        With(AddBoolTransition(graph.ProneIdle, graph.ProneRightTurn, 0.2f, false, "IsTurningRight", true), AnimatorConditionMode.IfNot, 0, "IsMoving");

        AddFloatTransition(graph.ProneForward, graph.ProneIdle, 0.2f, "MoveY", 0.1f, false);
        AddBoolTransition(graph.ProneForward, graph.ProneIdle, 0.2f, false, "IsMoving", false);
        With(AddFloatTransition(graph.ProneForward, graph.ProneBackward, 0.2f, "MoveY", -0.25f, false), AnimatorConditionMode.If, 0, "IsMoving");

        AddFloatTransition(graph.ProneBackward, graph.ProneIdle, 0.2f, "MoveY", -0.1f, true);
        AddBoolTransition(graph.ProneBackward, graph.ProneIdle, 0.2f, false, "IsMoving", false);
        With(AddFloatTransition(graph.ProneBackward, graph.ProneForward, 0.2f, "MoveY", 0.25f, true), AnimatorConditionMode.If, 0, "IsMoving");

        AddBoolTransition(graph.ProneLeftTurn, graph.ProneIdle, 0.2f, false, "IsTurningLeft", false);
        AddBoolTransition(graph.ProneRightTurn, graph.ProneIdle, 0.2f, false, "IsTurningRight", false);
        With(AddBoolTransition(graph.ProneLeftTurn, graph.ProneForward, 0.1f, false, "IsMoving", true), AnimatorConditionMode.Greater, 0.25f, "MoveY");
        With(AddBoolTransition(graph.ProneRightTurn, graph.ProneForward, 0.1f, false, "IsMoving", true), AnimatorConditionMode.Greater, 0.25f, "MoveY");
        With(AddBoolTransition(graph.ProneLeftTurn, graph.ProneBackward, 0.1f, false, "IsMoving", true), AnimatorConditionMode.Less, -0.25f, "MoveY");
        With(AddBoolTransition(graph.ProneRightTurn, graph.ProneBackward, 0.1f, false, "IsMoving", true), AnimatorConditionMode.Less, -0.25f, "MoveY");

        // Child-state-machine transitions do not evaluate at runtime in this
        // Unity version. Stance changes must be wired from the actual states.
        AnimatorState[] standingStates = { graph.StandingIdle, graph.StandingLocomotion, graph.Sprint };
        AddDirectBools(standingStates, graph.StandingToCrouching, StanceBlend,
            ("IsCrouching", true), ("IsProne", false), ("IsDolphinDiving", false));
        AddDirectBools(standingStates, graph.CrouchingToProne, StanceBlend,
            ("IsProne", true), ("IsDolphinDiving", false));

        AnimatorState[] crouchingStates = { graph.CrouchingIdle, graph.CrouchingLocomotion };
        AddDirectBools(crouchingStates, graph.CrouchingToStanding, StanceBlend,
            ("IsCrouching", false), ("IsProne", false), ("IsDolphinDiving", false));
        AddDirectBools(crouchingStates, graph.CrouchingToProne, 0.06f,
            ("IsProne", true));

        AnimatorState[] proneStates =
        {
            graph.ProneIdle, graph.ProneForward, graph.ProneBackward,
            graph.ProneLeftTurn, graph.ProneRightTurn, graph.ProneLocomotion
        };
        AddDirectBools(proneStates, graph.ProneToCrouching, 0.08f,
            ("IsProne", false), ("IsCrouching", true));
        AddDirectBools(proneStates, graph.StandingIdle, ProneExitBlend,
            ("IsProne", false), ("IsCrouching", false), ("IsDolphinDiving", false));

        AddBoolTransition(graph.StandingToCrouching, graph.CrouchingIdle, StanceBlend, true, "IsMoving", false, TransitionExit);
        AddBoolTransition(graph.StandingToCrouching, graph.CrouchingLocomotion, 0.1f, false, "IsMoving", true);
        AddBoolTransition(graph.StandingToCrouching, graph.CrouchingToProne, StanceBlend, false, "IsProne", true);
        AnimatorStateTransition cancelCrouchEnter = AddBoolTransition(graph.StandingToCrouching, graph.CrouchingToStanding, StanceBlend, false, "IsCrouching", false);
        cancelCrouchEnter.AddCondition(AnimatorConditionMode.IfNot, 0, "IsProne");

        AnimatorStateTransition standUpIdle = AddBoolTransition(graph.CrouchingToStanding, graph.StandingIdle, StanceBlend, true, "IsMoving", false, TransitionExit);
        standUpIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
        AnimatorStateTransition standUpWalk = AddBoolTransition(graph.CrouchingToStanding, graph.StandingLocomotion, 0.1f, false, "IsMoving", true);
        standUpWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
        AnimatorStateTransition standUpSprint = AddBoolTransition(graph.CrouchingToStanding, graph.Sprint, SprintBlend, false, "IsSprinting", true);
        standUpSprint.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        AddBoolTransition(graph.CrouchingToStanding, graph.StandingToCrouching, StanceBlend, false, "IsCrouching", true);
        AddBoolTransition(graph.CrouchingToStanding, graph.CrouchingToProne, StanceBlend, false, "IsProne", true);

        AddTransition(graph.CrouchingToProne, graph.ProneIdle, 0.1f, true, TransitionExit);
        AnimatorStateTransition cancelProneToCrouch = AddBoolTransition(graph.CrouchingToProne, graph.CrouchingIdle, 0.12f, false, "IsProne", false);
        cancelProneToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        AnimatorStateTransition cancelProneToStand = AddBoolTransition(graph.CrouchingToProne, graph.StandingIdle, 0.12f, false, "IsProne", false);
        cancelProneToStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

        AddBoolTransition(graph.ProneToCrouching, graph.CrouchingIdle, StanceBlend, true, "IsMoving", false, TransitionExit);
        AddBoolTransition(graph.ProneToCrouching, graph.CrouchingLocomotion, 0.1f, false, "IsMoving", true);
        AddBoolTransition(graph.ProneToCrouching, graph.CrouchingToProne, StanceBlend, false, "IsProne", true);
        With(AddBoolTransition(graph.ProneToCrouching, graph.StandingIdle, 0.12f, false, "IsCrouching", false), AnimatorConditionMode.IfNot, 0, "IsProne");

        AnimatorStateTransition anyDive = root.AddAnyStateTransition(graph.DolphinDive);
        Configure(anyDive, 0.12f, false);
        anyDive.canTransitionToSelf = false;
        anyDive.AddCondition(AnimatorConditionMode.If, 0, "IsDolphinDiving");

        AnimatorStateTransition diveToProne = AddBoolTransition(graph.DolphinDive, graph.ProneIdle, 0.15f, false, "IsDolphinDiving", false);
        diveToProne.AddCondition(AnimatorConditionMode.If, 0, "IsProne");
        AnimatorStateTransition diveToCrouch = AddBoolTransition(graph.DolphinDive, graph.CrouchingIdle, 0.15f, false, "IsDolphinDiving", false);
        diveToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching");
        AnimatorStateTransition diveToStand = AddBoolTransition(graph.DolphinDive, graph.StandingIdle, 0.15f, false, "IsDolphinDiving", false);
        diveToStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsProne");
        diveToStand.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching");

        AnimatorStateTransition anySprintJump = root.AddAnyStateTransition(graph.SprintToJump);
        Configure(anySprintJump, 0.14f, false);
        anySprintJump.canTransitionToSelf = false;
        anySprintJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        anySprintJump.AddCondition(AnimatorConditionMode.If, 0, "JumpFromSprint");
        anySprintJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsProne");
        anySprintJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDolphinDiving");

        AnimatorStateTransition anySprintJumpFromSprint = root.AddAnyStateTransition(graph.SprintToJump);
        Configure(anySprintJumpFromSprint, 0.14f, false);
        anySprintJumpFromSprint.canTransitionToSelf = false;
        anySprintJumpFromSprint.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        anySprintJumpFromSprint.AddCondition(AnimatorConditionMode.If, 0, "IsSprinting");
        anySprintJumpFromSprint.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        anySprintJumpFromSprint.AddCondition(AnimatorConditionMode.IfNot, 0, "IsProne");
        anySprintJumpFromSprint.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDolphinDiving");

        AnimatorStateTransition anyIdleJump = root.AddAnyStateTransition(graph.IdleToJump);
        Configure(anyIdleJump, 0.14f, false);
        anyIdleJump.canTransitionToSelf = false;
        anyIdleJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        anyIdleJump.AddCondition(AnimatorConditionMode.IfNot, 0, "JumpFromSprint");
        anyIdleJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSprinting");
        anyIdleJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsProne");
        anyIdleJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsDolphinDiving");

        AnimatorStateTransition sprintStateJump = AddTransition(graph.Sprint, graph.SprintToJump, 0.14f, false);
        sprintStateJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");

        WireJumpLanding(graph.IdleToJump, graph, 0.35f);
        WireJumpLanding(graph.SprintToJump, graph, 0.35f);
    }

    private static AnimationClip CreateTakeoffClip(AnimationClip source, string path, float endNormalized)
    {
        AnimationClip takeoff = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (takeoff == null)
        {
            takeoff = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(path), frameRate = 30f };
            AssetDatabase.CreateAsset(takeoff, path);
        }

        takeoff.ClearCurves();
        takeoff.frameRate = source.frameRate > 1f ? source.frameRate : 30f;
        float endTime = Mathf.Clamp01(endNormalized) * source.length;
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
        for (int i = 0; i < bindings.Length; i++)
        {
            AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(source, bindings[i]);
            if (sourceCurve == null)
                continue;

            var keys = new List<Keyframe>();
            for (int k = 0; k < sourceCurve.length; k++)
            {
                if (sourceCurve.keys[k].time > endTime + 0.0001f)
                    break;
                keys.Add(sourceCurve.keys[k]);
            }

            if (keys.Count == 0 || keys[keys.Count - 1].time < endTime - 0.0001f)
                keys.Add(new Keyframe(endTime, sourceCurve.Evaluate(endTime), 0f, 0f));

            AnimationUtility.SetEditorCurve(takeoff, bindings[i], new AnimationCurve(keys.ToArray()));
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(takeoff);
        settings.loopTime = false;
        settings.loopBlend = false;
        settings.startTime = 0f;
        settings.stopTime = endTime;
        AnimationUtility.SetAnimationClipSettings(takeoff, settings);
        EditorUtility.SetDirty(takeoff);
        return takeoff;
    }

    private static void WireJumpLanding(AnimatorState from, LocomotionGraph graph, float exitTime = 0f)
    {
        bool waitForTakeoff = exitTime > 0.01f;
        float usedExit = waitForTakeoff ? exitTime : 0.9f;

        AnimatorStateTransition toIdle = AddBoolTransition(from, graph.StandingIdle, 0.1f, waitForTakeoff, "IsGrounded", true, usedExit);
        With(toIdle, AnimatorConditionMode.IfNot, 0, "IsMoving");
        With(toIdle, AnimatorConditionMode.IfNot, 0, "IsCrouching");
        With(toIdle, AnimatorConditionMode.IfNot, 0, "IsProne");

        AnimatorStateTransition toWalk = AddBoolTransition(from, graph.StandingLocomotion, 0.1f, waitForTakeoff, "IsGrounded", true, usedExit);
        With(toWalk, AnimatorConditionMode.If, 0, "IsMoving");
        With(toWalk, AnimatorConditionMode.IfNot, 0, "IsSprinting");
        With(toWalk, AnimatorConditionMode.IfNot, 0, "IsCrouching");
        With(toWalk, AnimatorConditionMode.IfNot, 0, "IsProne");

        AnimatorStateTransition toSprint = AddBoolTransition(from, graph.Sprint, 0.1f, waitForTakeoff, "IsGrounded", true, usedExit);
        With(toSprint, AnimatorConditionMode.If, 0, "IsSprinting");
        With(toSprint, AnimatorConditionMode.If, 0, "IsMoving");
        With(toSprint, AnimatorConditionMode.IfNot, 0, "IsCrouching");
        With(toSprint, AnimatorConditionMode.IfNot, 0, "IsProne");

        AnimatorStateTransition toCrouchIdle = AddBoolTransition(from, graph.CrouchingIdle, 0.1f, waitForTakeoff, "IsGrounded", true, usedExit);
        With(toCrouchIdle, AnimatorConditionMode.If, 0, "IsCrouching");
        With(toCrouchIdle, AnimatorConditionMode.IfNot, 0, "IsMoving");

        AnimatorStateTransition toCrouchWalk = AddBoolTransition(from, graph.CrouchingLocomotion, 0.1f, waitForTakeoff, "IsGrounded", true, usedExit);
        With(toCrouchWalk, AnimatorConditionMode.If, 0, "IsCrouching");
        With(toCrouchWalk, AnimatorConditionMode.If, 0, "IsMoving");
    }

    private static BlendTree CreateStandingLocomotionTree(Dictionary<string, AnimationClip> clips)
    {
        BlendTree tree = CreateBlendTree("StandingLocomotion");
        tree.AddChild(clips["StandingIdle"], new Vector2(0f, 0f));
        tree.AddChild(clips["WalkingForward"], new Vector2(0f, 1f));
        tree.AddChild(clips["WalkingBackward"], new Vector2(0f, -1f));
        tree.AddChild(clips["WalkingLeftStrafe"], new Vector2(-1f, 0f));
        tree.AddChild(clips["WalkingRightStrafe"], new Vector2(1f, 0f));
        ChildMotion[] children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (Mathf.Abs(children[i].position.x) > 0.5f)
                children[i].timeScale = WalkingStrafePlayback;
            else if (children[i].position.y > 0.5f)
                children[i].timeScale = WalkingForwardPlayback;
            else if (children[i].position.y < -0.5f)
                children[i].timeScale = WalkingBackwardPlayback;
        }

        tree.children = children;
        return tree;
    }

    private static BlendTree CreateSprintLocomotionTree(Dictionary<string, AnimationClip> clips)
    {
        BlendTree tree = CreateBlendTree("SprintLocomotion");
        tree.AddChild(clips["SprintForward"], new Vector2(0f, 1f));
        tree.AddChild(clips["SprintingLeftStrafe"], new Vector2(-1f, 0.2f));
        tree.AddChild(clips["SprintingRightStrafe"], new Vector2(1f, 0.2f));
        tree.AddChild(clips["SprintingLeftStrafe"], new Vector2(-1f, 0f));
        tree.AddChild(clips["SprintingRightStrafe"], new Vector2(1f, 0f));
        tree.AddChild(clips["WalkingBackward"], new Vector2(0f, -1f));
        ChildMotion[] children = tree.children;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].position.y < -0.5f)
                children[i].timeScale = SprintBackwardFallbackPlayback;
        }

        tree.children = children;
        return tree;
    }

    private static BlendTree CreateCrouchLocomotionTree(Dictionary<string, AnimationClip> clips)
    {
        BlendTree tree = CreateBlendTree("CrouchingLocomotion");
        tree.AddChild(clips["CrouchingIdle"], new Vector2(0f, 0f));
        tree.AddChild(clips["CrouchingWalkForward"], new Vector2(0f, 1f));
        tree.AddChild(clips["CrouchingWalkBackward"], new Vector2(0f, -1f));
        tree.AddChild(clips["CrouchingWalkLeft"], new Vector2(-1f, 0f));
        tree.AddChild(clips["CrouchingWalkRight"], new Vector2(1f, 0f));
        return tree;
    }

    private static BlendTree CreateProneLocomotionTree(Dictionary<string, AnimationClip> clips)
    {
        BlendTree tree = CreateBlendTree("ProneLocomotion_ReplaceCrawlClips");
        tree.AddChild(clips["ProneIdle"], new Vector2(0f, 0f));
        tree.AddChild(clips["ProneForward"], new Vector2(0f, 1f));
        tree.AddChild(clips["ProneBackward"], new Vector2(0f, -1f));
        tree.AddChild(clips["ProneIdle"], new Vector2(-1f, 0f));
        tree.AddChild(clips["ProneIdle"], new Vector2(1f, 0f));
        return tree;
    }

    private static BlendTree CreateBlendTree(string name)
    {
        BlendTree tree = new BlendTree
        {
            name = name,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            useAutomaticThresholds = false
        };
        AssetDatabase.AddObjectToAsset(tree, ControllerPath);
        return tree;
    }

    private static AnimatorState AddState(
        AnimatorStateMachine machine,
        string name,
        Motion motion,
        Vector3 position,
        bool speedFromParameter = false)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        state.speed = 1f;
        if (speedFromParameter)
        {
            state.speedParameterActive = true;
            state.speedParameter = "LocomotionPlaySpeed";
        }

        return state;
    }

    private static AnimatorStateTransition AddBoolTransition(
        AnimatorState from,
        AnimatorState to,
        float duration,
        bool hasExitTime,
        string parameter,
        bool value,
        float exitTime = 0.9f)
    {
        AnimatorStateTransition transition = AddTransition(from, to, duration, hasExitTime, exitTime);
        transition.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, parameter);
        return transition;
    }

    private static AnimatorStateTransition With(
        AnimatorStateTransition transition,
        AnimatorConditionMode mode,
        float threshold,
        string parameter)
    {
        transition.AddCondition(mode, threshold, parameter);
        return transition;
    }

    private static AnimatorStateTransition AddFloatTransition(
        AnimatorState from,
        AnimatorState to,
        float duration,
        string parameter,
        float threshold,
        bool greater)
    {
        AnimatorStateTransition transition = AddTransition(from, to, duration, false);
        transition.AddCondition(
            greater ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less,
            threshold,
            parameter);
        return transition;
    }

    private static AnimatorStateTransition AddTransition(
        AnimatorState from,
        AnimatorState to,
        float duration,
        bool hasExitTime,
        float exitTime = 0.9f)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        Configure(transition, duration, hasExitTime, exitTime);
        return transition;
    }

    private static void AddDirectBools(
        AnimatorState[] fromStates,
        AnimatorState destination,
        float duration,
        params (string name, bool value)[] conditions)
    {
        for (int i = 0; i < fromStates.Length; i++)
        {
            AnimatorStateTransition transition = AddTransition(fromStates[i], destination, duration, false);
            for (int c = 0; c < conditions.Length; c++)
            {
                transition.AddCondition(
                    conditions[c].value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                    0,
                    conditions[c].name);
            }
        }
    }

    private static void Configure(AnimatorStateTransition transition, float duration, bool hasExitTime, float exitTime = 0.9f)
    {
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.hasFixedDuration = true;
        transition.offset = 0f;
        transition.interruptionSource = TransitionInterruptionSource.Destination;
    }

    private static void AddFloat(AnimatorController controller, string name, float value = 0f)
    {
        controller.AddParameter(name, AnimatorControllerParameterType.Float);
        SetDefaultFloat(controller, name, value);
    }

    private static void AddBool(AnimatorController controller, string name, bool value = false)
    {
        controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != name)
                continue;
            parameters[i].defaultBool = value;
            controller.parameters = parameters;
            return;
        }
    }

    private static void AddInt(AnimatorController controller, string name)
    {
        controller.AddParameter(name, AnimatorControllerParameterType.Int);
    }

    private static void SetDefaultFloat(AnimatorController controller, string name, float value)
    {
        if (Mathf.Abs(value) < 0.0001f)
            return;

        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name != name)
                continue;
            parameters[i].defaultFloat = value;
            controller.parameters = parameters;
            return;
        }
    }

    private static Avatar LoadAvatar(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
    }

    private static bool StrafeClipFilesExist()
    {
        for (int i = 0; i < StrafeClipFileNames.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(AnimationFolder + "/" + StrafeClipFileNames[i]) == null)
                return false;
        }

        return true;
    }

    private static bool ControllerHasSprintLocomotionTree()
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
        {
            if (asset is BlendTree tree && tree.name == "SprintLocomotion")
                return true;
        }

        return false;
    }

    private static readonly ClipImport[] ClipImports =
    {
        new ClipImport("Standing Idle.fbx", "StandingIdle", true),
        new ClipImport("Walking Forward.fbx", "WalkingForward", true),
        new ClipImport("Walking Backward.fbx", "WalkingBackward", true),
        new ClipImport("Walking Left Strafe.fbx", "WalkingLeftStrafe", true),
        new ClipImport("Walking Right Strafe.fbx", "WalkingRightStrafe", true),
        new ClipImport("Sprint Forward.fbx", "SprintForward", true),
        new ClipImport("Sprinting Left Strafe.fbx", "SprintingLeftStrafe", true),
        new ClipImport("Sprinting Right Strafe.fbx", "SprintingRightStrafe", true),
        new ClipImport("Standing to Crouching.fbx", "StandingToCrouching", false),
        new ClipImport("Crouching Idle.fbx", "CrouchingIdle", true),
        new ClipImport("Crouching Walk Forward.fbx", "CrouchingWalkForward", true),
        new ClipImport("Crouching Walk Backward.fbx", "CrouchingWalkBackward", true),
        new ClipImport("Crouching Walk Left.fbx", "CrouchingWalkLeft", true),
        new ClipImport("Crouching Walk Right.fbx", "CrouchingWalkRight", true),
        new ClipImport("Crouching to Standing.fbx", "CrouchingToStanding", false),
        new ClipImport("Crouch to Prone.fbx", "CrouchToProne", false),
        new ClipImport("Prone Idle.fbx", "ProneIdle", true),
        new ClipImport("Prone Forward.fbx", "ProneForward", true),
        new ClipImport("Prone Backward.fbx", "ProneBackward", true),
        new ClipImport("Prone Left Turn.fbx", "ProneLeftTurn", true),
        new ClipImport("Prone Right Turn.fbx", "ProneRightTurn", true),
        new ClipImport("Prone to Crouching.fbx", "ProneToCrouching", false),
        new ClipImport("Idle to Jump.fbx", "IdleToJump", false),
        new ClipImport("Sprint to Jump.fbx", "SprintToJump", false)
    };

    private struct ClipImport
    {
        public readonly string fileName;
        public readonly string clipName;
        public readonly bool loop;

        public ClipImport(string fileName, string clipName, bool loop)
        {
            this.fileName = fileName;
            this.clipName = clipName;
            this.loop = loop;
        }
    }

    private sealed class LocomotionGraph
    {
        public AnimatorStateMachine Standing;
        public AnimatorStateMachine Crouching;
        public AnimatorStateMachine Prone;
        public AnimatorState StandingIdle;
        public AnimatorState StandingLocomotion;
        public AnimatorState Sprint;
        public AnimatorState StandingToCrouching;
        public AnimatorState CrouchingToStanding;
        public AnimatorState CrouchingIdle;
        public AnimatorState CrouchingLocomotion;
        public AnimatorState CrouchingToProne;
        public AnimatorState ProneToCrouching;
        public AnimatorState ProneIdle;
        public AnimatorState ProneForward;
        public AnimatorState ProneBackward;
        public AnimatorState ProneLeftTurn;
        public AnimatorState ProneRightTurn;
        public AnimatorState ProneLocomotion;
        public AnimatorState DolphinDive;
        public AnimatorState IdleToJump;
        public AnimatorState SprintToJump;
        public AnimatorState Airborne;
    }
}
