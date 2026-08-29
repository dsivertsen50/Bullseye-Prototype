using UnityEngine;

/// <summary>
/// Analytical two-bone IK used to layer weapon holds over Mixamo locomotion.
/// Equivalent to Animation Rigging TwoBoneIK for this prototype.
/// </summary>
public static class ThirdPersonTwoBoneIK
{
    public static void Solve(
        Transform upper,
        Transform lower,
        Transform end,
        Vector3 target,
        Vector3 pole,
        float weight,
        float maxReachFraction = 0.999f)
    {
        if (upper == null || lower == null || end == null || weight <= 0.0001f)
            return;

        Vector3 upperPos = upper.position;
        Vector3 lowerPos = lower.position;
        Vector3 endPos = end.position;

        float upperLength = Vector3.Distance(upperPos, lowerPos);
        float lowerLength = Vector3.Distance(lowerPos, endPos);
        if (upperLength < 0.0001f || lowerLength < 0.0001f)
            return;

        Vector3 desiredEnd = Vector3.Lerp(endPos, target, weight);
        float maxReach = (upperLength + lowerLength) * Mathf.Clamp(maxReachFraction, 0.55f, 0.999f);
        float minReach = Mathf.Abs(upperLength - lowerLength) + 0.001f;
        Vector3 toTarget = desiredEnd - upperPos;
        float distance = Mathf.Clamp(toTarget.magnitude, minReach, maxReach);
        if (toTarget.sqrMagnitude < 0.0000001f)
            return;

        desiredEnd = upperPos + toTarget.normalized * distance;

        float denomUpper = 2f * upperLength * distance;
        float denomLower = 2f * upperLength * lowerLength;
        if (denomUpper < 0.0001f || denomLower < 0.0001f)
            return;

        float shoulderAngle = Mathf.Acos(Mathf.Clamp(
            (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / denomUpper,
            -1f,
            1f));

        Vector3 targetDir = (desiredEnd - upperPos).normalized;
        Vector3 poleDir = pole - upperPos;
        Vector3 bendAxis = Vector3.Cross(targetDir, poleDir);
        if (bendAxis.sqrMagnitude < 0.000001f)
            bendAxis = Vector3.Cross(targetDir, upper.up);
        if (bendAxis.sqrMagnitude < 0.000001f)
            return;

        bendAxis.Normalize();
        Vector3 desiredLower = upperPos +
            Quaternion.AngleAxis(shoulderAngle * Mathf.Rad2Deg, bendAxis) * targetDir * upperLength;

        Vector3 currentUpperDir = lowerPos - upperPos;
        Vector3 desiredUpperDir = desiredLower - upperPos;
        if (currentUpperDir.sqrMagnitude > 0.0000001f && desiredUpperDir.sqrMagnitude > 0.0000001f)
        {
            Quaternion upperDelta = Quaternion.FromToRotation(currentUpperDir, desiredUpperDir);
            upper.rotation = Quaternion.Slerp(Quaternion.identity, upperDelta, weight) * upper.rotation;
        }

        Vector3 newLowerPos = lower.position;
        Vector3 currentLowerDir = end.position - newLowerPos;
        Vector3 desiredLowerDir = desiredEnd - newLowerPos;
        if (currentLowerDir.sqrMagnitude > 0.0000001f && desiredLowerDir.sqrMagnitude > 0.0000001f)
        {
            Quaternion lowerDelta = Quaternion.FromToRotation(currentLowerDir, desiredLowerDir);
            lower.rotation = Quaternion.Slerp(Quaternion.identity, lowerDelta, weight) * lower.rotation;
        }
    }

    public static void ApplyEndRotation(Transform end, Quaternion desired, float weight)
    {
        if (end == null || weight <= 0.0001f)
            return;

        end.rotation = Quaternion.Slerp(end.rotation, desired, weight);
    }
}
