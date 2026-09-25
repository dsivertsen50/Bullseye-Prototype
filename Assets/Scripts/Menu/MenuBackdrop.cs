using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Lightweight 3D menu stage: a slowly turning presentation copy of the player,
/// framed on the right so the left-side UI can sit over a dark scrim.
/// Assign menuPresentationController later to play a looping dance in place.
/// </summary>
public class MenuBackdrop : MonoBehaviour
{
    [SerializeField] private Transform subjectRoot;
    [SerializeField] private Camera menuCamera;
    [SerializeField] private GameObject playerPresentationPrefab;
    [SerializeField, Tooltip("Optional looping clip or controller for a future menu dance. Assigned onto the presentation Animator without changing the gameplay player.")]
    private RuntimeAnimatorController menuPresentationController;
    [SerializeField] private float rotateSpeed = 14f;
    [SerializeField] private bool frameCamera = true;

    public Animator PresentationAnimator { get; private set; }

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

        PresentPlayer();
        CreateKeyLight(stage.transform);
    }

    private void PresentPlayer()
    {
        GameObject source = playerPresentationPrefab;
        if (source == null)
            source = FindScenePawn();

        if (source == null)
            return;

        GameObject holder = new GameObject("MenuPlayer");
        holder.transform.SetParent(subjectRoot, false);
        holder.SetActive(false);

        GameObject player = Instantiate(source, holder.transform);
        player.name = "MenuPlayerVisual";
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;
        player.transform.localScale = Vector3.one;

        if (player.GetComponent<MenuDisplayPawn>() == null)
            player.AddComponent<MenuDisplayPawn>();

        MenuDisplayPawn.Neutralize(player);
        HidePlaceholderCapsule(player);
        HidePresentationHud(player);
        PreparePresentationAnimator(player);

        holder.SetActive(true);
        MenuDisplayPawn.Neutralize(player);
        player.SetActive(true);
        HideSceneStandIns();
    }

    private void HideSceneStandIns()
    {
        MenuDisplayPawn[] pawns = FindObjectsByType<MenuDisplayPawn>(FindObjectsInactive.Include);
        for (int i = 0; i < pawns.Length; i++)
        {
            if (pawns[i] == null || (subjectRoot != null && pawns[i].transform.IsChildOf(subjectRoot)))
                continue;

            Renderer[] renderers = pawns[i].GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] != null)
                    renderers[r].enabled = false;
            }
        }
    }

    private static GameObject FindScenePawn()
    {
        MenuDisplayPawn[] pawns = FindObjectsByType<MenuDisplayPawn>(FindObjectsInactive.Include);
        for (int i = 0; i < pawns.Length; i++)
        {
            if (pawns[i] != null)
                return pawns[i].gameObject;
        }

        return null;
    }

    private static void HidePlaceholderCapsule(GameObject player)
    {
        Transform capsule = player.transform.Find("Capsule");
        if (capsule != null)
            capsule.gameObject.SetActive(false);
    }

    private static void HidePresentationHud(GameObject player)
    {
        Canvas[] canvases = player.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null)
                canvases[i].enabled = false;
        }
    }

    private void PreparePresentationAnimator(GameObject player)
    {
        Transform visualRoot = player.transform.Find("VisualRoot");
        Animator animator = visualRoot != null
            ? visualRoot.GetComponentInChildren<Animator>(true)
            : player.GetComponentInChildren<Animator>(true);
        PresentationAnimator = animator;
        if (animator == null)
            return;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (menuPresentationController != null)
            animator.runtimeAnimatorController = menuPresentationController;
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
