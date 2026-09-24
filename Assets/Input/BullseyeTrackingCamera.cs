using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Local-player HUD camera that follows the authoritative bullseye.
/// Renders the owner's third-person body into a small RenderTexture without
/// putting that body into the first-person gameplay view.
/// </summary>
[DefaultExecutionOrder(220)]
public class BullseyeTrackingCamera : NetworkBehaviour
{
    private const string MonitorLayerName = "BullseyeMonitor";
    private const string LocalPlayerBodyLayerName = "LocalPlayerBody";
    private const string FirstPersonWeaponLayerName = "FirstPersonWeapon";

    private static readonly List<BullseyeTrackingCamera> Active = new();
    private static bool hooksRegistered;

    [Header("Enable")]
    [SerializeField] private bool monitorEnabled = true;

    [Header("Framing")]
    [SerializeField] private float trackingDistance = 0.6f;
    [SerializeField] private float minimumDistance = 0.18f;
    [SerializeField] private float fieldOfView = 46f;
    [SerializeField] private float attachedFarClip = 4.5f;
    [SerializeField] private float detachedFarClip = 8f;
    [SerializeField] private float bodyContext = 0.22f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.14f;
    [SerializeField] private float rotationSmoothSpeed = 5.5f;
    [SerializeField] private float normalSmoothSpeed = 6f;
    [SerializeField] private float maximumCameraSpeed = 2.75f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.07f;
    [SerializeField] private float collisionPadding = 0.04f;

    [Header("Output")]
    [SerializeField] private int renderWidth = 256;
    [SerializeField] private int renderHeight = 320;
    [SerializeField] private float targetFramesPerSecond = 20f;

    [Header("Monitor Look")]
    [SerializeField] private float fixedExposure = 12.5f;
    [SerializeField] private float contrast = 6f;
    [SerializeField] private float saturation = -8f;
    [SerializeField] private float grainIntensity = 0.12f;
    [SerializeField] private float fillLightIntensity = 220f;
    [SerializeField] private Color backgroundColor = new Color(0.035f, 0.04f, 0.045f, 1f);

    [Header("Debug")]
    [SerializeField] private bool debugVisualization;

    private BullseyeMover mover;
    private BullseyeDetachController detach;
    private PlayerHealth playerHealth;
    private BullseyeCameraHud hud;
    private Camera trackingCamera;
    private HDAdditionalCameraData trackingCameraData;
    private Light fillLight;
    private RenderTexture feed;
    private VolumeProfile volumeProfile;

    private readonly List<RendererToggle> revealed = new();
    private readonly List<RendererToggle> hiddenFirstPerson = new();
    private readonly RaycastHit[] collisionHits = new RaycastHit[24];

    private bool ownerActive;
    private bool hasPose;
    private bool wasDead;
    private Vector3 filteredNormal = Vector3.forward;
    private Vector3 detachedOffset = new Vector3(0f, 0.35f, -1f);
    private Vector3 positionVelocity;
    private BullseyeCameraTarget lastTarget;
    private float nextRenderTime;

    private struct RendererToggle
    {
        public Renderer Renderer;
        public ShadowCastingMode ShadowMode;
        public bool Enabled;
    }

    private void Awake()
    {
        mover = GetComponent<BullseyeMover>();
        detach = GetComponent<BullseyeDetachController>();
        playerHealth = GetComponent<PlayerHealth>();
        hud = GetComponent<BullseyeCameraHud>();
        if (hud == null)
            hud = gameObject.AddComponent<BullseyeCameraHud>();
    }

    public override void OnNetworkSpawn()
    {
        ownerActive = IsOwner;
        if (!ownerActive)
        {
            enabled = false;
            return;
        }

        EnsureCamera();
        RegisterHooks();
    }

    public override void OnNetworkDespawn()
    {
        ownerActive = false;
        UnregisterHooks();
        SetPresented(false);
        hasPose = false;
    }

    private void OnDestroy()
    {
        UnregisterHooks();
        if (feed != null)
        {
            feed.Release();
            Destroy(feed);
            feed = null;
        }

        if (volumeProfile != null)
            Destroy(volumeProfile);
    }

