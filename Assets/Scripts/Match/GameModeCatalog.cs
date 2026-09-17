using UnityEngine;

[CreateAssetMenu(
    fileName = "GameModeCatalog",
    menuName = "Bullseye/Match/Game Mode Catalog")]
public class GameModeCatalog : ScriptableObject
{
    [SerializeField] private GameModeDefinition[] modes = System.Array.Empty<GameModeDefinition>();

    public int Count => modes != null ? modes.Length : 0;

    public GameModeDefinition Get(int index)
    {
        if (modes == null || index < 0 || index >= modes.Length)
            return null;
        return modes[index];
    }

    public GameModeDefinition GetById(string gameModeId)
    {
        if (string.IsNullOrEmpty(gameModeId) || modes == null)
            return null;

        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i] != null && modes[i].GameModeId == gameModeId)
                return modes[i];
        }

        return null;
    }

    public GameModeDefinition GetDefaultAvailable()
    {
        if (modes == null)
            return null;

        for (int i = 0; i < modes.Length; i++)
        {
            if (modes[i] != null && modes[i].IsAvailable)
                return modes[i];
        }

        return Get(0);
    }
}
