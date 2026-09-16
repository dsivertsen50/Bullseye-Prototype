using UnityEditor;
using UnityEngine;

public class PlayerProfileDebugWindow : EditorWindow
{
    private Vector2 scroll;

    [MenuItem("Bullseye/Debug/Player Profile")]
    public static void Open()
    {
        var window = GetWindow<PlayerProfileDebugWindow>("Player Profile");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    [MenuItem("Bullseye/Debug/Run Player Profile Self-Test")]
    public static void RunSelfTest()
    {
        PlayerProfileSelfTest.RunAll(out string report);
        EditorUtility.DisplayDialog("Player Profile Self-Test", report, "OK");
    }

    private void OnGUI()
    {
        PlayerProfileManager manager = Application.isPlaying
            ? PlayerProfileManager.Ensure()
            : FindAnyObjectByType<PlayerProfileManager>();

        EditorGUILayout.LabelField("REQ-062 Local Profile", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This is a development inspector only. The player-facing career screen is on the main menu (Profile).",
            MessageType.Info);

        if (manager == null || manager.GetProfile() == null)
        {
            EditorGUILayout.HelpBox(
                Application.isPlaying
                    ? "Profile manager is not loaded."
                    : "Enter Play Mode to inspect the live profile, or run the self-test.",
                MessageType.Warning);
        }
        else
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(manager.FormatDebugDump(), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space(8f);
        if (GUILayout.Button("Run Self-Test"))
            RunSelfTest();

        using (new EditorGUI.DisabledScope(!Application.isPlaying || manager == null))
        {
            if (GUILayout.Button("Dump Profile To Console"))
                Debug.Log(manager.FormatDebugDump());

            if (GUILayout.Button("Reset Player Profile"))
            {
                if (EditorUtility.DisplayDialog(
                        "Reset Player Profile",
                        "Delete the local profile, generate a new PlayerProfileId, and reset stats?",
                        "Reset",
                        "Cancel"))
                {
                    manager.ResetProfileInternal();
                }
            }
        }
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }
}

[CustomEditor(typeof(PlayerProfileManager))]
public class PlayerProfileManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var manager = (PlayerProfileManager)target;
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to inspect or reset the loaded profile.", MessageType.Info);
            if (GUILayout.Button("Open Profile Window"))
                PlayerProfileDebugWindow.Open();
            return;
        }

        PlayerProfile profile = manager.GetProfile();
        if (profile == null)
            return;

        EditorGUILayout.LabelField("PlayerProfileId", profile.PlayerProfileId);
        EditorGUILayout.LabelField("DisplayName", profile.DisplayName);
        EditorGUILayout.LabelField("MatchesPlayed", profile.LifetimeStats.MatchesPlayed.ToString());
        EditorGUILayout.LabelField("Eliminations", profile.LifetimeStats.Eliminations.ToString());
        EditorGUILayout.LabelField("Deaths", profile.LifetimeStats.Deaths.ToString());
        EditorGUILayout.LabelField("Assists", profile.LifetimeStats.Assists.ToString());
        EditorGUILayout.LabelField("Weapons", profile.WeaponStats != null ? profile.WeaponStats.Count.ToString() : "0");

        if (GUILayout.Button("Dump Profile To Console"))
            Debug.Log(manager.FormatDebugDump());

        if (GUILayout.Button("Reset Player Profile"))
        {
            if (EditorUtility.DisplayDialog(
                    "Reset Player Profile",
                    "Delete the local profile, generate a new PlayerProfileId, and reset stats?",
                    "Reset",
                    "Cancel"))
            {
                manager.ResetProfileInternal();
            }
        }
    }
}
