using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime builders for Profile screen widgets. Matches MainMenuController
/// visuals so career UI feels like the rest of the menus.
/// </summary>
public static class ProfileUiFactory
{
    public static readonly Color SectionColor = new Color(0.55f, 0.9f, 0.62f, 1f);
    public static readonly Color LabelColor = new Color(0.82f, 0.85f, 0.9f, 1f);
    public static readonly Color CardColor = new Color(0.12f, 0.14f, 0.18f, 0.92f);
    public static readonly Color MutedColor = new Color(0.7f, 0.73f, 0.78f, 1f);

    public static ScrollRect CreateScrollArea(Transform parent, string name, Vector2 position, Vector2 size)
    {
        Image root = MenuUiFactory.CreateImage(parent, name, new Color(0.04f, 0.05f, 0.07f, 0.4f));
        root.rectTransform.sizeDelta = size;
        root.rectTransform.anchoredPosition = position;

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        scroll.inertia = true;

        Image viewport = MenuUiFactory.CreateImage(root.transform, "Viewport", new Color(1f, 1f, 1f, 0.02f));
        MenuUiFactory.Stretch(viewport.rectTransform);
        viewport.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 2f;
        layout.padding = new RectOffset(20, 20, 8, 16);
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.rectTransform;
        scroll.content = contentRect;
        return scroll;
    }

    public static Text CreateSectionHeader(Transform parent, string name, string text)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 42f;
        layout.preferredHeight = 42f;

        Text label = go.GetComponent<Text>();
        label.font = MenuUiFactory.ResolveUiFont();
        label.fontSize = 26;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = SectionColor;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    public static ProfileStatRow CreateStatRow(Transform parent, string name, string label)
    {
        Image image = MenuUiFactory.CreateImage(parent, name, Color.clear);
        image.raycastTarget = false;
        LayoutElement layout = image.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 34f;
        layout.preferredHeight = 34f;

        HorizontalLayoutGroup group = image.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlHeight = true;
        group.childControlWidth = true;
        group.childForceExpandHeight = true;
        group.childForceExpandWidth = true;
        group.spacing = 12f;

        Text labelText = CreateFlexLabel(image.transform, "Label", label, 22, TextAnchor.MiddleLeft, LabelColor);
        Text valueText = CreateFlexLabel(image.transform, "Value", ProfileStatFormatter.EmDash, 24, TextAnchor.MiddleRight, Color.white);
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.minWidth = 180f;
        valueLayout.preferredWidth = 220f;
        valueLayout.flexibleWidth = 0.35f;
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;

        ProfileStatRow row = image.gameObject.AddComponent<ProfileStatRow>();
        row.Bind(labelText, valueText);
        return row;
    }

    public static Text CreateBodyLabel(Transform parent, string name, string text, int size, TextAnchor alignment, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
        go.transform.SetParent(parent, false);
        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;

        Text label = go.GetComponent<Text>();
        label.font = MenuUiFactory.ResolveUiFont();
        label.fontSize = size;
        label.alignment = alignment;
        label.color = MutedColor;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    public static WeaponProfileEntry CreateWeaponEntry(Transform parent, string name)
    {
        Image card = MenuUiFactory.CreateImage(parent, name, CardColor);
        LayoutElement cardLayout = card.gameObject.AddComponent<LayoutElement>();
        cardLayout.minHeight = 380f;
        cardLayout.preferredHeight = 380f;

        VerticalLayoutGroup group = card.gameObject.AddComponent<VerticalLayoutGroup>();
        group.childAlignment = TextAnchor.UpperCenter;
        group.padding = new RectOffset(16, 16, 10, 12);
        group.spacing = 0f;
        group.childControlHeight = true;
        group.childControlWidth = true;
        group.childForceExpandHeight = false;
        group.childForceExpandWidth = true;

        Button button = card.gameObject.AddComponent<Button>();
        button.colors = MenuUiFactory.MenuColors(CardColor);
        button.targetGraphic = card;
        button.transition = Selectable.Transition.ColorTint;
        MenuUiFactory.WirePointerFocus(button);

        Text weaponName = CreateBodyLabel(card.transform, "WeaponName", "Weapon", 28, TextAnchor.MiddleLeft, 36f);
        weaponName.color = Color.white;
        weaponName.fontStyle = FontStyle.Bold;

        ProfileStatRow eliminations = CreateStatRow(card.transform, "Eliminations", "Eliminations");
        ProfileStatRow shotsFired = CreateStatRow(card.transform, "ShotsFired", "Shots Fired");
        ProfileStatRow shotsHit = CreateStatRow(card.transform, "ShotsHit", "Shots Hit");
        ProfileStatRow accuracy = CreateStatRow(card.transform, "Accuracy", "Accuracy");
        ProfileStatRow damage = CreateStatRow(card.transform, "Damage", "Damage Dealt");
        ProfileStatRow bullseye = CreateStatRow(card.transform, "BullseyeHits", "Bullseye Hits");
        ProfileStatRow head = CreateStatRow(card.transform, "HeadHits", "Head Hits");
        ProfileStatRow body = CreateStatRow(card.transform, "BodyHits", "Body Hits");
        ProfileStatRow longest = CreateStatRow(card.transform, "Longest", "Longest Elimination");

        WeaponProfileEntry entry = card.gameObject.AddComponent<WeaponProfileEntry>();
        entry.BindWidgets(
            button,
            weaponName,
            eliminations,
            shotsFired,
            shotsHit,
            accuracy,
            damage,
            bullseye,
            head,
            body,
            longest);
        return entry;
    }

    private static Text CreateFlexLabel(Transform parent, string name, string text, int size, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>();
        label.font = MenuUiFactory.ResolveUiFont();
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.text = text;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }
}
