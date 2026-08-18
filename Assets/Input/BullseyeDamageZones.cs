using UnityEngine;

public enum BullseyeBodyZone
{
    Lower,
    Middle,
    Upper
}

/// <summary>
/// Capsule-only vertical zone classification. Isolated so a later animated
/// body can replace this mapping without rewriting health or hit processing.
/// </summary>
public static class BullseyeDamageZones
{
    public static BullseyeBodyZone Classify(
        CapsuleCollider capsule,
        Vector3 worldPosition,
        float lowerMiddleBoundary,
        float middleUpperBoundary)
    {
        float height = Mathf.Clamp01(
            CapsuleBodySurface.GetNormalizedHeight(capsule, worldPosition));

        float lower = Mathf.Min(lowerMiddleBoundary, middleUpperBoundary);
        float upper = Mathf.Max(lowerMiddleBoundary, middleUpperBoundary);

        if (height < lower)
            return BullseyeBodyZone.Lower;
        if (height < upper)
            return BullseyeBodyZone.Middle;
        return BullseyeBodyZone.Upper;
    }
}
