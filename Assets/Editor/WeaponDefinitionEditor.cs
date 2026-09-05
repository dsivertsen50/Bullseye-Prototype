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
        WeaponDefinition definition = (WeaponDefinition)target;
        bool previewOwns = ThirdPersonWeaponPosePreviewWindow.OwnsDefinition(definition);

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "worldLocalPosition",
            "worldLocalEuler",
            "worldLocalScale",
            "worldStanceHeightOffset",
            "thirdPersonPoseCategory",
            "supportHandIkEnabled",
            "ikBlendDuration",
            "sprintSupportIkWeight",
            "thirdPersonClass",
            "thirdPersonPose");

        EditorGUILayout.Space(8f);
        if (previewOwns)
        {
            EditorGUILayout.HelpBox(
                "Third-Person Pose Preview is editing this asset. Socket and hold numbers " +
                "are locked here so Inspector text fields cannot overwrite Scene drags " +
                "when you click Save.",
                MessageType.Warning);
            DrawLockedPoseSummary(definition);
        }
        else
        {
            DrawSocket();
            DrawThirdPersonPose();
        }

        bool inspectorChanged = EditorGUI.EndChangeCheck();
        if (inspectorChanged && serializedObject.ApplyModifiedProperties())
            SaveDefinition(definition, refreshPreview: true);
        else
            serializedObject.Update();
    }

    private void DrawSocket()
    {
        EditorGUILayout.LabelField("Third-Person Weapon Socket", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The world weapon attaches to RightHandWeaponSocket. " +
            "These offsets move the weapon relative to the hand, not the arms. " +
            "Left-hand placement lives on the weapon prefab as LeftHandGrip / LeftElbowHint.",
            MessageType.Info);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("worldLocalPosition"),
            new GUIContent("Weapon Position Offset"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("worldLocalEuler"),
            new GUIContent("Weapon Rotation Offset"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("worldLocalScale"),
            new GUIContent("Weapon Scale"));
        if (GUILayout.Button("Reset Socket Rotation"))
        {
            serializedObject.FindProperty("worldLocalEuler").vector3Value = Vector3.zero;
            SerializedProperty extraTilt = serializedObject.FindProperty("thirdPersonPose").FindPropertyRelative("gunEuler");
            if (extraTilt != null)
                extraTilt.vector3Value = Vector3.zero;
        }
    }

    private void DrawThirdPersonPose()
    {
        SerializedProperty pose = serializedObject.FindProperty("thirdPersonPose");
        if (pose == null)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Third-Person Animation Rig", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Long-gun weapons share an upper-body pose. The right hand carries the weapon. " +
            "Two Bone IK puts the left hand on LeftHandGrip. Tune grip and elbow hint on the world prefab.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview In Scene"))
                ThirdPersonWeaponPosePreviewWindow.Open((WeaponDefinition)target);
            if (GUILayout.Button("Save To Disk"))
            {
                DiscardInspectorStaleEdits((WeaponDefinition)target);
                SaveDefinition((WeaponDefinition)target, refreshPreview: false, forceReserialize: true);
                FlushDefinitionsToDisk();
            }
        }

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("thirdPersonPoseCategory"),
            new GUIContent("Pose Category", "Pistol or LongGun. AK, DMR, and shotgun share LongGun."));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("supportHandIkEnabled"),
            new GUIContent("Support Hand IK"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("ikBlendDuration"),
            new GUIContent("IK Blend Duration"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("sprintSupportIkWeight"),
            new GUIContent("Sprint Support IK Weight"));
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("thirdPersonClass"),
            new GUIContent("Legacy Shape", "Older pistol/rifle/shotgun label. Pose Category is the authoring control."));

        showWeights = EditorGUILayout.Foldout(showWeights, "Legacy Per-Bone Pose (unused by REQ-048)", true);
        if (showWeights)
        {
            EditorGUILayout.HelpBox(
                "These fields remain for old assets. The new rig does not pose arms from them.",
                MessageType.Warning);
            EditorGUILayout.PropertyField(pose, true);
        }
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
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("crouchRightHandPosition"), new GUIContent("Crouch Right Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("crouchRightWristEuler"), new GUIContent("Crouch Right Wrist"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("crouchLeftHandPosition"), new GUIContent("Crouch Left Hand"));
        EditorGUILayout.PropertyField(pose.FindPropertyRelative("crouchLeftWristEuler"), new GUIContent("Crouch Left Wrist"));
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
        return false;
    }

    public static bool SeedLeftHandFromGrip(WeaponDefinition definition, int stance)
    {
        return false;
    }

    public static void SetLeftHandFollowGrip(WeaponDefinition definition, bool follow)
    {
        if (definition == null)
            return;

        SerializedObject so = new SerializedObject(definition);
        so.Update();
        SerializedProperty pose = so.FindProperty("thirdPersonPose");
        if (pose == null)
            return;

        pose.FindPropertyRelative("leftHandFollowGrip").boolValue = follow;
        if (so.ApplyModifiedProperties())
            SaveDefinition(definition, refreshPreview: true);
    }

    public static void CopyStandHoldToStance(WeaponDefinition definition, int stance)
    {
        if (definition == null || stance <= 0)
            return;

        SerializedObject so = new SerializedObject(definition);
        so.Update();
        SerializedProperty pose = so.FindProperty("thirdPersonPose");
        if (pose == null)
            return;

        Vector3 right = pose.FindPropertyRelative("rightHandPosition").vector3Value;
        Vector3 rightWrist = pose.FindPropertyRelative("rightWristEuler").vector3Value;
        Vector3 left = pose.FindPropertyRelative("leftHandPosition").vector3Value;
        Vector3 leftWrist = pose.FindPropertyRelative("leftWristEuler").vector3Value;
        Vector3 gunPos = so.FindProperty("worldLocalPosition").vector3Value;
        Vector3 gunEuler = so.FindProperty("worldLocalEuler").vector3Value;
        switch (stance)
        {
            case 1:
                pose.FindPropertyRelative("aimRightHandPosition").vector3Value = right;
                pose.FindPropertyRelative("aimRightWristEuler").vector3Value = rightWrist;
                pose.FindPropertyRelative("aimLeftHandPosition").vector3Value = left;
                pose.FindPropertyRelative("aimLeftWristEuler").vector3Value = leftWrist;
                pose.FindPropertyRelative("aimGunPosition").vector3Value = gunPos;
                pose.FindPropertyRelative("aimGunEuler").vector3Value = gunEuler;
                break;
            case 2:
                pose.FindPropertyRelative("sprintRightHandPosition").vector3Value = right;
                pose.FindPropertyRelative("sprintRightWristEuler").vector3Value = rightWrist;
                pose.FindPropertyRelative("sprintLeftHandPosition").vector3Value = left;
                pose.FindPropertyRelative("sprintLeftWristEuler").vector3Value = leftWrist;
                pose.FindPropertyRelative("sprintGunPosition").vector3Value = gunPos;
                pose.FindPropertyRelative("sprintGunEuler").vector3Value = gunEuler;
                break;
            case 3:
                pose.FindPropertyRelative("crouchRightHandPosition").vector3Value = right;
                pose.FindPropertyRelative("crouchRightWristEuler").vector3Value = rightWrist;
                pose.FindPropertyRelative("crouchLeftHandPosition").vector3Value = left;
                pose.FindPropertyRelative("crouchLeftWristEuler").vector3Value = leftWrist;
                pose.FindPropertyRelative("crouchGunPosition").vector3Value = gunPos;
                pose.FindPropertyRelative("crouchGunEuler").vector3Value = gunEuler;
                break;
            case 4:
                pose.FindPropertyRelative("proneRightHandPosition").vector3Value = right;
                pose.FindPropertyRelative("proneRightWristEuler").vector3Value = rightWrist;
                pose.FindPropertyRelative("proneLeftHandPosition").vector3Value = left;
                pose.FindPropertyRelative("proneLeftWristEuler").vector3Value = leftWrist;
                pose.FindPropertyRelative("proneGunPosition").vector3Value = gunPos;
                pose.FindPropertyRelative("proneGunEuler").vector3Value = gunEuler;
                break;
            default:
                return;
        }

        if (so.ApplyModifiedProperties())
            SaveDefinition(definition, refreshPreview: true);
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
            case 3:
                handField = "crouchLeftHandPosition";
                wristField = "crouchLeftWristEuler";
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

    public static void SaveDefinition(
        WeaponDefinition definition,
        bool refreshPreview = true,
        bool forceReserialize = false)
    {
        if (definition == null)
            return;

        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
        if (forceReserialize)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.ForceReserializeAssets(new[] { path });
        }

        SyncInspectors(definition);
        if (refreshPreview)
            ThirdPersonWeaponPosePreviewWindow.NotifyDefinitionChanged(definition);
    }

    public static void FlushDefinitionsToDisk()
    {
        AssetDatabase.SaveAssets();
    }

    public static void DiscardInspectorStaleEdits(Object target)
    {
        if (target == null)
            return;

        UnityEditor.Editor[] editors = ActiveEditorTracker.sharedTracker.activeEditors;
        for (int i = 0; i < editors.Length; i++)
        {
            UnityEditor.Editor editor = editors[i];
            if (editor == null || editor.serializedObject == null)
                continue;

            Object[] targets = editor.targets;
            for (int t = 0; t < targets.Length; t++)
            {
                if (targets[t] != target)
                    continue;
                editor.serializedObject.Update();
                editor.Repaint();
                break;
            }
        }
    }

    public static string DescribeSavedPose(WeaponDefinition definition, int stance)
    {
        if (definition == null)
            return "Nothing to save.";

        string stanceName = stance switch
        {
            1 => "Aim",
            2 => "Sprint",
            3 => "Crouch",
            4 => "Prone",
            _ => "Stand"
        };

        return
            $"Saved {definition.DisplayName} {stanceName} to {AssetDatabase.GetAssetPath(definition)}\n" +
            $"Socket Pos {definition.WorldLocalPosition}  Rot {definition.WorldLocalEuler}\n" +
            $"Category {definition.PoseCategory}  Support IK {definition.UsesSupportHandIk}";
    }

    private void DrawLockedPoseSummary(WeaponDefinition definition)
    {
        EditorGUILayout.LabelField("Third-Person Weapon Socket", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Socket Position", definition.WorldLocalPosition.ToString("F3"));
        EditorGUILayout.LabelField("Socket Rotation", definition.WorldLocalEuler.ToString("F2"));
        EditorGUILayout.LabelField("Socket Scale", definition.WorldLocalScale.ToString("F2"));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview In Scene"))
                ThirdPersonWeaponPosePreviewWindow.Open(definition);
            if (GUILayout.Button("Save Pose To Disk"))
            {
                DiscardInspectorStaleEdits(definition);
                SaveDefinition(definition, refreshPreview: false, forceReserialize: true);
                FlushDefinitionsToDisk();
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Current Rig", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Pose Category", definition.PoseCategory.ToString());
        EditorGUILayout.LabelField("Support Hand IK", definition.UsesSupportHandIk.ToString());
    }

    private static void SyncInspectors(Object target)
    {
        Editor[] editors = ActiveEditorTracker.sharedTracker.activeEditors;
        for (int i = 0; i < editors.Length; i++)
        {
            Editor editor = editors[i];
            if (editor == null || editor.serializedObject == null)
                continue;

            Object[] targets = editor.targets;
            for (int t = 0; t < targets.Length; t++)
            {
                if (targets[t] != target)
                    continue;
                editor.serializedObject.Update();
                editor.Repaint();
                break;
            }
        }
    }
}
