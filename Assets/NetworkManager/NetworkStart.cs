using UnityEngine;
using Unity.Netcode;

public class NetworkStart : MonoBehaviour
{
    private void Start()
    {
        NetworkManager.Singleton.StartHost();
    }
}