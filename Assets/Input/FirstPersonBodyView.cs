using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Local-only first-person lower body. The world StickMan other players see
/// stays intact. The owner sees SM_StickMan_FPBody instead of a sliced copy
/// of that mesh. Leg motion is the world skeleton's pose, copied onto the
/// dedicated body. Nothing here is networked.
/// </summary>
[DefaultExecutionOrder(120)]
public class FirstPersonBodyView : NetworkBehaviour
{
    private const string LocalPlayerBodyLayerName = "LocalPlayerBody";
    private const string BodyPrefabPath = "Assets/Player/SM_StickMan_FPBody.fbx";

    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private GameObject fpBodyPrefab;
    [SerializeField] private Vector3 fpBodyPositionOffset = new Vector3(0f, -0.03f, -0.06f);
    [SerializeField] private Vector3 fpBodyRotationOffset = Vector3.zero;
    [SerializeField] private float eyeHeightOffset = 0.12f;
    [SerializeField] private float eyeForwardOffset = 0.06f;
    [SerializeField] private float followSharpness = 16f;
    [SerializeField] private float wallProbeRadius = 0.07f;
    [SerializeField] private float wallPadding = 0.08f;
    [SerializeField, Tooltip("First-person legs while climbing, in camera-root space. X shifts left/right, Y is down from the camera, Z is forward.")]
    private Vector3 climbLegsCameraOffset = new Vector3(0f, -0.42f, 0.08f);

    private PlayerHealth playerHealth;
    private PlayerMovement movement;
    private readonly List<RendererState> worldRenderers = new();
    private readonly List<BoneLink> boneLinks = new();
    private GameObject fpBodyRoot;
    private bool ownerActive;
    private bool eliminationPresentation;
    private bool hasSmoothedCamera;
    private bool holdClimbCamera;
    private Vector3 smoothedCameraLocal;
    private Vector3 cameraLocalVelocity;
    private readonly RaycastHit[] wallProbeHits = new RaycastHit[16];

    public bool IsOwnerViewActive => ownerActive;
    public Transform BodyRoot => fpBodyRoot != null ? fpBodyRoot.transform : null;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        movement = GetComponent<PlayerMovement>();
        ResolveReferences();
    }

    public override void OnNetworkSpawn()
    {
        ownerActive = IsOwner;
        if (!ownerActive)
        {
            enabled = false;
            return;
        }

        ResolveReferences();
        if (bodyAnimator != null)
            bodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CacheWorldRenderers();
        CreateDedicatedBody();
        ApplyPresentationMode();
        IncludeBodyOnOwnerCamera();
    }

    public override void OnNetworkDespawn()
    {
        SetWorldBodyVisible(true);
        if (fpBodyRoot != null)
            Destroy(fpBodyRoot);
        fpBodyRoot = null;
        boneLinks.Clear();
        ownerActive = false;
    }

    public void SetEliminationPresentation(bool showFullBody)
    {
        eliminationPresentation = showFullBody;
        if (!ownerActive)
            return;

        ApplyPresentationMode();
    }

    private void LateUpdate()
    {
        if (!ownerActive)
            return;

        bool dead = playerHealth != null && playerHealth.IsDead;
        if (dead != eliminationPresentation)
        {
            eliminationPresentation = dead;
            ApplyPresentationMode();
        }

        if (dead || eliminationPresentation)
        {
            hasSmoothedCamera = false;
            return;
        }

        HideWorldBody();
        if (movement != null && movement.IsClimbing)
            HoldClimbCamera();
        else
            FollowEyes();
        PoseDedicatedBody();
    }

    private void CreateDedicatedBody()
    {
        if (fpBodyRoot != null)
            return;

        GameObject prefab = ResolveBodyPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("SM_StickMan_FPBody is not assigned. The owner will not see a first-person lower body.");
            return;
        }

        fpBodyRoot = new GameObject("FirstPersonBody");
        fpBodyRoot.transform.SetParent(transform, false);
        GameObject instance = Instantiate(prefab, fpBodyRoot.transform, false);
        instance.name = "SM_StickMan_FPBody";
        DisableForeignAnimators(instance);
        ConfigurePresentationRenderers(instance, LocalPlayerBodyLayerName, castShadows: false);
        CacheBoneLinks(instance.transform);
        PoseDedicatedBody();
    }

    private GameObject ResolveBodyPrefab()
    {
        if (fpBodyPrefab != null)
            return fpBodyPrefab;

#if UNITY_EDITOR
        fpBodyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BodyPrefabPath);
