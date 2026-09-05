using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene-view handles for REQ-048 weapon authoring: socket offset,
/// LeftHandGrip, and LeftElbowHint. Does not pose arm bones.
/// </summary>
[InitializeOnLoad]
public static class ThirdPersonWeaponPoseSceneGuide
{
    private static readonly Color GunColor = new(1f, 0.85f, 0.15f, 1f);
    private static readonly Color GripColor = new(0.25f, 0.75f, 1f, 1f);
    private static readonly Color HintColor = new(0.95f, 0.45f, 1f, 1f);

    static ThirdPersonWeaponPoseSceneGuide()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static void OnSceneGui(SceneView sceneView)
    {
        if (!Application.isPlaying)
        {
            if (GUIUtility.hotControl == 0)
                ThirdPersonWeaponPoseAuthoringWindow.TickPreview();
            ThirdPersonWeaponRig preview = ThirdPersonWeaponPoseAuthoringWindow.ActivePreviewRig;
            if (preview == null || !preview.DrawPoseGuides || !preview.TryGetPoseGuide(out ThirdPersonPoseGuide previewGuide))
                return;

            DrawLabels(previewGuide);
            DrawHandles(preview, previewGuide);
            ThirdPersonWeaponPoseAuthoringWindow.ApplyActivePreview();
            return;
        }

        ThirdPersonWeaponRig[] rigs = Object.FindObjectsByType<ThirdPersonWeaponRig>(FindObjectsInactive.Exclude);
        for (int i = 0; i < rigs.Length; i++)
        {
            ThirdPersonWeaponRig rig = rigs[i];
            if (rig == null || !rig.DrawPoseGuides || !rig.TryGetPoseGuide(out ThirdPersonPoseGuide guide))
                continue;

            DrawLabels(guide);
        }
    }

    private static void DrawLabels(ThirdPersonPoseGuide guide)
    {
        Handles.color = GunColor;
        Handles.Label(guide.gunPosition + Vector3.up * 0.05f, "WEAPON SOCKET");
        Handles.DrawLine(guide.gunPosition, guide.gunPosition + guide.gunRotation * Vector3.forward * 0.28f);

        if (guide.leftGripPosition.sqrMagnitude > 0.0001f || guide.definition != null)
        {
            Handles.color = GripColor;
            Handles.Label(guide.leftGripPosition + Vector3.up * 0.04f, "LEFT HAND GRIP");
        }

        Handles.color = HintColor;
        Handles.Label(guide.leftElbowHintPosition + Vector3.up * 0.04f, "LEFT ELBOW HINT");
        if (!string.IsNullOrEmpty(guide.poseCategory))
        {
            Handles.color = Color.white;
            Handles.Label(
                guide.gunPosition + Vector3.up * 0.12f,
                $"{guide.poseCategory}  IK {guide.leftIkWeight:0.00}");
        }
    }

    private static void DrawHandles(ThirdPersonWeaponRig rig, ThirdPersonPoseGuide guide)
    {
        WeaponDefinition definition = guide.definition;
        if (definition == null || !IsEditing(definition, rig))
            return;

        SerializedObject so = new SerializedObject(definition);
        so.Update();

        EditorGUI.BeginChangeCheck();
        Vector3 nextGun = Handles.PositionHandle(guide.gunPosition, guide.gunRotation);
        if (EditorGUI.EndChangeCheck())
            so.FindProperty("worldLocalPosition").vector3Value = rig.WorldToSocket(nextGun);

        EditorGUI.BeginChangeCheck();
        Quaternion nextGunRotation = Handles.RotationHandle(guide.gunRotation, guide.gunPosition);
        if (EditorGUI.EndChangeCheck())
            so.FindProperty("worldLocalEuler").vector3Value = rig.WorldRotToSocketEuler(nextGunRotation);

        if (so.ApplyModifiedProperties())
        {
            WeaponDefinitionEditor.DiscardInspectorStaleEdits(definition);
            WeaponDefinitionEditor.SaveDefinition(definition, refreshPreview: false);
        }

        WorldWeaponView world = rig.GetComponent<WorldWeaponView>();
        ThirdPersonWeaponVisual visual = world != null ? world.CurrentVisual : null;
        if (visual == null)
            return;

        if (visual.LeftHandGrip != null)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 nextGrip = Handles.PositionHandle(visual.LeftHandGrip.position, visual.LeftHandGrip.rotation);
            Quaternion nextGripRot = Handles.RotationHandle(visual.LeftHandGrip.rotation, visual.LeftHandGrip.position);
            if (EditorGUI.EndChangeCheck())
            {
                visual.LeftHandGrip.SetPositionAndRotation(nextGrip, nextGripRot);
                SaveWeaponTarget(definition, "LeftHandGrip", visual.LeftHandGrip.localPosition, visual.LeftHandGrip.localEulerAngles);
            }
        }

        if (visual.LeftElbowHint != null)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 nextHint = Handles.PositionHandle(visual.LeftElbowHint.position, visual.LeftElbowHint.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                visual.LeftElbowHint.position = nextHint;
                SaveWeaponTarget(definition, "LeftElbowHint", visual.LeftElbowHint.localPosition, visual.LeftElbowHint.localEulerAngles);
            }
        }
    }

    private static void SaveWeaponTarget(WeaponDefinition definition, string childName, Vector3 localPosition, Vector3 localEuler)
    {
        if (definition == null || definition.WorldPrefab == null)
            return;

        string path = AssetDatabase.GetAssetPath(definition.WorldPrefab);
        if (string.IsNullOrEmpty(path))
            return;

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform target = FindChild(contents.transform, childName);
            if (target == null)
            {
                GameObject created = new GameObject(childName);
                created.transform.SetParent(contents.transform, false);
                target = created.transform;
            }

            target.localPosition = localPosition;
            target.localEulerAngles = localEuler;
            ThirdPersonWeaponVisual visual = contents.GetComponent<ThirdPersonWeaponVisual>();
            visual?.ResolveFallbacks();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            Object.DestroyImmediate(contents);
        }
    }

    private static bool IsEditing(WeaponDefinition definition, ThirdPersonWeaponRig rig)
    {
        return ThirdPersonWeaponPoseAuthoringWindow.IsPreviewRig(rig) &&
               ThirdPersonWeaponPoseAuthoringWindow.ActivePreviewDefinition == definition;
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
}
