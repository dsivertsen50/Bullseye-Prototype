using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepares and previews third-person weapon poses. Visual posing is done
/// in Very Animation on the generated .anim clips.
/// </summary>
public class ThirdPersonWeaponPoseAuthoringWindow : EditorWindow
{
    private const string PreviewName = "__BullseyeTpWeaponAuthoringPreview";
    private const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    private const string CatalogPath = "Assets/Scripts/Weapons/WeaponCatalog.asset";
    private static readonly string[] LocomotionLabels = { "Idle", "Walk", "Sprint", "Crouch", "Prone" };

    private static ThirdPersonWeaponPoseAuthoringWindow instance;

    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private int locomotion;
    [SerializeField] private bool aiming;
    [SerializeField] private float aimPitch;
    [SerializeField] private bool autoFrame = true;

    private Vector2 scroll;
    private GameObject previewRoot;
    private ThirdPersonWeaponRig previewRig;
    private WorldWeaponView previewWorld;
    private Animator previewAnimator;
    private ThirdPersonWeaponPoseBinder previewBinder;
    private WeaponDefinition spawnedDefinition;
    private string status;

    public static ThirdPersonWeaponRig ActivePreviewRig
    {
        get
        {
            if (instance == null)
                return null;

            ThirdPersonWeaponRig authoring = FindAuthoringRig();
            return authoring != null ? authoring : instance.previewRig;
        }
    }

    public static WeaponDefinition ActivePreviewDefinition =>
        instance != null ? instance.definition : null;

    public static bool IsPreviewRig(ThirdPersonWeaponRig rig)
    {
        if (rig == null || instance == null)
            return false;
        if (instance.previewRig == rig)
            return true;
        return FindAuthoringRig() == rig;
    }

    private static ThirdPersonWeaponRig FindAuthoringRig()
    {
        GameObject character = GameObject.Find(ThirdPersonWeaponPoseAuthoringSetup.AuthoringCharacterName);
        return character != null ? character.GetComponent<ThirdPersonWeaponRig>() : null;
    }

    public static bool OwnsDefinition(WeaponDefinition weapon)
    {
        return instance != null && weapon != null && instance.definition == weapon;
    }

    [MenuItem("Bullseye/Third-Person Weapon Pose Authoring (Deprecated)")]
    public static void Open()
    {
        ThirdPersonWeaponHoldSetupWindow.Open();
    }

    public static void Open(WeaponDefinition weapon)
    {
        ThirdPersonWeaponHoldSetupWindow.Open(weapon);
    }

    public static void TickPreview()
    {
        if (instance == null || Application.isPlaying)
            return;

        if (FindAuthoringRig() != null)
            return;

        instance.EnsurePreview();
        instance.SamplePreview();
    }

    public static void ApplyActivePreview()
    {
        if (instance == null || Application.isPlaying)
            return;

        ThirdPersonWeaponRig authoring = FindAuthoringRig();
        if (authoring != null)
        {
            authoring.ApplyEditorPreview(0f, 0f, 0f, 0f, 1f, 0f);
            return;
        }

        instance.ApplyPreviewPose();
    }

    public static void NotifyDefinitionChanged(WeaponDefinition weapon)
    {
        if (instance == null || weapon == null || instance.definition != weapon)
            return;

        instance.RefreshPreview();
    }

    private void OnEnable()
    {
        instance = this;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload += DestroyPreview;
        if (definition == null)
            definition = LoadDefaultDefinition();
        EnsurePreview();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= DestroyPreview;
        DestroyPreview();
        if (instance == this)
            instance = null;
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(
            "Locomotion stays on the base Animator. This tool prepares writable pose clips " +
            "and previews them on the third-person character. Open a clip in Very Animation " +
            "to pose arms visually. Do not edit Very Animation internals from here.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        definition = (WeaponDefinition)EditorGUILayout.ObjectField(
            "Weapon Definition",
            definition,
            typeof(WeaponDefinition),
            false);
        DrawCatalogPopup();
        bool changed = EditorGUI.EndChangeCheck();

        DrawWeaponSummary();
        DrawSetupButtons();
        DrawPoseButtons();
        DrawPreviewControls();
        DrawValidation();

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.Info);

