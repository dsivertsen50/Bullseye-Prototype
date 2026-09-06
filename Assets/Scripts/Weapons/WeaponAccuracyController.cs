using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner-side source of truth for the equipped weapon's current spread.
/// Hitscan and the local reticle both read from this component.
/// </summary>
[DefaultExecutionOrder(100)]
public class WeaponAccuracyController : MonoBehaviour
{
    private static readonly WeaponAccuracySettings FallbackSettings = new();

    [SerializeField, Tooltip("Shows current spread values on screen. Off for normal play.")]
    private bool showDebug;

    private PlayerWeaponInventory inventory;
    private PlayerMovement playerMovement;
    private PlayerHealth playerHealth;
    private WeaponPresentationController weaponPresentation;
    private PlayerAimZoom playerAimZoom;
    private NetworkObject networkObject;

    private WeaponDefinition trackedDefinition;
    private int trackedCatalogIndex = int.MinValue;
    private float shotBloom;
    private float sprintBloom;
    private float timeSinceShot = 999f;

    public WeaponAccuracySettings Settings
    {
        get
        {
            WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : trackedDefinition;
            return definition != null && definition.Accuracy != null
                ? definition.Accuracy
                : FallbackSettings;
        }
    }

    public float BaseSpread => Settings.BaseSpread;
    public float MaxSpread => Settings.MaxSpread;
    public float ShotBloom => shotBloom;
    public float SprintBloom => sprintBloom;
    public float CurrentSpread
    {
        get
        {
            float raw = Mathf.Clamp(BaseSpread + shotBloom + sprintBloom, BaseSpread, MaxSpread);
            return Mathf.Clamp(raw * AdsSpreadScale, 0f, MaxSpread);
        }
    }
    public float CurrentSpreadPixels => ToScreenPixels(CurrentSpread);

    private void Awake()
    {
        inventory = GetComponent<PlayerWeaponInventory>();
        playerMovement = GetComponent<PlayerMovement>();
        playerHealth = GetComponent<PlayerHealth>();
        weaponPresentation = GetComponent<WeaponPresentationController>();
        playerAimZoom = GetComponent<PlayerAimZoom>();
        networkObject = GetComponent<NetworkObject>();
    }

    private void OnEnable()
    {
        if (inventory != null)
            inventory.InventoryChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryChanged -= OnInventoryChanged;
    }

    private void Update()
    {
        if (!IsLocalOwner())
            return;

        if (playerHealth != null && playerHealth.IsDead)
        {
            ResetBloom();
            return;
        }

        SyncEquippedWeapon();
        float dt = Time.deltaTime;
        UpdateSprintBloom(dt);
        UpdateShotBloom(dt);
    }

    public void NotifyShotFired()
    {
        if (!IsLocalOwner())
            return;

        WeaponAccuracySettings settings = Settings;
        float remaining = Mathf.Max(0f, settings.MaxSpread - CurrentSpread);
        shotBloom += Mathf.Min(settings.BloomPerShot, remaining);
        timeSinceShot = 0f;
    }

    public Ray GetHitscanRay(Camera camera)
    {
        return GetHitscanRay(camera, CurrentSpread);
    }

    public Ray GetCenterHitscanRay(Camera camera)
    {
        if (camera == null)
            return new Ray(transform.position, transform.forward);

        var screenPoint = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        return camera.ScreenPointToRay(screenPoint);
    }

    public Ray GetHitscanRay(Camera camera, float spreadAt1080)
    {
        if (camera == null)
            return new Ray(transform.position, transform.forward);

        Vector2 offset = SampleSpreadOffset(spreadAt1080);
        var screenPoint = new Vector3(
            Screen.width * 0.5f + offset.x,
            Screen.height * 0.5f + offset.y,
            0f);
        return camera.ScreenPointToRay(screenPoint);
    }

    public Vector2 SampleSpreadOffset()
    {
        return SampleSpreadOffset(CurrentSpread);
    }

    public Vector2 SampleSpreadOffset(float spreadAt1080)
    {
        float radius = ToScreenPixels(Mathf.Max(0f, spreadAt1080));
        if (radius <= 0.01f)
            return Vector2.zero;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = radius * Mathf.Sqrt(Random.value);
        return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
    }

    public static float ToScreenPixels(float spreadAt1080)
    {
        float height = Screen.height > 0 ? Screen.height : WeaponAccuracySettings.ReferenceScreenHeight;
        return spreadAt1080 * (height / WeaponAccuracySettings.ReferenceScreenHeight);
    }

    private float AdsSpreadScale
    {
        get
        {
            float t = 0f;
            if (weaponPresentation != null)
                t = weaponPresentation.AimBlend;
            else if (playerAimZoom != null && playerAimZoom.IsAiming)
                t = 1f;

            return Mathf.Lerp(1f, Settings.AdsSpreadMultiplier, Mathf.Clamp01(t));
        }
    }

    private void OnInventoryChanged()
    {
        SyncEquippedWeapon();
    }

    private void SyncEquippedWeapon()
    {
        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        int catalogIndex = inventory != null ? inventory.ActiveState.CatalogIndex : -1;
        if (definition == trackedDefinition && catalogIndex == trackedCatalogIndex)
            return;

        trackedDefinition = definition;
        trackedCatalogIndex = catalogIndex;
        ResetBloom(keepSprintState: true);
    }

    private void UpdateSprintBloom(float deltaTime)
    {
        WeaponAccuracySettings settings = Settings;
        bool sprinting = playerMovement != null && playerMovement.IsSprinting;
        float target = sprinting ? settings.SprintBloomTarget : 0f;
        float speed = sprinting ? settings.SprintSpreadIncreaseSpeed : settings.SprintSpreadRecoverySpeed;
        sprintBloom = Mathf.MoveTowards(sprintBloom, target, speed * deltaTime);
        ClampBloomToMax();
    }

    private void UpdateShotBloom(float deltaTime)
    {
        timeSinceShot += deltaTime;
        WeaponAccuracySettings settings = Settings;
        if (timeSinceShot < settings.BloomRecoveryDelay)
            return;

        shotBloom = Mathf.MoveTowards(shotBloom, 0f, settings.BloomRecoverySpeed * deltaTime);
    }

    private void ResetBloom(bool keepSprintState = false)
    {
        shotBloom = 0f;
        timeSinceShot = 999f;
        if (keepSprintState && playerMovement != null && playerMovement.IsSprinting)
            sprintBloom = Settings.SprintBloomTarget;
        else
            sprintBloom = 0f;
    }

    private void ClampBloomToMax()
    {
        float overflow = BaseSpread + shotBloom + sprintBloom - MaxSpread;
        if (overflow <= 0f)
            return;

        if (shotBloom >= overflow)
            shotBloom -= overflow;
        else
        {
            overflow -= shotBloom;
            shotBloom = 0f;
            sprintBloom = Mathf.Max(0f, sprintBloom - overflow);
        }
    }

    private bool IsLocalOwner()
    {
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
    }

    private void OnGUI()
    {
        if (!showDebug || !IsLocalOwner())
            return;

        WeaponAccuracySettings settings = Settings;
        string text =
            $"Spread {CurrentSpread:0.0} / {settings.MaxSpread:0.0}\n" +
            $"Base {settings.BaseSpread:0.0}\n" +
            $"Shot bloom {shotBloom:0.0}\n" +
            $"Sprint bloom {sprintBloom:0.0}";

        GUI.Label(new Rect(16f, 96f, 280f, 90f), text);
    }
}
