using UnityEngine;
using Unity.Netcode;

public class PlayerNetworkSetup : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            return;

        playerCamera.enabled = false;
        playerMovement.enabled = false;
        playerLook.enabled = false;
    }
}