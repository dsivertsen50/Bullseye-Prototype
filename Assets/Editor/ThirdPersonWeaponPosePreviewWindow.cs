using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Edit-mode Scene view preview of a posed third-person player and gun.
/// Does not require Play Mode.
/// </summary>
public class ThirdPersonWeaponPosePreviewWindow : EditorWindow
{
    private const string PreviewName = "__BullseyeTpWeaponPreview";
    private const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    private const string CatalogPath = "Assets/Scripts/Weapons/WeaponCatalog.asset";
    private static readonly string[] StanceLabels = { "Stand", "Aim", "Sprint", "Crouch", "Prone" };

    private static ThirdPersonWeaponPosePreviewWindow instance;

    [SerializeField] private WeaponDefinition definition;
    [SerializeField] private int stance;
    [SerializeField] private float aimPitch;
    [SerializeField] private bool autoFrame = true;

    private GameObject previewRoot;
    private ThirdPersonWeaponRig previewRig;
    private WorldWeaponView previewWorld;
    private Animator previewAnimator;
    private WeaponDefinition spawnedDefinition;
    private int spawnedStance = -1;
    private bool refreshing;
    private string lastSaveSummary;

    public static ThirdPersonWeaponRig ActivePreviewRig =>
        instance != null ? instance.previewRig : null;

    public static WeaponDefinition ActivePreviewDefinition =>
        instance != null ? instance.definition : null;

    public static int ActiveStance =>
        instance != null ? instance.stance : 0;

    public static bool IsPreviewRig(ThirdPersonWeaponRig rig)
    {
        return rig != null && instance != null && instance.previewRig == rig;
    }

    public static bool OwnsDefinition(WeaponDefinition weapon)
    {
        return instance != null && weapon != null && instance.definition == weapon;
    }

    public static void TickPreview()
    {
        if (instance == null || Application.isPlaying)
            return;

        instance.EnsurePreview();
        instance.SampleStanceAnimation();
        instance.ApplyPreviewPose();
    }

    public static void ApplyActivePreview()
    {
        if (instance == null || Application.isPlaying)
            return;

        instance.ApplyPreviewPose();
    }

    public static void NotifyDefinitionChanged(WeaponDefinition definition)
    {
        if (instance == null || definition == null || instance.definition != definition)
            return;

        instance.RefreshPreview();
    }

    [MenuItem("Bullseye/Weapons/Third-Person Pose Preview (Deprecated)")]
    public static void Open()
    {
        ThirdPersonWeaponHoldSetupWindow.Open();
    }

    public static void Open(WeaponDefinition weapon)
    {
        ThirdPersonWeaponPoseAuthoringWindow.Open(weapon);
    }

    private void OnEnable()
    {
        instance = this;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.quitting += DestroyPreview;
        Undo.undoRedoPerformed += RefreshPreview;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorApplication.update += OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        if (definition == null)
            definition = LoadDefaultDefinition();
        EnsurePreview();
    }

