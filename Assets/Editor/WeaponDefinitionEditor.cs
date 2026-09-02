using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponDefinition))]
public class WeaponDefinitionEditor : Editor
{
    private static bool showAimHold;
    private static bool showWeights;
    private static bool showRecoil;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "worldLocalPosition",
            "worldLocalEuler",
            "worldLocalScale",
            "worldStanceHeightOffset",
            "thirdPersonClass",
            "thirdPersonPose");

        EditorGUILayout.Space(8f);
        DrawSocket();
        DrawThirdPersonPose();

        if (serializedObject.ApplyModifiedProperties())
            SaveDefinition((WeaponDefinition)target);
    }

    private void DrawSocket()
    {
        EditorGUILayout.LabelField("Third-Person Weapon Socket", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "World weapons follow the player's right-hand WeaponSocket at full size. " +
            "Tune these local offsets per weapon instead of editing animation clips.\n\n" +
            "Position is in the hand's local space, in meters. Rotation is degrees. " +
            "If the barrel points the wrong way, change Socket Rotation first.",
            MessageType.Info);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("worldLocalPosition"),
            new GUIContent("Socket Position"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("worldLocalEuler"),
            new GUIContent("Socket Rotation"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("worldLocalScale"),
            new GUIContent("Socket Scale"));
    }

    private void DrawThirdPersonPose()
    {
        SerializedProperty pose = serializedObject.FindProperty("thirdPersonPose");
        if (pose == null)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Third-Person Upper-Body Hold", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The gun follows the right hand. These values pose the right arm and shape left-hand IK.\n\n" +
            "Positions are meters from the upper chest:\n" +
            "X = across the body (positive is right)\n" +
            "Y = up\n" +
            "Z = forward\n\n" +
            "Open Third-Person Pose Preview to orbit a posed player in the Scene view " +
            "without Play Mode. Yellow = gun. Green = right hand. Cyan = left hand / elbows. " +
            "The left hand is independent of the gun unless Follow Weapon Grip is on.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview In Scene"))
                ThirdPersonWeaponPosePreviewWindow.Open((WeaponDefinition)target);
            if (GUILayout.Button("Save Pose To Disk"))
                SaveDefinition((WeaponDefinition)target);
            if (GUILayout.Button("Reset Pose To Defaults"))
                ResetPoseToDefaults();
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("thirdPersonClass"), new GUIContent(
            "Weapon Shape",
            "Pistol, rifle, or shotgun. Used only for defaults if a pose is missing."));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Gun", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("gunEuler"), new GUIContent("Extra Socket Tilt"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("gunScale"), new GUIContent("Scale"));

        DrawArm(pose, "Right Arm", "rightHandPosition", "rightWristEuler", "rightArmReach", "rightElbowYaw");
        DrawLeftArm(pose, (WeaponDefinition)target);
        DrawAimHold(pose);
        DrawAimFollow(pose);
        DrawRecoil(pose);
        DrawWeights(pose);
    }

    private static void DrawArm(
        SerializedProperty pose,
        string title,
        string handField,
        string wristField,
        string reachField,
        string elbowField)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative(handField), new GUIContent("Hand Position"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative(wristField), new GUIContent("Wrist Tilt"));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative(reachField),
            new GUIContent("Elbow Straightness", "1 is almost locked. Lower keeps a bent elbow."));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative(elbowField),
            new GUIContent("Elbow Swing", "Rotates the elbow around the shoulder-to-hand line. 0 is out from the body."));
    }

    private static void DrawLeftArm(SerializedProperty pose, WeaponDefinition definition)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Left Arm", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The left hand is posed on its own. Moving the gun does not move it.\n\n" +
            "Turn on Follow Weapon Grip if you want the old behavior, where the support " +
            "hand sticks to LeftHandGrip. Snap To Grip copies the current grip into the pose, " +
            "then you can keep editing both independently.",
            MessageType.None);
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative("leftHandFollowGrip"),
            new GUIContent("Follow Weapon Grip"));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Snap Left Hand To Grip"))
                SnapLeftHandToGrip(definition, CurrentPreviewStance());
        }

        EditorGUILayout.PropertyField(pose.FindPropertyRelative("leftHandPosition"), new GUIContent("Hand Position"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("leftWristEuler"), new GUIContent("Wrist Tilt"));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative("leftArmReach"),
            new GUIContent("Elbow Straightness"));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative("leftElbowYaw"),
            new GUIContent("Elbow Swing"));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative("sprintLeftIkWeight"),
            new GUIContent("Sprint IK Weight"));
    }

    private static void DrawAimHold(SerializedProperty pose)
    {
        EditorGUILayout.Space(4f);
        showAimHold = EditorGUILayout.Foldout(showAimHold, "Aimed / Sprint / Prone Holds", true);
        if (!showAimHold)
            return;

        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimRightHandPosition"), new GUIContent("Aimed Right Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimRightWristEuler"), new GUIContent("Aimed Right Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimLeftHandPosition"), new GUIContent("Aimed Left Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimLeftWristEuler"), new GUIContent("Aimed Left Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimRaisePitch"), new GUIContent("Aim Raise Pitch"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("sprintRightHandPosition"), new GUIContent("Sprint Right Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("sprintRightWristEuler"), new GUIContent("Sprint Right Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("sprintLeftHandPosition"), new GUIContent("Sprint Left Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("sprintLeftWristEuler"), new GUIContent("Sprint Left Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("proneRightHandPosition"), new GUIContent("Prone Right Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("proneRightWristEuler"), new GUIContent("Prone Right Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("proneLeftHandPosition"), new GUIContent("Prone Left Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("proneLeftWristEuler"), new GUIContent("Prone Left Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("proneBodyPitch"), new GUIContent("Prone Body Pitch"));
    }

    private static void DrawAimFollow(SerializedProperty pose)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Aim Follow", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("maxAimPitch"), new GUIContent("Max Look Pitch Up"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("maxAimPitchDown"), new GUIContent("Max Look Pitch Down"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("spineAimWeight"), new GUIContent("Spine Follow"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("upperChestAimShare"), new GUIContent("Upper Chest Share"));
    }

    private static void DrawRecoil(SerializedProperty pose)
    {
        showRecoil = EditorGUILayout.Foldout(showRecoil, "Upper-Body Recoil", true);
        if (!showRecoil)
            return;

        EditorGUILayout.PropertyField(pose.FindPropertyRelative("recoilPitch"), new GUIContent("Recoil Pitch"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("recoilRightRoll"), new GUIContent("Recoil Right Roll"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("recoilYaw"), new GUIContent("Recoil Yaw"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("recoilInTime"), new GUIContent("Recoil In Time"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("recoilOutTime"), new GUIContent("Recoil Recovery"));
    }

    private static void DrawWeights(SerializedProperty pose)
    {
        showWeights = EditorGUILayout.Foldout(showWeights, "Stance Pose Weights", true);
        if (!showWeights)
            return;

        EditorGUILayout.HelpBox(
            "How strongly the upper-body hold overrides Mixamo locomotion. 1 is full hold. Sprint / dive should stay lower.",
            MessageType.None);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("defaultWeight"), new GUIContent("Stand / Walk"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("sprintWeight"), new GUIContent("Sprint"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("crouchWeight"), new GUIContent("Crouch"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("proneWeight"), new GUIContent("Prone"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("diveWeight"), new GUIContent("Dolphin Dive"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("jumpWeight"), new GUIContent("Jump"));
    }

    private void ResetPoseToDefaults()
    {
        WeaponDefinition definition = (WeaponDefinition)target;
        ThirdPersonWeaponClass weaponClass = (ThirdPersonWeaponClass)serializedObject.FindProperty("thirdPersonClass").enumValueIndex;
        ThirdPersonWeaponPose defaults = ThirdPersonWeaponPose.CreateDefault(weaponClass);
        SerializedProperty pose = serializedObject.FindProperty("thirdPersonPose");
        WriteVector(pose, "gunEuler", defaults.gunEuler);
        WriteVector(pose, "gunScale", defaults.gunScale);
        WriteVector(pose, "rightHandPosition", defaults.rightHandPosition);
        WriteVector(pose, "rightWristEuler", defaults.rightWristEuler);
        WriteVector(pose, "aimRightHandPosition", defaults.aimRightHandPosition);
        WriteVector(pose, "aimRightWristEuler", defaults.aimRightWristEuler);
        WriteVector(pose, "sprintRightHandPosition", defaults.sprintRightHandPosition);
        WriteVector(pose, "sprintRightWristEuler", defaults.sprintRightWristEuler);
        WriteVector(pose, "proneRightHandPosition", defaults.proneRightHandPosition);
        WriteVector(pose, "proneRightWristEuler", defaults.proneRightWristEuler);
        WriteVector(pose, "leftHandPosition", defaults.leftHandPosition);
        WriteVector(pose, "leftWristEuler", defaults.leftWristEuler);
        WriteVector(pose, "aimLeftHandPosition", defaults.aimLeftHandPosition);
        WriteVector(pose, "aimLeftWristEuler", defaults.aimLeftWristEuler);
        WriteVector(pose, "sprintLeftHandPosition", defaults.sprintLeftHandPosition);
        WriteVector(pose, "sprintLeftWristEuler", defaults.sprintLeftWristEuler);
        WriteVector(pose, "proneLeftHandPosition", defaults.proneLeftHandPosition);
        WriteVector(pose, "proneLeftWristEuler", defaults.proneLeftWristEuler);
        pose.FindPropertyRelative("leftHandFollowGrip").boolValue = false;
        pose.FindPropertyRelative("rightArmReach").floatValue = defaults.rightArmReach;
        pose.FindPropertyRelative("leftArmReach").floatValue = defaults.leftArmReach;
        pose.FindPropertyRelative("rightElbowYaw").floatValue = defaults.rightElbowYaw;
        pose.FindPropertyRelative("leftElbowYaw").floatValue = defaults.leftElbowYaw;
        pose.FindPropertyRelative("sprintLeftIkWeight").floatValue = defaults.sprintLeftIkWeight;
        pose.FindPropertyRelative("proneBodyPitch").floatValue = defaults.proneBodyPitch;
        pose.FindPropertyRelative("maxAimPitchDown").floatValue = defaults.maxAimPitchDown;
        serializedObject.ApplyModifiedProperties();
        SaveDefinition(definition);
    }

    public static bool SnapLeftHandToGrip(WeaponDefinition definition, int stance)
    {
        ThirdPersonWeaponRig rig = ThirdPersonWeaponPosePreviewWindow.ActivePreviewRig;
        if (definition == null || rig == null || !rig.TryGetLeftGripWorld(out Vector3 gripPos, out Quaternion gripRot))
            return false;

        ResolveLeftFields(stance, out string handField, out string wristField);
        SerializedObject so = new SerializedObject(definition);
        SerializedProperty pose = so.FindProperty("thirdPersonPose");
        if (pose == null)
            return false;

        so.Update();
        pose.FindPropertyRelative("leftHandFollowGrip").boolValue = false;
        pose.FindPropertyRelative(handField).vector3Value = rig.WorldToChest(gripPos);
        pose.FindPropertyRelative(wristField).vector3Value = rig.WorldRotToChestEuler(gripRot);
        if (!so.ApplyModifiedProperties())
            return false;

        SaveDefinition(definition);
        return true;
    }

    public static bool SeedLeftHandFromGrip(WeaponDefinition definition, int stance)
    {
        ThirdPersonWeaponRig rig = ThirdPersonWeaponPosePreviewWindow.ActivePreviewRig;
        if (definition == null || rig == null)
            return false;

        SerializedObject so = new SerializedObject(definition);
        SerializedProperty pose = so.FindProperty("thirdPersonPose");
        if (pose == null)
            return false;

        so.Update();
        if (pose.FindPropertyRelative("leftHandFollowGrip").boolValue)
            return false;

        ResolveLeftFields(stance, out string handField, out string wristField);
        SerializedProperty hand = pose.FindPropertyRelative(handField);
        if (hand == null || hand.vector3Value.sqrMagnitude > 0.0001f)
            return false;
        if (!rig.TryGetLeftGripWorld(out Vector3 gripPos, out Quaternion gripRot))
            return false;

        hand.vector3Value = rig.WorldToChest(gripPos);
        SerializedProperty wrist = pose.FindPropertyRelative(wristField);
        if (wrist != null && wrist.vector3Value.sqrMagnitude < 0.0001f)
            wrist.vector3Value = rig.WorldRotToChestEuler(gripRot);
        if (!so.ApplyModifiedProperties())
            return false;

        SaveDefinition(definition);
        return true;
    }

    private static int CurrentPreviewStance()
    {
        return ThirdPersonWeaponPosePreviewWindow.ActiveStance;
    }

    private static void ResolveLeftFields(int stance, out string handField, out string wristField)
    {
        switch (stance)
        {
            case 1:
                handField = "aimLeftHandPosition";
                wristField = "aimLeftWristEuler";
                return;
            case 2:
                handField = "sprintLeftHandPosition";
                wristField = "sprintLeftWristEuler";
                return;
            case 4:
                handField = "proneLeftHandPosition";
                wristField = "proneLeftWristEuler";
                return;
            default:
                handField = "leftHandPosition";
                wristField = "leftWristEuler";
                return;
        }
    }

    private static void WriteVector(SerializedProperty pose, string field, Vector3 value)
    {
        SerializedProperty property = pose.FindPropertyRelative(field);
        if (property != null)
            property.vector3Value = value;
    }

    public static void SaveDefinition(WeaponDefinition definition)
    {
        if (definition == null)
            return;

        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
        ThirdPersonWeaponPosePreviewWindow.NotifyDefinitionChanged(definition);
    }
}
