using System.Collections;
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

    [Header("Physics")]
    [SerializeField] private float mass = 0.85f;
    [SerializeField] private float linearDamping = 0.35f;
    [SerializeField] private float angularDamping = 2.2f;
    [SerializeField] private Vector3 physicsBoxSize = new(0.85f, 0.16f, 0.28f);
    [SerializeField] private Vector3 physicsBoxCenter = new(0f, 0.08f, 0f);
    [SerializeField] private PhysicsMaterial physicsMaterial;

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
    private Vector3 pendingVelocity;
    private bool hasPendingVelocity;
    private float spawnedAt;
    private Rigidbody body;
    private BoxCollider physicsCollider;
    private Coroutine dropperIgnoreRoutine;

    public WeaponCatalog Catalog => catalog;
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

        if (GetComponent<SphereCollider>() == null)
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.7f;
            sphere.center = new Vector3(0f, 0.12f, 0f);
        }

        EnsurePhysicsBody();
    }

    public static GroundWeaponPickup SpawnDropped(
        GroundWeaponPickup prefab,
        WeaponDefinition definition,
        WeaponRuntimeState state,
        Vector3 position,
        Quaternion rotation,
        ulong ignoreClientId,
        float ignoreDuration,
        Vector3 initialVelocity = default)
    {
        if (prefab == null || definition == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return null;

        GroundWeaponPickup instance = Instantiate(prefab, position, rotation);
        instance.gameObject.name = "GroundWeaponPickup_" + definition.DisplayName;
        instance.definition = definition;
        instance.catalog = instance.catalog != null ? instance.catalog : prefab.catalog;
        instance.pendingDefinition = definition;
        instance.pendingMagazine = state.Magazine;
        instance.pendingReserve = state.Reserve;
        instance.pendingIgnoreClientId = ignoreClientId;
        instance.hasPendingState = true;
        instance.pendingVelocity = initialVelocity;
        instance.hasPendingVelocity = initialVelocity.sqrMagnitude > 0.0001f;
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

            ApplyPendingVelocity();
            IgnoreDropperCollisionsTemporarily();
        }

        RebuildVisual();
    }

    public override void OnNetworkDespawn()
    {
        catalogIndex.OnValueChanged -= OnIdentityChanged;
        magazine.OnValueChanged -= OnAmmoChanged;
        reserve.OnValueChanged -= OnAmmoChanged;
        if (dropperIgnoreRoutine != null)
        {
            StopCoroutine(dropperIgnoreRoutine);
            dropperIgnoreRoutine = null;
        }
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
        ApplyPickupPhysicsShape(current);
    }

    private WeaponCatalog FindCatalogFallback()
    {
        PlayerWeaponInventory inventory = FindAnyObjectByType<PlayerWeaponInventory>();
        return inventory != null ? inventory.Catalog : null;
    }

    private void EnsurePhysicsBody()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.mass = Mathf.Max(0.1f, mass);
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.linearDamping = Mathf.Max(0f, linearDamping);
        body.angularDamping = Mathf.Max(0f, angularDamping);
        body.sleepThreshold = 0.08f;

        physicsCollider = GetComponent<BoxCollider>();
        if (physicsCollider == null)
            physicsCollider = gameObject.AddComponent<BoxCollider>();

        physicsCollider.isTrigger = false;
        physicsCollider.size = physicsBoxSize;
        physicsCollider.center = physicsBoxCenter;
        if (physicsMaterial != null)
            physicsCollider.material = physicsMaterial;
    }

    private void ApplyPickupPhysicsShape(WeaponDefinition current)
    {
        if (physicsCollider == null || current == null)
            return;

        if (!current.FitPickupColliderToMesh)
        {
            if (current.PickupPhysicsBoxSize.sqrMagnitude > 0.0001f)
            {
                physicsCollider.size = current.PickupPhysicsBoxSize;
                physicsCollider.center = current.PickupPhysicsBoxCenter;
            }

            return;
        }

        if (!TryGetVisualLocalBounds(out Bounds localBounds))
            return;

        Vector3 size = localBounds.size;
        size.x = Mathf.Max(0.08f, size.x);
        size.z = Mathf.Max(0.08f, size.z);
        float minHeight = current.PickupColliderMinHeight;
        float bottom = localBounds.min.y;
        size.y = Mathf.Max(minHeight, size.y);

        Vector3 center = localBounds.center;
        center.y = bottom + size.y * 0.5f;
        physicsCollider.size = size;
        physicsCollider.center = center;
        if (body != null)
            body.centerOfMass = center;
    }

    private bool TryGetVisualLocalBounds(out Bounds localBounds)
    {
        localBounds = default;
        if (spawnedModel == null)
            return false;

        Renderer[] renderers = spawnedModel.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return false;

        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            EncapsulateWorldBounds(renderers[i].bounds, ref localBounds, ref initialized);
        }

        return initialized;
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref Bounds localBounds, ref bool initialized)
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 world = center + new Vector3(extents.x * x, extents.y * y, extents.z * z);
                    Vector3 local = transform.InverseTransformPoint(world);
                    if (!initialized)
                    {
                        localBounds = new Bounds(local, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(local);
                    }
                }
            }
        }
    }

    private void ApplyPendingVelocity()
    {
        if (!IsServer || body == null)
            return;

        body.isKinematic = false;
        body.WakeUp();
        if (hasPendingVelocity)
            body.linearVelocity = pendingVelocity;
    }

    private void IgnoreDropperCollisionsTemporarily()
    {
        if (!IsServer || pendingIgnoreClientId == ulong.MaxValue || NetworkManager == null)
            return;

        NetworkObject playerObject = NetworkManager.SpawnManager.GetPlayerNetworkObject(pendingIgnoreClientId);
        if (playerObject == null)
            return;

        Collider[] playerColliders = playerObject.GetComponentsInChildren<Collider>(true);
        Collider[] pickupColliders = GetComponents<Collider>();
        SetIgnoreCollisions(pickupColliders, playerColliders, true);

        if (dropperIgnoreRoutine != null)
            StopCoroutine(dropperIgnoreRoutine);

        dropperIgnoreRoutine = StartCoroutine(RestoreDropperCollisionsAfterWindow(pickupColliders, playerColliders));
    }

    private IEnumerator RestoreDropperCollisionsAfterWindow(Collider[] pickupColliders, Collider[] playerColliders)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, dropIgnoreWindow));
        SetIgnoreCollisions(pickupColliders, playerColliders, false);
        dropperIgnoreRoutine = null;
    }

    private static void SetIgnoreCollisions(Collider[] first, Collider[] second, bool ignore)
    {
        if (first == null || second == null)
            return;

        for (int i = 0; i < first.Length; i++)
        {
            Collider a = first[i];
            if (a == null || a.isTrigger)
                continue;

            for (int j = 0; j < second.Length; j++)
            {
                Collider b = second[j];
                if (b == null || b.isTrigger)
                    continue;

                Physics.IgnoreCollision(a, b, ignore);
            }
        }
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
