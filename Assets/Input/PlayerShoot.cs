using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerShoot : NetworkBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private InputActionReference fireAction;
    [SerializeField] private float range = 100f;

    private void OnEnable()
    {
        // Enable only. Player instances share this action in-process.
        fireAction.action.Enable();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (fireAction.action.WasPressedThisFrame())
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (Physics.Raycast(
            playerCamera.transform.position,
            playerCamera.transform.forward,
            out RaycastHit hit,
            range))
        {
            if (hit.collider.TryGetComponent<BullseyeTarget>(out BullseyeTarget target))
            {
                target.Hit();
                Debug.Log("BULLSEYE HIT!");
            }
        }
    }
}