    private void OnDisable()
    {
        SavePosesToDisk(forceReserialize: false);
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.quitting -= DestroyPreview;
        Undo.undoRedoPerformed -= RefreshPreview;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        DestroyPreview();
        if (instance == this)
            instance = null;
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Edit-mode preview of the equipped world weapon. Yellow = weapon on the right-hand socket. " +
            "Cyan = LeftHandGrip. Magenta = LeftElbowHint.\n\n" +
            "Move the weapon, grip, and elbow hint in the Scene view. " +
            "Do not pose individual arm bones. Play Mode is not required.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        definition = (WeaponDefinition)EditorGUILayout.ObjectField(
            "Weapon",
            definition,
            typeof(WeaponDefinition),
            false);
        DrawWeaponQuickSelect();
        stance = GUILayout.Toolbar(stance, StanceLabels);
        aimPitch = EditorGUILayout.Slider("Look Pitch", aimPitch, -50f, 50f);
        autoFrame = EditorGUILayout.Toggle("Frame On Change", autoFrame);
        bool changed = EditorGUI.EndChangeCheck();

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Preview"))
                RebuildPreview();
            if (GUILayout.Button("Frame Preview"))
                FramePreview();
        }

        if (GUILayout.Button("Select Weapon Asset"))
        {
            if (definition != null)
            {
                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
            }
        }

        if (definition != null && definition.WorldPrefab != null && GUILayout.Button("Select World Prefab"))
        {
            Selection.activeObject = definition.WorldPrefab;
            EditorGUIUtility.PingObject(definition.WorldPrefab);
        }

        if (GUILayout.Button("Reset Socket Offset"))
            ResetCurrentStanceGun();

        if (GUILayout.Button("Save Poses To Disk"))
            SavePosesToDisk(forceReserialize: true);

        if (!string.IsNullOrEmpty(lastSaveSummary))
            EditorGUILayout.HelpBox(lastSaveSummary, MessageType.Info);

        if (previewRoot == null)
            EditorGUILayout.HelpBox("Preview is not in the scene. Click Refresh Preview.", MessageType.Warning);

        if (changed)
            RefreshPreview();
    }

    private void DrawWeaponQuickSelect()
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
        {
            previewRig = previewRoot.GetComponent<ThirdPersonWeaponRig>();
            previewWorld = previewRoot.GetComponent<WorldWeaponView>();
            previewAnimator = ResolveCharacterAnimator(previewRoot);
            if (previewRig != null && !previewRig.IsEditorPreview)
                previewRig.BeginEditorPreview();
        }

        if (definition != spawnedDefinition || stance != spawnedStance)
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
        previewRig = previewRoot.GetComponent<ThirdPersonWeaponRig>();
        previewWorld = previewRoot.GetComponent<WorldWeaponView>();
        previewAnimator = ResolveCharacterAnimator(previewRoot);
        if (previewRig != null)
            previewRig.BeginEditorPreview();

        spawnedDefinition = null;
        spawnedStance = -1;
        RefreshPreview();
        if (autoFrame)
            FramePreview();
    }

    private void RefreshPreview()
    {
        if (refreshing || previewRoot == null || previewWorld == null || previewRig == null)
            return;
        if (definition == null)
            definition = LoadDefaultDefinition();
        if (definition == null)
            return;

        refreshing = true;
        try
        {
            if (spawnedDefinition != definition)
            {
                previewWorld.PrepareEditorPreview(definition);
                spawnedDefinition = definition;
            }

            SampleStanceAnimation();
            ApplyPreviewPose();
            spawnedStance = stance;
            SceneView.RepaintAll();
        }
        finally
        {
            refreshing = false;
        }
    }

    private void ApplyPreviewPose()
    {
        if (previewRig == null)
            return;

        float aim = stance == 1 ? 1f : 0f;
        float sprint = stance == 2 ? 1f : 0f;
        float crouch = stance == 3 ? 1f : 0f;
        float prone = stance == 4 ? 1f : 0f;
        previewRig.ApplyEditorPreview(aim, sprint, prone, aimPitch, 1f, crouch);
    }

    private void SampleStanceAnimation()
    {
        if (previewAnimator == null)
            return;

        previewAnimator.enabled = true;
        previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        previewAnimator.applyRootMotion = false;
        previewAnimator.speed = 1f;
        string state = stance switch
        {
            2 => "Sprint Locomotion",
            3 => "Crouching Idle",
            4 => "Prone Idle",
            _ => "Standing Idle"
        };

        if (previewAnimator.runtimeAnimatorController == null)
            return;

        int hash = Animator.StringToHash(state);
        if (previewAnimator.HasState(0, hash))
            previewAnimator.Play(hash, 0, 0.05f);
        else
            previewAnimator.Play(state, 0, 0.05f);

        int poseLayer = previewAnimator.GetLayerIndex("WeaponPose");
        if (poseLayer >= 0)
        {
            string poseState = stance switch
            {
                2 => "LongGunSprint",
                4 => "LongGunProne",
                _ => "LongGunReady"
            };
            int poseHash = Animator.StringToHash(poseState);
            if (previewAnimator.HasState(poseLayer, poseHash))
                previewAnimator.Play(poseHash, poseLayer, 0f);
            previewAnimator.SetLayerWeight(poseLayer, definition != null &&
                definition.PoseCategory == ThirdPersonPoseCategory.LongGun ? 1f : 0.35f);
        }

        previewAnimator.Update(0.016f);
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
            if (behaviour is ThirdPersonWeaponRig || behaviour is WorldWeaponView || behaviour is PlayerVisualRig)
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
            Object.DestroyImmediate(previewRoot);

        previewRoot = null;
        previewRig = null;
        previewWorld = null;
        previewAnimator = null;
        spawnedDefinition = null;
        spawnedStance = -1;

        GameObject leftover = GameObject.Find(PreviewName);
        if (leftover != null)
            Object.DestroyImmediate(leftover);
    }

    private void ResetCurrentStanceGun()
    {
        if (definition == null)
            return;

        SerializedObject so = new SerializedObject(definition);
        so.Update();
        so.FindProperty("worldLocalPosition").vector3Value = Vector3.zero;
        so.FindProperty("worldLocalEuler").vector3Value = Vector3.zero;
        if (so.ApplyModifiedProperties())
            WeaponDefinitionEditor.SaveDefinition(definition, refreshPreview: true);
    }

    private void SavePosesToDisk(bool forceReserialize = false)
    {
        if (definition == null)
            return;

        WeaponDefinitionEditor.DiscardInspectorStaleEdits(definition);
        WeaponDefinitionEditor.SaveDefinition(definition, refreshPreview: false, forceReserialize: forceReserialize);
        WeaponDefinitionEditor.FlushDefinitionsToDisk();
        lastSaveSummary = WeaponDefinitionEditor.DescribeSavedPose(definition, stance);
        Repaint();
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying || previewRoot == null || previewRig == null)
            return;
        if (GUIUtility.hotControl != 0)
            return;

        SampleStanceAnimation();
        ApplyPreviewPose();
    }

    private void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode)
        {
            SavePosesToDisk();
            DestroyPreview();
        }
    }

    private void OnSceneSaving(Scene scene, string path)
    {
        SavePosesToDisk();
    }

    private void OnBeforeAssemblyReload()
    {
        SavePosesToDisk();
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
