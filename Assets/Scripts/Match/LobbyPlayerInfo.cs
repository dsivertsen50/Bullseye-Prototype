/// <summary>
/// One row in the match lobby roster. Ready is reserved for a later ready-up system.
/// </summary>
public class LobbyPlayerInfo
{
    public string SessionPlayerId;
    public ulong NetworkClientId;
    public string DisplayName;
    public string PublicTag;
    public bool IsHost;
    public bool IsLocal;
    public bool IsReady;
}
