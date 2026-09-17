using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One map catalog card. Focus/hover updates preview; clicking selects the map.
/// </summary>
public class MapCardView : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    private MapDefinition definition;
    private Button button;
    private Image thumbnail;
    private Text nameLabel;
    private Text statusLabel;
    private Outline chosenOutline;
    private Action<MapCardView> onFocused;
    private Action<MapCardView> onSelected;

    public MapDefinition Definition => definition;
    public Button Button => button;

    public static MapCardView Create(
        Transform parent,
        MapDefinition definition,
        Vector2 position,
        Vector2 size,
        Action<MapCardView> focused,
        Action<MapCardView> selected)
    {
        Image image = MenuUiFactory.CreateImage(parent, definition != null ? definition.MapId : "MapCard", MenuUiFactory.ButtonColor);
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = position;

        MapCardView card = image.gameObject.AddComponent<MapCardView>();
        card.definition = definition;
        card.onFocused = focused;
        card.onSelected = selected;
        card.button = image.gameObject.AddComponent<Button>();
        card.button.colors = MenuUiFactory.MenuColors(MenuUiFactory.ButtonColor);
        card.button.onClick.AddListener(card.HandleClick);
        MenuUiFactory.WirePointerFocus(card.button);

        Image thumb = MenuUiFactory.CreateImage(image.transform, "Thumb", new Color(0.18f, 0.2f, 0.24f, 1f));
        RectTransform thumbRect = thumb.rectTransform;
        thumbRect.anchorMin = new Vector2(0.08f, 0.32f);
        thumbRect.anchorMax = new Vector2(0.92f, 0.92f);
        thumbRect.offsetMin = Vector2.zero;
        thumbRect.offsetMax = Vector2.zero;
        thumb.preserveAspect = true;
        card.thumbnail = thumb;

        card.nameLabel = MenuUiFactory.CreateLabel(image.transform, "Name", definition != null ? definition.DisplayName : "Map", 16, new Vector2(0f, -size.y * 0.32f), new Vector2(size.x - 12f, 28f));
        card.statusLabel = MenuUiFactory.CreateLabel(image.transform, "Status", string.Empty, 14, new Vector2(0f, -size.y * 0.42f), new Vector2(size.x - 12f, 22f));
        card.statusLabel.color = new Color(1f, 0.78f, 0.35f, 1f);

        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = MenuUiFactory.SelectedGreen;
        outline.effectDistance = new Vector2(4f, -4f);
        outline.useGraphicAlpha = false;
        outline.enabled = false;
        card.chosenOutline = outline;

        card.RefreshVisual(false);
        return card;
    }

    public void RefreshVisual(bool selected)
    {
        bool available = definition != null && definition.CanStartMatch;
        if (thumbnail != null)
            thumbnail.sprite = definition != null ? definition.PreviewImage : null;
        if (nameLabel != null)
            nameLabel.text = definition != null ? definition.DisplayName : "Map";
        if (statusLabel != null)
            statusLabel.text = available ? string.Empty : "COMING SOON";
        if (chosenOutline != null)
            chosenOutline.enabled = selected;

        Color normal = selected ? MenuUiFactory.SelectedGreen : MenuUiFactory.ButtonColor;
        if (button != null)
            button.colors = MenuUiFactory.MenuColors(normal);
    }

    public void OnSelect(BaseEventData eventData)
    {
        onFocused?.Invoke(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        onFocused?.Invoke(this);
    }

    private void HandleClick()
    {
        onSelected?.Invoke(this);
    }
}
