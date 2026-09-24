using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Upper-right monitor that displays the bullseye tracking camera's RenderTexture.
/// Layout is anchor-based so it can be resized without screen-pixel hardcoding.
/// </summary>
public class BullseyeCameraHud : MonoBehaviour
{
    private static readonly Color BorderColor = new Color(0.78f, 0.82f, 0.86f, 0.55f);
    private static readonly Color BackdropColor = new Color(0.02f, 0.025f, 0.03f, 0.92f);
    private static readonly Color BracketColor = new Color(0.95f, 0.18f, 0.16f, 0.85f);

    [Header("Layout")]
    [SerializeField] private Vector2 monitorSize = new Vector2(270f, 300f);
    [SerializeField] private Vector2 margin = new Vector2(24f, 24f);
    [SerializeField] private float borderThickness = 3f;

    [Header("Optional")]
    [SerializeField] private bool showCenterBrackets;

    private Canvas canvas;
    private RawImage feedImage;
    private bool built;

    public Vector2 MonitorSize => monitorSize;

    public void SetFeed(Texture texture)
    {
        EnsureUi();
        if (feedImage != null)
            feedImage.texture = texture;
    }

    public void SetVisible(bool visible)
    {
        if (!visible && !built)
            return;

        EnsureUi();
        if (canvas != null && canvas.gameObject.activeSelf != visible)
            canvas.gameObject.SetActive(visible);
    }

    private void EnsureUi()
    {
        if (built)
            return;

        GameObject canvasObject = new GameObject(
            "BullseyeCameraCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 22;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform anchor = CreateRect(canvasObject.transform, "HudAnchor");
        anchor.anchorMin = Vector2.one;
        anchor.anchorMax = Vector2.one;
        anchor.pivot = Vector2.one;
        anchor.sizeDelta = monitorSize + Vector2.one * (borderThickness * 2f);
        anchor.anchoredPosition = new Vector2(-margin.x, -margin.y);

        Image border = CreateImage(anchor, "Border", BorderColor);
        Stretch(border.rectTransform);

        RectTransform panel = CreateRect(anchor, "Panel");
        Stretch(panel);
        panel.offsetMin = Vector2.one * borderThickness;
        panel.offsetMax = Vector2.one * -borderThickness;

        Image backdrop = CreateImage(panel, "Backdrop", BackdropColor);
        Stretch(backdrop.rectTransform);

        GameObject feedObject = new GameObject("Feed", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        feedObject.transform.SetParent(panel, false);
        feedImage = feedObject.GetComponent<RawImage>();
        feedImage.raycastTarget = false;
        feedImage.color = Color.white;
        Stretch(feedImage.rectTransform);

        if (showCenterBrackets)
            CreateBrackets(panel);

        built = true;
        canvas.gameObject.SetActive(false);
    }

    private void CreateBrackets(RectTransform parent)
    {
        const float length = 14f;
        const float thickness = 2f;
        const float inset = 8f;
        CreateBracket(parent, new Vector2(0f, 1f), new Vector2(inset, -inset), new Vector2(length, thickness));
        CreateBracket(parent, new Vector2(0f, 1f), new Vector2(inset, -inset), new Vector2(thickness, length));
        CreateBracket(parent, new Vector2(1f, 1f), new Vector2(-inset, -inset), new Vector2(length, thickness));
        CreateBracket(parent, new Vector2(1f, 1f), new Vector2(-inset, -inset), new Vector2(thickness, length));
        CreateBracket(parent, new Vector2(0f, 0f), new Vector2(inset, inset), new Vector2(length, thickness));
        CreateBracket(parent, new Vector2(0f, 0f), new Vector2(inset, inset), new Vector2(thickness, length));
        CreateBracket(parent, new Vector2(1f, 0f), new Vector2(-inset, inset), new Vector2(length, thickness));
        CreateBracket(parent, new Vector2(1f, 0f), new Vector2(-inset, inset), new Vector2(thickness, length));
    }

    private static void CreateBracket(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image mark = CreateImage(parent, "Bracket", BracketColor);
        RectTransform rect = mark.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = UiWhiteSprite.Get();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnValidate()
    {
        monitorSize.x = Mathf.Max(96f, monitorSize.x);
        monitorSize.y = Mathf.Max(96f, monitorSize.y);
        margin.x = Mathf.Max(0f, margin.x);
        margin.y = Mathf.Max(0f, margin.y);
        borderThickness = Mathf.Clamp(borderThickness, 1f, 12f);
    }
}
