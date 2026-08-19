using UnityEngine;

public enum BullseyeBodyZone
{
    LowerBody,
    Torso,
    Head
}

/// <summary>
/// Capsule-only vertical region classification. Isolated so a later player mesh
/// can replace this mapping without rewriting health or hit processing.
/// </summary>
public static class BullseyeDamageZones
{
    public static BullseyeBodyZone Classify(
        CapsuleCollider capsule,
        Vector3 worldPosition,
        float lowerTorsoBoundary,
        float torsoHeadBoundary)
    {
        float height = Mathf.Clamp01(
            CapsuleBodySurface.GetNormalizedHeight(capsule, worldPosition));

        float lower = Mathf.Min(lowerTorsoBoundary, torsoHeadBoundary);
        float upper = Mathf.Max(lowerTorsoBoundary, torsoHeadBoundary);

        if (height < lower)
            return BullseyeBodyZone.LowerBody;
        if (height < upper)
            return BullseyeBodyZone.Torso;
        return BullseyeBodyZone.Head;
    }
}
