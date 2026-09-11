using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner aim + server spawn for projectile weapons. Weapon-specific values
/// come from WeaponDefinition.ProjectileSettings.
/// </summary>
public class PlayerProjectileLauncher : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private WeaponPresentationController firstPersonWeapon;
    [SerializeField] private WorldWeaponView worldWeapon;
    [SerializeField] private PlayerWeaponInventory inventory;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private float maxReportedMuzzleDistance = 3.5f;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
        if (firstPersonWeapon == null)
            firstPersonWeapon = GetComponent<WeaponPresentationController>();
        if (worldWeapon == null)
            worldWeapon = GetComponent<WorldWeaponView>();
        if (inventory == null)
            inventory = GetComponent<PlayerWeaponInventory>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    public bool TryFire(WeaponDefinition definition)
    {
        if (!IsOwner || !IsSpawned || definition == null)
            return false;

        if (definition.FireType != WeaponFireType.Projectile)
            return false;

        WeaponProjectileSettings settings = definition.ProjectileSettings;
        if (settings == null || settings.ProjectilePrefab == null)
            return false;

        ResolveLaunch(definition, out Vector3 muzzle, out Vector3 aimPoint, out Vector3 direction);
        FireProjectileServerRpc(muzzle, aimPoint, direction);
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void FireProjectileServerRpc(
        Vector3 reportedMuzzle,
        Vector3 reportedAimPoint,
        Vector3 reportedDirection,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        WeaponDefinition definition = inventory != null ? inventory.ActiveDefinition : null;
        if (definition == null || definition.FireType != WeaponFireType.Projectile)
            return;

        WeaponProjectileSettings settings = definition.ProjectileSettings;
        GameObject prefab = settings != null ? settings.ProjectilePrefab : null;
        if (prefab == null || NetworkManager == null || !NetworkManager.IsListening)
            return;

        SanitizeLaunch(
            definition,
            reportedMuzzle,
            reportedAimPoint,
            reportedDirection,
            out Vector3 muzzle,
            out Vector3 aimPoint,
            out Vector3 direction);

        float offset = settings.SpawnForwardOffset;
        Vector3 spawnPosition = muzzle + direction * offset;
        Quaternion spawnRotation = Quaternion.LookRotation(direction, Vector3.up);

        GameObject instance = Instantiate(prefab, spawnPosition, spawnRotation);
        RocketProjectile rocket = instance.GetComponent<RocketProjectile>();
        if (rocket == null)
        {
            Destroy(instance);
            return;
        }

        rocket.Initialize(OwnerClientId, definition, settings, direction, aimPoint);

        NetworkObject networkObject = instance.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Destroy(instance);
            return;
        }

        networkObject.Spawn();

        if (settings.DebugAimTarget)
            Debug.DrawLine(spawnPosition, aimPoint, Color.cyan, 1.25f);
    }

    private void SanitizeLaunch(
        WeaponDefinition definition,
        Vector3 reportedMuzzle,
        Vector3 reportedAimPoint,
        Vector3 reportedDirection,
        out Vector3 muzzle,
        out Vector3 aimPoint,
        out Vector3 direction)
    {
        ResolveLaunch(definition, out Vector3 fallbackMuzzle, out Vector3 fallbackAim, out Vector3 fallbackDirection);

        muzzle = reportedMuzzle;
        if ((reportedMuzzle - transform.position).sqrMagnitude >
            maxReportedMuzzleDistance * maxReportedMuzzleDistance)
            muzzle = fallbackMuzzle;

        aimPoint = reportedAimPoint.sqrMagnitude > 0.0001f ? reportedAimPoint : fallbackAim;
        direction = reportedDirection.sqrMagnitude > 0.0001f
            ? reportedDirection.normalized
            : (aimPoint - muzzle);
        if (direction.sqrMagnitude < 0.0001f)
            direction = fallbackDirection;
        direction.Normalize();
    }

    private void ResolveLaunch(
        WeaponDefinition definition,
        out Vector3 muzzle,
        out Vector3 aimPoint,
        out Vector3 direction)
    {
        WeaponProjectileSettings settings = definition != null
            ? definition.ProjectileSettings
            : WeaponProjectileSettings.Fallback;
        float aimDistance = settings != null ? settings.AimDistance : 500f;

        Transform cameraTransform = playerCamera != null ? playerCamera.transform : transform;
        Vector3 aimOrigin = cameraTransform.position;
        Vector3 aimDirection = cameraTransform.forward;
        aimPoint = aimOrigin + aimDirection * aimDistance;

        if (Physics.Raycast(
                aimOrigin,
                aimDirection,
                out RaycastHit hit,
                aimDistance,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform))
                aimPoint = hit.point;
        }

        muzzle = ResolveMuzzle(cameraTransform, aimDirection);
        direction = aimPoint - muzzle;
        if (direction.sqrMagnitude < 0.0001f)
            direction = aimDirection;
        direction.Normalize();
    }

    private Vector3 ResolveMuzzle(Transform cameraTransform, Vector3 aimDirection)
    {
        Transform muzzle = null;
        if (IsOwner && firstPersonWeapon != null)
            muzzle = firstPersonWeapon.MuzzlePoint;
        if (muzzle == null && worldWeapon != null)
            muzzle = worldWeapon.MuzzlePoint;
        if (muzzle != null)
            return muzzle.position;

        return cameraTransform.position + aimDirection * 0.45f;
    }

    private void OnValidate()
    {
        maxReportedMuzzleDistance = Mathf.Max(0.5f, maxReportedMuzzleDistance);
    }
}
