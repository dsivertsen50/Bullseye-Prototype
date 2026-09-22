using System.Collections.Generic;
using UnityEngine;

public enum DigitalDespawnDirection
{
    BottomToTop = 0,
    TopToBottom = 1,
    Random = 2,
    CenterOut = 3
}

/// <summary>
/// Modular visual for REQ-066 digital deletion. Swaps visible renderers onto
/// a dissolve material, then restores the originals on respawn.
/// </summary>
public class DigitalDespawnPresenter : MonoBehaviour
{
    private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
    private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
    private static readonly int ScanLineStrengthId = Shader.PropertyToID("_ScanLineStrength");
    private static readonly int BoundsMinId = Shader.PropertyToID("_BoundsMin");
    private static readonly int BoundsMaxId = Shader.PropertyToID("_BoundsMax");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly string[] AlbedoTextureNames =
    {
        "_BaseColorMap", "_UnlitColorMap", "_BaseMap", "_MainTex", "_Albedo"
    };
    private static readonly string[] AlbedoColorNames =
    {
        "_BaseColor", "_UnlitColor", "_Color"
    };

    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private ParticleSystem digitalDespawnParticles;
    [SerializeField] private DigitalDespawnDirection dissolveDirection = DigitalDespawnDirection.BottomToTop;
    [SerializeField] private Color edgeColor = new Color(0.55f, 1.45f, 1.85f, 1f);
    [SerializeField] private float edgeWidth = 0.11f;
    [SerializeField] private float noiseScale = 18f;
    [SerializeField] private float pixelSize = 28f;
    [SerializeField] private float glitchStrength = 0.35f;
    [SerializeField] private float scanLineStrength = 0.55f;

    private readonly List<Renderer> capturedRenderers = new();
    private readonly List<Material[]> originalMaterials = new();
    private readonly List<Material[]> dissolveMaterials = new();
    private bool presenting;
    private float progress;
    private Bounds worldBounds;

    public float Progress => progress;
    public bool IsPresenting => presenting;

    public void Configure(
        Material material,
        ParticleSystem particles,
        DigitalDespawnDirection direction)
    {
        if (material != null)
            dissolveMaterial = material;
        if (particles != null)
            digitalDespawnParticles = particles;
        dissolveDirection = direction;
    }

    public void Begin(DigitalDespawnDirection? direction = null)
    {
        if (presenting)
            return;

        if (direction.HasValue)
            dissolveDirection = direction.Value;

        if (dissolveMaterial == null)
        {
            Shader shader = Shader.Find("Bullseye/DigitalDespawnDissolve");
            if (shader != null)
                dissolveMaterial = new Material(shader);
        }

        CaptureRenderers();
        if (capturedRenderers.Count == 0 || dissolveMaterial == null)
            return;

        presenting = true;
        progress = 0f;
        worldBounds = ComputeBounds();
        AssignDissolveMaterials();
        PlayParticles();
        ApplyProgress(0f);
    }

    public void SetProgress(float amount)
    {
        if (!presenting)
            return;

        ApplyProgress(Mathf.Clamp01(amount));
    }

    public void Restore()
    {
        StopParticles();
        RestoreMaterials();
        presenting = false;
        progress = 0f;
        capturedRenderers.Clear();
        originalMaterials.Clear();
        worldBounds = default;
    }

    private void ApplyProgress(float amount)
    {
        progress = amount;
        for (int i = 0; i < dissolveMaterials.Count; i++)
        {
            Material[] mats = dissolveMaterials[i];
            if (mats == null)
                continue;

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                    continue;
                ApplySharedProperties(mat);
                mat.SetFloat(DissolveId, progress);
            }
        }

