using UnityEngine;
using Unity.Netcode;

public class NetworkButtons : MonoBehaviour
{
    private void OnGUI()
    {
        if (NetworkManager.Singleton.IsClient ||
            NetworkManager.Singleton.IsServer)
            return;

        if (GUI.Button(new Rect(10, 10, 120, 40), "Start Host"))
        {
            NetworkManager.Singleton.StartHost();
        }

        if (GUI.Button(new Rect(10, 60, 120, 40), "Start Client"))
        {
            NetworkManager.Singleton.StartClient();
        }
    }
}