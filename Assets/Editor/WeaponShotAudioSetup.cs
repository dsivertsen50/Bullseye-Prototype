using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// REQ-045: placeholder impact/flyby clips, shared audio settings, and
/// NearMissReceiver wiring on the player prefab.
/// </summary>
public static class WeaponShotAudioSetup
{
    public const string AudioRoot = "Assets/Audio";
    public const string WeaponsFolder = AudioRoot + "/Weapons";
    public const string ImpactsFolder = WeaponsFolder + "/Impacts";
    public const string FlybysFolder = WeaponsFolder + "/Flybys";
    public const string ResourcesFolder = AudioRoot + "/Resources";
    public const string SettingsPath = ResourcesFolder + "/WeaponShotAudioSettings.asset";
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";

    private static readonly string[] ImpactPaths =
    {
        ImpactsFolder + "/BulletImpact_01.wav",
        ImpactsFolder + "/BulletImpact_02.wav",
        ImpactsFolder + "/BulletImpact_03.wav",
        ImpactsFolder + "/BulletImpact_04.wav",
        ImpactsFolder + "/BulletImpact_05.wav"
    };

    private static readonly string[] FlybyPaths =
    {
        FlybysFolder + "/BulletWhiz_01.wav",
        FlybysFolder + "/BulletWhiz_02.wav",
        FlybysFolder + "/BulletWhiz_03.wav",
        FlybysFolder + "/BulletWhiz_04.wav"
    };

    [MenuItem("Bullseye/Audio/Setup Bullet Impact and Near-Miss Audio (REQ-045)")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        EnsureFolders();

        var impactClips = new AudioClip[ImpactPaths.Length];
        for (int i = 0; i < ImpactPaths.Length; i++)
        {
            impactClips[i] = WriteImpactClip(ImpactPaths[i], i);
            if (impactClips[i] == null)
                return "FAILED: could not create " + ImpactPaths[i];
        }

        var flybyClips = new AudioClip[FlybyPaths.Length];
        for (int i = 0; i < FlybyPaths.Length; i++)
        {
            flybyClips[i] = WriteFlybyClip(FlybyPaths[i], i);
            if (flybyClips[i] == null)
                return "FAILED: could not create " + FlybyPaths[i];
        }

        WeaponShotAudioSettings settings = CreateSettings(impactClips, flybyClips);
        if (settings == null)
            return "FAILED: could not create WeaponShotAudioSettings.asset";

        ConfigureShotgunNearMiss(false);

        if (!AddReceiverToPlayer())
            return "FAILED: Player.prefab is missing";

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: bullet impact and near-miss audio are configured";
    }

    private static void EnsureFolders()
    {
        CreateFolder("Assets", "Audio");
        CreateFolder(AudioRoot, "Weapons");
        CreateFolder(WeaponsFolder, "Impacts");
        CreateFolder(WeaponsFolder, "Flybys");
        CreateFolder(AudioRoot, "Resources");
    }

    private static void CreateFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static AudioClip WriteImpactClip(string assetPath, int variant)
    {
        if (!File.Exists(ToAbsolute(assetPath)))
            PlaceholderWav.WriteImpact(ToAbsolute(assetPath), variant);

        return ImportClip(assetPath);
    }

    private static AudioClip WriteFlybyClip(string assetPath, int variant)
    {
        if (!File.Exists(ToAbsolute(assetPath)))
            PlaceholderWav.WriteFlyby(ToAbsolute(assetPath), variant);

        return ImportClip(assetPath);
    }