    private void LateUpdate()
    {
        if (!ownerActive)
            return;

        bool show = ShouldPresent();
        if (!show)
        {
            SetPresented(false);
            if (playerHealth != null && playerHealth.IsDead)
                wasDead = true;
            return;
        }

        if (wasDead)
        {
            hasPose = false;
            wasDead = false;
        }

        BullseyeCameraTarget target = BullseyeCameraTarget.Sample(mover, detach);
        if (!target.Valid)
        {
            SetPresented(false);
            hasPose = false;
            return;
        }

        lastTarget = target;
        Vector3 desiredPosition = ResolveDesiredPosition(target);
        Quaternion desiredRotation = ResolveDesiredRotation(target, desiredPosition);
        ApplyPose(desiredPosition, desiredRotation);
        DrawDebug(target, desiredPosition);
        SetPresented(true);
        UpdateRenderCadence();
    }

    private bool ShouldPresent()
    {
        if (!monitorEnabled)
            return false;
        if (playerHealth == null || !playerHealth.IsSpawned || !playerHealth.IsOwner || playerHealth.IsDead)
            return false;
        if (LocalPlayerMenuState.IsOpen(this))
            return false;
        return true;
    }

    private Vector3 ResolveDesiredPosition(BullseyeCameraTarget target)
    {
        Vector3 offset = target.Mode == BullseyeCameraTrackingMode.Attached
            ? SmoothNormal(target.Normal)
            : SmoothDetachedOffset(target.Position);

        float distance = ResolveDistance(target.Position, offset);
        return target.Position + offset * distance;
    }

    private Vector3 SmoothNormal(Vector3 rawNormal)
    {
        if (rawNormal.sqrMagnitude < 0.0001f)
            rawNormal = Vector3.forward;
        rawNormal.Normalize();

        if (!hasPose)
        {
            filteredNormal = rawNormal;
            return filteredNormal;
        }

        if (Vector3.Dot(filteredNormal, rawNormal) < 0.05f)
            rawNormal = Vector3.Slerp(filteredNormal, rawNormal, 0.35f).normalized;

        float blend = 1f - Mathf.Exp(-normalSmoothSpeed * Time.deltaTime);
        filteredNormal = Vector3.Slerp(filteredNormal, rawNormal, blend).normalized;
        return filteredNormal;
    }

    private Vector3 SmoothDetachedOffset(Vector3 bullseyePosition)
    {
        Vector3 current = hasPose && trackingCamera != null
            ? trackingCamera.transform.position - bullseyePosition
            : detachedOffset;

        if (current.sqrMagnitude < 0.0001f)
            current = new Vector3(0f, 0.35f, -1f);

        current.y = Mathf.Lerp(current.y, 0.28f, 0.2f);
        detachedOffset = current.normalized;
        return detachedOffset;
    }

    private float ResolveDistance(Vector3 origin, Vector3 offset)
    {
        float desired = Mathf.Max(minimumDistance, trackingDistance);
        Vector3 start = origin + offset * 0.05f;
        float castDistance = Mathf.Max(0.01f, desired - 0.05f);
        int hits = Physics.SphereCastNonAlloc(
            start,
            collisionRadius,
            offset,
            collisionHits,
            castDistance,
            CollisionMask(),
            QueryTriggerInteraction.Ignore);

        float allowed = desired;
        for (int i = 0; i < hits; i++)
        {
            Collider collider = collisionHits[i].collider;
            if (collider == null)
                continue;

            // The cast starts on the bullseye surface, so ignore the immediate self hit.
            // Keep later hits so another limb or a wall can pull the camera inward.
            if (collider.transform.IsChildOf(transform) && collisionHits[i].distance < 0.03f)
                continue;

            float candidate = collisionHits[i].distance + 0.05f - collisionPadding;
            if (candidate < allowed)
                allowed = candidate;
        }

        return Mathf.Clamp(allowed, minimumDistance, desired);
    }

    private Quaternion ResolveDesiredRotation(BullseyeCameraTarget target, Vector3 cameraPosition)
    {
        Vector3 focus = target.Position;
        if (target.Mode == BullseyeCameraTrackingMode.Attached)
        {
            Vector3 body = transform.position + Vector3.up * 1.05f;
            focus = Vector3.Lerp(target.Position, body, bodyContext);
            Transform weapon = WeaponRoot();
            if (weapon != null)
                focus = Vector3.Lerp(focus, weapon.position, 0.18f);
        }

        Vector3 forward = focus - cameraPosition;
        if (forward.sqrMagnitude < 0.0001f)
            forward = -filteredNormal;

        Vector3 up = transform.up;
        if (Mathf.Abs(Vector3.Dot(forward.normalized, up)) > 0.92f)
            up = transform.forward;

        return Quaternion.LookRotation(forward, up);
    }

