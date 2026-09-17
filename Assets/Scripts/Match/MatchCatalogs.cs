/// <summary>
/// Resolves and validates match configuration against the map/mode catalogs.
/// </summary>
public static class MatchCatalogs
{
    public static bool TryValidate(
        MatchConfiguration configuration,
        MapCatalog maps,
        GameModeCatalog modes,
        out string error,
        out MapDefinition map,
        out GameModeDefinition mode)
    {
        error = null;
        map = null;
        mode = null;

        if (configuration == null)
        {
            error = "Match settings are missing.";
            return false;
        }

        mode = modes != null ? modes.GetById(configuration.GameModeId) : null;
        if (mode == null)
        {
            error = "Select a game mode.";
            return false;
        }

        if (!mode.IsAvailable)
        {
            error = mode.DisplayName + " is coming soon.";
            return false;
        }

        map = maps != null ? maps.GetById(configuration.MapId) : null;
        if (map == null)
        {
            error = "Select a map.";
            return false;
        }

        if (!map.IsAvailable)
        {
            error = map.DisplayName + " is coming soon.";
            return false;
        }

        if (!map.HasLoadableScene)
        {
            error = "That map is not available yet.";
            return false;
        }

        return true;
    }

    public static GameVisibility ToGameVisibility(MatchVisibility visibility)
    {
        return visibility == MatchVisibility.Public ? GameVisibility.Public : GameVisibility.Private;
    }

    public static MatchVisibility ToMatchVisibility(GameVisibility visibility)
    {
        return visibility == GameVisibility.Public ? MatchVisibility.Public : MatchVisibility.Private;
    }
}
