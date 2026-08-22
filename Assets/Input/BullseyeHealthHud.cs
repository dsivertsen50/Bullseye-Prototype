using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owner-only local health presentation. Reads PlayerHealth and does not
/// change damage, regeneration, death, or respawn rules.
/// </summary>
public class BullseyeHealthHud : MonoBehaviour
{
    [Header("Health Source")]
    [SerializeField] private PlayerHealth healthSource;

    [Header("Base Artwork")]
    [Tooltip("Replaceable bullseye image. Drag a new sprite here; no code change required.")]
    [SerializeField] private Sprite baseBullseyeSprite;

    [Header("Crack Artwork")]
    [Tooltip("One overlay per missing HP before the shattered state. Drag replacement crack sprites here.")]
    [SerializeField] private Sprite[] crackSprites;

    [Header("Shattered Artwork")]
    [Tooltip("Shown at 0 HP. Drag a replacement shattered overlay here.")]
    [SerializeField] private Sprite shatteredSprite;

    [Header("Optional Flash Artwork")]
    [SerializeField] private Sprite damageFlashSprite;

    [Header("Audio Source")]
    [SerializeField] private AudioSource audioSource;

    [Header("Crack Sounds")]
    [Tooltip("Played when health decreases. If multiple clips are assigned, one is chosen at random.")]
    [SerializeField] private AudioClip[] crackSounds;

    [Header("Shatter Sound")]
    [Tooltip("Played when health reaches 0. May be left empty.")]
    [SerializeField] private AudioClip shatterSound;

    [Header("Repair Sounds")]
    [Tooltip("Optional clips played when a health point regenerates. May be left empty.")]
    [SerializeField] private AudioClip[] repairSounds;

    [Header("Audio Volume")]
    [SerializeField, Range(0f, 1f)] private float damageSoundVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float repairSoundVolume = 0.35f;

