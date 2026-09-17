using System;

/// <summary>
/// Host-owned match settings. Expand later with score/time limits and modifiers.
/// </summary>
[Serializable]
public class MatchConfiguration
{
    public string MapId = MatchIds.DefaultMapId;
    public string GameModeId = MatchIds.DefaultGameModeId;
    public MatchVisibility Visibility = MatchVisibility.Public;
    public int MaxPlayers = 8;

    public MatchConfiguration Clone()
    {
        return new MatchConfiguration
        {
            MapId = MapId,
            GameModeId = GameModeId,
            Visibility = Visibility,
            MaxPlayers = MaxPlayers
        };
    }

    public static MatchConfiguration CreateDefault(int maxPlayers)
    {
        return new MatchConfiguration
        {
            MapId = MatchIds.DefaultMapId,
            GameModeId = MatchIds.DefaultGameModeId,
            Visibility = MatchVisibility.Public,
            MaxPlayers = Math.Max(2, maxPlayers)
        };
    }
}
