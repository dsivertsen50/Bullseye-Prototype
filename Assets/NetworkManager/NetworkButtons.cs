using UnityEngine;
using Unity.Netcode;

public class NetworkButtons : MonoBehaviour
{
    private void OnGUI()
    {
        NetworkManager networkManager = NetworkManager.Singleton;

        if (GameSessionCoordinator.HasMenuDrivenSession)
            return;

        if (networkManager == null ||
            networkManager.IsClient ||
            networkManager.IsServer)
            return;

        if (GUI.Button(new Rect(10, 10, 120, 40), "Start Host"))
        {
            networkManager.StartHost();
        }

        if (GUI.Button(new Rect(10, 60, 120, 40), "Start Client"))
        {
            networkManager.StartClient();
        }
    }
}