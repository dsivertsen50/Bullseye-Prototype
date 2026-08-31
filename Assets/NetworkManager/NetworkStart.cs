using UnityEngine;
using Unity.Netcode;

public class NetworkStart : MonoBehaviour
{
    private void Start()
    {
        if (GameSessionCoordinator.HasMenuDrivenSession)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.IsListening)
            return;

        networkManager.StartHost();
    }
}