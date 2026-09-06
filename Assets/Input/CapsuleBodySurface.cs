using UnityEngine;

/// <summary>
/// Legacy capsule surface math. Bullseye movement now uses
/// <see cref="BullseyeSurfaceMap"/>. Kept for HUD/damage fallbacks and any
/// remaining capsule-space helpers.
/// </summary>
public static class CapsuleBodySurface
{
    public static void Evaluate(
        CapsuleCollider capsule,
        float u,
        float v,
        out Vector3 localPosition,
        out Vector3 localNormal)
    {
        v = Mathf.Clamp01(v);

        GetShape(capsule, out float radius, out float cylinderHalf, out float meridianLength);
        float s = v * meridianLength;
        float bottomHemi = Mathf.PI * radius * 0.5f;

        // u = 0 faces the capsule's local +Z (player front).
        float angle = u * Mathf.PI * 2f;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        Vector3 point;
        Vector3 normal;

        if (s <= bottomHemi)
        {
            float phi = s / radius;
            float ringRadius = radius * Mathf.Sin(phi);
            float y = -cylinderHalf - radius * Mathf.Cos(phi);
            point = new Vector3(ringRadius * sin, y, ringRadius * cos);
            normal = (point - new Vector3(0f, -cylinderHalf, 0f)).normalized;
        }
        else if (s <= bottomHemi + 2f * cylinderHalf)
        {
            float y = -cylinderHalf + (s - bottomHemi);
            point = new Vector3(radius * sin, y, radius * cos);
            normal = new Vector3(sin, 0f, cos);
        }
        else
        {
            float phi = (s - bottomHemi - 2f * cylinderHalf) / radius;
            float ringRadius = radius * Mathf.Cos(phi);
            float y = cylinderHalf + radius * Mathf.Sin(phi);
            point = new Vector3(ringRadius * sin, y, ringRadius * cos);
            normal = (point - new Vector3(0f, cylinderHalf, 0f)).normalized;
        }

        localPosition = capsule.center + AlignToColliderAxis(point, capsule.direction);
        localNormal = AlignToColliderAxis(normal, capsule.direction);
    }

    public static float EquatorRadius(CapsuleCollider capsule)
    {
        return Mathf.Max(0.0001f, capsule.radius);
    }

    /// <summary>
    /// Body-relative height of a world point along the capsule axis, 0 at the
    /// bottom pole and 1 at the top pole. Independent of world position, rotation,
    /// and jumping.
    /// </summary>
    public static float GetNormalizedHeight(CapsuleCollider capsule, Vector3 worldPosition)
    {
        Vector3 local = capsule.transform.InverseTransformPoint(worldPosition) - capsule.center;
        float axis = capsule.direction == 0
            ? local.x
            : capsule.direction == 2
                ? local.z
                : local.y;
        float halfHeight = Mathf.Max(capsule.height, capsule.radius * 2f) * 0.5f;
        return Mathf.InverseLerp(-halfHeight, halfHeight, axis);
    }

    /// <summary>
    /// Local UV scales so a (du, dv) step of 1 maps to meters along the capsule surface.
    /// u is one full turn around the body; v is one full meridian from bottom to top.
    /// </summary>
    public static void GetUvScales(
        CapsuleCollider capsule,
        float v,
        float surfaceOffset,
        out float metersPerU,
        out float metersPerV)
    {
        v = Mathf.Clamp01(v);
        GetShape(capsule, out float radius, out float cylinderHalf, out float meridianLength);

        float s = v * meridianLength;
        float bottomHemi = Mathf.PI * radius * 0.5f;
        float ringRadius;

        if (s <= bottomHemi)
        {
            float phi = s / radius;
            ringRadius = radius * Mathf.Sin(phi);
        }
        else if (s <= bottomHemi + 2f * cylinderHalf)
        {
            ringRadius = radius;
        }
        else
        {
            float phi = (s - bottomHemi - 2f * cylinderHalf) / radius;
            ringRadius = radius * Mathf.Cos(phi);
        }

        metersPerU = 2f * Mathf.PI * Mathf.Max(0.0001f, ringRadius + Mathf.Max(0f, surfaceOffset));
        metersPerV = Mathf.Max(0.0001f, meridianLength);
    }

    private static void GetShape(
        CapsuleCollider capsule,
        out float radius,
        out float cylinderHalf,
        out float meridianLength)
    {
        radius = Mathf.Max(0.0001f, capsule.radius);
        float height = Mathf.Max(capsule.height, radius * 2f);
        cylinderHalf = height * 0.5f - radius;
        meridianLength = Mathf.PI * radius + 2f * cylinderHalf;
    }

    private static Vector3 AlignToColliderAxis(Vector3 yAxisCapsulePoint, int direction)
    {
        switch (direction)
        {
            case 0: return new Vector3(yAxisCapsulePoint.y, yAxisCapsulePoint.x, yAxisCapsulePoint.z);
            case 2: return new Vector3(yAxisCapsulePoint.x, yAxisCapsulePoint.z, yAxisCapsulePoint.y);
            default: return yAxisCapsulePoint;
        }
    }
}
