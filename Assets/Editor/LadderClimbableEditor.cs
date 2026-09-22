using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LadderClimbable))]
public class LadderClimbableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Yellow arrow = climbable side. White handle = get-off height.\n" +
            "If the arrow is on the wrong face, click Next Climb Side until it matches the rungs.\n" +
            "Drag the white Top Exit handle in the Scene view to set dismount height.",
            MessageType.Info);

        DrawDefaultInspector();

        LadderClimbable ladder = (LadderClimbable)target;
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Next Climb Side"))
            {
                Undo.RecordObject(ladder, "Next Climb Side");
                ladder.CycleClimbSide();
                EditorUtility.SetDirty(ladder);
            }

            if (GUILayout.Button("Fit Volume To Mesh"))
            {
                Undo.RecordObject(ladder, "Fit Volume To Mesh");
                ladder.EnsureClimbVolume();
                ladder.EnsureExitPoints();
                EditorUtility.SetDirty(ladder);
            }
        }
    }

    private void OnSceneGUI()
    {
        LadderClimbable ladder = (LadderClimbable)target;
        if (ladder == null)
            return;

        ladder.EnsureExitPoints();
        DrawMoveHandle(ladder.TopExitPoint, "Top Exit", Color.white);
        DrawMoveHandle(ladder.BottomExitPoint, "Bottom", Color.gray);
    }

    private static void DrawMoveHandle(Transform marker, string label, Color color)
    {
        if (marker == null)
            return;

        Handles.color = color;
        EditorGUI.BeginChangeCheck();
        Vector3 next = Handles.PositionHandle(marker.position, Quaternion.identity);
        Handles.Label(next + Vector3.up * 0.2f, label);
        if (!EditorGUI.EndChangeCheck())
            return;

        Undo.RecordObject(marker, "Move " + label);
        marker.position = next;
        EditorUtility.SetDirty(marker);
    }
}
