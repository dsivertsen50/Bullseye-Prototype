using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-view labels and independent drag handles for remote third-person poses.
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
            return;

        ThirdPersonWeaponRig[] rigs = Object.FindObjectsByType<ThirdPersonWeaponRig>(FindObjectsSortMode.None);
        for (int i = 0; i < rigs.Length; i++)
        {
            ThirdPersonWeaponRig rig = rigs[i];
            if (rig == null || !rig.DrawPoseGuides || !rig.TryGetPoseGuide(out ThirdPersonPoseGuide guide))
                continue;

            DrawLabels(guide);
            DrawHandles(rig, guide);
        }
    }

    private static void DrawLabels(ThirdPersonPoseGuide guide)
    {
        Handles.color = GunColor;
        Handles.Label(guide.gunPosition + Vector3.up * 0.05f, "GUN");

        Handles.color = RightColor;
        Handles.Label(guide.rightHandPosition + Vector3.up * 0.05f, "RIGHT HAND");
        Handles.Label(guide.rightElbowPole + Vector3.up * 0.03f, "RIGHT ELBOW");

        Handles.color = LeftColor;
        Handles.Label(guide.leftHandPosition + Vector3.up * 0.05f, "LEFT HAND");
        Handles.Label(guide.leftElbowPole + Vector3.up * 0.03f, "LEFT ELBOW");
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
        bool aiming = guide.aimBlend > 0.5f;

        DrawIndependentHandle(
            rig,
            pose,
            guide.gunPosition,
            guide.gunRotation,
            aiming ? "aimGunPosition" : "gunPosition",
            aiming ? "aimGunEuler" : "gunEuler");

        DrawIndependentHandle(
            rig,
            pose,
            guide.rightHandPosition,
            guide.rightHandRotation,
            aiming ? "aimRightHandPosition" : "rightHandPosition",
            aiming ? "aimRightWristEuler" : "rightWristEuler");

        DrawIndependentHandle(
            rig,
            pose,
            guide.leftHandPosition,
            guide.leftHandRotation,
            aiming ? "aimLeftHandPosition" : "leftHandPosition",
            aiming ? "aimLeftWristEuler" : "leftWristEuler");

        DrawElbowHandle(rig, pose, guide.rightUpperPosition, guide.rightHandPosition, guide.rightElbowPole, false, "rightElbowYaw");
        DrawElbowHandle(rig, pose, guide.leftUpperPosition, guide.leftHandPosition, guide.leftElbowPole, true, "leftElbowYaw");

        if (so.ApplyModifiedProperties())
            WeaponDefinitionEditor.SaveDefinition(definition);
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
        Object selected = Selection.activeObject;
        if (selected == definition)
            return true;
        if (selected == rig || selected == rig.gameObject)
            return true;

        GameObject selectedObject = selected as GameObject;
        return selectedObject != null && selectedObject.transform.IsChildOf(rig.transform);
    }
}
