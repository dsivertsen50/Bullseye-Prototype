using UnityEngine;
using Unity.Netcode;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Local-only first-person weapon presentation. Visual only; hitscan aim stays
/// on the camera/reticle. Hidden for remote players and during death.
/// Renders on a dedicated overlay camera so the FP gun is not depth-tested
/// against world geometry (walls). World weapons keep using the main camera.
/// </summary>
public class FirstPersonWeaponView : NetworkBehaviour
{
    private const string FirstPersonWeaponLayerName = "FirstPersonWeapon";
    private const string OverlayCameraName = "FirstPersonWeaponCamera";

    [SerializeField] private GameObject weaponView;
    [SerializeField] private Transform weaponMount;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float overlayNearClip = 0.01f;

    private Camera weaponOverlayCamera;
    private bool ownerPresentationEnabled;
    private int firstPersonWeaponLayer = -1;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (weaponView == null)
        {
            Transform cameraTransform = transform.Find("Camera");
            if (cameraTransform == null)
                cameraTransform = transform.Find("CameraRoot/CameraEffectsRoot/Camera");
            if (cameraTransform != null)
                weaponView = cameraTransform.Find("WeaponView")?.gameObject;
        }

        if (playerCamera == null && weaponView != null)
            playerCamera = weaponView.GetComponentInParent<Camera>();
        if (playerCamera == null)
        {
            Transform cameraTransform = transform.Find("CameraRoot/CameraEffectsRoot/Camera");
            if (cameraTransform != null)
                playerCamera = cameraTransform.GetComponent<Camera>();
        }

        if (weaponMount == null && weaponView != null)
        {
            weaponMount = weaponView.transform.Find("WeaponMount");
            if (weaponMount == null)
            {
                Transform effectsRoot = weaponView.transform.Find("WeaponEffectsRoot");
                if (effectsRoot != null)
                    weaponMount = effectsRoot.Find("WeaponMount");
            }
        }

        firstPersonWeaponLayer = LayerMask.NameToLayer(FirstPersonWeaponLayerName);
        PreparePresentationObject();
        SetWeaponViewActive(false);
    }

    public override void OnNetworkSpawn()
    {
        ownerPresentationEnabled = IsOwner;
        if (!ownerPresentationEnabled)
        {
            SetWeaponViewActive(false);
            SetOverlayEnabled(false);
            enabled = false;
            return;
        }

        EnsureOverlayCamera();
        ExcludeFirstPersonWeaponFromMainCamera();
        RefreshOwnerVisibility();
        SyncOverlayCamera();
    }

    public override void OnNetworkDespawn()
    {
        ownerPresentationEnabled = false;
        SetWeaponViewActive(false);
        SetOverlayEnabled(false);
    }

    private void LateUpdate()
    {
        if (!ownerPresentationEnabled)
            return;

        RefreshOwnerVisibility();
        SyncOverlayCamera();
    }

    private void RefreshOwnerVisibility()
    {
        bool dead = playerHealth != null && playerHealth.IsDead;
        SetWeaponViewActive(!dead);
    }

    private void SetWeaponViewActive(bool active)
    {
        if (weaponView != null && weaponView.activeSelf != active)
            weaponView.SetActive(active);

        SetOverlayEnabled(ownerPresentationEnabled && active);
    }

    private void EnsureOverlayCamera()
    {
        if (playerCamera == null || firstPersonWeaponLayer < 0)
            return;

        Transform existing = playerCamera.transform.Find(OverlayCameraName);
        if (existing != null)
            weaponOverlayCamera = existing.GetComponent<Camera>();

        if (weaponOverlayCamera == null)
        {
            GameObject overlayObject = new GameObject(OverlayCameraName);
            overlayObject.transform.SetParent(playerCamera.transform, false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;
            weaponOverlayCamera = overlayObject.AddComponent<Camera>();
            overlayObject.AddComponent<HDAdditionalCameraData>();
        }

        ConfigureOverlayCamera();
    }

    private void ConfigureOverlayCamera()
    {
        if (weaponOverlayCamera == null || playerCamera == null)
            return;

        weaponOverlayCamera.CopyFrom(playerCamera);
        weaponOverlayCamera.cullingMask = 1 << firstPersonWeaponLayer;
        weaponOverlayCamera.clearFlags = CameraClearFlags.Depth;
        weaponOverlayCamera.depth = playerCamera.depth + 1;
        weaponOverlayCamera.nearClipPlane = Mathf.Max(0.001f, overlayNearClip);
        weaponOverlayCamera.farClipPlane = playerCamera.farClipPlane;
        weaponOverlayCamera.enabled = false;
        weaponOverlayCamera.stereoTargetEye = playerCamera.stereoTargetEye;

        HDAdditionalCameraData overlayHd = weaponOverlayCamera.GetComponent<HDAdditionalCameraData>();
        HDAdditionalCameraData mainHd = playerCamera.GetComponent<HDAdditionalCameraData>();
        if (overlayHd == null)
            return;

        overlayHd.clearColorMode = HDAdditionalCameraData.ClearColorMode.None;
        overlayHd.clearDepth = true;
        overlayHd.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
        overlayHd.dithering = false;
        overlayHd.allowDynamicResolution = false;

        if (mainHd != null)
        {
            overlayHd.volumeLayerMask = mainHd.volumeLayerMask;
            overlayHd.volumeAnchorOverride = mainHd.volumeAnchorOverride;
            overlayHd.probeLayerMask = mainHd.probeLayerMask;
        }
    }

    private void ExcludeFirstPersonWeaponFromMainCamera()
    {
        if (playerCamera == null || firstPersonWeaponLayer < 0)
            return;

        playerCamera.cullingMask &= ~(1 << firstPersonWeaponLayer);
    }

    private void SyncOverlayCamera()
    {
        if (weaponOverlayCamera == null || playerCamera == null)
            return;

        weaponOverlayCamera.fieldOfView = playerCamera.fieldOfView;
        weaponOverlayCamera.aspect = playerCamera.aspect;
        weaponOverlayCamera.rect = playerCamera.rect;
        weaponOverlayCamera.farClipPlane = playerCamera.farClipPlane;
        weaponOverlayCamera.nearClipPlane = Mathf.Max(0.001f, overlayNearClip);
        weaponOverlayCamera.targetDisplay = playerCamera.targetDisplay;
    }

    private void SetOverlayEnabled(bool overlayEnabled)
    {
        if (weaponOverlayCamera != null && weaponOverlayCamera.enabled != overlayEnabled)
            weaponOverlayCamera.enabled = overlayEnabled;
    }

    private void PreparePresentationObject()
    {
        if (weaponView == null)
            return;

        ApplyLayerRecursively(weaponView, FirstPersonWeaponLayerName);
        DisableGameplayCollision(weaponView);
    }

    private static void ApplyLayerRecursively(GameObject root, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
            transforms[i].gameObject.layer = layer;
    }

    private static void DisableGameplayCollision(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            bodies[i].isKinematic = true;
            bodies[i].detectCollisions = false;
        }
    }
}
