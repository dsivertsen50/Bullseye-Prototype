using UnityEngine;

/// <summary>
/// Bone-following combat volume. Stops shots and identifies the player body
/// without dealing damage. Bullseye hits remain the only firearm damage source.
/// </summary>
public class PlayerCombatHitbox : MonoBehaviour
{
    public BullseyeBodyZone Zone = BullseyeBodyZone.Torso;
}
