using System;

/// <summary>
/// Persistent local game identity.
/// PlayerProfileId is a GUID generated on first launch and never taken from
/// NetworkClientId, NetworkObjectId, or the display name.
/// SteamId is reserved for a future platform association and must not replace
/// PlayerProfileId. ResearchParticipantId is owned by the research
/// consent/telemetry system and is never stored or generated here.
/// </summary>
[Serializable]
public class PlayerIdentity
{
    public string PlayerProfileId;
    public string DisplayName;
    public string SteamId;
    public string CreatedAtUtc;
    public string LastPlayedAtUtc;

    public bool HasSteamId => !string.IsNullOrWhiteSpace(SteamId);

    public static PlayerIdentity CreateNew(string displayName = PlayerProfileConstants.DefaultDisplayName)
    {
        string now = PlayerProfileConstants.UtcNowString();
        return new PlayerIdentity
        {
            PlayerProfileId = Guid.NewGuid().ToString(),
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? PlayerProfileConstants.DefaultDisplayName
                : displayName.Trim(),
            SteamId = "",
            CreatedAtUtc = now,
            LastPlayedAtUtc = now
        };
    }

    public void TouchLastPlayed()
    {
        LastPlayedAtUtc = PlayerProfileConstants.UtcNowString();
    }

    public void SetDisplayName(string displayName)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? PlayerProfileConstants.DefaultDisplayName
            : displayName.Trim();
    }
}
