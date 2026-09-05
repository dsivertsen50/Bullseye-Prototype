using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// REQ-049 setup and preview tool. Developers edit weapon markers and
/// elbow hints, not animation timelines.
/// </summary>
public class ThirdPersonWeaponHoldSetupWindow : EditorWindow
{
    private const string PreviewName = "TPHoldPreview";
    private const string PlayerPrefabPath = "Assets/Player/Player.prefab";

    private static ThirdPersonWeaponHoldSetupWindow instance;

    private WeaponDefinition definition;
    private Vector2 scroll;
    private string status = "Select a weapon, then Auto Setup or Preview.";
    private string validationText = string.Empty;
    private int locomotion;
    private bool aiming;
    private float aimPitch;
    private bool autoFrame = true;
    private bool overwriteCustom;

    private GameObject previewRoot;
    private ThirdPersonWeaponRig previewRig;
    private WorldWeaponView previewWorld;
    private Animator previewAnimator;
    private WeaponDefinition spawnedDefinition;

    private static readonly string[] LocomotionLabels =
    {
        "Standing Idle",
        "Walk Forward",
        "Walk Backward",
        "Strafe Left",
        "Strafe Right",
        "Run",
        "Sprint",
        "Crouch",
        "Prone",
        "Aim"
    };

    public static ThirdPersonWeaponRig ActivePreviewRig
    {
        get
        {
            if (instance != null && instance.previewRig != null)
                return instance.previewRig;
            return null;
        }
    }

    public static WeaponDefinition ActivePreviewDefinition =>
        instance != null ? instance.definition : null;

    public static bool IsPreviewRig(ThirdPersonWeaponRig rig)
    {
        return instance != null && instance.previewRig == rig;
    }

    [MenuItem("Bullseye/Third-Person Weapon Hold Setup")]
    public static void Open()
    {
        Open(null);
    }

    public static void Open(WeaponDefinition weapon)
    {
        ThirdPersonWeaponHoldSetupWindow window =
            GetWindow<ThirdPersonWeaponHoldSetupWindow>("TP Weapon Hold");
        instance = window;
        if (weapon != null)
            window.definition = weapon;
        window.minSize = new Vector2(420f, 640f);
        window.Show();
        window.Focus();
    }

    public static bool OwnsDefinition(WeaponDefinition weapon)
    {
        return instance != null && instance.definition == weapon && instance.previewRig != null;
    }

    public static void NotifyDefinitionChanged(WeaponDefinition weapon)
    {
        if (instance != null && instance.definition == weapon)
            instance.RefreshPreview();
    }

    public static void TickPreview()
    {
        if (instance != null)
            instance.SamplePreview();
    }

    public static void ApplyActivePreview()
    {
        if (instance != null)
            instance.ApplyPreviewPose();
    }

    private void OnEnable()
    {
        instance = this;
        EditorApplication.update += OnEditorUpdate;
        if (definition == null)
            definition = LoadDefaultDefinition();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        if (instance == this)
            instance = null;
        DestroyPreview();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Third-Person Weapon Hold Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Click Preview Weapon, then work in Scene view (not Play Mode).\n" +
            "• Yellow handle — move/rotate the gun. Hands follow.\n" +
            "• Green / blue handles — place the right and left hands on the gun.\n" +
            "• Orange / pink handles — bend the elbows.\n" +
            "Moving Unity's regular bone gizmos does nothing. Use these handles.\n" +
            "Edits save when you release the handle. Press Ctrl+S after a session.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        definition = (WeaponDefinition)EditorGUILayout.ObjectField(
            "Weapon Definition",
            definition,
            typeof(WeaponDefinition),
            false);
        if (EditorGUI.EndChangeCheck())
            RefreshPreview();

