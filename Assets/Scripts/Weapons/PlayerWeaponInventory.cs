using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Server-authoritative inventory: one permanent default pistol plus at most
/// one optional temporary weapon. Presentation and firing read this state.
/// </summary>
[DefaultExecutionOrder(50)]
public class PlayerWeaponInventory : NetworkBehaviour
{
    public const int PermanentSlot = 0;
    public const int TemporarySlot = 1;

    [FormerlySerializedAs("startingSlot1")]
    [SerializeField] private WeaponDefinition defaultPistol;
    [SerializeField] private WeaponCatalog catalog;
    [SerializeField] private GroundWeaponPickup groundPickupPrefab;
    [SerializeField] private float dropForwardDistance = 1.15f;
    [SerializeField] private float dropHeight = 1.05f;
    [SerializeField] private float dropTossSpeed = 2.4f;
    [SerializeField] private float dropTossUpSpeed = 1.5f;
    [SerializeField] private float dropPickupIgnoreDuration = 0.8f;

    private readonly NetworkVariable<WeaponRuntimeState> permanentWeapon = new(
        WeaponRuntimeState.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<WeaponRuntimeState> temporaryWeapon = new(
        WeaponRuntimeState.Empty,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<byte> activeSlot = new(
        PermanentSlot,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> switching = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> reloading = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private WeaponPresentationCoordinator coordinator;
    private PlayerHealth playerHealth;
    private Coroutine reloadRoutine;
    private Coroutine switchRoutine;
    private float localBusyUntil;

    public WeaponCatalog Catalog => catalog;
    public int ActiveSlotIndex => activeSlot.Value;
    public bool IsSwitching => switching.Value;
    public bool IsReloading => reloading.Value;
    public bool IsBusy => switching.Value || reloading.Value;
    public bool IsLocallyBusy => IsBusy || (IsOwner && Time.time < localBusyUntil);
    public GroundWeaponPickup GroundPickupPrefab => groundPickupPrefab;
    public bool HasTemporaryWeapon => !GetSlot(TemporarySlot).IsEmpty;
    public bool IsTemporaryActive => ActiveSlotIndex == TemporarySlot && HasTemporaryWeapon;
    public WeaponRuntimeState PermanentState => GetSlot(PermanentSlot);
    public WeaponRuntimeState TemporaryState => GetSlot(TemporarySlot);

    public WeaponDefinition ActiveDefinition
    {
        get
        {
            WeaponDefinition active = GetDefinition(ActiveState);
            return active != null ? active : GetDefinition(GetSlot(PermanentSlot));
        }
    }

    public WeaponRuntimeState ActiveState
    {
        get
        {
            WeaponRuntimeState state = GetSlot(ActiveSlotIndex);
            return state.IsEmpty ? GetSlot(PermanentSlot) : state;
        }
    }

    public event System.Action InventoryChanged;

    private void Awake()
    {
        coordinator = GetComponent<WeaponPresentationCoordinator>();
        playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        permanentWeapon.OnValueChanged += OnSlotChanged;
        temporaryWeapon.OnValueChanged += OnSlotChanged;
        activeSlot.OnValueChanged += OnActiveSlotChanged;

        if (IsServer)
            RestoreDefaultLoadout();
        else
            ApplyActivePresentation();
    }

    public override void OnNetworkDespawn()
    {
        permanentWeapon.OnValueChanged -= OnSlotChanged;
        temporaryWeapon.OnValueChanged -= OnSlotChanged;
        activeSlot.OnValueChanged -= OnActiveSlotChanged;
        StopInventoryRoutines();
    }

    public WeaponRuntimeState GetSlot(int index)
    {
        return index == TemporarySlot ? temporaryWeapon.Value : permanentWeapon.Value;
    }

    public WeaponDefinition GetDefinition(WeaponRuntimeState state)
    {
        return catalog != null ? catalog.Get(state.CatalogIndex) : null;
    }

    public bool CanRequestSwitch()
    {
        if (!IsSpawned || (playerHealth != null && playerHealth.IsDead))
            return false;

        if (LocalPlayerMenuState.IsOpen(this))
            return false;

        return HasTemporaryWeapon && !IsLocallyBusy;
    }

    public bool CanFireActive()
    {
        if (IsLocallyBusy || (playerHealth != null && playerHealth.IsDead))
            return false;

        WeaponRuntimeState state = ActiveState;
        return !state.IsEmpty && state.Magazine > 0;
    }

    public bool CanReloadActive()
    {
        if (IsBusy || (playerHealth != null && playerHealth.IsDead))
            return false;

        WeaponRuntimeState state = ActiveState;
        WeaponDefinition definition = GetDefinition(state);
        if (definition == null)
            return false;

        if (state.Magazine >= definition.MagazineSize)
            return false;

        return definition.HasUnlimitedReserve || state.Reserve > 0;
    }

    public void RequestSwitch()
    {
        if (!IsOwner || !CanRequestSwitch())
            return;

        MarkLocalBusy(GetHolsterDuration() + GetDrawDuration());
        SwitchServerRpc();
    }

    public void RequestReload()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (IsLocallyBusy)
            return;

        if (TryGetComponent(out PlayerMovement movement) && movement.BlocksCombat)
            return;

        if (!CanReloadActive())
            return;

        WeaponDefinition definition = ActiveDefinition;
        MarkLocalBusy(definition != null ? definition.ReloadTime : 1f);
        coordinator?.NotifyReload();
        ReloadServerRpc();
    }

    public void InterruptReloadForDive()
    {
        if (!IsSpawned || !IsOwner)
            return;

        localBusyUntil = 0f;
        InterruptReloadServerRpc();
    }

    public void NotifyShotFired()
    {
        if (!IsSpawned || !IsOwner)
            return;

        ConsumeShotServerRpc();
    }

    public void RequestSwapPickup(NetworkObject pickupObject)
    {
        if (!IsSpawned || !IsOwner || pickupObject == null)
            return;

        SwapPickupServerRpc(pickupObject);
    }

    public void RestoreDefaultLoadout()
    {
        if (!IsServer)
            return;

        StopInventoryRoutines();
        switching.Value = false;
        reloading.Value = false;
        SetSlotState(PermanentSlot, CreateStartingState(ResolveDefaultPistol()));
        SetSlotState(TemporarySlot, WeaponRuntimeState.Empty);
        activeSlot.Value = PermanentSlot;
        ApplyActivePresentation();
        InventoryChanged?.Invoke();
    }

    public void DropTemporaryWeaponOnDeath()
    {
        if (!IsServer || !IsSpawned)
            return;

        StopInventoryRoutines();
        switching.Value = false;
        reloading.Value = false;

        WeaponRuntimeState dropped = GetSlot(TemporarySlot);
        SetSlotState(TemporarySlot, WeaponRuntimeState.Empty);
        activeSlot.Value = PermanentSlot;
        ApplyActivePresentation();
        InventoryChanged?.Invoke();

        if (!dropped.IsEmpty && dropped.TotalAmmo > 0)
            SpawnDroppedWeapon(dropped);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SwitchServerRpc()
    {
        if (IsBusy || !HasTemporaryWeapon)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        StopInventoryRoutines();
        switchRoutine = StartCoroutine(SwitchRoutine());
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReloadServerRpc()
    {
        StartReload();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void InterruptReloadServerRpc()
    {
        if (reloadRoutine == null && !reloading.Value)
            return;

        StopInventoryRoutines();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ConsumeShotServerRpc()
    {
        if (IsBusy)
            return;

        WeaponRuntimeState state = ActiveState;
        if (state.IsEmpty || state.Magazine <= 0)
            return;

        state.Magazine--;
        SetSlotState(ActiveSlotIndex == TemporarySlot ? TemporarySlot : PermanentSlot, state);
        InventoryChanged?.Invoke();
        CheckTemporaryExhaustion();
        TryAutoReloadEmptyMagazine();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void AutoReloadPresentationOwnerRpc()
    {
        PlayLocalReloadPresentation();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SwapPickupServerRpc(NetworkObjectReference pickupReference)
    {
        if (!pickupReference.TryGet(out NetworkObject pickupObject))
            return;

        if (!TryGetPickup(pickupObject, out GroundWeaponPickup pickup))
            return;

        if (!IsWithinPickupRange(pickup.transform.position))
            return;

        if (IsBusy || (playerHealth != null && playerHealth.IsDead))
            return;

        WeaponDefinition incoming = pickup.Definition;
        if (incoming == null || incoming.IsPermanentDefault)
            return;

        if (pickup.TotalAmmo <= 0)
            return;

        if (!pickup.TryClaim(out WeaponRuntimeState incomingState))
            return;

        WeaponRuntimeState dropped = GetSlot(TemporarySlot);
        SetSlotState(TemporarySlot, incomingState);
        activeSlot.Value = TemporarySlot;
        ApplyActivePresentation();
        InventoryChanged?.Invoke();

        if (!dropped.IsEmpty && dropped.TotalAmmo > 0)
            SpawnDroppedWeapon(dropped);
    }

    private IEnumerator SwitchRoutine()
    {
        if (!HasTemporaryWeapon)
            yield break;

        switching.Value = true;
        float holster = GetHolsterDuration();
        if (holster > 0f)
            yield return new WaitForSeconds(holster);

        if (!HasTemporaryWeapon)
        {
            activeSlot.Value = PermanentSlot;
            ApplyActivePresentation();
            switching.Value = false;
            switchRoutine = null;
            yield break;
        }

        activeSlot.Value = (byte)(ActiveSlotIndex == TemporarySlot ? PermanentSlot : TemporarySlot);
        ApplyActivePresentation();
        InventoryChanged?.Invoke();

        float draw = GetDrawDuration();
        if (draw > 0f)
            yield return new WaitForSeconds(draw);

        switching.Value = false;
        switchRoutine = null;
    }

    private IEnumerator ReloadRoutine()
    {
        WeaponDefinition definition = ActiveDefinition;
        if (definition == null)
            yield break;

        reloading.Value = true;

        yield return new WaitForSeconds(definition.ReloadTime);

        int slotIndex = ActiveSlotIndex == TemporarySlot ? TemporarySlot : PermanentSlot;
        WeaponRuntimeState state = GetSlot(slotIndex);
        WeaponDefinition current = GetDefinition(state);
        if (current != null && state.Magazine < current.MagazineSize)
        {
            if (current.HasUnlimitedReserve)
            {
                state.Magazine = current.MagazineSize;
                SetSlotState(slotIndex, state);
                InventoryChanged?.Invoke();
            }
            else if (state.Reserve > 0)
            {
                int needed = current.MagazineSize - state.Magazine;
                int transferred = Mathf.Min(needed, state.Reserve);
                state.Magazine += transferred;
                state.Reserve -= transferred;
                SetSlotState(slotIndex, state);
                InventoryChanged?.Invoke();
            }
        }

        reloading.Value = false;
        reloadRoutine = null;
    }

    private IEnumerator ExhaustTemporaryRoutine()
    {
        switching.Value = true;
        float holster = GetHolsterDuration();
        if (holster > 0f)
            yield return new WaitForSeconds(holster);

        SetSlotState(TemporarySlot, WeaponRuntimeState.Empty);
        activeSlot.Value = PermanentSlot;
        ApplyActivePresentation();
        InventoryChanged?.Invoke();

        float draw = GetDrawDuration();
        if (draw > 0f)
            yield return new WaitForSeconds(draw);

        switching.Value = false;
        switchRoutine = null;
    }

    private void TryAutoReloadEmptyMagazine()
    {
        if (ActiveState.Magazine > 0)
            return;

        if (!StartReload())
            return;

        if (IsOwner)
            PlayLocalReloadPresentation();
        else
            AutoReloadPresentationOwnerRpc();
    }

    private bool StartReload()
    {
        if (!CanReloadActive())
            return false;

        StopInventoryRoutines();
        reloadRoutine = StartCoroutine(ReloadRoutine());
        return true;
    }

    private void PlayLocalReloadPresentation()
    {
        WeaponDefinition definition = ActiveDefinition;
        MarkLocalBusy(definition != null ? definition.ReloadTime : 1f);
        coordinator?.NotifyReload();
    }

    private void CheckTemporaryExhaustion()
    {
        WeaponRuntimeState temp = GetSlot(TemporarySlot);
        if (temp.IsEmpty || temp.TotalAmmo > 0)
            return;

        if (ActiveSlotIndex == TemporarySlot)
        {
            StopInventoryRoutines();
            MarkLocalBusy(GetHolsterDuration() + GetDrawDuration());
            switchRoutine = StartCoroutine(ExhaustTemporaryRoutine());
            return;
        }

        SetSlotState(TemporarySlot, WeaponRuntimeState.Empty);
        InventoryChanged?.Invoke();
    }

    private void SpawnDroppedWeapon(WeaponRuntimeState state)
    {
        if (groundPickupPrefab == null || state.IsEmpty || state.TotalAmmo <= 0)
            return;

        WeaponDefinition definition = GetDefinition(state);
        if (definition == null || definition.IsPermanentDefault)
            return;

        Vector3 origin = transform.position + Vector3.up * dropHeight;
        Vector3 desired = origin + transform.forward * dropForwardDistance;
        int pickupLayer = LayerMask.NameToLayer("WeaponPickup");
        int mask = pickupLayer >= 0 ? ~(1 << pickupLayer) : ~0;
        if (Physics.SphereCast(
                origin,
                0.18f,
                transform.forward,
                out RaycastHit hit,
                dropForwardDistance,
                mask,
                QueryTriggerInteraction.Ignore))
        {
            desired = hit.point - transform.forward * 0.25f;
            desired.y = origin.y;
        }

        Vector3 toss = transform.forward * dropTossSpeed + Vector3.up * dropTossUpSpeed;
        GroundWeaponPickup.SpawnDropped(
            groundPickupPrefab,
            definition,
            state,
            desired,
            Quaternion.Euler(
                Random.Range(-8f, 8f),
                transform.eulerAngles.y + Random.Range(15f, 40f),
                Random.Range(-12f, 12f)),
            OwnerClientId,
            dropPickupIgnoreDuration,
            toss);
    }

    private WeaponRuntimeState CreateStartingState(WeaponDefinition definition)
    {
        int index = catalog != null ? catalog.IndexOf(definition) : -1;
        if (index < 0 || definition == null)
            return WeaponRuntimeState.Empty;

        return new WeaponRuntimeState
        {
            CatalogIndex = index,
            Magazine = definition.StartingMagazineAmmo,
            Reserve = definition.HasUnlimitedReserve ? 0 : definition.StartingReserveAmmo
        };
    }

    private WeaponDefinition ResolveDefaultPistol()
    {
        if (defaultPistol != null)
            return defaultPistol;

        return catalog != null ? catalog.GetPermanentDefault() : null;
    }

    private void SetSlotState(int index, WeaponRuntimeState state)
    {
        if (index == TemporarySlot)
            temporaryWeapon.Value = state;
        else
            permanentWeapon.Value = state;
    }

    private void ApplyActivePresentation()
    {
        coordinator?.ApplyDefinition(ActiveDefinition);
    }

    private void MarkLocalBusy(float duration)
    {
        localBusyUntil = Time.time + Mathf.Max(0.05f, duration);
    }

    private void OnSlotChanged(WeaponRuntimeState previous, WeaponRuntimeState next)
    {
        ApplyActivePresentation();
        InventoryChanged?.Invoke();
    }

    private void OnActiveSlotChanged(byte previous, byte next)
    {
        ApplyActivePresentation();
        InventoryChanged?.Invoke();
    }

    private void StopInventoryRoutines()
    {
        if (switchRoutine != null)
        {
            StopCoroutine(switchRoutine);
            switchRoutine = null;
        }

        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        if (IsServer && IsSpawned)
        {
            switching.Value = false;
            reloading.Value = false;
        }
    }

    private bool TryGetPickup(NetworkObject pickupObject, out GroundWeaponPickup pickup)
    {
        pickup = null;
        if (pickupObject == null || !pickupObject.IsSpawned)
            return false;

        return pickupObject.TryGetComponent(out pickup);
    }

    private bool IsWithinPickupRange(Vector3 worldPosition, float range = 3.25f)
    {
        return Vector3.Distance(transform.position, worldPosition) <= range;
    }

    private float GetHolsterDuration()
    {
        PlayerWeaponController controller = GetComponent<PlayerWeaponController>();
        return controller != null ? controller.HolsterDuration : 0.16f;
    }

    private float GetDrawDuration()
    {
        PlayerWeaponController controller = GetComponent<PlayerWeaponController>();
        return controller != null ? controller.DrawDuration : 0.2f;
    }
}
