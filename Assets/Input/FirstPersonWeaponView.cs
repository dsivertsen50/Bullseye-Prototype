using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Local-only first-person weapon presentation. Visual only; hitscan aim stays
/// on the camera/reticle. Hidden for remote players and during death.
/// </summary>
public class FirstPersonWeaponView : NetworkBehaviour
{
    private const string FirstPersonWeaponLayerName = "FirstPersonWeapon";

    [SerializeField] private GameObject weaponView;
    [SerializeField] private Transform weaponMount;
    [SerializeField] private PlayerHealth playerHealth;

    private bool ownerPresentationEnabled;

    private void Awake()
    {
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (weaponView == null)
        {
            Transform cameraTransform = transform.Find("Camera");
            if (cameraTransform != null)
                weaponView = cameraTransform.Find("WeaponView")?.gameObject;
        }

        if (weaponMount == null && weaponView != null)
            weaponMount = weaponView.transform.Find("WeaponMount");

        PreparePresentationObject();
        SetWeaponViewActive(false);
    }

    public override void OnNetworkSpawn()
    {
        ownerPresentationEnabled = IsOwner;
        if (!ownerPresentationEnabled)
        {
            SetWeaponViewActive(false);
            enabled = false;
            return;
        }

        RefreshOwnerVisibility();
    }

    public override void OnNetworkDespawn()
    {
        ownerPresentationEnabled = false;
        SetWeaponViewActive(false);
    }

    private void Update()
    {
        if (!ownerPresentationEnabled)
            return;

        RefreshOwnerVisibility();
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
