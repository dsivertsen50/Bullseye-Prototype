using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic lobby roster row. Ready is reserved for a later ready-up system.
/// </summary>
public class LobbyPlayerRow : MonoBehaviour
{
    private Text nameLabel;
    private Text tagLabel;
    private Text hostLabel;
    private Text youLabel;
    private Text readyLabel;

    public static LobbyPlayerRow Create(Transform parent)
    {
        Image image = MenuUiFactory.CreateImage(parent, "PlayerRow", new Color(0.12f, 0.14f, 0.18f, 0.92f));
        LayoutElement layout = image.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 44f;
        layout.preferredHeight = 44f;

        LobbyPlayerRow row = image.gameObject.AddComponent<LobbyPlayerRow>();
        row.nameLabel = MenuUiFactory.CreateLabel(image.transform, "Name", string.Empty, 20, new Vector2(-210f, 0f), new Vector2(280f, 36f), TextAnchor.MiddleLeft);
        row.tagLabel = MenuUiFactory.CreateLabel(image.transform, "Tag", string.Empty, 16, new Vector2(20f, 0f), new Vector2(90f, 36f), TextAnchor.MiddleLeft);
        row.tagLabel.color = ProfileUiFactory.MutedColor;
        row.youLabel = MenuUiFactory.CreateLabel(image.transform, "You", string.Empty, 16, new Vector2(140f, 0f), new Vector2(70f, 36f));
        row.youLabel.color = new Color(0.7f, 0.85f, 1f, 1f);
        row.hostLabel = MenuUiFactory.CreateLabel(image.transform, "Host", string.Empty, 16, new Vector2(220f, 0f), new Vector2(80f, 36f));
        row.hostLabel.color = MenuUiFactory.SelectedGreen;
        row.readyLabel = MenuUiFactory.CreateLabel(image.transform, "Ready", string.Empty, 14, new Vector2(300f, 0f), new Vector2(90f, 36f));
        row.readyLabel.color = ProfileUiFactory.MutedColor;
        return row;
    }

    public void Bind(LobbyPlayerInfo info)
    {
        if (info == null)
        {
            nameLabel.text = string.Empty;
            return;
        }

        nameLabel.text = string.IsNullOrWhiteSpace(info.DisplayName)
            ? PublicPlayerTagUtility.FallbackDisplayName(info.PublicTag)
            : info.DisplayName;
        tagLabel.text = PublicPlayerTagUtility.Format(info.PublicTag);
        youLabel.text = info.IsLocal ? "YOU" : string.Empty;
        hostLabel.text = info.IsHost ? "HOST" : string.Empty;
        readyLabel.text = string.Empty;
    }
}
