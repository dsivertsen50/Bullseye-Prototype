using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owner-only combat popups near the reticle. Displays gameplay results
/// passed in through <see cref="ShowCombatFeedback"/>. No network authority.
/// </summary>
public class CombatFeedbackHud : MonoBehaviour
{
    private static CombatFeedbackHud localOwner;

    [Header("Timing")]
    [SerializeField] private float damageTextDuration = 0.7f;
    [SerializeField] private float awardTextDuration = 1.15f;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Layout")]
    [SerializeField] private Vector2 reticleOffset = new Vector2(40f, 0f);
    [SerializeField] private float verticalOffset = 18f;
    [SerializeField] private float stackSpacing = 28f;
    [SerializeField] private int maximumSimultaneousMessages = 4;
    [SerializeField] private float rapidHitAccumulationWindow = 0.2f;

    [Header("Style")]
    [SerializeField] private int fontSize = 22;
    [SerializeField] private float outlineWidth = 1.15f;
    [SerializeField] private Color bullseyeRed = default;

    [Header("Labels")]
    [SerializeField] private string eliminationLabel = "ELIMINATION";
    [SerializeField] private CombatAwardLabel[] awardLabels =
    {
        new CombatAwardLabel { Award = CombatAwardType.DolphinDive, Text = "DOLPHIN DIVE" },
        new CombatAwardLabel { Award = CombatAwardType.DetachedBullseye, Text = "DETACHED BULLSEYE" },
        new CombatAwardLabel { Award = CombatAwardType.Ricochet, Text = "RICOCHET" }
    };

    private readonly List<FeedbackItem> items = new();
    private Canvas canvas;
    private RectTransform stackRoot;
    private PlayerHealth playerHealth;
    private NetworkObject networkObject;
    private bool built;
    private int nextSequence;

    public static void PresentLocal(CombatFeedbackEvent feedback)
    {
        if (localOwner != null)
            localOwner.ShowCombatFeedback(feedback);
    }

    public void ShowCombatFeedback(CombatFeedbackEvent feedback)
    {
        if (!IsLocalOwner())
            return;

        EnsureUi();
        if (feedback.IsPlainDamage && TryAccumulate(feedback))
            return;

        FeedbackItem item = RentItem();
        item.Sequence = nextSequence++;
        item.Priority = feedback.Priority;
        item.AccumulatesDamage = feedback.IsPlainDamage;
        item.Amount = feedback.Amount;
        item.HoldDuration = feedback.IsPlainDamage ? damageTextDuration : awardTextDuration;
        item.Age = 0f;
        item.Active = true;
        item.Label.text = Format(feedback);
        item.Root.gameObject.SetActive(true);
        ApplyStyle(item);
        TrimToMaximum();
        LayoutActiveItems();
    }

    private void Awake()
    {
        if (bullseyeRed.a <= 0f)
            bullseyeRed = BullseyeColors.UiRed;

        playerHealth = GetComponent<PlayerHealth>();
        networkObject = GetComponent<NetworkObject>();
    }

    private void OnEnable()
    {
        if (bullseyeRed.a <= 0f)
            bullseyeRed = BullseyeColors.UiRed;
    }

    private void OnDisable()
    {
        if (localOwner == this)
            localOwner = null;
    }

    private void OnDestroy()
    {
        if (localOwner == this)
            localOwner = null;
    }

    private void LateUpdate()
    {
        if (IsLocalOwner())
            localOwner = this;
        else if (localOwner == this)
            localOwner = null;

        if (!built)
            return;

        bool visible = IsLocalOwner() && !IsSuppressed();
        if (canvas != null && canvas.enabled != visible)
            canvas.enabled = visible;

        float delta = Time.unscaledDeltaTime;
        bool moved = false;
        for (int i = 0; i < items.Count; i++)
        {
            FeedbackItem item = items[i];
            if (!item.Active)
                continue;

            item.Age += delta;
            float fadeStart = item.HoldDuration;
            float end = fadeStart + fadeDuration;
            if (item.Age >= end)
            {
                Release(item);
                moved = true;
                continue;
            }

            float fade = item.Age <= fadeStart || fadeDuration <= 0f
                ? 1f
                : 1f - Mathf.Clamp01((item.Age - fadeStart) / fadeDuration);
            item.Group.alpha = fade;
            float rise = item.Age <= fadeStart
                ? 0f
                : verticalOffset * (1f - fade);
            item.Rise = rise;
            moved = true;
        }

        if (moved)
            LayoutActiveItems();
    }

