using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponDefinition))]
public class WeaponDefinitionEditor : Editor
{
    private static bool showAimHold;
    private static bool showWeights;
    private static bool showLegacyAttachment;

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
        DrawThirdPersonPose();
        DrawLegacyAttachment();

        if (serializedObject.ApplyModifiedProperties())
            SaveDefinition((WeaponDefinition)target);
    }

    private void DrawThirdPersonPose()
    {
        SerializedProperty pose = serializedObject.FindProperty("thirdPersonPose");
        if (pose == null)
            return;

        EditorGUILayout.LabelField("Third-Person World View", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Gun, right arm, left arm, wrists, and elbows are independent. " +
            "Moving one does not move the others.\n\n" +
            "All positions are meters from the upper chest:\n" +
            "X = across the body (positive is right)\n" +
            "Y = up\n" +
            "Z = forward\n\n" +
            "In Play Mode, select this asset and look at a remote player in the Scene view. " +
            "Yellow = gun, green = right hand / elbow, cyan = left hand / elbow. " +
            "Drag the handles, then click Save Pose To Disk. Inspector values now write to the asset file immediately when they change.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Pose To Disk"))
                SaveDefinition((WeaponDefinition)target);
            if (GUILayout.Button("Reset Pose To Defaults"))
                ResetPoseToDefaults();
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("thirdPersonClass"), new GUIContent(
            "Weapon Shape",
            "Pistol, rifle, or shotgun. Used only for defaults if a pose is missing."));

        DrawGun(pose);
        DrawArm(pose, "Right Arm", "rightHandPosition", "rightWristEuler", "rightArmReach", "rightElbowYaw");
        DrawArm(pose, "Left Arm", "leftHandPosition", "leftWristEuler", "leftArmReach", "leftElbowYaw");
        DrawAimHold(pose);
        DrawAimFollow(pose);
        DrawWeights(pose);
    }

    private static void DrawGun(SerializedProperty pose)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Gun", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Moves only the weapon mesh. Hands stay where you left them.", MessageType.None);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("gunPosition"), new GUIContent("Position"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("gunEuler"), new GUIContent("Tilt"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("gunScale"), new GUIContent("Scale"));
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
        EditorGUILayout.HelpBox(
            "Hand position, wrist tilt, elbow bend, and elbow swing are independent of the gun and the other arm.",
            MessageType.None);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative(handField), new GUIContent("Hand Position"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative(wristField), new GUIContent("Wrist Tilt"));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative(reachField),
            new GUIContent("Elbow Straightness", "1 is almost locked. Lower keeps a bent elbow."));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative(elbowField),
            new GUIContent("Elbow Swing", "Rotates the elbow around the shoulder-to-hand line. 0 is out from the body."));
    }

    private static void DrawAimHold(SerializedProperty pose)
    {
        EditorGUILayout.Space(4f);
        showAimHold = EditorGUILayout.Foldout(showAimHold, "Aimed / ADS Variants", true);
        if (!showAimHold)
            return;

        EditorGUILayout.HelpBox("Same independent controls, used while the remote player is aiming.", MessageType.None);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimGunPosition"), new GUIContent("Aimed Gun Position"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimGunEuler"), new GUIContent("Aimed Gun Tilt"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimRightHandPosition"), new GUIContent("Aimed Right Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimRightWristEuler"), new GUIContent("Aimed Right Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimLeftHandPosition"), new GUIContent("Aimed Left Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("aimLeftWristEuler"), new GUIContent("Aimed Left Wrist"));
        EditorGUILayout.PropertyField(
            pose.FindPropertyRelative("aimRaisePitch"),
            new GUIContent("Aim Raise Pitch", "Extra upward upper-body pitch while ADS / zoom is active."));
    }

    private static void DrawAimFollow(SerializedProperty pose)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Aim Follow", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("maxAimPitch"), new GUIContent("Max Look Pitch"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("spineAimWeight"), new GUIContent("Spine Follow"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("upperChestAimShare"), new GUIContent("Upper Chest Share"));
    }

    private static void DrawWeights(SerializedProperty pose)
    {
        showWeights = EditorGUILayout.Foldout(showWeights, "Stance Pose Weights", true);
        if (!showWeights)
            return;

        EditorGUILayout.HelpBox(
            "How strongly this hold overrides Mixamo locomotion. 1 is full hold. Sprint / dive should stay low.",
            MessageType.None);
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("defaultWeight"), new GUIContent("Stand / Walk"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("sprintWeight"), new GUIContent("Sprint"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("crouchWeight"), new GUIContent("Crouch"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("proneWeight"), new GUIContent("Prone"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("diveWeight"), new GUIContent("Dolphin Dive"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("jumpWeight"), new GUIContent("Jump"));
    }

    private void DrawLegacyAttachment()
    {
        EditorGUILayout.Space(8f);
        showLegacyAttachment = EditorGUILayout.Foldout(
            showLegacyAttachment,
            "Legacy World Attachment (unused by the third-person rig)",
            true);
        if (!showLegacyAttachment)
            return;

        EditorGUILayout.HelpBox(
            "These used to place a floating world gun on WeaponHandAnchor. The third-person rig ignores them.",
            MessageType.Warning);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("worldLocalPosition"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("worldLocalEuler"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("worldLocalScale"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("worldStanceHeightOffset"));
    }

    private void ResetPoseToDefaults()
    {
        WeaponDefinition definition = (WeaponDefinition)target;
        ThirdPersonWeaponClass weaponClass = (ThirdPersonWeaponClass)serializedObject.FindProperty("thirdPersonClass").enumValueIndex;
        ThirdPersonWeaponPose defaults = ThirdPersonWeaponPose.CreateDefault(weaponClass);
        SerializedProperty pose = serializedObject.FindProperty("thirdPersonPose");
        WriteVector(pose, "gunPosition", defaults.gunPosition);
        WriteVector(pose, "gunEuler", defaults.gunEuler);
        WriteVector(pose, "gunScale", defaults.gunScale);
        WriteVector(pose, "aimGunPosition", defaults.aimGunPosition);
        WriteVector(pose, "aimGunEuler", defaults.aimGunEuler);
        WriteVector(pose, "rightHandPosition", defaults.rightHandPosition);
        WriteVector(pose, "rightWristEuler", defaults.rightWristEuler);
        WriteVector(pose, "aimRightHandPosition", defaults.aimRightHandPosition);
        WriteVector(pose, "aimRightWristEuler", defaults.aimRightWristEuler);
        WriteVector(pose, "leftHandPosition", defaults.leftHandPosition);
        WriteVector(pose, "leftWristEuler", defaults.leftWristEuler);
        WriteVector(pose, "aimLeftHandPosition", defaults.aimLeftHandPosition);
        WriteVector(pose, "aimLeftWristEuler", defaults.aimLeftWristEuler);
        pose.FindPropertyRelative("rightArmReach").floatValue = defaults.rightArmReach;
        pose.FindPropertyRelative("leftArmReach").floatValue = defaults.leftArmReach;
        pose.FindPropertyRelative("rightElbowYaw").floatValue = defaults.rightElbowYaw;
        pose.FindPropertyRelative("leftElbowYaw").floatValue = defaults.leftElbowYaw;
        serializedObject.ApplyModifiedProperties();
        SaveDefinition(definition);
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
    }
}
