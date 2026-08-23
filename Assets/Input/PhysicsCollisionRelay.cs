using System;
using UnityEngine;

/// <summary>
/// Forwards solid collisions to a listener that is not on the Rigidbody
/// GameObject, such as BullseyeDetachController on the player.
/// </summary>
public class PhysicsCollisionRelay : MonoBehaviour
{
    public event Action<Collision> CollisionEntered;

    private void OnCollisionEnter(Collision collision)
    {
        CollisionEntered?.Invoke(collision);
    }
}
