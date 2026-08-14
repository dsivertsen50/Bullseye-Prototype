using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 2f, 0f);

    public void Kill()
    {
        if (!IsSpawned)
            return;

        KillServerRpc();
    }

    [Rpc(SendTo.Server)]
    private void KillServerRpc()
    {
        RespawnOwnerRpc();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void RespawnOwnerRpc()
    {
        PerformRespawn();
    }

    private void PerformRespawn()
    {
        CharacterController controller = GetComponent<CharacterController>();
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();

        controller.enabled = false;

        networkTransform.Teleport(
            respawnPosition,
            Quaternion.identity,
            transform.localScale
        );

        controller.enabled = true;

        Debug.Log("You were hit! Respawning.");
    }
}
