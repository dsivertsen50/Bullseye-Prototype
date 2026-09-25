using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Creates REQ-065 catalogs, placeholder map art, and the persistent NetworkManager.
/// </summary>
public static class MatchLobbyFoundationSetup
{
    public const string RootFolder = "Assets/Match";
    public const string ModesFolder = RootFolder + "/GameModes";
    public const string MapsFolder = RootFolder + "/Maps";
    public const string PreviewsFolder = RootFolder + "/Previews";
    public const string ModeCatalogPath = RootFolder + "/GameModeCatalog.asset";
    public const string MapCatalogPath = RootFolder + "/MapCatalog.asset";
    public const string NetworkManagerPrefabPath = "Assets/NetworkManager/PersistentNetworkManager.prefab";
    public const string LobbyNetworkPrefabPath = "Assets/NetworkManager/MatchLobbyNetwork.prefab";
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    public const string NetworkPrefabsPath = "Assets/DefaultNetworkPrefabs.asset";
    public const string MainMenuPath = "Assets/MainMenu.unity";
    public const string ArenaScenePath = "Assets/ArenaPrototype.unity";

    [MenuItem("Bullseye/Match/Setup Map Game Mode and Lobby Foundation")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        EnsureFolder("Assets", "Match");
        EnsureFolder(RootFolder, "GameModes");
        EnsureFolder(RootFolder, "Maps");
        EnsureFolder(RootFolder, "Previews");

        Sprite arenaPreview = CreatePreviewSprite("map_01_preview.png", new Color(0.18f, 0.42f, 0.28f, 1f), "PROTOTYPE\nARENA");
        Sprite comingSoon = CreatePreviewSprite("map_coming_soon.png", new Color(0.16f, 0.17f, 0.2f, 1f), "IMAGE\nCOMING SOON");

        GameModeDefinition ffa = CreateMode("Mode_FFA.asset", "mode_ffa", "Free-For-All", "No one is your friend. Most eliminations wins.", true);
        GameModeDefinition mode2 = CreateMode("Mode_02.asset", "mode_02", "Team Eliminations", "You have some friends... and enemies. The team with the most eliminations wins.", false);
        GameModeDefinition mode3 = CreateMode("Mode_03.asset", "mode_03", "Capture the Flag", "The classic test of conquest. Capture the enemy flag to score points.", false);
        GameModeDefinition mode4 = CreateMode("Mode_04.asset", "mode_04", "Bullseye Ball", "Don't like ball sports? Try this one. Hold the bullseye to score points.", false);
        GameModeDefinition mode5 = CreateMode("Mode_05.asset", "mode_05", "Zoned Out", "King of the hill with a twist. Keep your bullseye in the zone to score points.", false);

        SceneAsset arenaScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ArenaScenePath);
        MapDefinition map1 = CreateMap("Map_01.asset", "map_01", "Prototype Arena", "A compact combat arena built around close-to-mid-range firefights and vertical movement.", arenaPreview, "ArenaPrototype", arenaScene, true);
        MapDefinition map2 = CreateMap("Map_02.asset", "map_02", "Map 02", "A future Bullseye battleground currently under development.", comingSoon, string.Empty, null, false);
        MapDefinition map3 = CreateMap("Map_03.asset", "map_03", "Map 03", "A future Bullseye battleground currently under development.", comingSoon, string.Empty, null, false);
        MapDefinition map4 = CreateMap("Map_04.asset", "map_04", "Map 04", "A future Bullseye battleground currently under development.", comingSoon, string.Empty, null, false);
        MapDefinition map5 = CreateMap("Map_05.asset", "map_05", "Map 05", "A future Bullseye battleground currently under development.", comingSoon, string.Empty, null, false);
        MapDefinition map6 = CreateMap("Map_06.asset", "map_06", "Map 06", "A future Bullseye battleground currently under development.", comingSoon, string.Empty, null, false);

        GameModeCatalog modeCatalog = GetOrCreate<GameModeCatalog>(ModeCatalogPath);
        SerializedObject modeSo = new SerializedObject(modeCatalog);
        SerializedProperty modes = modeSo.FindProperty("modes");
        modes.arraySize = 5;
        modes.GetArrayElementAtIndex(0).objectReferenceValue = ffa;
        modes.GetArrayElementAtIndex(1).objectReferenceValue = mode2;
        modes.GetArrayElementAtIndex(2).objectReferenceValue = mode3;
        modes.GetArrayElementAtIndex(3).objectReferenceValue = mode4;
        modes.GetArrayElementAtIndex(4).objectReferenceValue = mode5;
        modeSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(modeCatalog);

