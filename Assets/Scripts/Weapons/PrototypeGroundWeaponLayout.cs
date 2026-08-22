using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-spawned prototype temporary weapons so pickups can be tested without
/// timed spawn pads. The permanent pistol is never placed in the world.
/// </summary>
public class PrototypeGroundWeaponLayout : MonoBehaviour
{
    [SerializeField] private GroundWeaponPickup pickupPrefab;
    [SerializeField] private WeaponDefinition shotgun;
    [SerializeField] private WeaponDefinition rifle;
    [SerializeField] private Vector3[] shotgunPositions =
    {
        new(2.4f, 0.45f, -2.2f),
        new(-2.4f, 0.45f, -2.2f)
    };
    [SerializeField] private Vector3[] riflePositions =
    {
        new(2.4f, 0.45f, 2.2f),
        new(-2.4f, 0.45f, 2.2f)
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
        int shotgunCount = SpawnAll(shotgun, shotgunPositions);
        int rifleCount = SpawnAll(rifle, riflePositions);
        Debug.Log($"Spawned prototype ground weapons. Shotgun={shotgunCount} AK={rifleCount}");
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