    private bool TryAccumulate(CombatFeedbackEvent feedback)
    {
        FeedbackItem newest = null;
        for (int i = 0; i < items.Count; i++)
        {
            FeedbackItem candidate = items[i];
            if (!candidate.Active || !candidate.AccumulatesDamage)
                continue;

            if (newest == null || candidate.Sequence > newest.Sequence)
                newest = candidate;
        }

        if (newest == null || newest.Age > rapidHitAccumulationWindow)
            return false;

        newest.Amount += feedback.Amount;
        newest.Age = 0f;
        newest.Label.text = "+" + newest.Amount;
        return true;
    }

    private void TrimToMaximum()
    {
        int limit = Mathf.Max(1, maximumSimultaneousMessages);
        while (CountActive() > limit)
        {
            FeedbackItem victim = null;
            for (int i = 0; i < items.Count; i++)
            {
                FeedbackItem candidate = items[i];
                if (!candidate.Active)
                    continue;

                if (victim == null ||
                    candidate.Priority < victim.Priority ||
                    (candidate.Priority == victim.Priority && candidate.Sequence < victim.Sequence))
                {
                    victim = candidate;
                }
            }

            if (victim == null)
                return;

            Release(victim);
        }
    }

    private int CountActive()
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Active)
                count++;
        }

        return count;
    }

    private void LayoutActiveItems()
    {
        int shown = 0;
        int guard = items.Count;
        int previousSequence = int.MinValue;
        while (shown < guard)
        {
            FeedbackItem oldest = null;
            for (int i = 0; i < items.Count; i++)
            {
                FeedbackItem candidate = items[i];
                if (!candidate.Active || candidate.Sequence <= previousSequence)
                    continue;

                if (oldest == null || candidate.Sequence < oldest.Sequence)
                    oldest = candidate;
            }

            if (oldest == null)
                return;

            oldest.Rect.anchoredPosition = new Vector2(
                reticleOffset.x,
                reticleOffset.y - shown * stackSpacing + oldest.Rise);
            previousSequence = oldest.Sequence;
            shown++;
        }
    }

    private string Format(CombatFeedbackEvent feedback)
    {
        string text = "";
        if (feedback.Amount > 0)
            text = "+" + feedback.Amount;

        if (feedback.Type != CombatFeedbackType.Damage)
        {
            string elimination = string.IsNullOrWhiteSpace(eliminationLabel)
                ? "ELIMINATION"
                : eliminationLabel;
            text = string.IsNullOrEmpty(text) ? elimination : text + "  " + elimination;
        }

        AppendAward(ref text, feedback.Award);
        AppendAward(ref text, feedback.SecondaryAward);
        AppendAward(ref text, feedback.TertiaryAward);

        if (!string.IsNullOrWhiteSpace(feedback.CustomText))
            text = string.IsNullOrEmpty(text) ? feedback.CustomText : text + "\n" + feedback.CustomText;

        return text;
    }

    private void AppendAward(ref string text, CombatAwardType award)
    {
        string label = ResolveAwardLabel(award);
        if (string.IsNullOrEmpty(label))
            return;

        text = string.IsNullOrEmpty(text) ? label : text + "\n" + label;
    }

    private string ResolveAwardLabel(CombatAwardType award)
    {
        if (award == CombatAwardType.None || award == CombatAwardType.Elimination)
            return "";

        if (award == CombatAwardType.Ricochet)
            return "RICOCHET";

        if (awardLabels != null)
        {
            for (int i = 0; i < awardLabels.Length; i++)
            {
                if (awardLabels[i].Award == award && !string.IsNullOrWhiteSpace(awardLabels[i].Text))
                    return awardLabels[i].Text;
            }
        }

        return award.ToString();
    }

    private FeedbackItem RentItem()
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (!items[i].Active)
                return items[i];
        }

        return CreateItem();
    }

    private void Release(FeedbackItem item)
    {
        item.Active = false;
        item.AccumulatesDamage = false;
        item.Age = 0f;
        item.Rise = 0f;
        if (item.Root != null)
            item.Root.gameObject.SetActive(false);
    }

    private bool IsLocalOwner()
    {
        if (networkObject == null)
            networkObject = GetComponent<NetworkObject>();

        return networkObject != null && networkObject.IsSpawned && networkObject.IsOwner;
    }

    private bool IsSuppressed()
    {
        if (LocalPlayerMenuState.IsOpen(this))
            return true;

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        return playerHealth != null && playerHealth.IsDead;
    }

    private void EnsureUi()
    {
        if (built)
            return;

        var canvasObject = new GameObject(
            "CombatFeedbackCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var rootObject = new GameObject("Stack", typeof(RectTransform));
        rootObject.transform.SetParent(canvasObject.transform, false);
        stackRoot = rootObject.GetComponent<RectTransform>();
        stackRoot.anchorMin = new Vector2(0.5f, 0.5f);
        stackRoot.anchorMax = new Vector2(0.5f, 0.5f);
        stackRoot.pivot = new Vector2(0.5f, 0.5f);
        stackRoot.anchoredPosition = Vector2.zero;
        stackRoot.sizeDelta = new Vector2(480f, 240f);

        int pool = Mathf.Max(1, maximumSimultaneousMessages);
        for (int i = 0; i < pool; i++)
            CreateItem();

        built = true;
        canvas.enabled = IsLocalOwner() && !IsSuppressed();
    }

    private FeedbackItem CreateItem()
    {
        var itemObject = new GameObject("CombatFeedbackItem", typeof(RectTransform), typeof(CanvasGroup));
        itemObject.transform.SetParent(stackRoot, false);

        RectTransform rect = itemObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(420f, 72f);

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(itemObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObject.AddComponent<Text>();
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Outline outline = textObject.AddComponent<Outline>();
        outline.useGraphicAlpha = true;

        var item = new FeedbackItem
        {
            Root = itemObject.GetComponent<CanvasGroup>(),
            Group = itemObject.GetComponent<CanvasGroup>(),
            Rect = rect,
            Label = label,
            Outline = outline
        };
        item.Root.gameObject.SetActive(false);
        items.Add(item);
        ApplyStyle(item);
        return item;
    }

    private void ApplyStyle(FeedbackItem item)
    {
        if (item.Label == null)
            return;

        item.Label.fontSize = Mathf.Max(10, fontSize);
        item.Label.fontStyle = FontStyle.Italic;
        item.Label.color = Color.white;
        if (item.Outline != null)
        {
            float width = Mathf.Max(0.4f, outlineWidth);
            item.Outline.effectColor = bullseyeRed.a <= 0f ? BullseyeColors.UiRed : bullseyeRed;
            item.Outline.effectDistance = new Vector2(width, -width);
        }
    }

    [Serializable]
    public struct CombatAwardLabel
    {
        public CombatAwardType Award;
        public string Text;
    }

    private sealed class FeedbackItem
    {
        public CanvasGroup Root;
        public CanvasGroup Group;
        public RectTransform Rect;
        public Text Label;
        public Outline Outline;
        public bool Active;
        public bool AccumulatesDamage;
        public int Amount;
        public int Priority;
        public int Sequence;
        public float Age;
        public float HoldDuration;
        public float Rise;
    }
}
