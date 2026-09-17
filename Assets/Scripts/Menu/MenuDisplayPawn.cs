using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Marks an in-scene Player as a menu mannequin. Create Lobby starts NGO on
/// MainMenu, which would otherwise scene-spawn this pawn and enable FPS play
/// underneath the lobby UI.
/// </summary>
[DefaultExecutionOrder(-10000)]
public class MenuDisplayPawn : MonoBehaviour
{
    private void Awake()
    {
        Neutralize(gameObject);
    }

    public static void NeutralizeScenePawns()
    {
        MenuDisplayPawn[] marked = FindObjectsByType<MenuDisplayPawn>(FindObjectsInactive.Include);
        for (int i = 0; i < marked.Length; i++)
        {
            if (marked[i] != null)
                Neutralize(marked[i].gameObject);
        }

        PlayerNetworkSetup[] pawns = FindObjectsByType<PlayerNetworkSetup>(FindObjectsInactive.Include);
        for (int i = 0; i < pawns.Length; i++)
        {
            if (pawns[i] == null)
                continue;
            if (!pawns[i].gameObject.scene.IsValid())
                continue;
            if (pawns[i].gameObject.scene.name != GameSessionCoordinator.MainMenuSceneName)
                continue;
            Neutralize(pawns[i].gameObject);
        }
    }

    public static void Neutralize(GameObject pawn)
    {
        if (pawn == null)
            return;

        Camera[] cameras = pawn.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
                cameras[i].enabled = false;
        }

        AudioListener[] listeners = pawn.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null)
                listeners[i].enabled = false;
        }

        Behaviour[] behaviours = pawn.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour is MenuDisplayPawn || behaviour is Animator)
                continue;
            if (ShouldDisable(behaviour))
                behaviour.enabled = false;
        }

        Rigidbody body = pawn.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        NetworkObject networkObject = pawn.GetComponent<NetworkObject>();
        if (networkObject == null)
            return;

        if (networkObject.IsSpawned)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                networkObject.Despawn(true);
            return;
        }

        networkObject.enabled = false;
        DestroyImmediate(networkObject);
    }

    private static bool ShouldDisable(Behaviour behaviour)
    {
        return behaviour is Camera
            || behaviour is AudioListener
            || behaviour is PlayerLook
            || behaviour is PlayerMovement
            || behaviour is PlayerShoot
            || behaviour is PlayerNetworkSetup
            || behaviour is LocalPauseMenu
            || behaviour is PlayerAimZoom
            || behaviour is PlayerHaptics
            || behaviour is NetworkBehaviour
            || behaviour is UnityEngine.InputSystem.PlayerInput;
    }
}
