using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owner-only local pause/menu flag. Does not pause the simulation or network.
/// </summary>
public class LocalPlayerMenuState : NetworkBehaviour
{
    public bool IsMenuOpen { get; private set; }

    public static bool IsOpen(Component host)
    {
        if (host == null)
            return false;

        LocalPlayerMenuState state = host.GetComponent<LocalPlayerMenuState>();
        return state != null && state.IsMenuOpen;
    }

    public void SetMenuOpen(bool open)
    {
        if (!IsOwner)
            return;

        IsMenuOpen = open;
    }

    public override void OnNetworkDespawn()
    {
        IsMenuOpen = false;
    }
}
