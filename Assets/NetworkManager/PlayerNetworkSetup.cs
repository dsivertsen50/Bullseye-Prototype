using UnityEngine;
using Unity.Netcode;

public class PlayerNetworkSetup : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerAimZoom playerAimZoom;

    public static Camera LocalOwnedCamera { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalOwnedCamera = playerCamera;
            return;
        }

        DisableLocalCameras();
        if (playerLook != null)
            playerLook.enabled = false;

        if (playerAimZoom != null)
            playerAimZoom.enabled = false;

        if (TryGetComponent(out PlayerHaptics playerHaptics))
            playerHaptics.enabled = false;

        if (TryGetComponent(out PlayerCameraEffects cameraEffects))
            cameraEffects.enabled = false;

        if (TryGetComponent(out LocalPauseMenu pauseMenu))
            pauseMenu.enabled = false;

        if (TryGetComponent(out BullseyeSprintSpeedEffects sprintEffects))
            sprintEffects.enabled = false;

        if (TryGetComponent(out PlayerAimSightBlur aimSightBlur))
            aimSightBlur.enabled = false;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && LocalOwnedCamera == playerCamera)
            LocalOwnedCamera = null;
    }

    private void DisableLocalCameras()
    {
        if (playerCamera == null)
            return;

        Camera[] cameras = playerCamera.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
                cameras[i].enabled = false;
        }
    }
}