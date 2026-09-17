using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "MapDefinition",
    menuName = "Bullseye/Match/Map Definition")]
public class MapDefinition : ScriptableObject
{
    [SerializeField] private string mapId = "map_01";
    [SerializeField] private string displayName = "Map";
    [SerializeField] private Sprite previewImage;
    [SerializeField, TextArea(1, 3)] private string description = string.Empty;
#if UNITY_EDITOR
    [SerializeField] private SceneAsset sceneAsset;
#endif
    [SerializeField] private string sceneName = string.Empty;
    [SerializeField] private bool isAvailable;

    public string MapId => string.IsNullOrWhiteSpace(mapId) ? name : mapId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? MapId : displayName.Trim();
    public Sprite PreviewImage => previewImage;
    public string Description => description != null ? description.Trim() : string.Empty;
    public string SceneName => sceneName != null ? sceneName.Trim() : string.Empty;
    public bool IsAvailable => isAvailable;

    public bool HasLoadableScene
    {
        get
        {
            if (string.IsNullOrEmpty(SceneName))
                return false;
            if (Application.CanStreamedLevelBeLoaded(SceneName))
                return true;

            int count = SceneManager.sceneCountInBuildSettings;
            for (int i = 0; i < count; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrEmpty(path))
                    continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path) == SceneName)
                    return true;
            }

            return false;
        }
    }

    public bool CanStartMatch => IsAvailable && HasLoadableScene;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (sceneAsset != null)
            sceneName = sceneAsset.name;
    }
#endif
}
