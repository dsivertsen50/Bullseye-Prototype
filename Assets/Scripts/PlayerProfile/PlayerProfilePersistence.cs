using System;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON profile storage under a caller-provided directory. Gameplay code should
/// not call this directly; use PlayerProfileManager.
/// </summary>
public sealed class PlayerProfilePersistence
{
    public string DirectoryPath { get; }
    public string FileName { get; }
    public string FilePath => Path.Combine(DirectoryPath, FileName);
    public string BackupPath => Path.Combine(DirectoryPath, BackupFileName);

    private string BackupFileName =>
        string.Equals(FileName, PlayerProfileConstants.DefaultFileName, StringComparison.Ordinal)
            ? PlayerProfileConstants.BackupFileName
            : Path.GetFileNameWithoutExtension(FileName) + ".backup.json";

    public PlayerProfilePersistence(string directoryPath, string fileName = PlayerProfileConstants.DefaultFileName)
    {
        DirectoryPath = directoryPath;
        FileName = string.IsNullOrWhiteSpace(fileName)
            ? PlayerProfileConstants.DefaultFileName
            : fileName;
    }

    public static PlayerProfilePersistence ForLocalInstall(string slot = null)
    {
        string folder = Path.Combine(Application.persistentDataPath, PlayerProfileConstants.PlayerFolderName);
        string fileName = ResolveFileName(slot);
        return new PlayerProfilePersistence(folder, fileName);
    }

    public static string ResolveFileName(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return PlayerProfileConstants.DefaultFileName;

        string sanitized = SanitizeSlot(slot);
        return string.IsNullOrEmpty(sanitized)
            ? PlayerProfileConstants.DefaultFileName
            : "profile_" + sanitized + ".json";
    }

    public static string ResolveSlotFromEnvironment()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, PlayerProfileConstants.CommandLineSlotArg, StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            const string prefix = PlayerProfileConstants.CommandLineSlotArg + "=";
            if (arg != null && arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return arg.Substring(prefix.Length);
        }

        return Environment.GetEnvironmentVariable(PlayerProfileConstants.SlotEnvironmentVariable);
    }

    public bool FileExists => File.Exists(FilePath);

    public PlayerProfile LoadOrCreate(out ProfileLoadResult result)
    {
        result = ProfileLoadResult.Created;
        if (!File.Exists(FilePath))
            return PlayerProfile.CreateDefault();

        if (TryLoadFile(FilePath, out PlayerProfile profile, out string error))
        {
            result = ProfileLoadResult.Loaded;
            return profile;
        }

        Debug.LogError($"{PlayerProfileConstants.LogPrefix} Failed to load profile: {error}");
        PreserveCorruptFile(FilePath);

        if (File.Exists(BackupPath))
        {
            if (TryLoadFile(BackupPath, out PlayerProfile backup, out string backupError))
            {
                Debug.LogWarning($"{PlayerProfileConstants.LogPrefix} Recovered profile from backup.");
                result = ProfileLoadResult.RecoveredFromBackup;
                return backup;
            }

            Debug.LogError($"{PlayerProfileConstants.LogPrefix} Backup also failed: {backupError}");
        }

        result = ProfileLoadResult.ReplacedCorrupt;
        return PlayerProfile.CreateDefault();
    }

    public void Save(PlayerProfile profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        profile.EnsureCollections();
        if (profile.SchemaVersion <= 0)
            profile.SchemaVersion = PlayerProfileConstants.CurrentSchemaVersion;

        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        string json = JsonUtility.ToJson(profile, true);
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
            throw new InvalidOperationException("Profile serialization produced empty JSON.");

        string tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, json);

        string written = File.ReadAllText(tempPath);
        PlayerProfile parsed = JsonUtility.FromJson<PlayerProfile>(written);
        if (parsed == null || string.IsNullOrWhiteSpace(parsed.PlayerProfileId))
        {
            TryDelete(tempPath);
            throw new InvalidOperationException("Profile temp write failed validation.");
        }

        if (File.Exists(FilePath))
        {
            File.Copy(FilePath, BackupPath, overwrite: true);
            File.Delete(FilePath);
        }

        File.Move(tempPath, FilePath);
    }

    public void DeleteFiles()
    {
        TryDelete(FilePath);
        TryDelete(BackupPath);
        TryDelete(FilePath + ".tmp");
    }

    private static bool TryLoadFile(string path, out PlayerProfile profile, out string error)
    {
        profile = null;
        error = null;
        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Profile file was empty.";
                return false;
            }

            profile = JsonUtility.FromJson<PlayerProfile>(json);
            if (profile == null)
            {
                error = "JSON did not deserialize to a profile.";
                return false;
            }

            profile.EnsureCollections();
            if (string.IsNullOrWhiteSpace(profile.PlayerProfileId))
            {
                error = "Profile is missing PlayerProfileId.";
                profile = null;
                return false;
            }

            if (profile.SchemaVersion <= 0)
                profile.SchemaVersion = PlayerProfileConstants.CurrentSchemaVersion;

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            profile = null;
            return false;
        }
    }

    private void PreserveCorruptFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return;

            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string corruptPath = Path.Combine(
                DirectoryPath,
                Path.GetFileNameWithoutExtension(FileName) + ".corrupt." + stamp + ".json");
            File.Copy(path, corruptPath, overwrite: true);
            Debug.LogWarning($"{PlayerProfileConstants.LogPrefix} Preserved corrupt profile at {corruptPath}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{PlayerProfileConstants.LogPrefix} Could not preserve corrupt profile: {exception.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{PlayerProfileConstants.LogPrefix} Could not delete {path}: {exception.Message}");
        }
    }

    private static string SanitizeSlot(string slot)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var chars = slot.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            for (int j = 0; j < invalid.Length; j++)
            {
                if (chars[i] == invalid[j])
                {
                    chars[i] = '_';
                    break;
                }
            }
        }

        return new string(chars);
    }
}

public enum ProfileLoadResult
{
    Loaded = 0,
    Created = 1,
    RecoveredFromBackup = 2,
    ReplacedCorrupt = 3
}
