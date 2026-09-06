using UnityEngine;

/// <summary>
/// Shared geometric checks for a dolphin-dive body slam.
/// Used by BodySlamDetector and editor/debug tests so tuning stays in one place.
/// </summary>
public static class BodySlamValidation
{
    public const string Ok = "ok";

    public static bool TryValidateLanding(
        Vector3 attackerCenter,
        Vector3 victimCenter,
        float downwardSpeed,
        float minimumDownwardVelocity,
        float horizontalTolerance,
        float maximumVerticalSeparation,
        out string rejectReason)
    {
        if (downwardSpeed < minimumDownwardVelocity)
        {
            rejectReason = "weak_impact";
            return false;
        }

        float vertical = attackerCenter.y - victimCenter.y;
        if (vertical <= 0.05f)
        {
            rejectReason = "not_above";
            return false;
        }

        if (vertical > maximumVerticalSeparation)
        {
            rejectReason = "too_high";
            return false;
        }

        Vector3 attackerFlat = Flatten(attackerCenter);
        Vector3 victimFlat = Flatten(victimCenter);
        float horizontal = Vector3.Distance(attackerFlat, victimFlat);
        if (horizontal > horizontalTolerance)
        {
            rejectReason = "glancing";
            return false;
        }

        rejectReason = Ok;
        return true;
    }

    public static Vector3 Flatten(Vector3 point)
    {
        return new Vector3(point.x, 0f, point.z);
    }

    public static Vector3 ResolveBodyCenter(Transform root, CapsuleCollider capsule)
    {
        if (capsule != null && capsule.enabled)
            return root.TransformPoint(capsule.center);

        return root.position + Vector3.up * 0.45f;
    }
}