    private void ApplyPose(Vector3 position, Quaternion rotation)
    {
        Transform cameraTransform = trackingCamera.transform;
        if (!hasPose)
        {
            cameraTransform.SetPositionAndRotation(position, rotation);
            positionVelocity = Vector3.zero;
            hasPose = true;
            return;
        }

        float smooth = Mathf.Max(0.01f, positionSmoothTime);
        float maxSpeed = Mathf.Max(0.1f, maximumCameraSpeed);
        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position,
            position,
            ref positionVelocity,
            smooth,
            maxSpeed);

        float turn = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
        cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, rotation, turn);
    }

    private void UpdateRenderCadence()
    {
        if (trackingCamera == null)
            return;

        if (targetFramesPerSecond <= 0f)
        {
            trackingCamera.enabled = true;
            return;
        }

        if (Time.unscaledTime + 0.0001f >= nextRenderTime)
        {
            trackingCamera.enabled = true;
            nextRenderTime = Time.unscaledTime + (1f / targetFramesPerSecond);
        }
        else
        {
            trackingCamera.enabled = false;
        }
    }

    private void SetPresented(bool visible)
    {
        if (hud != null)
            hud.SetVisible(visible);
        if (!visible && trackingCamera != null)
            trackingCamera.enabled = false;
    }

    private void EnsureCamera()
    {
        if (trackingCamera != null)
            return;

        GameObject cameraObject = new GameObject("BullseyeTrackingCamera");
        cameraObject.transform.SetParent(transform, false);
        trackingCamera = cameraObject.AddComponent<Camera>();
        trackingCameraData = cameraObject.AddComponent<HDAdditionalCameraData>();

        int width = Mathf.Max(64, renderWidth);
        int height = Mathf.Max(64, renderHeight);
        feed = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "BullseyeTrackingFeed",
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1
        };
        feed.Create();

        trackingCamera.targetTexture = feed;
        trackingCamera.fieldOfView = fieldOfView;
        trackingCamera.nearClipPlane = 0.05f;
        trackingCamera.farClipPlane = attachedFarClip;
        trackingCamera.depth = -20f;
        trackingCamera.clearFlags = CameraClearFlags.SolidColor;
        trackingCamera.backgroundColor = backgroundColor;
        trackingCamera.cullingMask = BuildCullingMask();
        trackingCamera.allowMSAA = false;
        trackingCamera.useOcclusionCulling = true;
        trackingCamera.enabled = false;

        ConfigureHdrpCamera();
        CreateMonitorVolume(cameraObject);
        CreateFillLight(cameraObject.transform);
        hud.SetFeed(feed);
    }

    private void ConfigureHdrpCamera()
    {
        trackingCameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
        trackingCameraData.backgroundColorHDR = backgroundColor;
        trackingCameraData.clearDepth = true;
        trackingCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
        trackingCameraData.dithering = false;
        trackingCameraData.allowDynamicResolution = false;
        trackingCameraData.customRenderingSettings = true;

        ref FrameSettings settings = ref trackingCameraData.renderingPathCustomFrameSettings;
        FrameSettingsOverrideMask overrides = trackingCameraData.renderingPathCustomFrameSettingsOverrideMask;
        DisableFeature(ref settings, ref overrides, FrameSettingsField.Volumetrics);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.VolumetricClouds);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.SSR);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.ContactShadows);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.ShadowMaps);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.MotionVectors);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.MotionBlur);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.DepthOfField);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.Bloom);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.Distortion);
        DisableFeature(ref settings, ref overrides, FrameSettingsField.AtmosphericScattering);
        trackingCameraData.renderingPathCustomFrameSettingsOverrideMask = overrides;

        int layer = LayerMask.NameToLayer(MonitorLayerName);
        if (layer >= 0)
            trackingCameraData.volumeLayerMask = 1 << layer;
    }

    private static void DisableFeature(ref FrameSettings settings, ref FrameSettingsOverrideMask overrides, FrameSettingsField field)
    {
        settings.SetEnabled(field, false);
        overrides.mask[(uint)field] = true;
    }

    private void CreateMonitorVolume(GameObject cameraObject)
    {
        GameObject volumeObject = new GameObject("BullseyeMonitorVolume");
        volumeObject.transform.SetParent(cameraObject.transform, false);
        int layer = LayerMask.NameToLayer(MonitorLayerName);
        if (layer >= 0)
            volumeObject.layer = layer;

        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 2000f;
        volume.weight = 1f;

        volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volumeProfile.hideFlags = HideFlags.HideAndDontSave;

        Exposure exposure = volumeProfile.Add<Exposure>();
        exposure.mode.Override(ExposureMode.Fixed);
        exposure.fixedExposure.Override(fixedExposure);

        ColorAdjustments grading = volumeProfile.Add<ColorAdjustments>();
        grading.contrast.Override(contrast);
        grading.saturation.Override(saturation);

        FilmGrain grain = volumeProfile.Add<FilmGrain>();
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(grainIntensity);
        grain.response.Override(0.5f);

        volume.sharedProfile = volumeProfile;
    }

    private void CreateFillLight(Transform cameraTransform)
    {
        GameObject lightObject = new GameObject("BullseyeFillLight");
        lightObject.transform.SetParent(cameraTransform, false);
        fillLight = lightObject.AddComponent<Light>();
        fillLight.type = LightType.Spot;
        fillLight.spotAngle = 58f;
        fillLight.range = 2.2f;
        fillLight.color = new Color(0.82f, 0.86f, 0.92f);
        fillLight.intensity = fillLightIntensity;
        fillLight.shadows = LightShadows.None;
        fillLight.cullingMask = FillLightMask();
        fillLight.enabled = false;

        HDAdditionalLightData additional = lightObject.GetComponent<HDAdditionalLightData>();
        if (additional == null)
            additional = lightObject.AddComponent<HDAdditionalLightData>();
        additional.EnableShadows(false);
    }

    private int BuildCullingMask()
    {
        int mask = 1 << 0;
        AddLayer(ref mask, LocalPlayerBodyLayerName);
        AddLayer(ref mask, "WorldWeapon");
        AddLayer(ref mask, "BullseyeDebris");
        return mask;
    }

    private static int FillLightMask()
    {
        int mask = 0;
        AddLayer(ref mask, LocalPlayerBodyLayerName);
        AddLayer(ref mask, "WorldWeapon");
        return mask;
    }

    private static void AddLayer(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            mask |= 1 << layer;
    }

    private static int CollisionMask()
    {
        int mask = Physics.DefaultRaycastLayers;
        int weapon = LayerMask.NameToLayer(FirstPersonWeaponLayerName);
        if (weapon >= 0)
            mask &= ~(1 << weapon);
        return mask;
    }

    private void RegisterHooks()
    {
        if (!Active.Contains(this))
            Active.Add(this);

        if (hooksRegistered)
            return;

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        hooksRegistered = true;
    }

    private void UnregisterHooks()
    {
        Active.Remove(this);
        if (Active.Count > 0 || !hooksRegistered)
            return;

        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        hooksRegistered = false;
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            BullseyeTrackingCamera tracker = Active[i];
            if (tracker != null && tracker.trackingCamera == camera)
                tracker.PrepareOwnerBodyForMonitor();
        }
    }

    private static void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        for (int i = 0; i < Active.Count; i++)
        {
            BullseyeTrackingCamera tracker = Active[i];
            if (tracker != null && tracker.trackingCamera == camera)
                tracker.RestoreOwnerBodyAfterMonitor();
        }
    }

    private void PrepareOwnerBodyForMonitor()
    {
        if (trackingCamera != null)
        {
            trackingCamera.fieldOfView = fieldOfView;
            trackingCamera.farClipPlane = lastTarget.Mode == BullseyeCameraTrackingMode.Detached
                ? detachedFarClip
                : attachedFarClip;
        }

        if (fillLight != null)
            fillLight.enabled = true;

        revealed.Clear();
        hiddenFirstPerson.Clear();
        RevealWorldBody();
        RevealWorldWeapon();
        HideFirstPersonCopies();
    }

    private void RestoreOwnerBodyAfterMonitor()
    {
        for (int i = 0; i < revealed.Count; i++)
        {
            Renderer renderer = revealed[i].Renderer;
            if (renderer == null)
                continue;
            renderer.shadowCastingMode = revealed[i].ShadowMode;
        }

        for (int i = 0; i < hiddenFirstPerson.Count; i++)
        {
            Renderer renderer = hiddenFirstPerson[i].Renderer;
            if (renderer == null)
                continue;
            renderer.enabled = hiddenFirstPerson[i].Enabled;
        }

        revealed.Clear();
        hiddenFirstPerson.Clear();
        if (fillLight != null)
            fillLight.enabled = false;
    }

    private void RevealWorldBody()
    {
        Transform visualRoot = transform.Find("VisualRoot");
        if (visualRoot != null)
            RevealShadowsOnly(visualRoot);
    }

    private void RevealWorldWeapon()
    {
        Transform weaponRoot = WeaponRoot();
        if (weaponRoot != null)
            RevealShadowsOnly(weaponRoot);
    }

    private Transform WeaponRoot()
    {
        WorldWeaponView weaponView = GetComponent<WorldWeaponView>();
        if (weaponView != null && weaponView.WorldWeaponRoot != null)
            return weaponView.WorldWeaponRoot;

        return transform.Find("WeaponHandAnchor");
    }

    private void RevealShadowsOnly(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.forceRenderingOff || renderer is ParticleSystemRenderer)
                continue;
            if (renderer.shadowCastingMode != ShadowCastingMode.ShadowsOnly)
                continue;

            revealed.Add(new RendererToggle
            {
                Renderer = renderer,
                ShadowMode = renderer.shadowCastingMode,
                Enabled = renderer.enabled
            });
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }
    }

    private void HideFirstPersonCopies()
    {
        Transform firstPersonBody = transform.Find("FirstPersonBody");
        if (firstPersonBody == null)
            return;

        Renderer[] renderers = firstPersonBody.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            hiddenFirstPerson.Add(new RendererToggle
            {
                Renderer = renderer,
                Enabled = true
            });
            renderer.enabled = false;
        }
    }

    private void DrawDebug(BullseyeCameraTarget target, Vector3 desiredPosition)
    {
        if (!debugVisualization)
            return;

        Debug.DrawRay(target.Position, filteredNormal * trackingDistance, Color.red);
        Debug.DrawLine(target.Position, desiredPosition, Color.cyan);
        if (trackingCamera != null)
            Debug.DrawRay(trackingCamera.transform.position, trackingCamera.transform.forward * 0.4f, Color.yellow);
    }

    private void OnGUI()
    {
        if (!debugVisualization || !ownerActive || !hasPose)
            return;

        string text =
            "Bullseye camera\n" +
            "Mode " + lastTarget.Mode + "\n" +
            "Target " + lastTarget.Position.ToString("F2") + "\n" +
            "Normal " + filteredNormal.ToString("F2") + "\n" +
            "Desired " + (lastTarget.Position + filteredNormal * trackingDistance).ToString("F2") + "\n" +
            "Camera " + (trackingCamera != null ? trackingCamera.transform.position.ToString("F2") : "-");

        GUI.Label(new Rect(16f, 16f, 420f, 120f), text);
    }

    private void OnValidate()
    {
        trackingDistance = Mathf.Max(0.2f, trackingDistance);
        minimumDistance = Mathf.Clamp(minimumDistance, 0.08f, trackingDistance);
        fieldOfView = Mathf.Clamp(fieldOfView, 20f, 80f);
        positionSmoothTime = Mathf.Max(0.01f, positionSmoothTime);
        rotationSmoothSpeed = Mathf.Max(0.1f, rotationSmoothSpeed);
        normalSmoothSpeed = Mathf.Max(0.1f, normalSmoothSpeed);
        maximumCameraSpeed = Mathf.Max(0.2f, maximumCameraSpeed);
        renderWidth = Mathf.Clamp(renderWidth, 64, 512);
        renderHeight = Mathf.Clamp(renderHeight, 64, 512);
        targetFramesPerSecond = Mathf.Max(0f, targetFramesPerSecond);
        collisionRadius = Mathf.Clamp(collisionRadius, 0.02f, 0.2f);
    }
}
