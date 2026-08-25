using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owner-only Body HUD. Visualizes the local player's live bullseye location
/// and grenade-detach vulnerability. Does not simulate a second bullseye or
/// decide when the bullseye should detach.
/// </summary>
public class BullseyeBodyHud : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.11f, 0.82f);
    private static readonly Color BorderColor = new Color(0.82f, 0.84f, 0.88f, 0.42f);
    private static readonly Color TitleColor = new Color(0.93f, 0.94f, 0.96f, 1f);
    private static readonly Color MutedLabelColor = new Color(0.78f, 0.80f, 0.84f, 0.9f);
    private static readonly Color InactiveBodyColor = new Color(0.82f, 0.84f, 0.88f, 0.38f);
    private static readonly Color ActiveBodyColor = new Color(0.94f, 0.95f, 0.97f, 1f);

    [Header("Sources")]
    [SerializeField] private BullseyeBodyPositionMapper positionMapper;
    [SerializeField] private BullseyeDetachController detachController;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Artwork")]
    [Tooltip("FRONT body diagram. Drag a replacement sprite here.")]
    [SerializeField] private Sprite frontBodySprite;
    [Tooltip("BACK body diagram. Drag a replacement sprite here.")]
    [SerializeField] private Sprite backBodySprite;
    [Tooltip("Bullseye marker. Drag a replacement sprite here.")]
    [SerializeField] private Sprite markerSprite;

    [Header("Body Map")]
    [Tooltip("Normalized marker area inside each silhouette. Origin is bottom-left.")]
    [SerializeField] private Rect bodyMapNormalized = BullseyeBodyHudPlaceholders.BodyMapNormalized;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(320f, 236f);
    [SerializeField] private Vector2 margin = new Vector2(20f, 20f);
    [SerializeField] private float markerSize = 28f;

    [Header("Vulnerability")]
    [SerializeField] private bool showVulnerableLabel = true;
    [SerializeField] private Color flashColor = new Color(0.92f, 0.12f, 0.14f, 0.88f);
    [SerializeField] private float flashSpeed = 3.4f;

    private Canvas canvas;
    private Image borderImage;
    private Image panelImage;
    private Image frontBodyImage;
    private Image backBodyImage;
    private Image frontMarker;
    private Image backMarker;
    private Text titleLabel;
    private Text frontLabel;
    private Text backLabel;
    private Text vulnerableLabel;
    private bool built;
    private bool ownsGeneratedArt;
    private Sprite generatedFront;
    private Sprite generatedBack;
    private Sprite generatedMarker;

    private void Awake()
    {
        ResolveSources();
    }

    private void OnDestroy()
    {
        DestroyGeneratedArt();
    }

    private void LateUpdate()
    {
        if (!ShouldDisplay())
        {
            SetVisible(false);
            return;
        }

        EnsureUi();
        SetVisible(true);
        Refresh();
    }

    private void ResolveSources()
    {
        if (positionMapper == null)
            positionMapper = GetComponent<BullseyeBodyPositionMapper>();
        if (detachController == null)
            detachController = GetComponent<BullseyeDetachController>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    private bool ShouldDisplay()
    {
        ResolveSources();

        if (playerHealth == null || !playerHealth.IsSpawned || !playerHealth.IsOwner)
            return false;

        if (playerHealth.IsDead)
            return false;

        if (LocalPlayerMenuState.IsOpen(this))
            return false;

        return true;
    }

    private bool IsVulnerable()
    {
        return detachController != null && !detachController.IsAttached;
    }

    private void Refresh()
    {
        bool vulnerable = IsVulnerable();
        ApplyVulnerability(vulnerable);

        bool mapped = false;
        BullseyeBodyPosition mappedPosition = default;
        if (!vulnerable && positionMapper != null)
            mapped = positionMapper.TryMap(out mappedPosition);

        if (!mapped)
        {
            SetMarker(frontMarker, false, Vector2.zero);
            SetMarker(backMarker, false, Vector2.zero);
            TintBody(frontBodyImage, false, vulnerable);
            TintBody(backBodyImage, false, vulnerable);
            TintLabel(frontLabel, false);
            TintLabel(backLabel, false);
            return;
        }

        bool front = mappedPosition.Facing == BullseyeFacing.Front;
        Vector2 frontPos = ToMarkerAnchored(mappedPosition, frontBodyImage);
        Vector2 backPos = ToMarkerAnchored(mappedPosition, backBodyImage);
        SetMarker(frontMarker, front, frontPos);
        SetMarker(backMarker, !front, backPos);
        TintBody(frontBodyImage, front, false);
        TintBody(backBodyImage, !front, false);
        TintLabel(frontLabel, front);
        TintLabel(backLabel, !front);
    }

    private void ApplyVulnerability(bool vulnerable)
    {
        float pulse = 0f;
        if (vulnerable)
            pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * flashSpeed * Mathf.PI);

        if (borderImage != null)
            borderImage.color = Color.Lerp(BorderColor, flashColor, pulse);
        if (titleLabel != null)
            titleLabel.color = Color.Lerp(TitleColor, flashColor, pulse);
        if (panelImage != null)
        {
            Color rest = PanelColor;
            Color alert = new Color(flashColor.r * 0.35f, flashColor.g * 0.08f, flashColor.b * 0.08f, 0.9f);
            panelImage.color = Color.Lerp(rest, alert, pulse * 0.85f);
        }

        if (vulnerableLabel != null)
        {
            bool show = showVulnerableLabel && vulnerable;
            vulnerableLabel.enabled = show;
            if (show)
            {
                Color color = flashColor;
                color.a = 0.55f + 0.45f * pulse;
                vulnerableLabel.color = color;
            }
        }
    }

    private void TintBody(Image image, bool active, bool vulnerable)
    {
        if (image == null)
            return;

        if (vulnerable)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * flashSpeed * Mathf.PI);
            image.color = Color.Lerp(ActiveBodyColor, flashColor, pulse * 0.55f);
            return;
        }

        image.color = active ? ActiveBodyColor : InactiveBodyColor;
    }

    private static void TintLabel(Text label, bool active)
    {
        if (label == null)
            return;

        label.color = active ? TitleColor : MutedLabelColor;
    }

    private Vector2 ToMarkerAnchored(in BullseyeBodyPosition mapped, Image bodyImage)
    {
        if (bodyImage == null)
            return Vector2.zero;

        Rect map = bodyMapNormalized;
        if (map.width <= 0.01f || map.height <= 0.01f)
            map = BullseyeBodyHudPlaceholders.BodyMapNormalized;

        float u = Mathf.Lerp(map.xMin, map.xMax, 0.5f + mapped.NormalizedLateral * 0.5f);
        float v = Mathf.Lerp(map.yMin, map.yMax, mapped.NormalizedHeight);

        Rect rect = bodyImage.rectTransform.rect;
        float displayWidth = rect.width;
        float displayHeight = rect.height;
        Sprite sprite = bodyImage.sprite;
        if (bodyImage.preserveAspect && sprite != null && sprite.rect.height > 0.01f)
        {
            float spriteAspect = sprite.rect.width / sprite.rect.height;
            float rectAspect = rect.width / Mathf.Max(0.01f, rect.height);
            if (rectAspect > spriteAspect)
            {
                displayWidth = rect.height * spriteAspect;
                displayHeight = rect.height;
            }
            else
            {
                displayWidth = rect.width;
                displayHeight = rect.width / spriteAspect;
            }
        }

        return new Vector2((u - 0.5f) * displayWidth, (v - 0.5f) * displayHeight);
    }

    private static void SetMarker(Image marker, bool visible, Vector2 anchoredPosition)
    {
        if (marker == null)
            return;

        marker.enabled = visible;
        if (visible)
            marker.rectTransform.anchoredPosition = anchoredPosition;
    }

    private void EnsureUi()
    {
        if (built)
            return;

        GameObject canvasObject = new GameObject(
            "BullseyeBodyCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 21;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject anchorObject = new GameObject("HudAnchor", typeof(RectTransform));
        anchorObject.transform.SetParent(canvasObject.transform, false);
        RectTransform hudAnchor = anchorObject.GetComponent<RectTransform>();
        hudAnchor.anchorMin = Vector2.one;
        hudAnchor.anchorMax = Vector2.one;
        hudAnchor.pivot = Vector2.one;
        hudAnchor.sizeDelta = panelSize + new Vector2(8f, 8f);
        hudAnchor.anchoredPosition = new Vector2(-margin.x, -margin.y);

        borderImage = CreateImage(hudAnchor, "Border", BorderColor, UiWhiteSprite.Get());
        Stretch(borderImage.rectTransform);

        GameObject panelObject = new GameObject("Panel", typeof(RectTransform));
        panelObject.transform.SetParent(hudAnchor, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        Stretch(panelRect);
        panelRect.offsetMin = new Vector2(3f, 3f);
        panelRect.offsetMax = new Vector2(-3f, -3f);

        panelImage = panelObject.AddComponent<Image>();
        panelImage.sprite = UiWhiteSprite.Get();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        titleLabel = CreateLabel(panelRect, "Title", "YOUR BULLSEYE", 16, TextAnchor.UpperCenter);
        titleLabel.color = TitleColor;
        titleLabel.fontStyle = FontStyle.Bold;
        RectTransform titleRect = titleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -8f);
        titleRect.sizeDelta = new Vector2(-16f, 22f);

        vulnerableLabel = CreateLabel(panelRect, "Vulnerable", "VULNERABLE", 13, TextAnchor.UpperCenter);
        vulnerableLabel.fontStyle = FontStyle.Bold;
        RectTransform vulnerableRect = vulnerableLabel.rectTransform;
        vulnerableRect.anchorMin = new Vector2(0f, 1f);
        vulnerableRect.anchorMax = new Vector2(1f, 1f);
        vulnerableRect.pivot = new Vector2(0.5f, 1f);
        vulnerableRect.anchoredPosition = new Vector2(0f, -28f);
        vulnerableRect.sizeDelta = new Vector2(-16f, 18f);
        vulnerableLabel.enabled = false;

        RectTransform columns = CreateRect(panelRect, "Columns");
        columns.anchorMin = new Vector2(0f, 0f);
        columns.anchorMax = new Vector2(1f, 1f);
        columns.offsetMin = new Vector2(12f, 10f);
        columns.offsetMax = new Vector2(-12f, -48f);

        CreateBodyColumn(columns, true, out frontLabel, out frontBodyImage, out frontMarker);
        CreateBodyColumn(columns, false, out backLabel, out backBodyImage, out backMarker);

        built = true;
        SetVisible(false);
    }

    private void CreateBodyColumn(
        RectTransform parent,
        bool front,
        out Text label,
        out Image bodyImage,
        out Image marker)
    {
        string name = front ? "Front" : "Back";
        RectTransform column = CreateRect(parent, name + "Column");
        column.anchorMin = front ? new Vector2(0f, 0f) : new Vector2(0.5f, 0f);
        column.anchorMax = front ? new Vector2(0.5f, 1f) : new Vector2(1f, 1f);
        column.offsetMin = front ? Vector2.zero : new Vector2(8f, 0f);
        column.offsetMax = front ? new Vector2(-8f, 0f) : Vector2.zero;

        label = CreateLabel(column, name + "Label", front ? "FRONT" : "BACK", 12, TextAnchor.UpperCenter);
        label.color = MutedLabelColor;
        label.fontStyle = FontStyle.Bold;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(0f, 18f);

        RectTransform bodyRect = CreateRect(column, name + "Body");
        bodyRect.anchorMin = new Vector2(0.08f, 0.02f);
        bodyRect.anchorMax = new Vector2(0.92f, 0.88f);
        bodyRect.offsetMin = Vector2.zero;
        bodyRect.offsetMax = Vector2.zero;

        Sprite bodySprite = ResolveBodySprite(front);
        bodyImage = bodyRect.gameObject.AddComponent<Image>();
        bodyImage.sprite = bodySprite != null ? bodySprite : UiWhiteSprite.Get();
        bodyImage.color = InactiveBodyColor;
        bodyImage.raycastTarget = false;
        bodyImage.preserveAspect = true;
        bodyImage.type = Image.Type.Simple;

        GameObject markerObject = new GameObject(name + "Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        markerObject.transform.SetParent(bodyRect, false);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(markerSize, markerSize);

        marker = markerObject.GetComponent<Image>();
        marker.sprite = ResolveMarkerSprite();
        marker.color = Color.white;
        marker.raycastTarget = false;
        marker.preserveAspect = true;
        marker.enabled = false;
    }

    private Sprite ResolveBodySprite(bool front)
    {
        if (front)
        {
            if (frontBodySprite != null)
                return frontBodySprite;

            generatedFront = BullseyeBodyHudPlaceholders.CreateFrontBody();
            ownsGeneratedArt = true;
            return generatedFront;
        }

        if (backBodySprite != null)
            return backBodySprite;

        generatedBack = BullseyeBodyHudPlaceholders.CreateBackBody();
        ownsGeneratedArt = true;
        return generatedBack;
    }

    private Sprite ResolveMarkerSprite()
    {
        if (markerSprite != null)
            return markerSprite;

        generatedMarker = BullseyeBodyHudPlaceholders.CreateMarker();
        ownsGeneratedArt = true;
        return generatedMarker;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateLabel(Transform parent, string name, string text, int size, TextAnchor alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>();
        label.font = MenuUiFactory.ResolveUiFont();
        label.fontSize = size;
        label.alignment = alignment;
        label.color = TitleColor;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible)
            canvas.gameObject.SetActive(visible);
    }

    private void DestroyGeneratedArt()
    {
        if (!ownsGeneratedArt)
            return;

        DestroySprite(ref generatedFront);
        DestroySprite(ref generatedBack);
        DestroySprite(ref generatedMarker);
        ownsGeneratedArt = false;
    }

    private static void DestroySprite(ref Sprite sprite)
    {
        if (sprite == null)
            return;

        Texture2D texture = sprite.texture;
        Destroy(sprite);
        if (texture != null)
            Destroy(texture);

        sprite = null;
    }

    private void OnValidate()
    {
        panelSize.x = Mathf.Max(180f, panelSize.x);
        panelSize.y = Mathf.Max(140f, panelSize.y);
        margin.x = Mathf.Max(0f, margin.x);
        margin.y = Mathf.Max(0f, margin.y);
        markerSize = Mathf.Max(10f, markerSize);
        flashSpeed = Mathf.Max(0.5f, flashSpeed);

        if (bodyMapNormalized.width <= 0.01f || bodyMapNormalized.height <= 0.01f)
            bodyMapNormalized = BullseyeBodyHudPlaceholders.BodyMapNormalized;
    }
}
