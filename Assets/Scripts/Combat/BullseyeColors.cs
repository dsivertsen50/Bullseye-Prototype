using UnityEngine;

/// <summary>
/// Shared Bullseye red. The surface stamp uses the HDR value; UI outlines
/// use the same hue clamped into the 0-1 range.
/// </summary>
public static class BullseyeColors
{
    public static readonly Color StampRed = new Color(1.15f, 0.04f, 0.04f, 1f);

    public static readonly Color UiRed = new Color(
        1f,
        0.04f / 1.15f,
        0.04f / 1.15f,
        1f);
}
