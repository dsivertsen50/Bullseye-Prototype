using UnityEngine;

/// <summary>
/// Opt-in marker that lets hitscan bullets reflect from this collider.
/// Add to any GameObject that has a collider, or to a parent of that collider.
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.35f);
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null)
                continue;

            Gizmos.matrix = col.transform.localToWorldMatrix;
            if (col is BoxCollider box)
                Gizmos.DrawWireCube(box.center, box.size);
            else if (col is SphereCollider sphere)
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            else
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
#endif
}