    [Header("Damage Feedback")]
    [SerializeField] private float damageShakeStrength = 10f;
    [SerializeField] private float damageShakeDuration = 0.16f;
    [SerializeField] private float damageScalePunch = 0.08f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.92f, 0.88f, 0.42f);

    [Header("Repair Feedback")]
    [SerializeField] private float repairFadeDuration = 0.22f;
    [SerializeField] private float repairScalePulse = 0.04f;

    [Header("Layout")]
    [SerializeField] private float hudSize = 148f;
    [SerializeField] private float margin = 24f;

    private Canvas canvas;
    private RectTransform shakeRoot;
    private Image baseImage;
    private Image[] crackImages = System.Array.Empty<Image>();
    private Image shatteredImage;
    private Image flashImage;
    private Coroutine damageRoutine;
    private Coroutine repairRoutine;
    private bool built;
    private bool ownsGeneratedArt;
    private bool hasBaseline;
    private int displayedHealth;
    private Sprite generatedBaseSprite;
    private Sprite[] generatedCrackSprites;
    private Sprite generatedShatteredSprite;

    private void Awake()
    {
        if (healthSource == null)
            healthSource = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        Subscribe();
        hasBaseline = false;
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopFeedback();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        StopFeedback();
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

        if (!hasBaseline)
            CaptureBaseline();
    }

    private void Subscribe()
    {
        if (healthSource == null)
            healthSource = GetComponent<PlayerHealth>();

        if (healthSource != null)
            healthSource.HealthChanged += OnHealthChanged;
    }

    private void Unsubscribe()
    {
        if (healthSource != null)
            healthSource.HealthChanged -= OnHealthChanged;
    }

    private bool ShouldDisplay()
    {
        return healthSource != null
            && healthSource.IsSpawned
            && healthSource.IsOwner
            && !LocalPlayerMenuState.IsOpen(this);
    }

    private void CaptureBaseline()
    {
        displayedHealth = GetCurrentHealth();
        hasBaseline = true;
        ApplyImmediate(displayedHealth);
    }

    private void OnHealthChanged(int previous, int next)
    {
        if (healthSource == null || !healthSource.IsOwner)
            return;

        if (!ShouldDisplay())
        {
            displayedHealth = next;
            hasBaseline = true;
            if (built)
                ApplyImmediate(next);
            return;
        }

        EnsureUi();

        if (!hasBaseline)
        {
            displayedHealth = next;
            hasBaseline = true;
            ApplyImmediate(next);
            return;
        }

        ApplyHealthTransition(displayedHealth, next);
        displayedHealth = next;
    }

    private void ApplyHealthTransition(int previous, int next)
    {
        int max = GetMaxHealth();
        previous = Mathf.Clamp(previous, 0, max);
        next = Mathf.Clamp(next, 0, max);

        if (next == previous)
        {
            ApplyImmediate(next);
            return;
        }

        if (IsFullReset(previous, next, max))
        {
            StopFeedback();
            ApplyImmediate(next);
            return;
        }

        if (next < previous)
        {
            StopRepair();
            ApplyImmediate(next);
            PlayDamageFeedback();
            PlayDamageAudio(next);
            return;
        }

        StopDamage();
        PlayRepairFeedback(previous, next);
        PlayRepairAudio();
    }

    private static bool IsFullReset(int previous, int next, int max)
    {
        return next >= max && previous <= 0;
    }

    private void ApplyImmediate(int health)
    {
        health = Mathf.Clamp(health, 0, GetMaxHealth());
        int layersToShow = CrackLayersForHealth(health);
        bool shattered = health <= 0;

        if (baseImage != null)
        {
            baseImage.enabled = true;
            baseImage.color = Color.white;
        }

        for (int i = 0; i < crackImages.Length; i++)
        {
            Image layer = crackImages[i];
            if (layer == null)
                continue;

            bool visible = i < layersToShow;
            layer.enabled = visible;
            layer.color = Color.white;
        }

        if (shatteredImage != null)
        {
            shatteredImage.enabled = shattered;
            shatteredImage.color = Color.white;
        }

        if (flashImage != null)
        {
            Color flash = damageFlashColor;
            flash.a = 0f;
            flashImage.color = flash;
            flashImage.enabled = false;
        }

        ResetShakeTransform();
    }

    private int CrackLayersForHealth(int health)
    {
        if (crackImages == null || crackImages.Length == 0)
            return 0;

        int max = GetMaxHealth();
        int missing = Mathf.Clamp(max - Mathf.Clamp(health, 0, max), 0, max);
        if (health <= 0)
            return crackImages.Length;

        return Mathf.Min(crackImages.Length, missing);
    }

    private void PlayDamageFeedback()
    {
        StopDamage();
        if (!isActiveAndEnabled || !built)
            return;

        damageRoutine = StartCoroutine(DamageFeedbackRoutine());
    }

    private IEnumerator DamageFeedbackRoutine()
    {
        if (flashImage != null)
        {
            flashImage.enabled = true;
            flashImage.color = damageFlashColor;
        }

        float duration = Mathf.Max(0.01f, damageShakeDuration);
        float elapsed = 0f;
        Vector2 rest = Vector2.zero;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float falloff = 1f - Mathf.Clamp01(elapsed / duration);
            float magnitude = damageShakeStrength * falloff;
            if (shakeRoot != null)
            {
                shakeRoot.anchoredPosition = rest + Random.insideUnitCircle * magnitude;
                shakeRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1f, 1f) * magnitude * 0.35f);
                shakeRoot.localScale = Vector3.one * (1f + damageScalePunch * falloff);
            }

            if (flashImage != null)
            {
                Color flash = damageFlashColor;
                flash.a = damageFlashColor.a * falloff;
                flashImage.color = flash;
            }

            yield return null;
        }

        ResetShakeTransform();
        if (flashImage != null)
        {
            Color flash = damageFlashColor;
            flash.a = 0f;
            flashImage.color = flash;
            flashImage.enabled = false;
        }

        damageRoutine = null;
    }

    private void PlayRepairFeedback(int previous, int next)
    {
        StopRepair();
        if (!isActiveAndEnabled || !built)
        {
            ApplyImmediate(next);
            return;
        }

        repairRoutine = StartCoroutine(RepairFeedbackRoutine(previous, next));
    }

    private IEnumerator RepairFeedbackRoutine(int previous, int next)
    {
        int fromLayers = CrackLayersForHealth(previous);
        int toLayers = CrackLayersForHealth(next);
        bool fromShattered = previous <= 0;
        bool toShattered = next <= 0;

        ApplyImmediate(previous);

        for (int i = 0; i < crackImages.Length; i++)
        {
            if (crackImages[i] == null)
                continue;

            bool visible = i < fromLayers;
            crackImages[i].enabled = visible;
            crackImages[i].color = Color.white;
        }

        if (shatteredImage != null)
        {
            shatteredImage.enabled = fromShattered;
            shatteredImage.color = Color.white;
        }

        float duration = Mathf.Max(0.01f, repairFadeDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = 1f - t;

            for (int i = 0; i < crackImages.Length; i++)
            {
                Image layer = crackImages[i];
                if (layer == null)
                    continue;

                if (i < toLayers)
                {
                    layer.enabled = true;
                    layer.color = Color.white;
                    continue;
                }

                if (i < fromLayers)
                {
                    layer.enabled = alpha > 0.02f;
                    layer.color = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    layer.enabled = false;
                }
            }

            if (shatteredImage != null)
            {
                if (toShattered)
                {
                    shatteredImage.enabled = true;
                    shatteredImage.color = Color.white;
                }
                else if (fromShattered)
                {
                    shatteredImage.enabled = alpha > 0.02f;
                    shatteredImage.color = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    shatteredImage.enabled = false;
                }
            }

            if (shakeRoot != null)
            {
                float pulse = 1f + repairScalePulse * Mathf.Sin(t * Mathf.PI);
                shakeRoot.localScale = Vector3.one * pulse;
            }

            yield return null;
        }

        ApplyImmediate(next);
        repairRoutine = null;
    }

    private void PlayDamageAudio(int nextHealth)
    {
        if (nextHealth <= 0 && shatterSound != null)
        {
            PlayClip(shatterSound, damageSoundVolume);
            return;
        }

        PlayClip(PickRandom(crackSounds), damageSoundVolume);
    }

    private void PlayRepairAudio()
    {
        PlayClip(PickRandom(repairSounds), repairSoundVolume);
    }

    private void PlayClip(AudioClip clip, float volume)
    {
        if (clip == null)
            return;

        EnsureAudioSource();
        if (audioSource == null)
            return;

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip PickRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int assigned = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                assigned++;
        }

        if (assigned <= 0)
            return null;

        int pick = Random.Range(0, assigned);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (pick == 0)
                return clips[i];

            pick--;
        }

        return null;
    }

    private void EnsureUi()
    {
        if (built)
            return;

        GameObject canvasObject = new GameObject(
            "BullseyeHealthCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject anchorObject = new GameObject("HudAnchor", typeof(RectTransform));
        anchorObject.transform.SetParent(canvasObject.transform, false);
        RectTransform hudAnchor = anchorObject.GetComponent<RectTransform>();
        hudAnchor.anchorMin = Vector2.zero;
        hudAnchor.anchorMax = Vector2.zero;
        hudAnchor.pivot = new Vector2(0f, 0f);
        hudAnchor.sizeDelta = new Vector2(hudSize, hudSize);
        hudAnchor.anchoredPosition = new Vector2(margin, margin);

        GameObject rootObject = new GameObject("ShakeRoot", typeof(RectTransform));
        rootObject.transform.SetParent(hudAnchor, false);
        shakeRoot = rootObject.GetComponent<RectTransform>();
        shakeRoot.anchorMin = Vector2.zero;
        shakeRoot.anchorMax = Vector2.one;
        shakeRoot.offsetMin = Vector2.zero;
        shakeRoot.offsetMax = Vector2.zero;
        shakeRoot.pivot = new Vector2(0.5f, 0.5f);
        shakeRoot.anchoredPosition = Vector2.zero;

        Sprite baseSprite = ResolveBaseSprite();
        Sprite[] cracks = ResolveCrackSprites();
        Sprite shattered = ResolveShatteredSprite();
        Sprite flashSprite = damageFlashSprite != null ? damageFlashSprite : UiWhiteSprite.Get();

        baseImage = CreateLayer(shakeRoot, "BullseyeBase", baseSprite);
        crackImages = new Image[cracks.Length];
        for (int i = 0; i < cracks.Length; i++)
            crackImages[i] = CreateLayer(shakeRoot, $"CrackLayer{i + 1:00}", cracks[i]);

        shatteredImage = CreateLayer(shakeRoot, "ShatteredOverlay", shattered);
        flashImage = CreateLayer(shakeRoot, "DamageFlash", flashSprite);
        flashImage.color = new Color(damageFlashColor.r, damageFlashColor.g, damageFlashColor.b, 0f);
        flashImage.enabled = false;

        EnsureAudioSource();
        built = true;
        ApplyImmediate(GetCurrentHealth());
        SetVisible(false);
    }

    private Sprite ResolveBaseSprite()
    {
        if (baseBullseyeSprite != null)
            return baseBullseyeSprite;

        generatedBaseSprite = BullseyeHealthHudPlaceholders.CreateBaseBullseye();
        ownsGeneratedArt = true;
        return generatedBaseSprite;
    }

    private Sprite[] ResolveCrackSprites()
    {
        int count = crackSprites != null && crackSprites.Length > 0
            ? crackSprites.Length
            : BullseyeHealthHudPlaceholders.CrackLayerCount;

        var resolved = new Sprite[count];
        bool missingAny = false;
        for (int i = 0; i < count; i++)
        {
            if (crackSprites != null && i < crackSprites.Length && crackSprites[i] != null)
            {
                resolved[i] = crackSprites[i];
                continue;
            }

            missingAny = true;
            resolved[i] = BullseyeHealthHudPlaceholders.CreateCrackLayer(i);
        }

        if (missingAny)
        {
            generatedCrackSprites = resolved;
            ownsGeneratedArt = true;
        }

        return resolved;
    }

    private Sprite ResolveShatteredSprite()
    {
        if (shatteredSprite != null)
            return shatteredSprite;

        generatedShatteredSprite = BullseyeHealthHudPlaceholders.CreateShatteredOverlay();
        ownsGeneratedArt = true;
        return generatedShatteredSprite;
    }

    private static Image CreateLayer(RectTransform parent, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : UiWhiteSprite.Get();
        image.color = Color.white;
        image.raycastTarget = false;
        image.preserveAspect = true;
        image.type = Image.Type.Simple;
        return image;
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            ConfigureAudioSource(audioSource);
            return;
        }

        if (canvas == null)
            return;

        audioSource = canvas.GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = canvas.gameObject.AddComponent<AudioSource>();

        ConfigureAudioSource(audioSource);
    }

    private static void ConfigureAudioSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 64;
        PlayerGameSettings.RouteToSfx(source);
    }

    private void SetVisible(bool visible)
    {
        if (canvas != null && canvas.gameObject.activeSelf != visible)
            canvas.gameObject.SetActive(visible);
    }

    private void StopFeedback()
    {
        StopDamage();
        StopRepair();
        ResetShakeTransform();
    }

    private void StopDamage()
    {
        if (damageRoutine == null)
            return;

        StopCoroutine(damageRoutine);
        damageRoutine = null;
        ResetShakeTransform();
        if (flashImage != null)
        {
            Color flash = damageFlashColor;
            flash.a = 0f;
            flashImage.color = flash;
            flashImage.enabled = false;
        }
    }

    private void StopRepair()
    {
        if (repairRoutine == null)
            return;

        StopCoroutine(repairRoutine);
        repairRoutine = null;
        ResetShakeTransform();
    }

    private void ResetShakeTransform()
    {
        if (shakeRoot == null)
            return;

        shakeRoot.anchoredPosition = Vector2.zero;
        shakeRoot.localRotation = Quaternion.identity;
        shakeRoot.localScale = Vector3.one;
    }

    private int GetCurrentHealth()
    {
        return healthSource != null ? Mathf.Clamp(healthSource.CurrentHealth, 0, GetMaxHealth()) : 0;
    }

    private int GetMaxHealth()
    {
        return healthSource != null ? Mathf.Max(1, healthSource.MaxHealth) : 8;
    }

    private void DestroyGeneratedArt()
    {
        if (!ownsGeneratedArt)
            return;

        DestroySprite(ref generatedBaseSprite);
        DestroySprite(ref generatedShatteredSprite);
        if (generatedCrackSprites != null)
        {
            for (int i = 0; i < generatedCrackSprites.Length; i++)
                DestroySprite(ref generatedCrackSprites[i]);
        }

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
        hudSize = Mathf.Max(48f, hudSize);
        margin = Mathf.Max(0f, margin);
        damageShakeStrength = Mathf.Max(0f, damageShakeStrength);
        damageShakeDuration = Mathf.Max(0f, damageShakeDuration);
        damageScalePunch = Mathf.Max(0f, damageScalePunch);
        repairFadeDuration = Mathf.Max(0f, repairFadeDuration);
        repairScalePulse = Mathf.Max(0f, repairScalePulse);
        damageSoundVolume = Mathf.Clamp01(damageSoundVolume);
        repairSoundVolume = Mathf.Clamp01(repairSoundVolume);
    }
}
