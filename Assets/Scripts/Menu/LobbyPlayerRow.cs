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
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(0f, 36f);

        LayoutElement layout = image.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 36f;
        layout.preferredHeight = 36f;
        layout.flexibleWidth = 1f;

        HorizontalLayoutGroup group = image.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.padding = new RectOffset(10, 10, 2, 2);
        group.spacing = 8f;
        group.childAlignment = TextAnchor.MiddleLeft;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = false;
        group.childForceExpandHeight = true;

        LobbyPlayerRow row = image.gameObject.AddComponent<LobbyPlayerRow>();
        row.nameLabel = CreateCell(image.transform, "Name", 16, TextAnchor.MiddleLeft, 72f, 1f);
        row.tagLabel = CreateCell(image.transform, "Tag", 13, TextAnchor.MiddleLeft, 64f, 0f);
        row.tagLabel.color = ProfileUiFactory.MutedColor;
        row.youLabel = CreateCell(image.transform, "You", 13, TextAnchor.MiddleCenter, 40f, 0f);
        row.youLabel.color = new Color(0.7f, 0.85f, 1f, 1f);
        row.hostLabel = CreateCell(image.transform, "Host", 13, TextAnchor.MiddleRight, 48f, 0f);
        row.hostLabel.color = MenuUiFactory.SelectedGreen;
        row.readyLabel = CreateCell(image.transform, "Attribute", 13, TextAnchor.MiddleRight, 0f, 0f);
        row.readyLabel.color = ProfileUiFactory.MutedColor;
        return row;
    }

    private static Text CreateCell(Transform parent, string name, int size, TextAnchor alignment, float width, float flexibleWidth)
    {
        Text label = MenuUiFactory.CreateLabel(parent, name, string.Empty, size, Vector2.zero, new Vector2(Mathf.Max(width, 8f), 32f), alignment);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;

        LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = Mathf.Max(width, 8f);
        layout.flexibleWidth = flexibleWidth;
        if (width <= 0f)
        {
            layout.minWidth = 0f;
            layout.preferredWidth = 0f;
        }

        return label;
    }

    public void Bind(LobbyPlayerInfo info)
    {
        if (info == null)
        {
            BindWaiting();
            return;
        }

        SetCell(nameLabel, string.IsNullOrWhiteSpace(info.DisplayName)
            ? PublicPlayerTagUtility.FallbackDisplayName(info.PublicTag)
            : info.DisplayName, true);
        SetCell(tagLabel, PublicPlayerTagUtility.Format(info.PublicTag), false);
        SetCell(youLabel, info.IsLocal ? "YOU" : string.Empty, false);
        SetCell(hostLabel, info.IsHost ? "HOST" : string.Empty, false);
        SetCell(readyLabel, string.Empty, false);
        nameLabel.color = Color.white;
    }

    public void BindWaiting()
    {
        SetCell(nameLabel, "Waiting...", true);
        nameLabel.color = ProfileUiFactory.MutedColor;
        SetCell(tagLabel, string.Empty, false);
        SetCell(youLabel, string.Empty, false);
        SetCell(hostLabel, string.Empty, false);
        SetCell(readyLabel, string.Empty, false);
    }

    private static void SetCell(Text label, string text, bool alwaysVisible)
    {
        if (label == null)
            return;

        label.text = text ?? string.Empty;
        bool show = alwaysVisible || !string.IsNullOrEmpty(label.text);
        label.gameObject.SetActive(show);
    }
}
