using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Starts host/client from a main-menu session request when the gameplay scene loads.
/// Direct scene play still falls back to the existing NetworkButtons.
/// </summary>
public class GameplaySessionBootstrap : MonoBehaviour
{
    private void Awake()
    {
        if (!GameSessionCoordinator.HasMenuDrivenSession)
            return;

        NetworkButtons buttons = GetComponent<NetworkButtons>();
        if (buttons != null)
            buttons.enabled = false;

        NetworkStart autoStart = GetComponent<NetworkStart>();
        if (autoStart != null)
            autoStart.enabled = false;

        DestroyStaleNetworkManager();
    }

    private void Start()
    {
        GameSessionCoordinator coordinator = GameSessionCoordinator.Instance;
        if (coordinator == null || coordinator.PendingRequest == null)
            return;

        DestroyStaleNetworkManager();

        NetworkManager networkManager = GetComponent<NetworkManager>();
        if (networkManager != null && NetworkManager.Singleton == null)
            networkManager.SetSingleton();
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        coordinator.ExecutePendingRequest(networkManager);
    }

    private void DestroyStaleNetworkManager()
    {
        NetworkManager sceneManager = GetComponent<NetworkManager>();
        NetworkManager singleton = NetworkManager.Singleton;
        if (singleton == null || singleton == sceneManager)
            return;

        if (singleton.IsListening || singleton.ShutdownInProgress)
            singleton.Shutdown();
        Destroy(singleton.gameObject);
    }
}
