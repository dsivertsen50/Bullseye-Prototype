/// <summary>
/// High-level connection lifecycle for menu and debug UI.
/// </summary>
public enum MultiplayerConnectionState
{
    Offline,
    InitializingServices,
    Authenticating,
    CreatingSession,
    WaitingForPlayers,
    JoiningSession,
    Connecting,
    Connected,
    Disconnecting,
    Error
}
