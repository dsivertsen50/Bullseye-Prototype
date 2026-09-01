using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owner-only ADS optic overlay. Opacity follows existing ADS progress so
/// weapon pose, camera zoom, and scope appearance stay on one transition.
/// Visual only; does not change shot direction or get networked.
/// </summary>
[DefaultExecutionOrder(90)]
public class PlayerScopeOverlay : NetworkBehaviour
{
    private const int CanvasSortOrder = 15;
    private const string PeripheralShaderName = "Bullseye/UI/ScopePeripheral";
    private static readonly int CanvasSizeId = Shader.PropertyToID("_CanvasSize");
    private static readonly int LensRadiusId = Shader.PropertyToID("_LensRadius");
    private static readonly int InnerOpacityId = Shader.PropertyToID("_InnerOpacity");
    private static readonly int PeripheralOpacityId = Shader.PropertyToID("_PeripheralOpacity");

    private PlayerWeaponInventory inventory;
    private WeaponPresentationController weaponPresentation;
    private PlayerAimZoom playerAimZoom;
    private PlayerHealth playerHealth;
    private bool ownerOverlayEnabled;

    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform housingRect;
    private RectTransform vignetteRect;
    private RectTransform tintRect;
    private RectTransform reticleRect;
    private RectTransform reticleDotRect;
    private Image peripheralImage;
    private Image housingImage;
    private Image vignetteImage;
    private Image tintImage;
    private Image reticleImage;
    private Image reticleDotImage;
    private Material peripheralMaterial;

    private ScopeDefinition appliedScope;
    private float overlayOpacity;

    public float OverlayOpacity => overlayOpacity;

    public float HipFireReticleAlpha
    {
        get
        {
            if (appliedScope == null || !appliedScope.HideHipFireReticle)
                return 1f;
            return 1f - overlayOpacity;
        }
    }

    private void Awake()
    {
        inventory = GetComponent<PlayerWeaponInventory>();
        weaponPresentation = GetComponent<WeaponPresentationController>();
        playerAimZoom = GetComponent<PlayerAimZoom>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        ownerOverlayEnabled = IsOwner;
        if (!ownerOverlayEnabled)
        {
            enabled = false;
            return;
        }

        EnsureUi();
        ApplyVisuals(null, 0f);
    }

    public override void OnNetworkDespawn()
    {
        ownerOverlayEnabled = false;
        ApplyVisuals(null, 0f);
        DestroyUi();
    }

    public override void OnDestroy()
    {
        DestroyUi();
        base.OnDestroy();
    }

    private void OnDisable()
    {
        ApplyVisuals(appliedScope, 0f);
    }

    private void LateUpdate()
    {
        if (!ownerOverlayEnabled)
            return;

        EnsureUi();
        ScopeDefinition scope = ResolveActiveScope();
        float opacity = ResolveOpacity(scope);
        if (!Mathf.Approximately(opacity, overlayOpacity) || scope != appliedScope)
            ApplyVisuals(scope, opacity);
        else if (opacity > 0.001f)
            RefreshLayout(scope);
    }

    private ScopeDefinition ResolveActiveScope()
    {
        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        if (definition == null || !definition.UsesScopeOverlay)
            return null;
        return definition.ScopePresentation;
    }

    private float ResolveOpacity(ScopeDefinition scope)
    {
        if (scope == null || !scope.UsesScopeOverlay)
            return 0f;

        if (playerHealth != null && playerHealth.IsDead)
            return 0f;

        if (LocalPlayerMenuState.IsOpen(this))
            return 0f;

        float adsProgress = 0f;
        if (weaponPresentation != null)
            adsProgress = weaponPresentation.AimBlend;
        else if (playerAimZoom != null && playerAimZoom.IsAiming)
            adsProgress = 1f;

        return scope.EvaluateOpacity(adsProgress);
    }

    private void ApplyVisuals(ScopeDefinition scope, float opacity)
    {
        appliedScope = scope;
        overlayOpacity = Mathf.Clamp01(opacity);

        if (canvas == null)
            return;

        bool visible = ownerOverlayEnabled && overlayOpacity > 0.001f && scope != null;
        if (canvas.gameObject.activeSelf != visible)
            canvas.gameObject.SetActive(visible);

        if (!visible)
            return;

        BindSprites(scope);
        RefreshLayout(scope);
        ApplyColors(scope, overlayOpacity);
    }

    private void BindSprites(ScopeDefinition scope)
    {
        if (housingImage != null)
        {
            housingImage.sprite = scope.OverlaySprite != null ? scope.OverlaySprite : ScopePlaceholderSprites.Housing;
            housingImage.preserveAspect = true;
        }

        if (vignetteImage != null)
        {
            vignetteImage.sprite = scope.VignetteSprite != null ? scope.VignetteSprite : ScopePlaceholderSprites.Vignette;
            vignetteImage.preserveAspect = true;
        }

        if (reticleImage != null)
        {
            reticleImage.sprite = scope.ReticleSprite != null ? scope.ReticleSprite : ScopePlaceholderSprites.Reticle;
            reticleImage.preserveAspect = true;
        }

        if (reticleDotImage != null)
        {
            reticleDotImage.sprite = ScopePlaceholderSprites.Dot;
            reticleDotImage.preserveAspect = true;
        }
    }

