using System;

/// <summary>
/// Transport-agnostic description of a multiplayer session.
/// Join codes are the player-facing identifier; address/port stay internal.
/// </summary>
[Serializable]
public class GameSessionInfo
{
    public string JoinCode;
    public GameVisibility Visibility;
    public string Address;
    public ushort Port;
    public string ListenAddress;
    public int HostProcessId;
    public long CreatedUtcTicks;
    public bool IsPublic => Visibility == GameVisibility.Public;
}
