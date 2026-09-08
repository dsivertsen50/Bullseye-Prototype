using UnityEngine;

/// <summary>
/// Pushes the authoritative attached bullseye pose into the character HDRP
/// material so the mark is painted on the animated mesh.
/// </summary>
public class BullseyeSurfaceVisual : MonoBehaviour
{
    private static readonly int CenterEnabledId = Shader.PropertyToID("_BullseyeCenterEnabled");
    private static readonly int NormalRadiusId = Shader.PropertyToID("_BullseyeNormalRadius");
    private static readonly int TangentId = Shader.PropertyToID("_BullseyeTangentWS");
    private static readonly int BitangentId = Shader.PropertyToID("_BullseyeBitangentWS");
    private static readonly int WrapAxisRadiusId = Shader.PropertyToID("_BullseyeWrapAxisRadius");
    private static readonly int RegionStateId = Shader.PropertyToID("_BullseyeRegionState");
    private static readonly int WrapFlashId = Shader.PropertyToID("_BullseyeWrapFlash");
    private static readonly int TextureId = Shader.PropertyToID("_BullseyeTexture");

    [SerializeField] private SkinnedMeshRenderer characterRenderer;
    [SerializeField] private Texture2D bullseyeTexture;
    [SerializeField] private float stampRadius = 0.13f;
    [SerializeField] private float flashDuration = 0.12f;
    [SerializeField] private bool debugVisualization;

    private MaterialPropertyBlock propertyBlock;

    private bool attachedVisible = true;
    private bool ownerSuppressed;
    private float flashUntil;
    private Vector3 lastPosition;
    private Vector3 lastNormal = Vector3.forward;
    private Vector3 lastTangent = Vector3.right;
    private Vector3 lastBitangent = Vector3.up;
    private Vector3 lastWrapAxis = Vector3.up;
    private float lastWrapRadius = 0.12f;
    private int lastCurrentRegion;
    private int lastTargetRegion;
    private float lastProgress;
    private bool hasPose;

    public float StampRadius
    {
        get => stampRadius;
        set => stampRadius = Mathf.Max(0.04f, value);
    }

    public void Configure(SkinnedMeshRenderer renderer, Texture2D texture, float radius)
    {
        characterRenderer = renderer;
        bullseyeTexture = texture;
        stampRadius = Mathf.Max(0.04f, radius);
        ApplyShaderState(false);
    }

    private void Awake()
    {
        if (characterRenderer == null)
            characterRenderer = FindCharacterRenderer();
        ApplyShaderState(false);
    }

    public void SetAttachedVisible(bool visible)
    {
        attachedVisible = visible;
        if (!visible)
            ApplyShaderState(false);
    }

    public void SetSuppressedForOwner(bool suppressed)
    {
        ownerSuppressed = suppressed;
        if (suppressed)
            ApplyShaderState(false);
    }

    public void PlayHitFlash()
    {
        if (!IsShowing())
            return;

        flashUntil = Time.time + Mathf.Max(0.02f, flashDuration);
    }

    public void ApplyPose(Vector3 worldPosition, Vector3 worldNormal, Quaternion rotation)
    {
        ApplyPose(worldPosition, worldNormal, rotation, Vector3.up, 0.13f, 0, 0, 1f);
    }

    public void ApplyPose(
        Vector3 worldPosition,
        Vector3 worldNormal,
        Quaternion rotation,
        Vector3 wrapAxis,
        float wrapRadius)
    {
        ApplyPose(worldPosition, worldNormal, rotation, wrapAxis, wrapRadius, 0, 0, 1f);
    }

    public void ApplyPose(
        Vector3 worldPosition,
        Vector3 worldNormal,
        Quaternion rotation,
        Vector3 wrapAxis,
        float wrapRadius,
        int currentRegion,
        int targetRegion,
        float travelProgress)
    {
        if (worldNormal.sqrMagnitude < 0.0001f)
            worldNormal = Vector3.forward;
        worldNormal.Normalize();
        if (wrapAxis.sqrMagnitude < 0.0001f)
            wrapAxis = Vector3.up;
        wrapAxis.Normalize();

        float sphereBlend = 0f;
        if (BullseyeSurfaceFamilies.UsesSphericalWrap((BullseyeSurfaceRegionId)currentRegion))
            sphereBlend += 1f - Mathf.Clamp01(travelProgress);
        if (BullseyeSurfaceFamilies.UsesSphericalWrap((BullseyeSurfaceRegionId)targetRegion))
            sphereBlend += Mathf.Clamp01(travelProgress);
        bool spherical = sphereBlend >= 0.45f;
        if (!spherical && Mathf.Abs(Vector3.Dot(wrapAxis, worldNormal)) > 0.94f)
        {
            wrapAxis = Vector3.Cross(worldNormal, Vector3.up);
            if (wrapAxis.sqrMagnitude < 0.0001f)
                wrapAxis = Vector3.Cross(worldNormal, Vector3.right);
            wrapAxis.Normalize();
        }

        Vector3 tangent = spherical
            ? Vector3.Cross(worldNormal, Vector3.up)
            : Vector3.Cross(wrapAxis, worldNormal);
        if (tangent.sqrMagnitude < 0.0001f)
            tangent = rotation * Vector3.right;
        tangent.Normalize();
        if (hasPose && Vector3.Dot(tangent, lastTangent) < 0f)
            tangent = -tangent;
        Vector3 bitangent = Vector3.Cross(worldNormal, tangent).normalized;

        lastPosition = worldPosition;
        lastNormal = worldNormal;
        lastTangent = tangent;
        lastBitangent = bitangent;
        lastWrapAxis = wrapAxis;
        lastWrapRadius = Mathf.Max(0.02f, wrapRadius);
        lastCurrentRegion = currentRegion;
        lastTargetRegion = targetRegion;
        lastProgress = Mathf.Clamp01(travelProgress);
        hasPose = true;

        ApplyShaderState(IsShowing());
        DisableLegacyAttachedVisuals();
    }