    private static AudioClip ImportClip(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
        if (importer != null)
        {
            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            sample.compressionFormat = AudioCompressionFormat.PCM;
            sample.quality = 1f;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = sample;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
    }

    private static WeaponShotAudioSettings CreateSettings(AudioClip[] impactClips, AudioClip[] flybyClips)
    {
        WeaponShotAudioSettings settings = AssetDatabase.LoadAssetAtPath<WeaponShotAudioSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<WeaponShotAudioSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        SerializedObject so = new SerializedObject(settings);
        so.FindProperty("impactEnabled").boolValue = true;
        AssignClips(so.FindProperty("impactClips"), impactClips);
        so.FindProperty("impactVolume").floatValue = 1f;
        so.FindProperty("impactPitchVariation").floatValue = 0.05f;
        so.FindProperty("impactVolumeVariation").floatValue = 0.1f;
        so.FindProperty("maxImpactSoundsPerShot").intValue = 3;
        so.FindProperty("impactSoundSeparation").floatValue = 0.6f;
        so.FindProperty("impactMinDistance").floatValue = 1.2f;
        so.FindProperty("impactMaxDistance").floatValue = 42f;
        so.FindProperty("nearMissEnabled").boolValue = true;
        AssignClips(so.FindProperty("flybyClips"), flybyClips);
        so.FindProperty("nearMissRadius").floatValue = 1.5f;
        so.FindProperty("innerNearMissRadius").floatValue = 0.75f;
        so.FindProperty("nearMissVolume").floatValue = 0.4f;
        so.FindProperty("innerNearMissVolumeMultiplier").floatValue = 1.25f;
        so.FindProperty("nearMissPitchVariation").floatValue = 0.05f;
        so.FindProperty("nearMissCooldown").floatValue = 0.12f;
        so.FindProperty("flybyMinDistance").floatValue = 0.35f;
        so.FindProperty("flybyMaxDistance").floatValue = 10f;
        so.FindProperty("debugNearMiss").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        return settings;
    }

    private static void ConfigureShotgunNearMiss(bool enabled)
    {
        WeaponDefinition shotgun = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scripts/Weapons/ShotgunDefinition.asset");
        if (shotgun == null)
            return;

        SerializedObject so = new SerializedObject(shotgun);
        SerializedProperty overrides = so.FindProperty("shotAudioOverrides");
        if (overrides == null)
            return;

        overrides.FindPropertyRelative("nearMissEnabled").boolValue = enabled;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shotgun);
    }

    private static void AssignClips(SerializedProperty property, AudioClip[] clips)
    {
        if (property == null || clips == null)
            return;

        property.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    private static bool AddReceiverToPlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
            return false;

        try
        {
            NearMissReceiver receiver = root.GetComponent<NearMissReceiver>();
            if (receiver == null)
                receiver = root.AddComponent<NearMissReceiver>();

            SerializedObject so = new SerializedObject(receiver);
            so.FindProperty("playerHealth").objectReferenceValue = root.GetComponent<PlayerHealth>();
            so.FindProperty("networkObject").objectReferenceValue = root.GetComponent<NetworkObject>();
            so.FindProperty("bodyCollider").objectReferenceValue = root.GetComponentInChildren<CapsuleCollider>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string ToAbsolute(string assetPath)
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(project, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static class PlaceholderWav
    {
        private const int SampleRate = 44100;

        public static void WriteImpact(string absolutePath, int variant)
        {
            float duration = 0.16f + variant * 0.012f;
            int count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            var samples = new float[count];
            uint rng = 1103515245u + (uint)variant * 7919u;
            float clickFreq = 2100f + variant * 180f;
            float bodyFreq = 420f + variant * 55f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * (22f + variant * 2f));
                float noise = NextSigned(ref rng);
                float click = Mathf.Sin(2f * Mathf.PI * clickFreq * t) * Mathf.Exp(-t * 70f);
                float body = Mathf.Sin(2f * Mathf.PI * bodyFreq * t) * Mathf.Exp(-t * 18f);
                samples[i] = Mathf.Clamp((noise * 0.55f + click * 0.7f + body * 0.35f) * env, -1f, 1f);
            }

            WriteMono16(absolutePath, samples);
        }

        public static void WriteFlyby(string absolutePath, int variant)
        {
            float duration = 0.2f + variant * 0.02f;
            int count = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
            var samples = new float[count];
            uint rng = 2246822519u + (uint)variant * 104729u;
            float startFreq = 2600f - variant * 220f;
            float endFreq = 780f + variant * 40f;
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float t01 = count > 1 ? i / (float)(count - 1) : 0f;
                float env = Mathf.Sin(t01 * Mathf.PI);
                float freq = Mathf.Lerp(startFreq, endFreq, t01 * t01);
                phase += 2f * Mathf.PI * freq / SampleRate;
                float tone = Mathf.Sin(phase);
                float air = NextSigned(ref rng) * 0.12f;
                samples[i] = Mathf.Clamp((tone * 0.9f + air) * env, -1f, 1f);
            }

            WriteMono16(absolutePath, samples);
        }

        private static float NextSigned(ref uint rng)
        {
            rng = rng * 1664525u + 1013904223u;
            return ((rng >> 8) / 16777215f) * 2f - 1f;
        }

        private static void WriteMono16(string absolutePath, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            int dataSize = samples.Length * 2;
            using var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                short pcm = (short)Mathf.RoundToInt(clamped * 32767f);
                writer.Write(pcm);
            }
        }
    }
}
