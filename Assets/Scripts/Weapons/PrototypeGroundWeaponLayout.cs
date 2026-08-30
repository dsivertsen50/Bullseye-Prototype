using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Server-spawned prototype temporary weapons so pickups can be tested without
/// timed spawn pads. The permanent pistol is never placed in the world.
/// </summary>
public class PrototypeGroundWeaponLayout : MonoBehaviour
{
    [SerializeField] private GroundWeaponPickup pickupPrefab;
    [SerializeField] private WeaponDefinition shotgun;
    [FormerlySerializedAs("rifle")]
    [SerializeField] private WeaponDefinition ak;
    [SerializeField] private WeaponDefinition dmr;
    [SerializeField] private Vector3[] shotgunPositions =
    {
        new(2.4f, 0.45f, -2.2f),
        new(-2.4f, 0.45f, -2.2f)
    };
    [FormerlySerializedAs("riflePositions")]
    [SerializeField] private Vector3[] akPositions =
    {
        new(2.4f, 0.45f, 2.2f),
        new(-2.4f, 0.45f, 2.2f)
    };
    [SerializeField] private Vector3[] dmrPositions =
    {
        new(1.6f, 0.7f, 2.2f),
        new(1.6f, 0.7f, -2.2f)
    };

    private bool spawned;

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted += HandleServerStarted;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
    }

    private void Update()
    {
        TrySpawn();
        if (spawned)
            enabled = false;
    }

    private void HandleServerStarted()
    {
        TrySpawn();
    }

    private void TrySpawn()
    {
        if (spawned)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer || !networkManager.IsListening)
            return;

        spawned = true;
        WeaponCatalog catalog = pickupPrefab != null ? pickupPrefab.Catalog : null;
        Vector3[] dmrSpawn = PositionsBesideOtherWeapons();
        int shotgunCount = SpawnAll(Resolve(shotgun, catalog, "shotgun"), shotgunPositions);
        int akCount = SpawnAll(Resolve(ak, catalog, "ak"), akPositions);
        int dmrCount = SpawnAll(Resolve(dmr, catalog, "dmr"), dmrSpawn);
        Debug.Log(
            $"Spawned prototype ground weapons. Shotgun={shotgunCount} AK={akCount} DMR={dmrCount} " +
            $"DMR at {dmrSpawn[0]} and {dmrSpawn[1]}");
    }

    private static WeaponDefinition Resolve(WeaponDefinition assigned, WeaponCatalog catalog, string weaponId)
    {
        if (assigned != null)
            return assigned;
        return catalog != null ? catalog.GetById(weaponId) : null;
    }

    private Vector3[] PositionsBesideOtherWeapons()
    {
        Vector3 ak = akPositions != null && akPositions.Length > 0
            ? akPositions[0]
            : new Vector3(2.4f, 0.45f, 2.2f);
        Vector3 shotgun = shotgunPositions != null && shotgunPositions.Length > 0
            ? shotgunPositions[0]
            : new Vector3(2.4f, 0.45f, -2.2f);

        return new[]
        {
            new Vector3(ak.x - 0.8f, 0.7f, ak.z),
            new Vector3(shotgun.x - 0.8f, 0.7f, shotgun.z)
        };
    }

    private int SpawnAll(WeaponDefinition definition, Vector3[] positions)
    {
        if (definition == null || pickupPrefab == null || positions == null)
            return 0;

        if (definition.IsPermanentDefault)
            return 0;

        int spawnedCount = 0;
        for (int i = 0; i < positions.Length; i++)
        {
            GroundWeaponPickup pickup = GroundWeaponPickup.SpawnDropped(
                pickupPrefab,
                definition,
                new WeaponRuntimeState
                {
                    CatalogIndex = 0,
                    Magazine = definition.StartingMagazineAmmo,
                    Reserve = definition.StartingReserveAmmo
                },
                positions[i],
                Quaternion.Euler(0f, 40f * i, 0f),
                ulong.MaxValue,
                0f);
            if (pickup != null)
                spawnedCount++;
        }

        return spawnedCount;
    }
}
