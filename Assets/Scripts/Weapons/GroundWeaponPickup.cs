using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked ground temporary weapon. Server owns remaining ammo, claim, and despawn.
/// Pickups are claimed whole; ammunition is not scavenged from duplicates.
/// </summary>
public class GroundWeaponPickup : NetworkBehaviour
{
    [SerializeField] private WeaponCatalog catalog;
    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private Transform modelRoot;
    [SerializeField] private bool useDefinitionStartingAmmo = true;
    [SerializeField] private int placedMagazine = -1;
    [SerializeField] private int placedReserve = -1;
    [SerializeField] private float dropIgnoreWindow = 0.8f;

    private readonly NetworkVariable<int> catalogIndex = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> magazine = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> reserve = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> ignoredClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private GameObject spawnedModel;
    private bool claimed;
    private WeaponDefinition pendingDefinition;
    private int pendingMagazine;
    private int pendingReserve;
    private ulong pendingIgnoreClientId = ulong.MaxValue;
    private bool hasPendingState;
    private float spawnedAt;

    public WeaponDefinition Definition => catalog != null ? catalog.Get(catalogIndex.Value) : definition;
    public string WeaponId => Definition != null ? Definition.WeaponId : string.Empty;
    public int Magazine => magazine.Value;
    public int Reserve => reserve.Value;
    public int TotalAmmo => Mathf.Max(0, magazine.Value) + Mathf.Max(0, reserve.Value);
    public bool IsAvailable => IsSpawned && !claimed;

    private void Awake()
    {
        int layer = LayerMask.NameToLayer("WeaponPickup");
        if (layer >= 0)
            gameObject.layer = layer;

        if (GetComponent<Collider>() == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.7f;
            sphere.center = new Vector3(0f, 0.12f, 0f);
        }
    }

    public static GroundWeaponPickup SpawnDropped(
        GroundWeaponPickup prefab,
        WeaponDefinition definition,
        WeaponRuntimeState state,
        Vector3 position,
        Quaternion rotation,
        ulong ignoreClientId,
        float ignoreDuration)
    {
        if (prefab == null || definition == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return null;

        GroundWeaponPickup instance = Instantiate(prefab, position, rotation);
        instance.definition = definition;
        instance.catalog = instance.catalog != null ? instance.catalog : prefab.catalog;
        instance.pendingDefinition = definition;
        instance.pendingMagazine = state.Magazine;
        instance.pendingReserve = state.Reserve;
        instance.pendingIgnoreClientId = ignoreClientId;
        instance.hasPendingState = true;
        if (ignoreDuration > 0f)
            instance.dropIgnoreWindow = ignoreDuration;

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        networkObject.Spawn();
        return instance;
    }

    public override void OnNetworkSpawn()
    {
        spawnedAt = Time.time;
        catalogIndex.OnValueChanged += OnIdentityChanged;
        magazine.OnValueChanged += OnAmmoChanged;
        reserve.OnValueChanged += OnAmmoChanged;

        if (IsServer)
        {
            if (hasPendingState && pendingDefinition != null)
            {
                ConfigureServerState(pendingDefinition, pendingMagazine, pendingReserve);
                ignoredClientId.Value = pendingIgnoreClientId;
            }
            else if (catalogIndex.Value < 0)
                ApplyPlacedConfiguration();
        }

        RebuildVisual();
    }

    public override void OnNetworkDespawn()
    {
        catalogIndex.OnValueChanged -= OnIdentityChanged;
        magazine.OnValueChanged -= OnAmmoChanged;
        reserve.OnValueChanged -= OnAmmoChanged;
    }

    public bool IsIgnoredFor(ulong clientId)
    {
        return clientId == ignoredClientId.Value &&
               ignoredClientId.Value != ulong.MaxValue &&
               Time.time < spawnedAt + Mathf.Max(0.05f, dropIgnoreWindow);
    }

    public bool TryClaim(out WeaponRuntimeState state)
    {
        state = WeaponRuntimeState.Empty;
        if (!IsServer || claimed || !IsSpawned)
            return false;

        WeaponDefinition current = Definition;
        if (current == null || catalog == null)
            return false;

        int index = catalog.IndexOf(current);
        if (index < 0)
            return false;

        claimed = true;
        state = new WeaponRuntimeState
        {
            CatalogIndex = index,
            Magazine = magazine.Value,
            Reserve = reserve.Value
        };

        NetworkObject.Despawn(true);
        return true;
    }

    private void ApplyPlacedConfiguration()
    {
        WeaponDefinition placed = definition;
        if (placed == null || catalog == null)
            return;

        int mag = useDefinitionStartingAmmo ? placed.StartingMagazineAmmo : Mathf.Max(0, placedMagazine);
        int res = useDefinitionStartingAmmo ? placed.StartingReserveAmmo : Mathf.Max(0, placedReserve);
        ConfigureServerState(placed, mag, res);
    }

    private void ConfigureServerState(WeaponDefinition source, int mag, int res)
    {
        if (catalog == null)
            catalog = FindCatalogFallback();

        int index = catalog != null ? catalog.IndexOf(source) : -1;
        definition = source;
        catalogIndex.Value = index;
        magazine.Value = Mathf.Max(0, mag);
        reserve.Value = Mathf.Max(0, res);
    }

    private void OnIdentityChanged(int previous, int next)
    {
        RebuildVisual();
    }

    private void OnAmmoChanged(int previous, int next)
    {
        // Visual remains; remaining ammo is networked state only.
    }

    private void RebuildVisual()
    {
        if (modelRoot == null)
        {
            Transform existing = transform.Find("Model");
            if (existing != null)
            {
                modelRoot = existing;
            }
            else
            {
                GameObject modelObject = new("Model");
                modelRoot = modelObject.transform;
                modelRoot.SetParent(transform, false);
            }
        }

        if (spawnedModel != null)
        {
            Destroy(spawnedModel);
            spawnedModel = null;
        }

        WeaponDefinition current = Definition;
        if (current == null || current.PickupPrefab == null)
            return;

        spawnedModel = Instantiate(current.PickupPrefab, modelRoot);
        spawnedModel.transform.localPosition = current.PickupLocalPosition;
        spawnedModel.transform.localRotation = Quaternion.Euler(current.PickupLocalEuler);
        spawnedModel.transform.localScale = current.PickupLocalScale;
        DisableGameplayCollision(spawnedModel);
        ApplyLayerRecursively(spawnedModel, gameObject.layer);
    }

    private WeaponCatalog FindCatalogFallback()
    {
        PlayerWeaponInventory inventory = FindAnyObjectByType<PlayerWeaponInventory>();
        return inventory != null ? inventory.Catalog : null;
    }

    private static void DisableGameplayCollision(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }
    }

    private static void ApplyLayerRecursively(GameObject root, int layer)
    {
        if (root == null || layer < 0)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
    }
}