        if (progress >= 0.999f)
            HideCapturedRenderers();
    }

    private void CaptureRenderers()
    {
        capturedRenderers.Clear();
        originalMaterials.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!ShouldDissolve(renderer))
                continue;

            capturedRenderers.Add(renderer);
            originalMaterials.Add(renderer.sharedMaterials);
        }
    }

    private bool ShouldDissolve(Renderer renderer)
    {
        if (renderer == null || renderer is ParticleSystemRenderer)
            return false;
        if (renderer.GetComponentInParent<Canvas>() != null)
            return false;
        if (renderer.GetComponentInParent<WorldHealthBar>() != null)
            return false;
        if (IsFirstPersonWeapon(renderer.transform))
            return false;
        if (!renderer.enabled || renderer.forceRenderingOff)
            return false;
        return renderer.sharedMaterials != null && renderer.sharedMaterials.Length > 0;
    }

    private static bool IsFirstPersonWeapon(Transform transform)
    {
        Transform current = transform;
        while (current != null)
        {
            string name = current.name;
            if (name == "WeaponView" || name == "FirstPersonWeaponCamera" || name == "WeaponMount")
                return true;
            current = current.parent;
        }

        return transform.gameObject.layer == LayerMask.NameToLayer("FirstPersonWeapon");
    }

    private Bounds ComputeBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        for (int i = 0; i < capturedRenderers.Count; i++)
        {
            Renderer renderer = capturedRenderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        return bounds;
    }

    private void AssignDissolveMaterials()
    {
        ClearDissolveMaterials();
        for (int i = 0; i < capturedRenderers.Count; i++)
        {
            Renderer renderer = capturedRenderers[i];
            Material[] source = originalMaterials[i];
            if (renderer == null || source == null)
            {
                dissolveMaterials.Add(null);
                continue;
            }

            Material[] created = new Material[source.Length];
            for (int m = 0; m < source.Length; m++)
            {
                Material instance = new Material(dissolveMaterial);
                CopyAppearance(source[m], instance);
                ApplySharedProperties(instance);
                created[m] = instance;
            }

            dissolveMaterials.Add(created);
            renderer.materials = created;
        }
    }

    private void ApplySharedProperties(Material material)
    {
        material.SetColor(EdgeColorId, edgeColor);
        material.SetFloat(EdgeWidthId, Mathf.Max(0.001f, edgeWidth));
        material.SetFloat(NoiseScaleId, Mathf.Max(0.01f, noiseScale));
        material.SetFloat(PixelSizeId, Mathf.Max(1f, pixelSize));
        material.SetFloat(DirectionId, (float)dissolveDirection);
        material.SetFloat(GlitchStrengthId, Mathf.Clamp01(glitchStrength));
        material.SetFloat(ScanLineStrengthId, Mathf.Clamp01(scanLineStrength));
        material.SetVector(BoundsMinId, worldBounds.min);
        material.SetVector(BoundsMaxId, worldBounds.max);
    }

    private static void CopyAppearance(Material source, Material destination)
    {
        if (source == null || destination == null)
            return;

        Texture albedo = null;
        for (int i = 0; i < AlbedoTextureNames.Length; i++)
        {
            if (!source.HasProperty(AlbedoTextureNames[i]))
                continue;
            Texture texture = source.GetTexture(AlbedoTextureNames[i]);
            if (texture == null)
                continue;
            albedo = texture;
            break;
        }

        if (albedo != null && destination.HasProperty(BaseColorMapId))
            destination.SetTexture(BaseColorMapId, albedo);

        Color color = Color.white;
        for (int i = 0; i < AlbedoColorNames.Length; i++)
        {
            if (!source.HasProperty(AlbedoColorNames[i]))
                continue;
            color = source.GetColor(AlbedoColorNames[i]);
            break;
        }

        if (destination.HasProperty(BaseColorId))
            destination.SetColor(BaseColorId, color);
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < capturedRenderers.Count; i++)
        {
            Renderer renderer = capturedRenderers[i];
            if (renderer == null)
                continue;

            if (i < originalMaterials.Count && originalMaterials[i] != null)
                renderer.sharedMaterials = originalMaterials[i];
            renderer.enabled = true;
            renderer.forceRenderingOff = false;
        }

        ClearDissolveMaterials();
    }

    private void HideCapturedRenderers()
    {
        for (int i = 0; i < capturedRenderers.Count; i++)
        {
            Renderer renderer = capturedRenderers[i];
            if (renderer == null)
                continue;
            renderer.enabled = false;
            renderer.forceRenderingOff = true;
        }
    }

    private void PlayParticles()
    {
        if (digitalDespawnParticles == null)
            return;

        digitalDespawnParticles.transform.position = worldBounds.center;
        digitalDespawnParticles.Play(true);
    }

    private void StopParticles()
    {
        if (digitalDespawnParticles == null)
            return;

        digitalDespawnParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ClearDissolveMaterials()
    {
        for (int i = 0; i < dissolveMaterials.Count; i++)
        {
            Material[] mats = dissolveMaterials[i];
            if (mats == null)
                continue;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null)
                    Destroy(mats[m]);
            }
        }

        dissolveMaterials.Clear();
    }

    private void OnDestroy()
    {
        ClearDissolveMaterials();
    }
}
