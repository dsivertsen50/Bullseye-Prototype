using UnityEngine;
using Unity.Netcode;

public class PlayerNetworkSetup : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerAimZoom playerAimZoom;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            return;

        playerCamera.enabled = false;
        playerLook.enabled = false;

        if (playerAimZoom != null)
            playerAimZoom.enabled = false;
    }
}