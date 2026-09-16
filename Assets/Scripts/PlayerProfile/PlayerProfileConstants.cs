using System;

/// <summary>
/// Shared constants and identity-namespace notes for the local profile system.
/// These IDs are related but must not be treated as interchangeable:
/// PlayerProfileId, SteamId (future), ResearchParticipantId (future/consent),
/// and NetworkClientId (temporary match/session identity).
/// </summary>
public static class PlayerProfileConstants
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultDisplayName = "Player";
    public const string LogPrefix = "[PlayerProfile]";
    public const string DefaultFileName = "profile.json";
    public const string BackupFileName = "profile.backup.json";
    public const string PlayerFolderName = "player";
    public const string CommandLineSlotArg = "-profileSlot";
    public const string SlotEnvironmentVariable = "BULLSEYE_PROFILE_SLOT";

    public static string UtcNowString()
    {
        return DateTime.UtcNow.ToString("o");
    }
}
