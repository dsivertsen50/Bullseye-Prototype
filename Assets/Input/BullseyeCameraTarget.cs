using UnityEngine;

public enum BullseyeCameraTrackingMode
{
    Attached = 0,
    Detached = 1,
    Unavailable = 2
}

/// <summary>
/// Read-only sample of the bullseye the HUD camera should follow.
/// Uses the same surface pose as the decal. Does not move the bullseye.
/// </summary>
public struct BullseyeCameraTarget
{
    public bool Valid;
    public BullseyeCameraTrackingMode Mode;
    public Vector3 Position;
    public Vector3 Normal;
    public Transform Follow;

    public static BullseyeCameraTarget Sample(BullseyeMover mover, BullseyeDetachController detach)
    {
        BullseyeCameraTarget target = default;
        bool attached = detach == null || detach.IsAttached;

        if (attached && mover != null && mover.TryGetSurfacePose(out Vector3 position, out Vector3 normal, out _))
        {
            if (normal.sqrMagnitude < 0.0001f)
                normal = Vector3.forward;

            target.Valid = true;
            target.Mode = BullseyeCameraTrackingMode.Attached;
            target.Position = position;
            target.Normal = normal.normalized;
            target.Follow = mover.PhysicalBullseye;
            return target;
        }

        Transform bullseye = detach != null ? detach.BullseyeTransform : null;
        if (!attached && bullseye != null)
        {
            target.Valid = true;
            target.Mode = BullseyeCameraTrackingMode.Detached;
            target.Position = bullseye.position;
            target.Normal = Vector3.zero;
            target.Follow = bullseye;
            return target;
        }

        target.Mode = BullseyeCameraTrackingMode.Unavailable;
        return target;
    }
}
