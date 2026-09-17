/// <summary>
/// Lifecycle of a multiplayer session from lobby through gameplay.
/// </summary>
public enum MatchState
{
    None = 0,
    Lobby = 1,
    Starting = 2,
    InMatch = 3
}
