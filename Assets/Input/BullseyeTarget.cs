using UnityEngine;

public class BullseyeTarget : MonoBehaviour
{
    private PlayerHealth playerHealth;

    private void Awake()
    {
        playerHealth = GetComponentInParent<PlayerHealth>();
    }

    public void Hit()
    {
        playerHealth.Kill();
    }
}