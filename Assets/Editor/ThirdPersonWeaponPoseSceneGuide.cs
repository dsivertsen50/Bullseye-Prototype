using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-view labels and drag handles for third-person weapon holds.
/// Works in Play Mode on remote players, and in Edit Mode on the pose preview.
/// </summary>
[InitializeOnLoad]
public static class ThirdPersonWeaponPoseSceneGuide
{
    private static readonly Color GunColor = new(1f, 0.85f, 0.15f, 1f);
    private static readonly Color RightColor = new(0.25f, 1f, 0.35f, 1f);
    private static readonly Color LeftColor = new(0.25f, 0.75f, 1f, 1f);

    static ThirdPersonWeaponPoseSceneGuide()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (!Application.isPlaying)
        {
            ThirdPersonWeaponPosePreviewWindow.TickPreview();
            ThirdPersonWeaponRig preview = ThirdPersonWeaponPosePreviewWindow.ActivePreviewRig;
            if (preview == null || !preview.DrawPoseGuides || !preview.TryGetPoseGuide(out ThirdPersonPoseGuide previewGuide))
                return;

            DrawLabels(previewGuide, preview);
            DrawHandles(preview, previewGuide);
            return;
        }

        ThirdPersonWeaponRig[] rigs = Object.FindObjectsByType<ThirdPersonWeaponRig>(FindObjectsInactive.Exclude);
        for (int i = 0; i < rigs.Length; i++)
        {
            ThirdPersonWeaponRig rig = rigs[i];
            if (rig == null || !rig.DrawPoseGuides || !rig.TryGetPoseGuide(out ThirdPersonPoseGuide guide))
                continue;

            DrawLabels(guide, rig);
            DrawHandles(rig, guide);
        }
    }

    private static void DrawLabels(ThirdPersonPoseGuide guide, ThirdPersonWeaponRig rig)
    {
        Handles.color = GunColor;
        Handles.Label(guide.gunPosition + Vector3.up * 0.05f, "GUN / SOCKET");

        Handles.color = RightColor;
        Handles.Label(guide.rightHandPosition + Vector3.up * 0.05f, "RIGHT HAND");
        Handles.Label(guide.rightElbowPole + Vector3.up * 0.03f, "RIGHT ELBOW");

        Handles.color = LeftColor;
        Handles.Label(
            guide.leftHandPosition + Vector3.up * 0.05f,
            guide.leftHandFollowsGrip ? "LEFT HAND (FOLLOWS GUN)" : "LEFT HAND");
        Handles.Label(guide.leftElbowPole + Vector3.up * 0.03f, "LEFT ELBOW");

        if (rig.AimTarget != null)
        {
            Handles.color = GunColor;
            Handles.Label(rig.AimTarget.position + Vector3.up * 0.04f, "AIM TARGET");
        }
    }

    private static void DrawHandles(ThirdPersonWeaponRig rig, ThirdPersonPoseGuide guide)
    {
        WeaponDefinition definition = guide.definition;
        if (definition == null || !IsEditing(definition, rig))
            return;

        SerializedObject so = new SerializedObject(definition);
        SerializedProperty pose = so.FindProperty("thirdPersonPose");
        if (pose == null)
            return;

        so.Update();
        ResolveHoldFields(guide, out string handField, out string wristField);
        ResolveLeftHoldFields(guide, out string leftHandField, out string leftWristField);

        EditorGUI.BeginChangeCheck();
        Vector3 nextGun = Handles.PositionHandle(guide.gunPosition, guide.gunRotation);
        if (EditorGUI.EndChangeCheck())
            so.FindProperty("worldLocalPosition").vector3Value = rig.WorldToSocket(nextGun);

        EditorGUI.BeginChangeCheck();
        Quaternion nextGunRotation = Handles.RotationHandle(guide.gunRotation, guide.gunPosition);
        if (EditorGUI.EndChangeCheck())
            so.FindProperty("worldLocalEuler").vector3Value = rig.WorldRotToSocketEuler(nextGunRotation);

        DrawIndependentHandle(
            rig,
            pose,
            guide.rightHandPosition,
            guide.rightHandRotation,
            handField,
            wristField);

        if (!guide.leftHandFollowsGrip)
        {
            DrawIndependentHandle(
                rig,
                pose,
                guide.leftHandPosition,
                guide.leftHandRotation,
                leftHandField,
                leftWristField);
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            Vector3 nextLeft = Handles.PositionHandle(guide.leftHandPosition, guide.leftHandRotation);
            Quaternion nextLeftRot = Handles.RotationHandle(guide.leftHandRotation, guide.leftHandPosition);
            if (EditorGUI.EndChangeCheck())
            {
                pose.FindPropertyRelative("leftHandFollowGrip").boolValue = false;
                pose.FindPropertyRelative(leftHandField).vector3Value = rig.WorldToChest(nextLeft);
                pose.FindPropertyRelative(leftWristField).vector3Value = rig.WorldRotToChestEuler(nextLeftRot);
            }
        }

        DrawElbowHandle(rig, pose, guide.rightUpperPosition, guide.rightHandPosition, guide.rightElbowPole, false, "rightElbowYaw");
        DrawElbowHandle(rig, pose, guide.leftUpperPosition, guide.leftHandPosition, guide.leftElbowPole, true, "leftElbowYaw");

        if (so.ApplyModifiedProperties())
            WeaponDefinitionEditor.SaveDefinition(definition);
    }

    private static void ResolveHoldFields(ThirdPersonPoseGuide guide, out string handField, out string wristField)
    {
        if (guide.proneBlend > 0.5f)
        {
            handField = "proneRightHandPosition";
            wristField = "proneRightWristEuler";
            return;
        }

        if (guide.sprintBlend > 0.5f)
        {
            handField = "sprintRightHandPosition";
            wristField = "sprintRightWristEuler";
            return;
        }

        if (guide.aimBlend > 0.5f)
        {
            handField = "aimRightHandPosition";
            wristField = "aimRightWristEuler";
            return;
        }

        handField = "rightHandPosition";
        wristField = "rightWristEuler";
    }

    private static void ResolveLeftHoldFields(ThirdPersonPoseGuide guide, out string handField, out string wristField)
    {
        if (guide.proneBlend > 0.5f)
        {
            handField = "proneLeftHandPosition";
            wristField = "proneLeftWristEuler";
            return;
        }

        if (guide.sprintBlend > 0.5f)
        {
            handField = "sprintLeftHandPosition";
            wristField = "sprintLeftWristEuler";
            return;
        }

        if (guide.aimBlend > 0.5f)
        {
            handField = "aimLeftHandPosition";
            wristField = "aimLeftWristEuler";
            return;
        }

        handField = "leftHandPosition";
        wristField = "leftWristEuler";
    }

    private static void DrawIndependentHandle(
        ThirdPersonWeaponRig rig,
        SerializedProperty pose,
        Vector3 position,
        Quaternion rotation,
        string positionField,
        string eulerField)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 nextPosition = Handles.PositionHandle(position, rotation);
        if (EditorGUI.EndChangeCheck())
            pose.FindPropertyRelative(positionField).vector3Value = rig.WorldToChest(nextPosition);

        EditorGUI.BeginChangeCheck();
        Quaternion nextRotation = Handles.RotationHandle(rotation, position);
        if (EditorGUI.EndChangeCheck())
            pose.FindPropertyRelative(eulerField).vector3Value = rig.WorldRotToChestEuler(nextRotation);
    }

    private static void DrawElbowHandle(
        ThirdPersonWeaponRig rig,
        SerializedProperty pose,
        Vector3 upper,
        Vector3 hand,
        Vector3 pole,
        bool left,
        string yawField)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 nextPole = Handles.FreeMoveHandle(pole, 0.03f, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
            pose.FindPropertyRelative(yawField).floatValue = rig.ElbowYawFromPole(upper, hand, nextPole, left);
    }

    private static bool IsEditing(WeaponDefinition definition, ThirdPersonWeaponRig rig)
    {
        if (ThirdPersonWeaponPosePreviewWindow.IsPreviewRig(rig) &&
            ThirdPersonWeaponPosePreviewWindow.ActivePreviewDefinition == definition)
            return true;

        Object selected = Selection.activeObject;
        if (selected == definition)
            return true;
        if (selected == rig || selected == rig.gameObject)
            return true;

        GameObject selectedObject = selected as GameObject;
        return selectedObject != null && selectedObject.transform.IsChildOf(rig.transform);
    }
}