        MapCatalog mapCatalog = GetOrCreate<MapCatalog>(MapCatalogPath);
        SerializedObject mapSo = new SerializedObject(mapCatalog);
        SerializedProperty maps = mapSo.FindProperty("maps");
        maps.arraySize = 6;
        maps.GetArrayElementAtIndex(0).objectReferenceValue = map1;
        maps.GetArrayElementAtIndex(1).objectReferenceValue = map2;
        maps.GetArrayElementAtIndex(2).objectReferenceValue = map3;
        maps.GetArrayElementAtIndex(3).objectReferenceValue = map4;
        maps.GetArrayElementAtIndex(4).objectReferenceValue = map5;
        maps.GetArrayElementAtIndex(5).objectReferenceValue = map6;
        mapSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mapCatalog);

        GameObject lobbyPrefab = CreateLobbyNetworkPrefab();
        GameObject networkPrefab = CreatePersistentNetworkManagerPrefab();
        AddPrefabToNetworkList(lobbyPrefab);

        WireMainMenu(mapCatalog, modeCatalog, networkPrefab, lobbyPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: REQ-065 map/mode catalogs, lobby prefabs, and MainMenu wiring are in place.";
    }

    private static GameModeDefinition CreateMode(string fileName, string id, string displayName, string description, bool available)
    {
        string path = ModesFolder + "/" + fileName;
        GameModeDefinition asset = GetOrCreate<GameModeDefinition>(path);
        SerializedObject so = new SerializedObject(asset);
        so.FindProperty("gameModeId").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("shortDescription").stringValue = description;
        so.FindProperty("isAvailable").boolValue = available;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static MapDefinition CreateMap(string fileName, string id, string displayName, string description, Sprite preview, string sceneName, SceneAsset sceneAsset, bool available)
    {
        string path = MapsFolder + "/" + fileName;
        MapDefinition asset = GetOrCreate<MapDefinition>(path);
        SerializedObject so = new SerializedObject(asset);
        so.FindProperty("mapId").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("previewImage").objectReferenceValue = preview;
        so.FindProperty("sceneName").stringValue = sceneName;
        SerializedProperty sceneProperty = so.FindProperty("sceneAsset");
        if (sceneProperty != null)
            sceneProperty.objectReferenceValue = sceneAsset;
        so.FindProperty("isAvailable").boolValue = available;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static Sprite CreatePreviewSprite(string fileName, Color color, string label)
    {
        string path = PreviewsFolder + "/" + fileName;
        const int width = 640;
        const int height = 360;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color dark = color * 0.55f;
        dark.a = 1f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool border = x < 8 || y < 8 || x >= width - 8 || y >= height - 8;
                texture.SetPixel(x, y, border ? Color.white * 0.15f : Color.Lerp(dark, color, y / (float)height));
            }
        }

        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateLobbyNetworkPrefab()
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(LobbyNetworkPrefabPath);
        if (existing != null && existing.GetComponent<MatchLobbyNetwork>() != null)
            return existing;

        GameObject go = new GameObject("MatchLobbyNetwork");
        go.AddComponent<NetworkObject>();
        go.AddComponent<MatchLobbyNetwork>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, LobbyNetworkPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static GameObject CreatePersistentNetworkManagerPrefab()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        NetworkPrefabsList prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);

        GameObject go = new GameObject("PersistentNetworkManager");
        NetworkManager networkManager = go.AddComponent<NetworkManager>();
        UnityTransport transport = go.AddComponent<UnityTransport>();
        SerializedObject so = new SerializedObject(networkManager);
        so.FindProperty("NetworkConfig.NetworkTransport").objectReferenceValue = transport;
        so.FindProperty("NetworkConfig.PlayerPrefab").objectReferenceValue = player;
        so.FindProperty("NetworkConfig.EnableSceneManagement").boolValue = true;
        so.FindProperty("NetworkConfig.AutoSpawnPlayerPrefabClientSide").boolValue = false;
        so.FindProperty("NetworkConfig.ConnectionApproval").boolValue = true;
        so.FindProperty("NetworkConfig.TickRate").intValue = 30;
        SerializedProperty lists = so.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
        if (lists != null)
        {
            lists.arraySize = 1;
            lists.GetArrayElementAtIndex(0).objectReferenceValue = prefabList;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, NetworkManagerPrefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void AddPrefabToNetworkList(GameObject prefab)
    {
        NetworkPrefabsList list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
        if (list == null || prefab == null)
            return;

        SerializedObject so = new SerializedObject(list);
        SerializedProperty array = so.FindProperty("List");
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("Prefab").objectReferenceValue == prefab)
                return;
        }

        array.arraySize++;
        SerializedProperty added = array.GetArrayElementAtIndex(array.arraySize - 1);
        added.FindPropertyRelative("Override").intValue = 0;
        added.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(list);
    }

    private static void WireMainMenu(MapCatalog mapCatalog, GameModeCatalog modeCatalog, GameObject networkPrefab, GameObject lobbyPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        GameSessionCoordinator coordinator = Object.FindAnyObjectByType<GameSessionCoordinator>();
        if (coordinator != null)
        {
            SerializedObject so = new SerializedObject(coordinator);
            so.FindProperty("enterMatchImmediately").boolValue = false;
            so.FindProperty("mapCatalog").objectReferenceValue = mapCatalog;
            so.FindProperty("gameModeCatalog").objectReferenceValue = modeCatalog;
            so.FindProperty("persistentNetworkManagerPrefab").objectReferenceValue = networkPrefab;
            so.FindProperty("lobbyNetworkPrefab").objectReferenceValue = lobbyPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coordinator);
        }

        MainMenuController menu = Object.FindAnyObjectByType<MainMenuController>();
        if (menu != null)
        {
            SerializedObject so = new SerializedObject(menu);
            so.FindProperty("mapCatalog").objectReferenceValue = mapCatalog;
            so.FindProperty("gameModeCatalog").objectReferenceValue = modeCatalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(menu);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
