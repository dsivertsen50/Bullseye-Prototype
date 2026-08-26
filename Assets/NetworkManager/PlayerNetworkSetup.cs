using UnityEngine;
using Unity.Netcode;

public class PlayerNetworkSetup : NetworkBehaviour
{
    private const string LocalPlayerBodyLayerName = "LocalPlayerBody";

    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerAimZoom playerAimZoom;
    [SerializeField] private Transform visualRoot;

    public static Camera LocalOwnedCamera { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalOwnedCamera = playerCamera;
            HideLocalBodyFromFirstPersonCamera();
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

    private void HideLocalBodyFromFirstPersonCamera()
    {
        Transform body = visualRoot;
        if (body == null)
            body = transform.Find("VisualRoot");
        if (body == null && playerMovement != null)
            body = playerMovement.BodyVisual;
        if (body == null)
            return;

        int layer = LayerMask.NameToLayer(LocalPlayerBodyLayerName);
        if (layer >= 0)
        {
            ApplyLayerRecursively(body.gameObject, layer);
            if (playerCamera != null)
                playerCamera.cullingMask &= ~(1 << layer);
            return;
        }

        Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }
    }

    private static void ApplyLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
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
