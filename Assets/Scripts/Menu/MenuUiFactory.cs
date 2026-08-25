using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared pause/main-menu widget factory so both menus can look and navigate the same way.
/// </summary>
public static class MenuUiFactory
{
    public static Color PanelColor => new Color(0.08f, 0.09f, 0.12f, 0.94f);
    public static Color ButtonColor => new Color(0.16f, 0.18f, 0.24f, 1f);
    public static Color SelectedGreen => new Color(0.35f, 0.82f, 0.42f, 1f);

    public static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color? color = null)
    {
        Image panel = CreateImage(parent, name, color ?? PanelColor);
        panel.rectTransform.sizeDelta = size;
        panel.rectTransform.anchoredPosition = Vector2.zero;
        return panel.gameObject;
    }

    public static void DockLeft(GameObject panel, float leftPadding)
    {
        if (panel == null)
            return;

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(leftPadding, 0f);
    }

    public static Image CreateLeftScrim(Transform parent, float widthNormalized = 0.46f)
    {
        Image image = CreateImage(parent, "LeftScrim", Color.white);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Clamp01(widthNormalized), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0f, 0.5f);
        image.sprite = CreateHorizontalFadeSprite();
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
        return image;
    }

    public static Sprite CreateHorizontalFadeSprite()
    {
        const int width = 64;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int x = 0; x < width; x++)
        {
            float t = x / (width - 1f);
            float alpha = Mathf.SmoothStep(0.88f, 0f, Mathf.InverseLerp(0.55f, 1f, t));
            texture.SetPixel(x, 0, new Color(0.025f, 0.03f, 0.045f, alpha));
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, 1f), new Vector2(0f, 0.5f), 1f);
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    public static Text CreateLabel(Transform parent, string name, string text, int size, Vector2 position, Vector2 sizeDelta, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = position;
        Text label = go.GetComponent<Text>();
        label.font = ResolveUiFont();
        label.fontSize = size;
        label.alignment = alignment;
        label.color = Color.white;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
    {
        Vector2 buttonSize = size ?? new Vector2(280f, 54f);
        Image image = CreateImage(parent, name, ButtonColor);
        image.rectTransform.sizeDelta = buttonSize;
        image.rectTransform.anchoredPosition = position;

        Button button = image.gameObject.AddComponent<Button>();
        button.colors = MenuColors(ButtonColor);
        button.onClick.AddListener(onClick);
        WirePointerFocus(button);
        CreateLabel(image.transform, "Label", label, 24, Vector2.zero, buttonSize);
        return button;
    }

    public static Slider CreateLabeledSlider(Transform parent, string label, float y, float min, float max, UnityEngine.Events.UnityAction<float> onChanged)
    {
        CreateLabel(parent, label + "Label", label, 18, new Vector2(0f, y + 22f), new Vector2(520f, 24f));

        Image track = CreateImage(parent, label + "Track", new Color(0.12f, 0.12f, 0.14f, 1f));
        track.rectTransform.sizeDelta = new Vector2(420f, 18f);
        track.rectTransform.anchoredPosition = new Vector2(0f, y);

        Slider slider = track.gameObject.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = false;
        slider.colors = MenuColors(Color.white);

        Image fill = CreateImage(track.transform, "Fill", new Color(0.22f, 0.72f, 0.34f, 1f));
        Stretch(fill.rectTransform);
        Image handle = CreateImage(track.transform, "Handle", Color.white);
        handle.rectTransform.sizeDelta = new Vector2(18f, 24f);

        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(track.transform, false);
        Stretch(fillArea);
        fill.rectTransform.SetParent(fillArea, false);
        Stretch(fill.rectTransform);

        RectTransform handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(track.transform, false);
        Stretch(handleArea);
        handle.rectTransform.SetParent(handleArea, false);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.onValueChanged.AddListener(onChanged);
        WirePointerFocus(slider);
        return slider;
    }

    public static Toggle CreateToggle(Transform parent, string label, float y, UnityEngine.Events.UnityAction<bool> onChanged)
    {
        CreateLabel(parent, label + "Label", label, 18, new Vector2(-40f, y), new Vector2(200f, 28f));
        Image box = CreateImage(parent, label + "Box", new Color(0.12f, 0.12f, 0.14f, 1f));
        box.rectTransform.sizeDelta = new Vector2(28f, 28f);
        box.rectTransform.anchoredPosition = new Vector2(80f, y);

        Image check = CreateImage(box.transform, "Check", new Color(0.22f, 0.86f, 0.32f, 1f));
        Stretch(check.rectTransform);
        check.rectTransform.offsetMin = new Vector2(4f, 4f);
        check.rectTransform.offsetMax = new Vector2(-4f, -4f);

        Toggle toggle = box.gameObject.AddComponent<Toggle>();
        toggle.graphic = check;
        toggle.targetGraphic = box;
        toggle.colors = MenuColors(new Color(0.12f, 0.12f, 0.14f, 1f));
        toggle.onValueChanged.AddListener(onChanged);
        WirePointerFocus(toggle);
        return toggle;
    }

    public static InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(parent, name, new Color(0.12f, 0.12f, 0.14f, 1f));
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = position;

        Text placeholderLabel = CreateLabel(image.transform, "Placeholder", placeholder, 22, Vector2.zero, size, TextAnchor.MiddleLeft);
        placeholderLabel.color = new Color(1f, 1f, 1f, 0.35f);
        placeholderLabel.raycastTarget = false;
        RectTransform placeholderRect = placeholderLabel.rectTransform;
        placeholderRect.offsetMin = new Vector2(16f, 0f);
        placeholderRect.offsetMax = new Vector2(-16f, 0f);

        Text text = CreateLabel(image.transform, "Text", string.Empty, 22, Vector2.zero, size, TextAnchor.MiddleLeft);
        text.supportRichText = false;
        text.raycastTarget = false;
        RectTransform textRect = text.rectTransform;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);

        InputField input = image.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholderLabel;
        input.characterLimit = 32;
        input.caretColor = Color.white;
        input.selectionColor = new Color(0.25f, 0.55f, 0.32f, 0.65f);
        input.colors = MenuColors(new Color(0.12f, 0.12f, 0.14f, 1f));
        WirePointerFocus(input);
        return input;
    }

    public static void SetVerticalNav(Selectable current, Selectable up, Selectable down)
    {
        if (current == null)
            return;

        Navigation navigation = current.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        current.navigation = navigation;
    }

    public static void SetHorizontalNav(Selectable current, Selectable left, Selectable right)
    {
        if (current == null)
            return;

        Navigation navigation = current.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnLeft = left;
        navigation.selectOnRight = right;
        current.navigation = navigation;
    }

    public static void SetNav(Selectable current, Selectable up, Selectable down, Selectable left = null, Selectable right = null)
    {
        if (current == null)
            return;

        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
        current.navigation = navigation;
    }

    public static void WirePointerFocus(Selectable selectable)
    {
        if (selectable == null)
            return;

        EventTrigger trigger = selectable.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = selectable.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        });
        trigger.triggers.Add(entry);
    }

    public static ColorBlock MenuColors(Color normal)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normal;
        colors.highlightedColor = new Color(0.25f, 0.55f, 0.32f, 1f);
        colors.selectedColor = SelectedGreen;
        colors.pressedColor = new Color(0.18f, 0.4f, 0.24f, 1f);
        colors.disabledColor = new Color(0.12f, 0.12f, 0.14f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    public static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = UiWhiteSprite.Get();
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return image;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    public static void SetButtonSelectedVisual(Button button, bool selected)
    {
        if (button == null)
            return;

        Color normal = selected ? SelectedGreen : ButtonColor;
        button.colors = MenuColors(normal);
        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = normal;
    }

    public static Font ResolveUiFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            return font;

        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null)
            return font;

        string[] names = Font.GetOSInstalledFontNames();
        if (names != null && names.Length > 0)
            return Font.CreateDynamicFontFromOSFont(names[0], 16);

        return null;
    }
}
