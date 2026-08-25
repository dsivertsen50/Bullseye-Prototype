using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Starts host/client from a main-menu session request when the gameplay scene loads.
/// Direct scene play still falls back to the existing NetworkButtons.
/// </summary>
public class GameplaySessionBootstrap : MonoBehaviour
{
    private void Start()
    {
        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        if (coordinator == null || coordinator.PendingRequest == null)
            return;

        NetworkButtons buttons = GetComponent<NetworkButtons>();
        if (buttons != null)
            buttons.enabled = false;

        NetworkStart autoStart = GetComponent<NetworkStart>();
        if (autoStart != null)
            autoStart.enabled = false;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
            networkManager = GetComponent<NetworkManager>();

        coordinator.ExecutePendingRequest(networkManager);
    }
}
