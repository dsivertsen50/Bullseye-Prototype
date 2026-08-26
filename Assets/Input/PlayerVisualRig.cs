using UnityEngine;

/// <summary>
/// Socket and bullseye-anchor lookup for the third-person character rig.
/// First-person weapons do not use these transforms.
/// </summary>
public class PlayerVisualRig : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private Transform rightHandWeaponSocket;
    [SerializeField] private Transform leftHandIkTarget;
    [SerializeField] private Transform weaponHolsterSocket;
    [SerializeField] private Transform backWeaponSocket;

    [Header("Bullseye Anchors")]
    [SerializeField] private Transform bullseyeHeadAnchor;
    [SerializeField] private Transform bullseyeUpperTorsoAnchor;
    [SerializeField] private Transform bullseyeLowerTorsoAnchor;
    [SerializeField] private Transform bullseyeLeftArmAnchor;
    [SerializeField] private Transform bullseyeRightArmAnchor;
    [SerializeField] private Transform bullseyeLeftLegAnchor;
    [SerializeField] private Transform bullseyeRightLegAnchor;

    public Transform RightHandWeaponSocket => rightHandWeaponSocket;
    public Transform LeftHandIkTarget => leftHandIkTarget;
    public Transform WeaponHolsterSocket => weaponHolsterSocket;
    public Transform BackWeaponSocket => backWeaponSocket;
    public Transform BullseyeHeadAnchor => bullseyeHeadAnchor;
    public Transform BullseyeUpperTorsoAnchor => bullseyeUpperTorsoAnchor;
    public Transform BullseyeLowerTorsoAnchor => bullseyeLowerTorsoAnchor;
    public Transform BullseyeLeftArmAnchor => bullseyeLeftArmAnchor;
    public Transform BullseyeRightArmAnchor => bullseyeRightArmAnchor;
    public Transform BullseyeLeftLegAnchor => bullseyeLeftLegAnchor;
    public Transform BullseyeRightLegAnchor => bullseyeRightLegAnchor;

    public void Assign(
        Transform rightHand,
        Transform leftHandIk,
        Transform holster,
        Transform back,
        Transform head,
        Transform upperTorso,
        Transform lowerTorso,
        Transform leftArm,
        Transform rightArm,
        Transform leftLeg,
        Transform rightLeg)
    {
        rightHandWeaponSocket = rightHand;
        leftHandIkTarget = leftHandIk;
        weaponHolsterSocket = holster;
        backWeaponSocket = back;
        bullseyeHeadAnchor = head;
        bullseyeUpperTorsoAnchor = upperTorso;
        bullseyeLowerTorsoAnchor = lowerTorso;
        bullseyeLeftArmAnchor = leftArm;
        bullseyeRightArmAnchor = rightArm;
        bullseyeLeftLegAnchor = leftLeg;
        bullseyeRightLegAnchor = rightLeg;
    }
}
