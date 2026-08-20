using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Routes gameplay weapon actions to first-person and world presentation.
/// Does not apply damage.
/// </summary>
public class WeaponPresentationCoordinator : NetworkBehaviour
{
    private const float AimPitchQuantize = 0.25f;
    private const float AimPitchSendThreshold = 0.35f;

    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private WeaponPresentationController firstPersonWeapon;
    [SerializeField] private WorldWeaponView worldWeapon;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerHealth playerHealth;

    private readonly NetworkVariable<float> aimPitch = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public event Action Fired;
    public event Action Reloaded;
    public event Action<bool> AimChanged;
    public event Action<string> WeaponChanged;

    public WeaponDefinition Definition => definition;
    public float AimPitch => aimPitch.Value;
    public string CurrentWeaponId => definition != null ? definition.WeaponId : string.Empty;

    private void Awake()
    {
        if (firstPersonWeapon == null)
            firstPersonWeapon = GetComponent<WeaponPresentationController>();
        if (worldWeapon == null)
            worldWeapon = GetComponent<WorldWeaponView>();
        if (playerLook == null)
            playerLook = GetComponent<PlayerLook>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            WriteAimPitch(true);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        WriteAimPitch(false);
    }

    public void NotifyFire()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (firstPersonWeapon != null)
            firstPersonWeapon.PlayFirePresentation();

        Fired?.Invoke();
        FirePresentationRpc();
    }

    public void NotifyReload()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (firstPersonWeapon != null)
            firstPersonWeapon.PlayReloadPresentation();

        Reloaded?.Invoke();
        ReloadPresentationRpc();
    }

    public void NotifyAimChanged(bool isAiming)
    {
        AimChanged?.Invoke(isAiming);
    }

    public void NotifyWeaponChanged(string weaponId)
    {
        WeaponChanged?.Invoke(weaponId);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Owner)]
    private void FirePresentationRpc()
    {
        if (IsOwner)
            return;

        if (worldWeapon != null)
            worldWeapon.PlayFirePresentation();

        Fired?.Invoke();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Owner)]
    private void ReloadPresentationRpc()
    {
        if (IsOwner)
            return;

        if (worldWeapon != null)
            worldWeapon.PlayReloadPresentation();

        Reloaded?.Invoke();
    }

    private void WriteAimPitch(bool force)
    {
        if (playerLook == null)
            return;

        float quantized = QuantizePitch(playerLook.Pitch);
        if (!force && Mathf.Abs(quantized - aimPitch.Value) < AimPitchSendThreshold)
            return;

        aimPitch.Value = quantized;
    }

    private static float QuantizePitch(float pitch)
    {
        return Mathf.Round(pitch / AimPitchQuantize) * AimPitchQuantize;
    }
}
