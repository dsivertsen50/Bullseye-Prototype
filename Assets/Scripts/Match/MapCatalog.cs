using UnityEngine;

[CreateAssetMenu(
    fileName = "MapCatalog",
    menuName = "Bullseye/Match/Map Catalog")]
public class MapCatalog : ScriptableObject
{
    [SerializeField] private MapDefinition[] maps = System.Array.Empty<MapDefinition>();

    public int Count => maps != null ? maps.Length : 0;

    public MapDefinition Get(int index)
    {
        if (maps == null || index < 0 || index >= maps.Length)
            return null;
        return maps[index];
    }

    public MapDefinition GetById(string mapId)
    {
        if (string.IsNullOrEmpty(mapId) || maps == null)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].MapId == mapId)
                return maps[i];
        }

        return null;
    }

    public MapDefinition GetDefaultAvailable()
    {
        if (maps == null)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].CanStartMatch)
                return maps[i];
        }

        return Get(0);
    }
}
