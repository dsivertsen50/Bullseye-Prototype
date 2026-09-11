using UnityEngine;

/// <summary>
/// Labels a collider (or its children) as a footstep material.
/// Add this to floors, platforms, and props the player can walk on.
/// </summary>
public class FootstepSurface : MonoBehaviour
{
    [SerializeField] private FootstepSurfaceKind surface = FootstepSurfaceKind.Default;

    public FootstepSurfaceKind Surface => surface;

    public static bool TryGet(Collider collider, out FootstepSurfaceKind kind)
    {
        kind = FootstepSurfaceKind.Default;
        if (collider == null)
            return false;

        FootstepSurface surface = collider.GetComponentInParent<FootstepSurface>();
        if (surface == null || !surface.isActiveAndEnabled)
            return false;

        kind = surface.surface;
        return true;
    }
}
