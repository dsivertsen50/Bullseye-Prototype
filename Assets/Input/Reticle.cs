using UnityEngine;
using Unity.Netcode;

public class Reticle : NetworkBehaviour
{
    [SerializeField] private float size = 4f;
    [SerializeField] private float length = 12f;

    private void OnGUI()
    {
        if (!IsOwner)
            return;

        float centerX = Screen.width / 2f;
        float centerY = Screen.height / 2f;

        GUI.DrawTexture(
            new Rect(centerX - size / 2f, centerY - length / 2f, size, length),
            Texture2D.whiteTexture);

        GUI.DrawTexture(
            new Rect(centerX - length / 2f, centerY - size / 2f, length, size),
            Texture2D.whiteTexture);
    }
}