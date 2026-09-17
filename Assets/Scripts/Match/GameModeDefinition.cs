using UnityEngine;

[CreateAssetMenu(
    fileName = "GameModeDefinition",
    menuName = "Bullseye/Match/Game Mode Definition")]
public class GameModeDefinition : ScriptableObject
{
    [SerializeField] private string gameModeId = "mode_ffa";
    [SerializeField] private string displayName = "Game Mode";
    [SerializeField, TextArea(2, 4)] private string shortDescription = string.Empty;
    [SerializeField] private bool isAvailable;

    public string GameModeId => string.IsNullOrWhiteSpace(gameModeId) ? name : gameModeId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? GameModeId : displayName.Trim();
    public string ShortDescription => shortDescription != null ? shortDescription.Trim() : string.Empty;
    public bool IsAvailable => isAvailable;
}
