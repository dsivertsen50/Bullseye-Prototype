using UnityEngine;

/// <summary>
/// Makes a weapon mesh visible at a known length, regardless of Blender export units.
/// Instantiates the source model if the prefab instance has no mesh, then orients
/// the longest mesh axis along local +Z so first-person aim points forward.
/// </summary>
public class FittedWeaponModel : MonoBehaviour
{
    private static readonly Quaternion BlenderToUnity = new Quaternion(0.5f, -0.5f, -0.5f, -0.5f);

    [SerializeField] private GameObject sourceModel;
    [SerializeField] private Material material;
    [SerializeField] private float targetLength = 0.85f;
    [SerializeField] private Vector3 extraLocalEuler;

    private bool fitted;

    private void Awake()
    {
        Fit();
    }

    private void OnEnable()
    {
        Fit();
    }

    public void Fit()
    {
        FitInternal(false);
    }

    public void ForceFit()
    {
        FitInternal(true);
    }

    private void FitInternal(bool force)
    {
        if (fitted && !force)
            return;

        Transform model = transform.Find("Model");
        MeshFilter filter = GetComponentInChildren<MeshFilter>(true);
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.bounds.size.sqrMagnitude < 0.0000001f)
        {
            if (sourceModel == null)
                return;

            if (model != null)
            {
                if (Application.isPlaying)
                    Destroy(model.gameObject);
                else
                    DestroyImmediate(model.gameObject);
            }

            GameObject instance = Instantiate(sourceModel, transform);
            instance.name = "Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            filter = instance.GetComponentInChildren<MeshFilter>(true);
            model = instance.transform;
        }

        if (filter == null || filter.sharedMesh == null)
            return;

        if (material != null)
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sharedMaterial = material;
        }

        if (model == null)
            model = filter.transform;

        Mesh mesh = filter.sharedMesh;
        Vector3 size = mesh.bounds.size;
        float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (longest < 0.0001f)
            return;

        float scale = targetLength / longest;
        model.localScale = Vector3.one * scale;
        model.localRotation = Quaternion.Euler(extraLocalEuler) * BlenderToUnity;
        AlignLongAxisToForward(model, mesh);
        model.localPosition = model.localRotation * (-mesh.bounds.center * scale);
        fitted = true;
    }

    private static void AlignLongAxisToForward(Transform model, Mesh mesh)
    {
        Vector3 oriented = OrientedSize(mesh.bounds.size, model.localRotation);
        if (oriented.z >= oriented.x && oriented.z >= oriented.y)
            return;

        if (oriented.y >= oriented.x)
            model.localRotation = Quaternion.Euler(90f, 0f, 0f) * model.localRotation;
        else
            model.localRotation = Quaternion.Euler(0f, -90f, 0f) * model.localRotation;
    }

    private static Vector3 OrientedSize(Vector3 size, Quaternion rotation)
    {
        Vector3 x = rotation * new Vector3(size.x, 0f, 0f);
        Vector3 y = rotation * new Vector3(0f, size.y, 0f);
        Vector3 z = rotation * new Vector3(0f, 0f, size.z);
        return new Vector3(
            Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
    }
}