    private void RefreshLayout(ScopeDefinition scope)
    {
        if (canvasRect == null || scope == null)
            return;

        Vector2 canvasSize = canvasRect.rect.size;
        if (canvasSize.x < 1f || canvasSize.y < 1f)
            return;

        float shortest = Mathf.Min(canvasSize.x, canvasSize.y);
        float diameter = shortest * scope.LensRadius;

        if (peripheralMaterial != null)
        {
            peripheralMaterial.SetVector(CanvasSizeId, new Vector4(canvasSize.x, canvasSize.y, 0f, 0f));
            peripheralMaterial.SetFloat(LensRadiusId, scope.LensRadius);
            peripheralMaterial.SetFloat(InnerOpacityId, scope.InnerOpacity);
            peripheralMaterial.SetFloat(PeripheralOpacityId, scope.PeripheralOpacity);
        }

        const float housingInner = 0.88f;
        SetCenteredSquare(housingRect, diameter / housingInner);
        SetCenteredSquare(vignetteRect, diameter);
        SetCenteredSquare(tintRect, diameter);
        SetCenteredSquare(reticleRect, diameter * scope.ReticleScale);
        float dotSize = diameter * scope.ReticleDotSize;
        SetCenteredSquare(reticleDotRect, dotSize);
        if (reticleDotImage != null)
            reticleDotImage.enabled = dotSize > 0.5f;
    }

    private void ApplyColors(ScopeDefinition scope, float opacity)
    {
        Color peripheral = scope.PeripheralColor;
        peripheral.a = opacity;
        SetImageColor(peripheralImage, peripheral);

        Color housing = scope.HousingColor;
        housing.a *= opacity;
        SetImageColor(housingImage, housing);

        Color vignette = Color.black;
        vignette.a = scope.VignetteStrength * opacity;
        SetImageColor(vignetteImage, vignette);

        Color tint = scope.LensTint;
        tint.a *= opacity;
        SetImageColor(tintImage, tint);
        if (tintImage != null)
            tintImage.enabled = tint.a > 0.001f;

        Color reticle = scope.ReticleColor;
        reticle.a *= opacity;
        SetImageColor(reticleImage, reticle);
        SetImageColor(reticleDotImage, reticle);
    }

    private void EnsureUi()
    {
        if (canvas != null)
            return;

        GameObject canvasObject = new GameObject(
            "ScopeOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasSortOrder;
        canvas.pixelPerfect = false;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        peripheralImage = CreateImage(canvasObject.transform, "Peripheral", UiWhiteSprite.Get());
        Stretch(peripheralImage.rectTransform);
        Shader peripheralShader = Shader.Find(PeripheralShaderName);
        if (peripheralShader != null)
        {
            peripheralMaterial = new Material(peripheralShader)
            {
                name = "ScopePeripheral (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            peripheralImage.material = peripheralMaterial;
        }
        else
        {
            peripheralImage.enabled = false;
        }

        housingImage = CreateImage(canvasObject.transform, "ScopeHousing", ScopePlaceholderSprites.Housing);
        housingRect = housingImage.rectTransform;
        Center(housingRect);

        vignetteImage = CreateImage(canvasObject.transform, "LensVignette", ScopePlaceholderSprites.Vignette);
        vignetteRect = vignetteImage.rectTransform;
        Center(vignetteRect);

        tintImage = CreateImage(canvasObject.transform, "LensTint", UiWhiteSprite.Get());
        tintRect = tintImage.rectTransform;
        Center(tintRect);

        reticleImage = CreateImage(canvasObject.transform, "ScopeReticle", ScopePlaceholderSprites.Reticle);
        reticleRect = reticleImage.rectTransform;
        Center(reticleRect);

        reticleDotImage = CreateImage(canvasObject.transform, "ScopeReticleDot", ScopePlaceholderSprites.Dot);
        reticleDotRect = reticleDotImage.rectTransform;
        Center(reticleDotRect);

        canvasObject.SetActive(false);
    }

    private void DestroyUi()
    {
        if (canvas == null)
            return;

        if (Application.isPlaying)
            Destroy(canvas.gameObject);
        else
            DestroyImmediate(canvas.gameObject);

        canvas = null;
        canvasRect = null;
        housingRect = null;
        vignetteRect = null;
        tintRect = null;
        reticleRect = null;
        reticleDotRect = null;
        peripheralImage = null;
        housingImage = null;
        vignetteImage = null;
        tintImage = null;
        reticleImage = null;
        reticleDotImage = null;

        if (peripheralMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(peripheralMaterial);
            else
                DestroyImmediate(peripheralMaterial);
            peripheralMaterial = null;
        }
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.maskable = false;
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

    private static void Center(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private static void SetCenteredSquare(RectTransform rect, float size)
    {
        if (rect == null)
            return;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;
    }

    private static void SetImageColor(Image image, Color color)
    {
        if (image != null)
            image.color = color;
    }
}