        DrawWeaponSummary();
        DrawActions();
        DrawPreviewControls();
        DrawValidation();
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void DrawWeaponSummary()
    {
        if (definition == null)
        {
            EditorGUILayout.HelpBox("Select a WeaponDefinition.", MessageType.Warning);
            return;
        }

        ThirdPersonWeaponMarkerReport report = ThirdPersonWeaponHoldSetup.InspectDefinition(definition);
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Weapon", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Weapon Name", definition.DisplayName);
            EditorGUILayout.ObjectField("Weapon Prefab", definition.WorldPrefab, typeof(GameObject), false);
            EditorGUILayout.EnumPopup("Weapon Hold Class", definition.ThirdPersonHoldClass);
            EditorGUILayout.Toggle("Use Left Hand Grip", definition.UseLeftHandGrip);
            EditorGUILayout.Vector3Field("Anchor Position Offset", definition.ThirdPersonAnchorPositionOffset);
            EditorGUILayout.Vector3Field("Anchor Rotation Offset", definition.ThirdPersonAnchorRotationOffset);
            EditorGUILayout.TextField("Current Rig Profile", report.holdProfileName);
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Markers", EditorStyles.boldLabel);
        DrawMarkerRow("Grip_R", report.hasGripR, ThirdPersonWeaponMarkers.GripR);
        DrawMarkerRow("Grip_L", report.hasGripL || !report.usesLeftHand, ThirdPersonWeaponMarkers.GripL);
        DrawMarkerRow("Aim", report.hasAim, ThirdPersonWeaponMarkers.Aim);
        DrawMarkerRow("Muzzle", report.hasMuzzle, ThirdPersonWeaponMarkers.Muzzle);
        EditorGUILayout.LabelField("Validation Status", report.IsValid ? "Ready" : "Needs setup");
        if (!string.IsNullOrEmpty(report.issues))
            EditorGUILayout.HelpBox(report.issues, MessageType.Warning);
    }

    private void DrawMarkerRow(string label, bool present, string markerName)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField((present ? "✓ " : "✗ ") + label);
            if (!present && GUILayout.Button("Create Missing " + label, GUILayout.Width(180f)))
            {
                ThirdPersonWeaponHoldSetup.CreateMissingMarker(definition, markerName);
                status = "Created " + label + " on " + definition.DisplayName + ". Position it in Scene view.";
                RefreshPreview();
            }
        }
    }

    private void DrawActions()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
        overwriteCustom = EditorGUILayout.Toggle(
            new GUIContent("Overwrite Custom Markers", "If enabled, Auto Setup may replace existing marker transforms."),
            overwriteCustom);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Setup Weapon"))
            {
                if (definition == null)
                    status = "Select a WeaponDefinition first.";
                else
                {
                    if (overwriteCustom && !EditorUtility.DisplayDialog(
                            "Overwrite custom configuration?",
                            "This can replace existing grip or aim markers on " + definition.DisplayName + ".",
                            "Overwrite",
                            "Cancel"))
                        return;

                    status = ThirdPersonWeaponHoldSetup.AutoSetupWeapon(definition, overwriteCustom);
                    RefreshPreview();
                }
            }

            if (GUILayout.Button("Preview Weapon"))
            {
                EnsurePreview();
                if (autoFrame)
                    FramePreview();
                status = "Previewing " + (definition != null ? definition.DisplayName : "weapon") +
                         " on the real third-person player model.";
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Validate All Weapons"))
                validationText = ThirdPersonWeaponHoldSetup.ValidateAllWeapons();
            if (GUILayout.Button("Auto Configure All Valid Weapons"))
            {
                status = ThirdPersonWeaponHoldSetup.AutoConfigureAllValidWeapons();
                validationText = ThirdPersonWeaponHoldSetup.ValidateAllWeapons();
                RefreshPreview();
            }
        }

        if (GUILayout.Button("Open Preview Scene"))
            OpenPreviewScene();
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Preview Locomotion", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        locomotion = EditorGUILayout.Popup("Base Animation", locomotion, LocomotionLabels);
        aiming = locomotion == 9 || EditorGUILayout.Toggle("Aim Hold", aiming || locomotion == 9);
        aimPitch = EditorGUILayout.Slider("Aim Pitch", aimPitch, -40f, 40f);
        autoFrame = EditorGUILayout.Toggle("Auto Frame", autoFrame);
        if (EditorGUI.EndChangeCheck() && previewRoot != null)
            RefreshPreview();

        EditorGUILayout.HelpBox(
            "Changing the dropdown only switches the base locomotion clip. " +
            "The procedural arm rig stays on the weapon.",
            MessageType.None);
    }

    private void DrawValidation()
    {
        if (string.IsNullOrEmpty(validationText))
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Validation Report", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(validationText, GUILayout.MinHeight(160f));
    }

    private void OpenPreviewScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ThirdPersonWeaponHoldSetup.PreviewScenePath, OpenSceneMode.Single);
        EnsurePreview();
        if (autoFrame)
            FramePreview();
        status = "Opened preview scene. No multiplayer client is required.";
    }

    private void EnsurePreview()
    {
        if (Application.isPlaying)
        {
            DestroyPreview();
            return;
        }

        if (previewRoot == null)
            previewRoot = GameObject.Find(PreviewName);

        if (previewRoot == null)
            RebuildPreview();
        else
            CachePreview();

        if (definition != spawnedDefinition)
            RefreshPreview();
    }

    private void RebuildPreview()
    {
        DestroyPreview();
        if (Application.isPlaying)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
            return;

        previewRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        previewRoot.name = PreviewName;
        previewRoot.hideFlags = HideFlags.DontSave;
        previewRoot.transform.position = new Vector3(2f, 0f, 0f);
        previewRoot.transform.rotation = Quaternion.identity;
        StripPreviewForEditMode(previewRoot);
        CachePreview();
        spawnedDefinition = null;
        RefreshPreview();
        if (autoFrame)
            FramePreview();
    }

    private void CachePreview()
    {
        if (previewRoot == null)
            return;

        previewRig = previewRoot.GetComponent<ThirdPersonWeaponRig>();
        previewWorld = previewRoot.GetComponent<WorldWeaponView>();
        previewAnimator = ResolveCharacterAnimator(previewRoot);
        if (previewRig != null && !previewRig.IsEditorPreview)
            previewRig.BeginEditorPreview();
    }

    private void RefreshPreview()
    {
        if (previewRoot == null || previewWorld == null || previewRig == null)
            return;
        if (definition == null)
            definition = LoadDefaultDefinition();
        if (definition == null)
            return;

        if (spawnedDefinition != definition)
        {
            previewWorld.PrepareEditorPreview(definition);
            spawnedDefinition = definition;
        }

        SamplePreview();
        SceneView.RepaintAll();
    }

    private void SamplePreview()
    {
        if (previewAnimator == null)
            return;

        previewAnimator.enabled = true;
        previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        previewAnimator.applyRootMotion = false;
        previewAnimator.speed = 1f;
        if (previewAnimator.runtimeAnimatorController == null)
            return;

        string state = locomotion switch
        {
            1 => "Standing Locomotion",
            2 => "Standing Locomotion",
            3 => "Standing Locomotion",
            4 => "Standing Locomotion",
            5 => "Standing Locomotion",
            6 => "Sprint Locomotion",
            7 => "Crouching Idle",
            8 => "Prone Idle",
            _ => "Standing Idle"
        };
        int hash = Animator.StringToHash(state);
        if (previewAnimator.HasState(0, hash))
            previewAnimator.Play(hash, 0, 0.18f);
        else
            previewAnimator.Play(state, 0, 0.18f);

        float moveX = locomotion switch
        {
            3 => -1f,
            4 => 1f,
            _ => 0f
        };
        float moveY = locomotion switch
        {
            1 or 5 => 1f,
            2 => -1f,
            _ => 0f
        };
        previewAnimator.SetFloat("MoveX", moveX);
        previewAnimator.SetFloat("MoveY", moveY);
        previewAnimator.SetFloat("MoveSpeed", locomotion is >= 1 and <= 6 ? 1f : 0f);
        previewAnimator.SetBool("IsMoving", locomotion is >= 1 and <= 6);
        previewAnimator.SetBool("IsSprinting", locomotion == 6);
        previewAnimator.SetBool("IsCrouching", locomotion == 7);
        previewAnimator.SetBool("IsProne", locomotion == 8);
        previewAnimator.SetBool("IsAiming", aiming || locomotion == 9);

        int poseLayer = previewAnimator.GetLayerIndex(ThirdPersonWeaponPoseBinder.LayerName);
        if (poseLayer < 0)
            poseLayer = previewAnimator.GetLayerIndex(ThirdPersonWeaponPoseBinder.LegacyLayerName);
        if (poseLayer >= 0)
            previewAnimator.SetLayerWeight(poseLayer, 0f);

        previewAnimator.Update(0.016f);
        ApplyPreviewPose();
    }

    private void ApplyPreviewPose()
    {
        if (previewRig == null)
            return;

        float aim = aiming || locomotion == 9 ? 1f : 0f;
        float sprint = locomotion == 6 ? 1f : 0f;
        float crouch = locomotion == 7 ? 1f : 0f;
        float prone = locomotion == 8 ? 1f : 0f;
        previewRig.ApplyEditorPreview(aim, sprint, prone, aimPitch, 1f, crouch);
    }

    private void FramePreview()
    {
        if (previewRoot == null)
            return;

        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
            return;

        Bounds bounds = new Bounds(previewRoot.transform.position + Vector3.up, Vector3.one * 2.2f);
        Renderer[] renderers = previewRoot.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled)
                continue;
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        sceneView.Frame(bounds, false);
    }

    private static void StripPreviewForEditMode(GameObject root)
    {
        HideNamedChild(root.transform, "CameraRoot");
        HideNamedChild(root.transform, "WorldHealthUI");
        HideNamedChild(root.transform, "Capsule");

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].enabled = false;
        AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
            listeners[i].enabled = false;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;
            if (behaviour is ThirdPersonWeaponRig ||
                behaviour is WorldWeaponView ||
                behaviour is PlayerVisualRig ||
                behaviour is ThirdPersonWeaponVisual)
                continue;
            behaviour.enabled = false;
        }

        Animator animator = ResolveCharacterAnimator(root);
        if (animator != null)
        {
            animator.enabled = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
        }
    }

    private static Animator ResolveCharacterAnimator(GameObject root)
    {
        if (root == null)
            return null;

        Transform visual = root.transform.Find("VisualRoot");
        if (visual != null)
        {
            Animator visualAnimator = visual.GetComponentInChildren<Animator>(true);
            if (visualAnimator != null)
                return visualAnimator;
        }

        return root.GetComponentInChildren<Animator>(true);
    }

    private static void HideNamedChild(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        if (child != null)
            child.gameObject.SetActive(false);
    }

    private void DestroyPreview()
    {
        if (previewRig != null)
            previewRig.EndEditorPreview();
        if (previewRoot != null)
            DestroyImmediate(previewRoot);

        previewRoot = null;
        previewRig = null;
        previewWorld = null;
        previewAnimator = null;
        spawnedDefinition = null;

        GameObject leftover = GameObject.Find(PreviewName);
        if (leftover != null)
            DestroyImmediate(leftover);
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying || previewRoot == null)
            return;
        if (GUIUtility.hotControl != 0)
            return;

        SamplePreview();
    }

    private static WeaponDefinition LoadDefaultDefinition()
    {
        WeaponDefinition ak = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Scripts/Weapons/AKDefinition.asset");
        if (ak != null)
            return ak;

        WeaponDefinition[] all = ThirdPersonWeaponHoldSetup.LoadAllDefinitions();
        return all.Length > 0 ? all[0] : null;
    }
}
