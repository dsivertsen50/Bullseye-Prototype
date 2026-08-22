using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Owner-only ADS presentation. Blurs the world around a sharp center window
/// while the first-person overlay sights stay crisp. Cosmetic; not networked.
/// </summary>
[DefaultExecutionOrder(80)]
public class PlayerAimSightBlur : NetworkBehaviour
{
    private const string ShaderName = "Hidden/Bullseye/AdsSightBlur";

    [SerializeField] private Camera playerCamera;
    [SerializeField] private Shader blurShader;

    [Header("Sight Window")]
    [SerializeField] [Range(0.02f, 0.6f)] private float sharpRadius = 0.16f;
    [SerializeField] [Range(0.05f, 1.2f)] private float blurOuterRadius = 0.42f;
    [SerializeField] [Range(0f, 0.08f)] private float blurSize = 0.022f;
    [SerializeField] [Range(0f, 1f)] private float intensity = 0.9f;
    [SerializeField] private float blendSpeed = 10f;

    private static readonly int IntensityId = Shader.PropertyToID("_AdsBlurIntensity");
    private static readonly int SharpRadiusId = Shader.PropertyToID("_AdsSharpRadius");
    private static readonly int OuterRadiusId = Shader.PropertyToID("_AdsBlurOuterRadius");
    private static readonly int BlurSizeId = Shader.PropertyToID("_AdsBlurSize");

    private PlayerAimZoom playerAimZoom;
    private WeaponPresentationController weaponPresentation;
    private PlayerHealth playerHealth;
    private CustomPassVolume passVolume;
    private FullScreenCustomPass fullScreenPass;
    private Material runtimeMaterial;
    private bool ownerEffectsEnabled;
    private float currentBlend;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        playerAimZoom = GetComponent<PlayerAimZoom>();
        weaponPresentation = GetComponent<WeaponPresentationController>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        ownerEffectsEnabled = IsOwner;
        if (!ownerEffectsEnabled)
        {
            enabled = false;
            return;
        }

        EnsurePass();
        ApplyBlur(0f);
    }

    public override void OnNetworkDespawn()
    {
        ownerEffectsEnabled = false;
        ApplyBlur(0f);
        TeardownPass();
    }

    private void OnDisable()
    {
        ApplyBlur(0f);
    }

    public override void OnDestroy()
    {
        TeardownPass();
        base.OnDestroy();
    }

    private void LateUpdate()
    {
        if (!ownerEffectsEnabled)
            return;

        EnsurePass();
        if (fullScreenPass == null || runtimeMaterial == null)
            return;

        float target = ReadAimBlend();
        if (weaponPresentation != null)
            currentBlend = target;
        else
            currentBlend = Mathf.MoveTowards(currentBlend, target, blendSpeed * Time.deltaTime);

        ApplyBlur(currentBlend * intensity);
    }

    private float ReadAimBlend()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return 0f;

        if (LocalPlayerMenuState.IsOpen(this))
            return 0f;

        if (weaponPresentation != null)
            return weaponPresentation.AimBlend;

        return playerAimZoom != null && playerAimZoom.IsAiming ? 1f : 0f;
    }

    private void ApplyBlur(float blend)
    {
        float amount = Mathf.Clamp01(blend);
        bool active = ownerEffectsEnabled && amount > 0.001f;

        if (fullScreenPass != null)
            fullScreenPass.enabled = active;

        if (passVolume != null)
            passVolume.enabled = ownerEffectsEnabled;

        if (runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(IntensityId, amount);
        runtimeMaterial.SetFloat(SharpRadiusId, sharpRadius);
        runtimeMaterial.SetFloat(OuterRadiusId, Mathf.Max(sharpRadius + 0.01f, blurOuterRadius));
        runtimeMaterial.SetFloat(BlurSizeId, blurSize);
    }

    private void EnsurePass()
    {
        if (playerCamera == null)
            return;

        if (runtimeMaterial == null)
        {
            Shader shader = blurShader != null ? blurShader : Shader.Find(ShaderName);
            if (shader == null)
                return;

            runtimeMaterial = new Material(shader)
            {
                name = "AdsSightBlur (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        if (passVolume == null)
        {
            passVolume = playerCamera.GetComponent<CustomPassVolume>();
            if (passVolume == null)
                passVolume = playerCamera.gameObject.AddComponent<CustomPassVolume>();

            passVolume.hideFlags = HideFlags.DontSave;
            passVolume.isGlobal = false;
            passVolume.targetCamera = playerCamera;
            passVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
            passVolume.priority = 0f;
        }

        if (fullScreenPass == null)
        {
            for (int i = 0; i < passVolume.customPasses.Count; i++)
            {
                if (passVolume.customPasses[i] is FullScreenCustomPass existing)
                {
                    fullScreenPass = existing;
                    break;
                }
            }

            if (fullScreenPass == null)
            {
                fullScreenPass = new FullScreenCustomPass();
                passVolume.customPasses.Add(fullScreenPass);
            }
        }

        fullScreenPass.name = "ADS Sight Blur";
        fullScreenPass.fetchColorBuffer = true;
        fullScreenPass.fullscreenPassMaterial = runtimeMaterial;
        fullScreenPass.materialPassName = "Custom Pass 0";
        passVolume.targetCamera = playerCamera;
        passVolume.isGlobal = false;
        passVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
    }

    private void TeardownPass()
    {
        if (fullScreenPass != null)
            fullScreenPass.enabled = false;

        if (passVolume != null)
        {
            if (Application.isPlaying)
                Destroy(passVolume);
            else
                DestroyImmediate(passVolume);
            passVolume = null;
        }

        fullScreenPass = null;

        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
            runtimeMaterial = null;
        }
    }
}
