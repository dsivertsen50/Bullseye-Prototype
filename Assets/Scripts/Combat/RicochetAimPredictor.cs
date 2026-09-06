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
    [SerializeField] private float markerRadius = 0.09f;
    [SerializeField] private float markerWidth = 0.018f;
    [SerializeField] private Color markerColor = new Color(1f, 0.82f, 0.28f, 0.85f);
    [SerializeField] private bool showRicochetDebug;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private PlayerWeaponInventory inventory;
    private WeaponAccuracyController accuracy;
    private PlayerHealth playerHealth;
    private NetworkObject networkObject;
    private Transform marker;
    private LineRenderer ring;
    private Material markerMaterial;

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
            hit.point + normal * 0.02f,
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

        ring = go.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.textureMode = LineTextureMode.Stretch;
        ring.numCapVertices = 0;
        ring.numCornerVertices = 2;
        ring.positionCount = 28;
        ring.startWidth = markerWidth;
        ring.endWidth = markerWidth;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Hidden/Internal-Colored");
        if (shader != null)
        {
            markerMaterial = new Material(shader)
            {
                color = markerColor
            };
            ring.sharedMaterial = markerMaterial;
        }

        ring.startColor = markerColor;
        ring.endColor = markerColor;

        float radius = Mathf.Max(0.03f, markerRadius);
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = (i / (float)ring.positionCount) * Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        go.SetActive(false);
    }

    private bool IsLocalOwner()
    {
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
    }
}
