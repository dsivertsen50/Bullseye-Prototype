using UnityEngine;

/// <summary>
/// Opt-in marker that lets hitscan bullets reflect from this collider.
/// Add to any GameObject that has a collider, or to a parent of that collider.
/// This does not change mesh, scale, or collider shape.
/// </summary>
public class RicochetSurface : MonoBehaviour
{
    [SerializeField, Tooltip("When disabled, this surface behaves like a normal wall.")]
    private bool ricochetEnabled = true;

    public bool RicochetEnabled => ricochetEnabled;

    public static bool TryGetEnabled(Collider collider, out RicochetSurface surface)
    {
        surface = null;
        if (collider == null)
            return false;

        surface = collider.GetComponentInParent<RicochetSurface>();
        return surface != null && surface.isActiveAndEnabled && surface.ricochetEnabled;
    }
}
