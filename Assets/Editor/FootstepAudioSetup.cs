using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates FootstepAudioSettings and wires PlayerFootstepAudio on the player.
/// Clip slots stay empty until walk / sprint assets are assigned.
/// </summary>
public static class FootstepAudioSetup
{
    public const string ResourcesFolder = "Assets/Audio/Resources";
    public const string SettingsPath = ResourcesFolder + "/FootstepAudioSettings.asset";
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";

    [MenuItem("Bullseye/Audio/Setup Footstep Audio Slots")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio"))
                AssetDatabase.CreateFolder("Assets", "Audio");
            AssetDatabase.CreateFolder("Assets/Audio", "Resources");
        }

        FootstepAudioSettings settings = AssetDatabase.LoadAssetAtPath<FootstepAudioSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<FootstepAudioSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
            return "FAILED: Player.prefab is missing";

        try
        {
            PlayerFootstepAudio audio = root.GetComponent<PlayerFootstepAudio>();
            if (audio == null)
                audio = root.AddComponent<PlayerFootstepAudio>();

            SerializedObject so = new SerializedObject(audio);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: footstep audio slots are on FootstepAudioSettings and Player.prefab";
    }
}
