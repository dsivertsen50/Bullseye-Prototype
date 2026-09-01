using UnityEngine;

/// <summary>
/// Visual optic presentation while ADS. Independent of optical magnification
/// on WeaponDefinition so a red-dot magnifier and a sniper scope can share
/// zoom math without sharing overlay art.
/// </summary>
[CreateAssetMenu(
    fileName = "ScopeDefinition",
    menuName = "Bullseye/Weapons/Scope Definition")]
public class ScopeDefinition : ScriptableObject
{
    [SerializeField] private ScopePresentationType style = ScopePresentationType.Dmr;
    [SerializeField, Tooltip("When disabled, ADS uses magnification/pose only and does not show this overlay.")]
    private bool usesScopeOverlay = true;

    [Header("Lens")]
    [SerializeField, Range(0.35f, 0.95f), Tooltip("Lens diameter as a fraction of the shortest screen edge. Larger is a more open low-power optic.")]
    private float lensRadius = 0.78f;

    [Header("Replaceable Art")]
    [SerializeField, Tooltip("Scope housing / ring around the lens. Leave empty to use the generated placeholder.")]
    private Sprite overlaySprite;
    [SerializeField, Tooltip("Precision reticle drawn at screen center. Leave empty to use the generated placeholder.")]
    private Sprite reticleSprite;
    [SerializeField, Tooltip("Darkening toward the lens edge. Leave empty to use the generated placeholder.")]
    private Sprite vignetteSprite;
    [SerializeField, Tooltip("Unused for the lens cutout. Peripheral darkening always uses a generated circular hole so the inside of the optic stays clearer than the outside.")]
    private Sprite maskSprite;

    [Header("Peripheral")]
    [SerializeField, Range(0f, 1f), Tooltip("Opacity outside the lens circle. Higher is darker.")]
    private float peripheralOpacity = 0.72f;
    [SerializeField, Range(0f, 1f), Tooltip("Opacity inside the lens circle. Keep this lower than Peripheral Opacity so the optic stays readable.")]
    private float innerOpacity = 0.18f;
    [SerializeField] private Color peripheralColor = new(0.015f, 0.015f, 0.02f, 1f);

    [Header("Housing")]
    [SerializeField] private Color housingColor = Color.white;
    [SerializeField, Range(0.02f, 0.25f), Tooltip("Housing ring thickness as a fraction of the lens radius.")]
    private float housingThickness = 0.08f;

    [Header("Lens Treatment")]
    [SerializeField, Range(0f, 1f)] private float vignetteStrength = 0.38f;
    [SerializeField, Tooltip("Optional lens color. Keep alpha at 0 for no tint.")]
    private Color lensTint = new(0.75f, 0.85f, 0.78f, 0f);

    [Header("Reticle")]
    [SerializeField] private bool hideHipFireReticle = true;
    [SerializeField] private Color reticleColor = new(0.94f, 0.94f, 0.9f, 0.95f);
    [SerializeField, Range(0.05f, 1.2f), Tooltip("Crosshair sprite size as a fraction of the lens diameter.")]
    private float reticleScale = 0.92f;
    [SerializeField, Range(0f, 0.12f), Tooltip("Center aiming-dot diameter as a fraction of the lens. 0 hides this extra dot. If the reticle PNG already has a baked-in dot, remove it from the PNG so only this size is used.")]
    private float reticleDotSize = 0.02f;

    [Header("Transition")]
    [SerializeField, Tooltip("Maps ADS progress (0-1) to overlay opacity. Keep this increasing so interrupted ADS reverses cleanly.")]
    private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Future Zoom")]
    [SerializeField, Tooltip("Reserved for a future multi-zoom optic. REQ-043 does not change magnification; the overlay stays active while zoom steps change.")]
    private float[] additionalMagnifications = System.Array.Empty<float>();

    public ScopePresentationType Style => style;
    public bool UsesScopeOverlay => usesScopeOverlay && style != ScopePresentationType.None;
    public float LensRadius => Mathf.Clamp(lensRadius, 0.35f, 0.95f);
    public Sprite OverlaySprite => overlaySprite;
    public Sprite ReticleSprite => reticleSprite;
    public Sprite VignetteSprite => vignetteSprite;
    public Sprite MaskSprite => maskSprite;
    public float PeripheralOpacity => Mathf.Clamp01(peripheralOpacity);
    public float InnerOpacity => Mathf.Clamp01(innerOpacity);
    public Color PeripheralColor => peripheralColor;
    public Color HousingColor => housingColor;
    public float HousingThickness => Mathf.Clamp(housingThickness, 0.02f, 0.25f);
    public float VignetteStrength => Mathf.Clamp01(vignetteStrength);
    public Color LensTint => lensTint;
    public bool HideHipFireReticle => hideHipFireReticle;
    public Color ReticleColor => reticleColor;
    public float ReticleScale => Mathf.Clamp(reticleScale, 0.05f, 1.2f);
    public float ReticleDotSize => Mathf.Clamp(reticleDotSize, 0f, 0.12f);
    public float[] AdditionalMagnifications => additionalMagnifications;

    public float EvaluateOpacity(float adsProgress)
    {
        float t = Mathf.Clamp01(adsProgress);
        if (transitionCurve == null || transitionCurve.length == 0)
            return t;
        return Mathf.Clamp01(transitionCurve.Evaluate(t));
    }

    private void OnValidate()
    {
        lensRadius = Mathf.Clamp(lensRadius, 0.35f, 0.95f);
        peripheralOpacity = Mathf.Clamp01(peripheralOpacity);
        innerOpacity = Mathf.Clamp01(innerOpacity);
        housingThickness = Mathf.Clamp(housingThickness, 0.02f, 0.25f);
        vignetteStrength = Mathf.Clamp01(vignetteStrength);
        reticleScale = Mathf.Clamp(reticleScale, 0.05f, 1.2f);
        reticleDotSize = Mathf.Clamp(reticleDotSize, 0f, 0.12f);
        if (transitionCurve == null || transitionCurve.length == 0)
            transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}
