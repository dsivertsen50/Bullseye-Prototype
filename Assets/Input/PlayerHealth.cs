using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] private Vector3 respawnPosition = new Vector3(0f, 2f, 0f);

    public void Kill()
    {
        RespawnRpc();
    }

    [Rpc(SendTo.Owner)]
    private void RespawnRpc()
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