using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Local-only bank-shot marker. Uses the same reflection helper as the
/// fired shot, but never participates in hit authority and ignores remote
/// player colliders on the reflected ray so it cannot reveal hidden enemies.
/// </summary>
[DefaultExecutionOrder(110)]
public class RicochetAimPredictor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float markerRadius = 0.11f;
    [SerializeField] private float markerWidth = 0.016f;
    [SerializeField] private float innerDotRadius = 0.028f;
    [SerializeField] private Color markerColor = new Color(1f, 0.16f, 0.12f, 1f);
    [SerializeField] private bool showRicochetDebug;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private PlayerWeaponInventory inventory;
    private WeaponAccuracyController accuracy;
    private PlayerHealth playerHealth;
    private NetworkObject networkObject;
    private Transform marker;
    private LineRenderer ring;
    private Material markerMaterial;
    private Material haloMaterial;
    private Material dotMaterial;

    private void Awake()
    {
        inventory = GetComponent<PlayerWeaponInventory>();
        accuracy = GetComponent<WeaponAccuracyController>();
        playerHealth = GetComponent<PlayerHealth>();
        networkObject = GetComponent<NetworkObject>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);
    }

    private void OnDisable()
    {
        HideMarker();
    }

    private void OnDestroy()
    {
        if (marker != null)
            Destroy(marker.gameObject);
        if (markerMaterial != null)
            Destroy(markerMaterial);
        if (haloMaterial != null)
            Destroy(haloMaterial);
        if (dotMaterial != null)
            Destroy(dotMaterial);
    }

    private void LateUpdate()
    {
        if (!IsLocalOwner())
        {
            HideMarker();
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            HideMarker();
            return;
        }

        if (LocalPlayerMenuState.IsOpen(this))
        {
            HideMarker();
            return;
        }

        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        if (definition != null && !definition.CanRicochet)
        {
            HideMarker();
            return;
        }

        if (!TryGetCenterRay(out Ray aimRay))
        {
            HideMarker();
            return;
        }

        float range = definition != null && definition.DamageSettings != null
            ? definition.DamageSettings.MaximumRange
            : 100f;

        HitscanRicochet.Trace(
            aimRay.origin,
            aimRay.direction,
            range,
            allowRicochet: true,
            HitscanRicochet.DefaultMaxRicochets,
            excludePlayersFromReflectedRay: true,
            networkObject,
            hits,
            out HitscanRicochet.TraceResult result);

        if (showRicochetDebug)
            HitscanRicochet.DrawDebug(result, Time.deltaTime * 2f);

        if (!result.hasBounce || !result.hasFinalHit || HitscanRicochet.IsPlayerOrBullseye(result.finalHit.collider))
        {
            HideMarker();
            return;
        }

        ShowMarker(result.finalHit);
    }

    private bool TryGetCenterRay(out Ray ray)
    {
        if (accuracy != null && playerCamera != null)
        {
            ray = accuracy.GetCenterHitscanRay(playerCamera);
            return true;
        }

        if (playerCamera != null)
        {
            ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            return true;
        }

        ray = default;
        return false;
    }

    private void ShowMarker(RaycastHit hit)
    {
        EnsureMarker();
        Vector3 normal = hit.normal.sqrMagnitude > 0.0001f ? hit.normal.normalized : Vector3.up;
        Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        marker.SetPositionAndRotation(
            hit.point + normal * 0.04f,
            Quaternion.LookRotation(normal, up));
        if (!marker.gameObject.activeSelf)
            marker.gameObject.SetActive(true);
    }

    private void HideMarker()
    {
        if (marker != null && marker.gameObject.activeSelf)
            marker.gameObject.SetActive(false);
    }

    private void EnsureMarker()
    {
        if (marker != null)
            return;

        var go = new GameObject("RicochetPredictionMarker");
        go.hideFlags = HideFlags.HideInHierarchy;
        marker = go.transform;

        markerMaterial = CreateMarkerMaterial(markerColor);
        Color haloColor = new Color(markerColor.r, markerColor.g, markerColor.b, 0.55f);

        ring = go.AddComponent<LineRenderer>();
        ConfigureRing(ring, markerMaterial, markerColor, markerWidth, Mathf.Max(0.03f, markerRadius));

        var haloObject = new GameObject("OuterHalo");
        haloObject.hideFlags = HideFlags.HideInHierarchy;
        haloObject.transform.SetParent(marker, false);
        LineRenderer halo = haloObject.AddComponent<LineRenderer>();
        haloMaterial = CreateMarkerMaterial(haloColor);
        ConfigureRing(halo, haloMaterial, haloColor, markerWidth * 1.6f, Mathf.Max(0.03f, markerRadius) * 1.35f);

        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(dot.GetComponent<Collider>());
        dot.name = "InnerDot";
        dot.hideFlags = HideFlags.HideInHierarchy;
        Transform dotTransform = dot.transform;
        dotTransform.SetParent(marker, false);
        float radius = Mathf.Max(0.03f, markerRadius);
        float dotSize = Mathf.Clamp(innerDotRadius * 2f, 0.012f, radius * 0.55f);
        dotTransform.localPosition = new Vector3(0f, 0f, 0.001f);
        dotTransform.localRotation = Quaternion.identity;
        dotTransform.localScale = new Vector3(dotSize, dotSize, 1f);

        if (markerMaterial != null)
        {
            dotMaterial = CreateMarkerMaterial(markerColor);
            MeshRenderer dotRenderer = dot.GetComponent<MeshRenderer>();
            dotRenderer.sharedMaterial = dotMaterial;
            dotRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dotRenderer.receiveShadows = false;
        }

        go.SetActive(false);
    }

    private static void ConfigureRing(
        LineRenderer line,
        Material material,
        Color color,
        float width,
        float radius)
    {
        line.loop = true;
        line.useWorldSpace = false;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.TransformZ;
        line.numCapVertices = 0;
        line.numCornerVertices = 2;
        line.positionCount = 36;
        line.startWidth = width;
        line.endWidth = width;
        line.sharedMaterial = material;
        line.startColor = color;
        line.endColor = color;
        line.allowOcclusionWhenDynamic = false;
        line.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = (i / (float)line.positionCount) * Mathf.PI * 2f;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private static Material CreateMarkerMaterial(Color color)
    {
        // Runtime HDRP/Unlit materials often draw nothing. Sprites/Default
        // is the same path that made the smaller marker visible.
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
            return null;

        var material = new Material(shader)
        {
            color = color,
            renderQueue = 3100
        };
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        return material;
    }

    private bool IsLocalOwner()
    {
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
    }
}
