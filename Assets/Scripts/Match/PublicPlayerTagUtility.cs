/// <summary>
/// Short player-facing tag derived from PlayerProfileId. Not the full GUID.
/// </summary>
public static class PublicPlayerTagUtility
{
    public static string FromProfileId(string playerProfileId)
    {
        if (string.IsNullOrWhiteSpace(playerProfileId))
            return "0000";

        char[] buffer = new char[4];
        int written = 0;
        for (int i = 0; i < playerProfileId.Length && written < buffer.Length; i++)
        {
            char c = playerProfileId[i];
            if (c == '-')
                continue;
            buffer[written] = char.ToUpperInvariant(c);
            written++;
        }

        while (written < buffer.Length)
        {
            buffer[written] = '0';
            written++;
        }

        return new string(buffer);
    }

    public static string Format(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return "#0000";
        return tag[0] == '#' ? tag : "#" + tag;
    }

    public static string FallbackDisplayName(string tag)
    {
        return "Player " + (string.IsNullOrEmpty(tag) ? "0000" : tag.TrimStart('#'));
    }

    public static string ResolveLocalTag()
    {
        return FromProfileId(PlayerProfileManager.Ensure().PlayerProfileId);
    }

    public static string ResolveLocalDisplayName()
    {
        string displayName = PlayerProfileManager.Ensure().DisplayName;
        if (!string.IsNullOrWhiteSpace(displayName) &&
            displayName != PlayerProfileConstants.DefaultDisplayName)
            return displayName.Trim();

        if (!string.IsNullOrWhiteSpace(displayName))
            return displayName.Trim();

        return FallbackDisplayName(ResolveLocalTag());
    }
}
