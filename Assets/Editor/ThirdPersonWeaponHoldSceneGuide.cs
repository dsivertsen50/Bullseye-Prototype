using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-view handles for REQ-049: Grip_R, Grip_L, Aim, and elbow hints.
/// Editing arms never moves the weapon.
/// </summary>
[InitializeOnLoad]
public static class ThirdPersonWeaponHoldSceneGuide
{
    private static readonly Color WeaponColor = new(1f, 0.85f, 0.15f, 1f);
    private static readonly Color GripRColor = new(0.3f, 1f, 0.35f, 1f);
    private static readonly Color GripLColor = new(0.25f, 0.75f, 1f, 1f);
    private static readonly Color AimColor = new(1f, 0.9f, 0.2f, 1f);
    private static readonly Color RightHintColor = new(1f, 0.55f, 0.2f, 1f);
    private static readonly Color LeftHintColor = new(0.95f, 0.45f, 1f, 1f);

    static ThirdPersonWeaponHoldSceneGuide()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (Application.isPlaying)
            return;

        if (GUIUtility.hotControl == 0)
            ThirdPersonWeaponHoldSetupWindow.TickPreview();

        ThirdPersonWeaponRig preview = ThirdPersonWeaponHoldSetupWindow.ActivePreviewRig;
        if (preview == null || !preview.DrawPoseGuides || !preview.TryGetPoseGuide(out ThirdPersonPoseGuide guide))
            return;

        DrawLabels(guide);
        DrawHandles(preview, guide);
        ThirdPersonWeaponHoldSetupWindow.ApplyActivePreview();
    }

    private static void DrawLabels(ThirdPersonPoseGuide guide)
    {
        Handles.color = WeaponColor;
        Handles.Label(guide.gunPosition + Vector3.up * 0.05f, "WEAPON ANCHOR");
        Handles.DrawLine(guide.gunPosition, guide.gunPosition + guide.gunRotation * Vector3.forward * 0.28f);
        Handles.DrawLine(guide.gunPosition, guide.gunPosition + guide.gunRotation * Vector3.up * 0.12f);

        Handles.color = GripRColor;
        Handles.Label(guide.rightGripPosition + Vector3.up * 0.04f, "Right Hand");
        Handles.color = GripLColor;
        Handles.Label(guide.leftGripPosition + Vector3.up * 0.04f, "Left Hand");
        Handles.color = RightHintColor;
        Handles.Label(guide.rightElbowHintPosition + Vector3.up * 0.04f, "Right Elbow");
        Handles.color = LeftHintColor;
        Handles.Label(guide.leftElbowHintPosition + Vector3.up * 0.04f, "Left Elbow");
        if (!string.IsNullOrEmpty(guide.poseCategory))
        {
            Handles.color = Color.white;
            Handles.Label(
                guide.gunPosition + Vector3.up * 0.12f,
                $"{guide.poseCategory}  R {guide.rightIkWeight:0.00}  L {guide.leftIkWeight:0.00}");
        }
    }

    private static void DrawHandles(ThirdPersonWeaponRig rig, ThirdPersonPoseGuide guide)
    {
        WeaponDefinition definition = guide.definition;
        if (definition == null || !ThirdPersonWeaponHoldSetupWindow.IsPreviewRig(rig))
            return;

        SerializedObject so = new SerializedObject(definition);
        so.Update();

        Vector3 lockedWeapon = guide.gunPosition;
        Quaternion lockedWeaponRot = guide.gunRotation;

        EditorGUI.BeginChangeCheck();
        Vector3 nextGun = Handles.PositionHandle(guide.gunPosition, guide.gunRotation);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 current = definition.ThirdPersonAnchorPositionOffset;
            so.FindProperty("thirdPersonAnchorPositionOffset").vector3Value =
                current + rig.WorldToAnchor(nextGun) - rig.WorldToAnchor(lockedWeapon);
        }

        EditorGUI.BeginChangeCheck();
        Quaternion nextGunRotation = Handles.RotationHandle(guide.gunRotation, guide.gunPosition);
        if (EditorGUI.EndChangeCheck())
        {
            Vector3 current = definition.ThirdPersonAnchorRotationOffset;
            Vector3 delta = rig.WorldRotToAnchorEuler(nextGunRotation) - rig.WorldRotToAnchorEuler(lockedWeaponRot);
            so.FindProperty("thirdPersonAnchorRotationOffset").vector3Value = current + delta;
        }

        if (so.ApplyModifiedProperties())
        {
            WeaponDefinitionEditor.DiscardInspectorStaleEdits(definition);
            WeaponDefinitionEditor.SaveDefinition(definition, refreshPreview: false);
            ThirdPersonWeaponHoldSetupWindow.NotifyDefinitionChanged(definition);
        }

        WorldWeaponView world = rig.GetComponent<WorldWeaponView>();
        ThirdPersonWeaponVisual visual = world != null ? world.CurrentVisual : null;
        if (visual == null)
            return;

        visual.ResolveFallbacks();
        EditMarker(definition, visual.GripR, ThirdPersonWeaponMarkers.GripR, GripRColor);
        EditMarker(definition, visual.GripL, ThirdPersonWeaponMarkers.GripL, GripLColor);
        EditMarker(definition, visual.Aim, ThirdPersonWeaponMarkers.Aim, AimColor);

        EditElbowHint(rig, rig.RightElbowHint, definition, true);
        EditElbowHint(rig, rig.LeftElbowHint, definition, false);
    }

    private static void EditMarker(WeaponDefinition definition, Transform marker, string markerName, Color color)
    {
        if (marker == null)
            return;

        Handles.color = color;
        Handles.DrawWireDisc(marker.position, marker.rotation * Vector3.up, 0.028f);
        Handles.DrawWireDisc(marker.position, marker.rotation * Vector3.forward, 0.028f);
        EditorGUI.BeginChangeCheck();
        Vector3 next = Handles.PositionHandle(marker.position, marker.rotation);
        Quaternion nextRot = Handles.RotationHandle(marker.rotation, marker.position);
        if (!EditorGUI.EndChangeCheck())
            return;

        marker.SetPositionAndRotation(next, nextRot);
        ThirdPersonWeaponHoldSetup.SaveMarkerLocal(
            definition,
            markerName,
            marker.localPosition,
            marker.localEulerAngles);
        ThirdPersonWeaponHoldSetupWindow.NotifyDefinitionChanged(definition);
    }

    private static void EditElbowHint(ThirdPersonWeaponRig rig, Transform hint, WeaponDefinition definition, bool right)
    {
        if (hint == null || definition == null)
            return;

        EditorGUI.BeginChangeCheck();
        Vector3 next = Handles.PositionHandle(hint.position, hint.rotation);
        if (!EditorGUI.EndChangeCheck())
            return;

        hint.position = next;
        ThirdPersonWeaponHoldProfile profile = ThirdPersonWeaponHoldResolver.Resolve(
            definition,
            ThirdPersonWeaponPoseKind.Hold);
        if (profile == null)
            return;

        Vector3 local = rig.WorldToAnchor(next);
        SerializedObject so = new SerializedObject(profile);
        so.Update();
        so.FindProperty(right ? "rightElbowHintLocalPosition" : "leftElbowHintLocalPosition").vector3Value = local;
        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            ThirdPersonWeaponHoldSetupWindow.NotifyDefinitionChanged(definition);
        }
    }
}
