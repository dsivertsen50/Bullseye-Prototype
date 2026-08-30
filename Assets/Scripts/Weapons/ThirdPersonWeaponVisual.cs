using UnityEngine;

/// <summary>
/// Marker on a third-person weapon prefab. Exposes the left-hand support
/// target and muzzle used by remote presentation.
/// </summary>
[DisallowMultipleComponent]
public class ThirdPersonWeaponVisual : MonoBehaviour
{
    [SerializeField] private Transform leftHandIkTarget;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Transform rightHandGrip;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private bool drawGizmos = true;

    public Transform LeftHandIkTarget => leftHandIkTarget;
    public Transform Muzzle => muzzle;
    public Transform RightHandGrip => rightHandGrip;
    public Transform AimTarget => aimTarget;

    public void ApplyLeftHandLocal(Vector3 localPosition, Vector3 localEuler)
    {
        if (leftHandIkTarget == null)
            return;

        leftHandIkTarget.localPosition = localPosition;
        leftHandIkTarget.localRotation = Quaternion.Euler(localEuler);
    }

    public void Assign(Transform leftHand, Transform muzzlePoint, Transform grip = null, Transform aim = null)
    {
        leftHandIkTarget = leftHand;
        muzzle = muzzlePoint;
        rightHandGrip = grip;
        aimTarget = aim;
    }

    public void ResolveFallbacks()
    {
        if (leftHandIkTarget == null)
            leftHandIkTarget = FindChild(transform, "LeftHandIKTarget");
        if (muzzle == null)
            muzzle = FindChild(transform, "Muzzle") ?? FindChild(transform, "MuzzlePoint");
        if (rightHandGrip == null)
            rightHandGrip = FindChild(transform, "RightHandGrip");
        if (aimTarget == null)
            aimTarget = FindChild(transform, "AimTarget") ?? FindChild(transform, "AimPoint");
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        DrawMarker(leftHandIkTarget, new Color(0.2f, 0.75f, 1f), 0.025f);
        DrawMarker(muzzle, new Color(1f, 0.45f, 0.15f), 0.018f);
        DrawMarker(rightHandGrip, new Color(0.3f, 1f, 0.35f), 0.02f);
        DrawMarker(aimTarget, new Color(1f, 0.9f, 0.2f), 0.016f);
    }

    private static void DrawMarker(Transform marker, Color color, float radius)
    {
        if (marker == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(marker.position, radius);
        Gizmos.DrawLine(marker.position, marker.position + marker.forward * radius * 3f);
    }
#endif
}