        EditorGUILayout.EndScrollView();
        if (changed)
            RefreshPreview();
    }

    private void DrawCatalogPopup()
    {
        WeaponCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
        if (catalog == null || catalog.Count == 0)
            return;

        var names = new List<string>();
        var weapons = new List<WeaponDefinition>();
        for (int i = 0; i < catalog.Count; i++)
        {
            WeaponDefinition weapon = catalog.Get(i);
            if (weapon == null)
                continue;
            weapons.Add(weapon);
            names.Add(weapon.DisplayName);
        }

        if (weapons.Count == 0)
            return;

        int current = Mathf.Max(0, weapons.IndexOf(definition));
        int next = EditorGUILayout.Popup("Catalog", current, names.ToArray());
        if (next >= 0 && next < weapons.Count)
            definition = weapons[next];
    }

    private void DrawWeaponSummary()
    {
        if (definition == null)
        {
            EditorGUILayout.HelpBox("Select a WeaponDefinition.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Selected Weapon", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Name", definition.DisplayName);
        EditorGUILayout.ObjectField("Prefab", definition.WorldPrefab, typeof(GameObject), false);
        EditorGUILayout.LabelField("Pose Class", definition.WeaponPoseClass.ToString());
        EditorGUILayout.ObjectField("Pose Profile", definition.PoseProfile, typeof(ThirdPersonWeaponPoseProfile), false);
        DrawResolvedClip("Hold", ThirdPersonWeaponPoseKind.Hold);
        DrawResolvedClip("Sprint", ThirdPersonWeaponPoseKind.Sprint);
        DrawResolvedClip("Prone", ThirdPersonWeaponPoseKind.Prone);
        DrawResolvedClip("Aim", ThirdPersonWeaponPoseKind.Aim);
        EditorGUILayout.LabelField("Support-Hand IK", definition.UsesSupportHandIk ? "Enabled" : "Disabled");
        DrawSocketOffsetFields();
    }

    private void DrawSocketOffsetFields()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Gun In Right Hand", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This moves the weapon on RightHandWeaponSocket. It does not rotate arm bones. " +
            "Keep this authoring window open and drag the yellow handle in the Scene view, " +
            "or edit the offsets here. Cyan = LeftHandGrip. Magenta = LeftElbowHint.",
            MessageType.Info);

        SerializedObject so = new SerializedObject(definition);
        so.Update();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            so.FindProperty("worldLocalPosition"),
            new GUIContent("Position Offset"));
        EditorGUILayout.PropertyField(
            so.FindProperty("worldLocalEuler"),
            new GUIContent("Rotation Offset"));
        if (EditorGUI.EndChangeCheck() && so.ApplyModifiedProperties())
        {
            WeaponDefinitionEditor.SaveDefinition(definition, refreshPreview: false);
            ThirdPersonWeaponRig authoring = FindAuthoringRig();
            if (authoring != null)
                authoring.ApplyEditorPreview(0f, 0f, 0f, 0f, 1f, 0f);
        }
    }

    private void DrawResolvedClip(string label, ThirdPersonWeaponPoseKind kind)
    {
        AnimationClip clip = ThirdPersonWeaponPoseResolver.ResolveClip(definition, kind);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.ObjectField(label, clip, typeof(AnimationClip), false);
            using (new EditorGUI.DisabledScope(clip == null))
            {
                if (GUILayout.Button("Open", GUILayout.Width(52f)))
                {
                    Selection.activeObject = clip;
                    EditorGUIUtility.PingObject(clip);
                }

                if (GUILayout.Button("Edit", GUILayout.Width(52f)))
                    PreparePoseForVeryAnimation(clip);
            }
        }
    }

    private void DrawSetupButtons()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("Create / Repair Pose Setup"))
        {
            status = ThirdPersonWeaponPoseAuthoringSetup.CreateOrRepairWeapon(definition);
            RefreshPreview();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Preview Scene"))
                OpenPreviewScene();
            if (GUILayout.Button("Load Weapon Into Preview"))
            {
                EnsurePreview();
                RefreshPreview();
                status = definition != null
                    ? "Preview loaded " + definition.DisplayName + "."
                    : "Select a weapon first.";
            }
        }
    }

    private void DrawPoseButtons()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Writable Pose Clips", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "These create ordinary Unity .anim assets. Open them in Very Animation to pose the character.",
            MessageType.None);

        DrawCreatePoseButton("Create Hold Pose", ThirdPersonWeaponPoseKind.Hold);
        DrawCreatePoseButton("Create Sprint Pose", ThirdPersonWeaponPoseKind.Sprint);
        DrawCreatePoseButton("Create Prone Pose", ThirdPersonWeaponPoseKind.Prone);
        DrawCreatePoseButton("Create Aim Pose", ThirdPersonWeaponPoseKind.Aim);

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Prepare Selected Pose For Very Animation", GUILayout.Height(28f)))
                PrepareSelectedPoseForVeryAnimation();
            if (GUILayout.Button("Flatten Current Pose Clip", GUILayout.Height(28f)))
                FlattenSelectedPoseClip();
        }
        EditorGUILayout.HelpBox(
            "Very Animation does not edit a clip from the Project window. " +
            "This selects the third-person character, assigns a simple authoring controller " +
            "with the pose clip, and opens Unity's Animation window. Then open " +
            "Window → Very Animation → Main and click Edit Animation. Pose the arms in the Scene view.",
            MessageType.Info);
    }

    private void DrawCreatePoseButton(string label, ThirdPersonWeaponPoseKind kind)
    {
        if (!GUILayout.Button(label) || definition == null)
            return;

        string folder = ThirdPersonWeaponPoseAuthoringSetup.RootFolder + "/" + definition.DisplayName;
        string path = kind == ThirdPersonWeaponPoseKind.Hold && definition.WeaponId == "ak"
            ? ThirdPersonWeaponPoseAuthoringSetup.AkHoldPath
            : folder + "/" + definition.DisplayName + "_" + kind + ".anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool overwrite = false;
        if (existing != null &&
            !EditorUtility.DisplayDialog(
                "Pose Already Exists",
                existing.name + " already exists. Overwrite it?",
                "Overwrite",
                "Keep Existing"))
        {
            Selection.activeObject = existing;
            return;
        }

        overwrite = existing != null;
        AnimationClip clip = ThirdPersonWeaponPoseAuthoringSetup.CreateWeaponPoseClip(definition, kind, overwrite);
        if (clip != null)
        {
            Selection.activeObject = clip;
            EditorGUIUtility.PingObject(clip);
            status = "Created " + clip.name + ". Open it in Very Animation to pose arms visually.";
        }
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Preview Locomotion + Pose", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        locomotion = GUILayout.Toolbar(locomotion, LocomotionLabels);
        aiming = EditorGUILayout.Toggle("Aim / ADS Pose", aiming);
        aimPitch = EditorGUILayout.Slider("Look Pitch", aimPitch, -50f, 50f);
        autoFrame = EditorGUILayout.Toggle("Frame On Change", autoFrame);
        if (EditorGUI.EndChangeCheck())
            RefreshPreview();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Preview"))
                RebuildPreview();
            if (GUILayout.Button("Frame Preview"))
                FramePreview();
        }

        if (previewRoot == null)
            EditorGUILayout.HelpBox("Preview is not in the scene. Click Load Weapon Into Preview.", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("Play Mode is not required. Yellow = weapon socket. Cyan = LeftHandGrip.", MessageType.None);
    }

    private void DrawValidation()
    {
        if (definition == null)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(BuildValidation(definition), MessageType.None);
    }

    public static string BuildValidation(WeaponDefinition definition)
    {
        if (definition == null)
            return "No weapon selected.";

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(definition.DisplayName);
        builder.AppendLine("Class: " + definition.WeaponPoseClass);
        builder.AppendLine();
        builder.AppendLine(Mark(definition.WorldPrefab != null, "Weapon prefab assigned"));
        builder.AppendLine(Mark(definition.PoseProfile != null, "Pose profile assigned"));

        AnimationClip hold = ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Hold);
        builder.AppendLine(Mark(hold != null, hold != null ? hold.name + " assigned" : "Hold pose missing"));

        AppendFallbackLine(builder, definition, ThirdPersonWeaponPoseKind.Sprint, "Sprint");
        AppendFallbackLine(builder, definition, ThirdPersonWeaponPoseKind.Prone, "Prone");
        AppendFallbackLine(builder, definition, ThirdPersonWeaponPoseKind.Aim, "Aim");

        bool hasGrip = HasChild(definition, "LeftHandGrip");
        bool hasHint = HasChild(definition, "LeftElbowHint");
        builder.AppendLine(Mark(hasGrip, "LeftHandGrip found"));
        builder.AppendLine(Mark(hasHint || definition.WeaponPoseClass == ThirdPersonWeaponPoseClass.ShortGun,
            hasHint ? "LeftElbowHint found" : "LeftElbowHint not required"));
        bool socketConfigured = definition.WorldPrefab != null;
        builder.AppendLine(Mark(socketConfigured, "Right-hand offset configured"));
        builder.AppendLine(Mark(definition.UsesSupportHandIk || definition.WeaponPoseClass == ThirdPersonWeaponPoseClass.ShortGun,
            definition.UsesSupportHandIk ? "Support-hand IK configured" : "Support-hand IK off"));
        return builder.ToString().TrimEnd();
    }

    private static void AppendFallbackLine(
        StringBuilder builder,
        WeaponDefinition definition,
        ThirdPersonWeaponPoseKind kind,
        string label)
    {
        string name = ThirdPersonWeaponPoseResolver.DescribeFallback(definition, kind, out bool fallback);
        if (fallback)
            builder.AppendLine("! " + label + " pose currently using " + name);
        else
            builder.AppendLine("✓ " + name + " assigned");
    }

    private static string Mark(bool ok, string message)
    {
        return (ok ? "✓ " : "✗ ") + message;
    }

    private static bool HasChild(WeaponDefinition definition, string childName)
    {
        if (definition == null || definition.WorldPrefab == null)
            return false;

        Transform[] children = definition.WorldPrefab.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return true;
        }

        return false;
    }

    private void PrepareSelectedPoseForVeryAnimation()
    {
        if (definition == null)
        {
            status = "Select a weapon first.";
            return;
        }

        ThirdPersonWeaponPoseKind kind = ThirdPersonWeaponPoseResolver.ResolveKind(
            definition,
            locomotion == 4,
            locomotion == 2,
            aiming && locomotion != 2 && locomotion != 4,
            locomotion == 3);
        AnimationClip clip = ThirdPersonWeaponPoseResolver.ResolveClip(definition, kind);
        if (clip == null && kind != ThirdPersonWeaponPoseKind.Hold)
            clip = ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Hold);
        PreparePoseForVeryAnimation(clip);
    }

    private void PreparePoseForVeryAnimation(AnimationClip clip)
    {
        if (clip == null)
        {
            status = "Create the pose clip first, then prepare it for Very Animation.";
            return;
        }

        GameObject character = EnsureVeryAnimationCharacter();
        if (character == null)
        {
            status = "Could not create an authoring character. Open AnimationAuthoringTest and try again.";
            return;
        }

        Animator animator = ResolveCharacterAnimator(character);
        if (animator == null)
        {
            status = "Authoring character has no Animator.";
            return;
        }

        ThirdPersonWeaponPoseAuthoringSetup.FlattenPoseClip(clip);
        RuntimeAnimatorController authoring =
            ThirdPersonWeaponPoseAuthoringSetup.EnsureAuthoringController(clip);
        animator.runtimeAnimatorController = authoring;
        animator.enabled = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;
        animator.Play("AuthoringPose", 0, 0f);
        animator.Update(0f);

        DestroyPreview();
        EquipAuthoringWeapon(character);
        Selection.activeGameObject = animator.gameObject;
        EditorGUIUtility.PingObject(clip);
        EditorApplication.ExecuteMenuItem("Window/Animation/Animation");
        EditorApplication.delayCall += () =>
        {
            if (animator != null)
                Selection.activeGameObject = animator.gameObject;
        };

        status =
            "Prepared " + clip.name + " as a single-frame pose with the weapon attached.\n" +
            "1. Click Unity's Animation window so it is focused.\n" +
            "2. Confirm the clip dropdown shows " + clip.name + " and stay on frame 0.\n" +
            "3. Open Window → Very Animation → Main.\n" +
            "4. Click Edit Animation.\n" +
            "5. Pose arms in the Scene view, then save.\n" +
            "If the pose slides back after saving, click Flatten Current Pose Clip.";
        FrameNamed(character);
        SceneView.RepaintAll();
    }

    private GameObject EnsureVeryAnimationCharacter()
    {
        GameObject character = GameObject.Find(ThirdPersonWeaponPoseAuthoringSetup.AuthoringCharacterName);
        if (character != null)
            return character;

        if (Application.isPlaying)
            return null;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
            return null;

        character = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        character.name = ThirdPersonWeaponPoseAuthoringSetup.AuthoringCharacterName;
        character.transform.position = new Vector3(2f, 0f, 0f);
        character.transform.rotation = Quaternion.identity;
        StripPreviewForEditMode(character);
        DisableIkWhileAuthoring(character);
        HideFirstPersonVisuals(character);
        EditorSceneManager.MarkSceneDirty(character.scene);
        return character;
    }

    private void FlattenSelectedPoseClip()
    {
        if (definition == null)
        {
            status = "Select a weapon first.";
            return;
        }

        AnimationClip clip = ThirdPersonWeaponPoseResolver.ResolveClip(definition, ThirdPersonWeaponPoseKind.Hold);
        if (clip == null)
        {
            status = "No pose clip to flatten.";
            return;
        }

        ThirdPersonWeaponPoseAuthoringSetup.FlattenPoseClip(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        status = "Flattened " + clip.name + " to a single-frame pose using the pose at frame 0.";
    }

    private void EquipAuthoringWeapon(GameObject character)
    {
        if (character == null || definition == null)
            return;

        DisableIkWhileAuthoring(character);
        HideFirstPersonVisuals(character);

        WorldWeaponView world = character.GetComponent<WorldWeaponView>();
        if (world != null)
        {
            world.enabled = true;
            world.PrepareEditorPreview(definition);
        }

        ThirdPersonWeaponRig rig = character.GetComponent<ThirdPersonWeaponRig>();
        if (rig != null)
        {
            rig.enabled = true;
            rig.BeginEditorPreview();
            rig.ApplyEditorPreview(0f, 0f, 0f, 0f, 1f, 0f);
        }
    }

    private static void DisableIkWhileAuthoring(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;
            if (behaviour.GetType().Name == "RigBuilder")
                behaviour.enabled = false;
        }
    }

    private static void HideFirstPersonVisuals(GameObject root)
    {
        HideNamedChild(root.transform, "CameraRoot");
        HideNamedChild(root.transform, "FirstPerson");
        HideNamedChild(root.transform, "FirstPersonRoot");
        HideNamedChild(root.transform, "Viewmodel");
        HideNamedChild(root.transform, "WeaponView");

        WeaponPresentationController firstPerson = root.GetComponent<WeaponPresentationController>();
        if (firstPerson != null)
            firstPerson.enabled = false;
    }

    private static void FrameNamed(GameObject root)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null || root == null)
            return;

        Bounds bounds = new Bounds(root.transform.position + Vector3.up, Vector3.one * 2.2f);
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
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

    private void OpenPreviewScene()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(ThirdPersonWeaponPoseAuthoringSetup.PreviewScenePath, OpenSceneMode.Single);
        EnsurePreview();
        if (autoFrame)
            FramePreview();
        status = "Opened AnimationAuthoringTest. Preview does not modify the production Player prefab.";
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
        previewBinder = previewRoot.GetComponent<ThirdPersonWeaponPoseBinder>()
            ?? previewRoot.AddComponent<ThirdPersonWeaponPoseBinder>();
        previewBinder.AssignSlots(
            ThirdPersonWeaponPoseAuthoringSetup.SlotHold,
            ThirdPersonWeaponPoseAuthoringSetup.SlotSprint,
            ThirdPersonWeaponPoseAuthoringSetup.SlotProne,
            ThirdPersonWeaponPoseAuthoringSetup.SlotAim,
            ThirdPersonWeaponPoseAuthoringSetup.SlotCrouch);
        if (previewAnimator != null)
            previewBinder.Bind(previewAnimator);
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
            2 => "Sprint Locomotion",
            3 => "Crouching Idle",
            4 => "Prone Idle",
            _ => "Standing Idle"
        };
        int hash = Animator.StringToHash(state);
        if (previewAnimator.HasState(0, hash))
            previewAnimator.Play(hash, 0, 0.12f);
        else
            previewAnimator.Play(state, 0, 0.12f);

        previewAnimator.SetFloat("MoveX", locomotion == 1 ? 0.35f : 0f);
        previewAnimator.SetFloat("MoveY", locomotion == 1 ? 1f : 0f);
        previewAnimator.SetBool("IsMoving", locomotion == 1 || locomotion == 2);
        previewAnimator.SetBool("IsSprinting", locomotion == 2);
        previewAnimator.SetBool("IsCrouching", locomotion == 3);
        previewAnimator.SetBool("IsProne", locomotion == 4);

        ThirdPersonWeaponPoseKind kind = ThirdPersonWeaponPoseResolver.ResolveKind(
            definition,
            locomotion == 4,
            locomotion == 2,
            aiming && locomotion != 2 && locomotion != 4,
            locomotion == 3);
        if (previewBinder != null)
            previewBinder.PlayPreview(definition, kind, 0f);

        previewAnimator.Update(0.016f);
        ApplyPreviewPose();
    }

    private void ApplyPreviewPose()
    {
        if (previewRig == null)
            return;

        float aim = aiming && locomotion != 2 && locomotion != 4 ? 1f : 0f;
        float sprint = locomotion == 2 ? 1f : 0f;
        float crouch = locomotion == 3 ? 1f : 0f;
        float prone = locomotion == 4 ? 1f : 0f;
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
                behaviour is ThirdPersonWeaponPoseBinder)
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
        previewBinder = null;
        spawnedDefinition = null;

        GameObject leftover = GameObject.Find(PreviewName);
        if (leftover != null)
            DestroyImmediate(leftover);
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying)
            return;

        GameObject authoring = GameObject.Find(ThirdPersonWeaponPoseAuthoringSetup.AuthoringCharacterName);
        if (authoring != null)
        {
            ThirdPersonWeaponRig rig = authoring.GetComponent<ThirdPersonWeaponRig>();
            if (rig != null)
                rig.ApplyEditorPreview(0f, 0f, 0f, 0f, 1f, 0f);
        }

        if (previewRoot == null || previewRig == null)
            return;
        if (GUIUtility.hotControl != 0)
            return;

        SamplePreview();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
            DestroyPreview();
    }

    private static WeaponDefinition LoadDefaultDefinition()
    {
        WeaponCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
        if (catalog != null)
            return catalog.GetPermanentDefault();

        string[] guids = AssetDatabase.FindAssets("t:WeaponDefinition");
        if (guids.Length == 0)
            return null;
        return AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }
}
