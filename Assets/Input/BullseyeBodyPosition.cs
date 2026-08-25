using UnityEngine;

public enum BullseyeFacing
{
    Front,
    Back
}

/// <summary>
/// Player-local bullseye location. Independent of world position, rotation,
/// and jumping. Shared by the Body HUD and later body-region statistics.
/// </summary>
public readonly struct BullseyeBodyPosition
{
    /// <summary>0 at the feet / bottom pole, 1 at the head / top pole.</summary>
    public readonly float NormalizedHeight;

    /// <summary>-1 at the player's left, +1 at the player's right.</summary>
    public readonly float NormalizedLateral;

    public readonly BullseyeFacing Facing;

    /// <summary>Offset from the body center, in body-root local space.</summary>
    public readonly Vector3 LocalOffset;

    public BullseyeBodyPosition(
        float normalizedHeight,
        float normalizedLateral,
        BullseyeFacing facing,
        Vector3 localOffset)
    {
        NormalizedHeight = Mathf.Clamp01(normalizedHeight);
        NormalizedLateral = Mathf.Clamp(normalizedLateral, -1f, 1f);
        Facing = facing;
        LocalOffset = localOffset;
    }

    /// <summary>
    /// Vertical region using the same LowerBody / Torso / Head bands as
    /// <see cref="BullseyeDamageZones"/>. Does not record statistics.
    /// </summary>
    public BullseyeBodyZone VerticalZone(float lowerTorsoBoundary, float torsoHeadBoundary)
    {
        float lower = Mathf.Min(lowerTorsoBoundary, torsoHeadBoundary);
        float upper = Mathf.Max(lowerTorsoBoundary, torsoHeadBoundary);

        if (NormalizedHeight < lower)
            return BullseyeBodyZone.LowerBody;
        if (NormalizedHeight < upper)
            return BullseyeBodyZone.Torso;
        return BullseyeBodyZone.Head;
    }
}
