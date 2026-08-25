using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Lightweight 3D menu stage: a slowly turning player stand-in with a bullseye,
/// framed on the right so the left-side UI can sit over a dark scrim.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    [SerializeField] private Transform subjectRoot;
    [SerializeField] private Camera menuCamera;
    [SerializeField] private float rotateSpeed = 14f;
    [SerializeField] private bool frameCamera = true;

    private void Awake()
    {
        if (subjectRoot == null)
            BuildStage();

        if (frameCamera)
            FrameCamera();
    }

    private void Update()
    {
        if (subjectRoot != null)
            subjectRoot.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
    }

    private void BuildStage()
    {
        GameObject stage = new GameObject("MenuStage");
        stage.transform.SetParent(transform, false);

        GameObject ground = CreatePrimitive("Ground", PrimitiveType.Cube, new Vector3(0f, -0.05f, 4.5f), new Vector3(18f, 0.1f, 18f), new Color(0.12f, 0.13f, 0.15f, 1f));
        ground.transform.SetParent(stage.transform, false);

        subjectRoot = new GameObject("Subject").transform;
        subjectRoot.SetParent(stage.transform, false);
        subjectRoot.position = new Vector3(2.55f, 0f, 5.35f);
        subjectRoot.rotation = Quaternion.Euler(0f, -18f, 0f);

        GameObject body = CreatePrimitive("Body", PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), new Vector3(0.9f, 1f, 0.9f), new Color(0.28f, 0.32f, 0.34f, 1f));
        body.transform.SetParent(subjectRoot, false);

        GameObject head = CreatePrimitive("Head", PrimitiveType.Sphere, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.42f, new Color(0.34f, 0.36f, 0.38f, 1f));
        head.transform.SetParent(subjectRoot, false);

        GameObject bullseye = CreateBullseye();
        bullseye.transform.SetParent(subjectRoot, false);
        bullseye.transform.localPosition = new Vector3(0f, 1.15f, 0.42f);
        bullseye.transform.localRotation = Quaternion.identity;

        CreateKeyLight(stage.transform);
    }

    private static void CreateKeyLight(Transform parent)
    {
        GameObject lightObject = new GameObject("MenuKeyLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.position = new Vector3(0.6f, 3.1f, 2.8f);
        lightObject.transform.LookAt(new Vector3(2.55f, 1.2f, 5.35f));

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.96f, 0.9f, 1f);
        light.range = 18f;
        light.spotAngle = 55f;
        light.intensity = 22000f;

        HDAdditionalLightData hd = lightObject.AddComponent<HDAdditionalLightData>();
        hd.EnableShadows(false);
    }

    private static GameObject CreateBullseye()
    {
        GameObject root = new GameObject("Bullseye");
        CreateDisc(root.transform, "Outer", 0.28f, 0.02f, new Color(1f, 1f, 0.96f, 1f), 0f, false);
        CreateDisc(root.transform, "Red", 0.2f, 0.021f, new Color(0.95f, 0.12f, 0.12f, 1f), 0.001f, true);
        CreateDisc(root.transform, "Inner", 0.12f, 0.022f, new Color(1f, 1f, 0.96f, 1f), 0.002f, false);
        CreateDisc(root.transform, "Center", 0.05f, 0.023f, new Color(1f, 0.16f, 0.12f, 1f), 0.003f, true);
        return root;
    }

    private static void CreateDisc(Transform parent, string name, float radius, float thickness, Color color, float z, bool emissive)
    {
        GameObject disc = CreatePrimitive(name, PrimitiveType.Cylinder, new Vector3(0f, 0f, z), new Vector3(radius * 2f, thickness, radius * 2f), color, emissive);
        disc.transform.SetParent(parent, false);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color, bool emissive = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = CreateLitMaterial(color, emissive);

        return go;
    }

    private static Material CreateLitMaterial(Color color, bool emissive)
    {
        Shader shader = Shader.Find(emissive ? "HDRP/Unlit" : "HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("HDRP/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            color = color
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_UnlitColor"))
            material.SetColor("_UnlitColor", color);
        if (emissive && material.HasProperty("_EmissiveColor"))
            material.SetColor("_EmissiveColor", color * 4f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.35f);

        return material;
    }

    private void FrameCamera()
    {
        if (menuCamera == null)
            menuCamera = Camera.main;
        if (menuCamera == null)
            return;

        menuCamera.transform.position = new Vector3(-1.2f, 1.48f, 1.9f);
        menuCamera.transform.rotation = Quaternion.LookRotation(new Vector3(0.2f, 1.12f, 5.15f) - menuCamera.transform.position);
        menuCamera.fieldOfView = 46f;

        HDAdditionalCameraData hd = menuCamera.GetComponent<HDAdditionalCameraData>();
        if (hd != null)
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
    }
}