    private bool IsShowing()
    {
        return attachedVisible && !ownerSuppressed;
    }

    private void ApplyShaderState(bool show)
    {
        if (characterRenderer == null)
            characterRenderer = FindCharacterRenderer();
        if (characterRenderer == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        characterRenderer.GetPropertyBlock(propertyBlock);
        float enabled = show && hasPose ? 1f : 0f;
        float radius = Mathf.Max(0.04f, stampRadius);
        float flash = 0f;
        if (show && Time.time < flashUntil)
            flash = 1f - Mathf.Clamp01((flashUntil - Time.time) / Mathf.Max(0.02f, flashDuration));

        var currentId = (BullseyeSurfaceRegionId)lastCurrentRegion;
        var targetId = (BullseyeSurfaceRegionId)lastTargetRegion;
        float currentFamily = BullseyeSurfaceFamilies.FromRegion(currentId);
        float targetFamily = BullseyeSurfaceFamilies.FromRegion(targetId);
        float facing = BullseyeSurfaceFamilies.FacingValue(CurrentFacing());
        if (Mathf.Abs(currentFamily - BullseyeSurfaceFamilies.Torso) < 0.01f &&
            Mathf.Abs(targetFamily - BullseyeSurfaceFamilies.Torso) < 0.01f &&
            CurrentFacing() != TargetFacing())
        {
            facing = 0f;
        }

        float sphereBlend = 0f;
        if (BullseyeSurfaceFamilies.UsesSphericalWrap(currentId))
            sphereBlend += 1f - lastProgress;
        if (BullseyeSurfaceFamilies.UsesSphericalWrap(targetId))
            sphereBlend += lastProgress;

        propertyBlock.SetVector(CenterEnabledId, new Vector4(lastPosition.x, lastPosition.y, lastPosition.z, enabled));
        propertyBlock.SetVector(NormalRadiusId, new Vector4(lastNormal.x, lastNormal.y, lastNormal.z, radius));
        propertyBlock.SetVector(TangentId, lastTangent);
        propertyBlock.SetVector(BitangentId, lastBitangent);
        propertyBlock.SetVector(WrapAxisRadiusId, new Vector4(lastWrapAxis.x, lastWrapAxis.y, lastWrapAxis.z, lastWrapRadius));
        propertyBlock.SetVector(RegionStateId, new Vector4(currentFamily, targetFamily, lastProgress, facing));
        propertyBlock.SetVector(WrapFlashId, new Vector4(sphereBlend, flash, 0f, 0f));
        if (bullseyeTexture != null)
            propertyBlock.SetTexture(TextureId, bullseyeTexture);

        characterRenderer.SetPropertyBlock(propertyBlock);
        ClearStampFromOtherRenderers();
    }

    private BullseyeFacing CurrentFacing()
    {
        if (TryGetComponent(out BullseyeSurfaceMap map))
            return map.GetFacing(lastCurrentRegion);
        return BullseyeFacing.Front;
    }

    private BullseyeFacing TargetFacing()
    {
        if (TryGetComponent(out BullseyeSurfaceMap map))
            return map.GetFacing(lastTargetRegion);
        return BullseyeFacing.Front;
    }

    private void DisableLegacyAttachedVisuals()
    {
        Transform system = transform.Find("BullseyeSystem");
        if (system == null)
            return;

        Transform visual = system.Find("AttachedVisual");
        if (visual != null)
            visual.gameObject.SetActive(false);

        Transform sticker = system.Find("AttachedSticker");
        if (sticker != null)
            sticker.gameObject.SetActive(false);

        Transform overlay = system.Find("StampOverlay");
        if (overlay != null)
        {
            overlay.gameObject.SetActive(false);
            var overlayRenderer = overlay.GetComponent<Renderer>();
            if (overlayRenderer != null)
            {
                overlayRenderer.enabled = false;
                overlayRenderer.forceRenderingOff = true;
            }
        }
    }

    private SkinnedMeshRenderer FindCharacterRenderer()
    {
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            string name = renderers[i].gameObject.name;
            if (name.IndexOf("StickMan", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return renderers[i];
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].gameObject.name != "StampOverlay")
                return renderers[i];
        }

        return null;
    }

    private void ClearStampFromOtherRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer == characterRenderer)
                continue;
            if (renderer is SkinnedMeshRenderer && renderer.gameObject.name.IndexOf("StickMan", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            renderer.SetPropertyBlock(null);
        }
    }

    private void OnDrawGizmos()
    {
        bool showDebug = debugVisualization;
        if (!showDebug && TryGetComponent(out BullseyeMover mover))
            showDebug = mover.DebugVisualization;
        if (!showDebug || !hasPose)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(lastPosition, 0.02f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(lastPosition, lastPosition + lastNormal * 0.22f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(lastPosition, lastPosition + lastTangent * 0.16f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(lastPosition, lastPosition + lastWrapAxis * 0.2f);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
        Gizmos.DrawWireSphere(lastPosition, lastWrapRadius);
    }

    private void OnValidate()
    {
        stampRadius = Mathf.Max(0.04f, stampRadius);
        flashDuration = Mathf.Max(0.02f, flashDuration);
    }
}
