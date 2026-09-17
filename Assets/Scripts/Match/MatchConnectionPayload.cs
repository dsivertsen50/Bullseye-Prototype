using System.Text;
using Unity.Netcode;

/// <summary>
/// NGO connection payload carrying the player-facing name and short tag.
/// </summary>
public static class MatchConnectionPayload
{
    public static byte[] EncodeLocal()
    {
        string displayName = PublicPlayerTagUtility.ResolveLocalDisplayName();
        string tag = PublicPlayerTagUtility.ResolveLocalTag();
        return Encoding.UTF8.GetBytes(displayName + "|" + tag);
    }

    public static bool TryDecode(byte[] payload, out string displayName, out string publicTag)
    {
        displayName = null;
        publicTag = null;
        if (payload == null || payload.Length == 0)
            return false;

        string raw = Encoding.UTF8.GetString(payload);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        int split = raw.IndexOf('|');
        if (split < 0)
        {
            displayName = raw.Trim();
            publicTag = PublicPlayerTagUtility.FromProfileId(displayName);
            return !string.IsNullOrEmpty(displayName);
        }

        displayName = raw.Substring(0, split).Trim();
        publicTag = raw.Substring(split + 1).Trim();
        if (string.IsNullOrEmpty(displayName))
            displayName = PublicPlayerTagUtility.FallbackDisplayName(publicTag);
        if (string.IsNullOrEmpty(publicTag))
            publicTag = PublicPlayerTagUtility.FromProfileId(displayName);
        return true;
    }

    public static void ApplyTo(NetworkManager networkManager)
    {
        if (networkManager == null)
            return;
        networkManager.NetworkConfig.ConnectionData = EncodeLocal();
    }
}