#endif
        return fpBodyPrefab;
    }

    private void PoseDedicatedBody()
    {
        if (fpBodyRoot == null)
            return;

        Transform root = fpBodyRoot.transform;
        bool climbing = movement != null && movement.IsClimbing;
        if (climbing && cameraRoot != null)
        {
            if (root.parent != cameraRoot)
                root.SetParent(cameraRoot, false);

            root.localRotation = Quaternion.Euler(fpBodyRotationOffset.x, fpBodyRotationOffset.y + 180f, fpBodyRotationOffset.z);
            root.localPosition = climbLegsCameraOffset;
            CopyBodyBones();
            CenterClimbBodyOnCamera();
            return;
        }

        if (root.parent != transform)
            root.SetParent(transform, false);

        root.localPosition = fpBodyPositionOffset;
        root.localRotation = Quaternion.Euler(fpBodyRotationOffset);
        root.localScale = Vector3.one;
        CopyBodyBones();
    }

    private void CopyBodyBones()
    {
        Transform root = fpBodyRoot.transform;
        root.localScale = Vector3.one;
        for (int i = 0; i < boneLinks.Count; i++)
        {
            BoneLink link = boneLinks[i];
            if (link.Source == null || link.Destination == null)
                continue;

            link.Destination.localPosition = link.Source.localPosition;
            link.Destination.localRotation = link.Source.localRotation;
        }
    }

    private void CenterClimbBodyOnCamera()
    {
        Transform hips = null;
        for (int i = 0; i < boneLinks.Count; i++)
        {
            if (boneLinks[i].Destination != null && boneLinks[i].Destination.name == "mixamorig:Hips")
            {
                hips = boneLinks[i].Destination;
                break;
            }
        }

        if (hips == null || cameraRoot == null)
            return;

        Vector3 local = cameraRoot.InverseTransformPoint(hips.position);
        fpBodyRoot.transform.localPosition -= new Vector3(local.x, 0f, local.z - climbLegsCameraOffset.z);
    }

    private void CacheBoneLinks(Transform dedicatedRoot)
    {
        boneLinks.Clear();
        if (bodyAnimator == null || dedicatedRoot == null)
            return;

        var worldBones = new Dictionary<string, Transform>();
        Transform[] sources = bodyAnimator.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            Transform source = sources[i];
            if (source == null || !source.name.StartsWith("mixamorig:"))
                continue;
            if (!worldBones.ContainsKey(source.name))
                worldBones.Add(source.name, source);
        }

        Transform[] destinations = dedicatedRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < destinations.Length; i++)
        {
            Transform destination = destinations[i];
            if (destination == null || !worldBones.TryGetValue(destination.name, out Transform source))
                continue;

            boneLinks.Add(new BoneLink { Source = source, Destination = destination });
        }
    }

    private void ApplyPresentationMode()
    {
        bool showWorld = eliminationPresentation;
        SetWorldBodyVisible(showWorld);
        if (fpBodyRoot != null && fpBodyRoot.activeSelf == showWorld)
            fpBodyRoot.SetActive(!showWorld);
    }

    private void HideWorldBody()
    {
        SetWorldBodyVisible(false);
    }

    private void CacheWorldRenderers()
    {
        worldRenderers.Clear();
        if (visualRoot == null)
            return;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            bool updateWhenOffscreen = false;
            if (renderer is SkinnedMeshRenderer skinned)
                updateWhenOffscreen = skinned.updateWhenOffscreen;

            worldRenderers.Add(new RendererState
            {
                Renderer = renderer,
                OriginallyEnabled = renderer.enabled,
                OriginalShadowMode = renderer.shadowCastingMode,
                OriginalUpdateWhenOffscreen = updateWhenOffscreen
            });
        }
    }

    private void SetWorldBodyVisible(bool visible)
    {
        for (int i = 0; i < worldRenderers.Count; i++)
        {
            RendererState state = worldRenderers[i];
            Renderer renderer = state.Renderer;
            if (renderer == null)
                continue;

            if (visible)
            {
                renderer.enabled = state.OriginallyEnabled;
                renderer.shadowCastingMode = state.OriginalShadowMode;
                if (renderer is SkinnedMeshRenderer shown)
                    shown.updateWhenOffscreen = state.OriginalUpdateWhenOffscreen;
                continue;
            }

            if (IsOwnerWorldBody(renderer) && state.OriginallyEnabled)
            {
                renderer.enabled = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                if (renderer is SkinnedMeshRenderer hidden)
                    hidden.updateWhenOffscreen = true;
                continue;
            }

            if (renderer.enabled)
                renderer.enabled = false;
        }
    }

    private static bool IsOwnerWorldBody(Renderer renderer)
    {
        if (renderer is not SkinnedMeshRenderer)
            return false;

        string name = renderer.gameObject.name;
        if (name == "StampOverlay")
            return false;
        if (name.IndexOf("Bullseye", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        return true;
    }

    private void IncludeBodyOnOwnerCamera()
    {
        if (playerCamera == null)
            return;

        int layer = LayerMask.NameToLayer(LocalPlayerBodyLayerName);
        if (layer >= 0)
            playerCamera.cullingMask |= 1 << layer;
    }

    private void HoldClimbCamera()
    {
        if (cameraRoot == null)
            return;

        if (!holdClimbCamera)
        {
            if (!hasSmoothedCamera)
                smoothedCameraLocal = cameraRoot.localPosition;
            smoothedCameraLocal.x = 0f;
            smoothedCameraLocal.z = eyeForwardOffset;
            holdClimbCamera = true;
            hasSmoothedCamera = true;
        }

        cameraRoot.localPosition = smoothedCameraLocal;
    }

    private void FollowEyes()
    {
        holdClimbCamera = false;
        if (cameraRoot == null || bodyAnimator == null || !bodyAnimator.isHuman)
            return;

        Transform head = bodyAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
            return;

        Vector3 headLocal = transform.InverseTransformPoint(head.position);
        Vector3 desiredLocal = new Vector3(0f, headLocal.y + eyeHeightOffset, headLocal.z + eyeForwardOffset);
        Vector3 desiredWorld = transform.TransformPoint(desiredLocal);
        desiredWorld = PullBackFromWalls(desiredWorld, headLocal.y);

        Vector3 desired = transform.InverseTransformPoint(desiredWorld);
        desired.x = 0f;
        if (!hasSmoothedCamera)
        {
            smoothedCameraLocal = cameraRoot.localPosition;
            hasSmoothedCamera = true;
        }

        float smooth = Mathf.Max(0.01f, followSharpness);
        smoothedCameraLocal = Vector3.SmoothDamp(
            smoothedCameraLocal,
            desired,
            ref cameraLocalVelocity,
            1f / smooth);
        cameraRoot.localPosition = smoothedCameraLocal;
    }

    private Vector3 PullBackFromWalls(Vector3 desiredWorld, float headHeight)
    {
        Vector3 origin = transform.position + Vector3.up * Mathf.Clamp(headHeight * 0.55f, 0.25f, 1.4f);
        Vector3 delta = desiredWorld - origin;
        float distance = delta.magnitude;
        if (distance < 0.05f)
            return desiredWorld;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            wallProbeRadius,
            delta / distance,
            wallProbeHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearest = distance;
        bool blocked = false;
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = wallProbeHits[i].collider;
            if (collider == null || collider.transform.IsChildOf(transform))
                continue;
            if (wallProbeHits[i].distance < nearest)
            {
                nearest = wallProbeHits[i].distance;
                blocked = true;
            }
        }

        if (!blocked)
            return desiredWorld;

        float safe = Mathf.Max(0.02f, nearest - wallPadding);
        return origin + (delta / distance) * safe;
    }

    private void ResolveReferences()
    {
        if (visualRoot == null)
            visualRoot = transform.Find("VisualRoot");
        if (bodyAnimator == null && visualRoot != null)
            bodyAnimator = visualRoot.GetComponentInChildren<Animator>(true);
        if (playerCamera == null)
        {
            Transform cameraTransform = transform.Find("CameraRoot/CameraEffectsRoot/Camera");
            if (cameraTransform != null)
                playerCamera = cameraTransform.GetComponent<Camera>();
        }

        if (cameraRoot == null)
            cameraRoot = transform.Find("CameraRoot");

        if (cameraRoot == null && playerCamera != null)
        {
            Transform effects = playerCamera.transform.parent;
            if (effects != null && effects.parent != null && effects.parent.name == "CameraRoot")
                cameraRoot = effects.parent;
        }
    }

    private static void DisableForeignAnimators(GameObject root)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].enabled = false;
    }

    private static void ConfigurePresentationRenderers(GameObject root, string layerName, bool castShadows)
    {
        int layer = LayerMask.NameToLayer(layerName);
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (layer >= 0)
                transforms[i].gameObject.layer = layer;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = castShadows;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private struct RendererState
    {
        public Renderer Renderer;
        public bool OriginallyEnabled;
        public UnityEngine.Rendering.ShadowCastingMode OriginalShadowMode;
        public bool OriginalUpdateWhenOffscreen;
    }

    private struct BoneLink
    {
        public Transform Source;
        public Transform Destination;
    }
}
