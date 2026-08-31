using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// REQ-043: placeholder DMR scope art, ScopeDefinition, and Player overlay wiring.
/// </summary>
public static class ScopePresentationSetup
{
    public const string ScopeFolder = "Assets/Weapons/DMR/Scope";
    public const string DefinitionPath = "Assets/Scripts/Weapons/DMRScopeDefinition.asset";
    public const string WeaponDefinitionPath = "Assets/Scripts/Weapons/DMRDefinition.asset";
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    private const string OverlayPath = ScopeFolder + "/DMR_ScopeHousing.png";
    private const string ReticlePath = ScopeFolder + "/DMR_ScopeReticle.png";
    private const string VignettePath = ScopeFolder + "/DMR_ScopeVignette.png";
    private const string MaskPath = ScopeFolder + "/DMR_ScopeMask.png";

    [MenuItem("Bullseye/Weapons/Setup DMR Scope Presentation (REQ-043)")]
    public static void Setup()
    {
        Debug.Log(SetupInternal());
    }

    public static string SetupInternal()
    {
        Directory.CreateDirectory(ToAbsolute(ScopeFolder));

        Sprite mask = WriteSprite(MaskPath, ScopePlaceholderSprites.CreateHoleTexture(512));
        Sprite housing = WriteSprite(OverlayPath, ScopePlaceholderSprites.CreateHousingTexture(512));
        Sprite vignette = WriteSprite(VignettePath, ScopePlaceholderSprites.CreateVignetteTexture(512));
        Sprite reticle = WriteSprite(ReticlePath, ScopePlaceholderSprites.CreateReticleTexture(512));
        if (mask == null || housing == null || vignette == null || reticle == null)
            return "FAILED: could not write DMR scope placeholder sprites";

        ScopeDefinition scope = CreateOrUpdateScopeDefinition(housing, reticle, vignette, mask);
        if (scope == null)
            return "FAILED: could not create DMRScopeDefinition.asset";

        if (!AssignToDmr(scope))
            return "FAILED: DMRDefinition.asset is missing";

        if (!AddOverlayToPlayer())
            return "FAILED: Player.prefab is missing";

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "OK: DMR scope presentation is configured";
    }

    private static ScopeDefinition CreateOrUpdateScopeDefinition(
        Sprite housing,
        Sprite reticle,
        Sprite vignette,
        Sprite mask)
    {
        ScopeDefinition scope = AssetDatabase.LoadAssetAtPath<ScopeDefinition>(DefinitionPath);
        if (scope == null)
        {
            scope = ScriptableObject.CreateInstance<ScopeDefinition>();
            AssetDatabase.CreateAsset(scope, DefinitionPath);
        }

        SerializedObject so = new SerializedObject(scope);
        so.FindProperty("style").enumValueIndex = (int)ScopePresentationType.Dmr;
        so.FindProperty("usesScopeOverlay").boolValue = true;
        so.FindProperty("lensRadius").floatValue = 0.78f;
        so.FindProperty("overlaySprite").objectReferenceValue = housing;
        so.FindProperty("reticleSprite").objectReferenceValue = reticle;
        so.FindProperty("vignetteSprite").objectReferenceValue = vignette;
        so.FindProperty("maskSprite").objectReferenceValue = mask;
        so.FindProperty("peripheralOpacity").floatValue = 0.72f;
        so.FindProperty("peripheralColor").colorValue = new Color(0.015f, 0.015f, 0.02f, 1f);
        so.FindProperty("housingColor").colorValue = Color.white;
        so.FindProperty("housingThickness").floatValue = 0.08f;
        so.FindProperty("vignetteStrength").floatValue = 0.38f;
        so.FindProperty("lensTint").colorValue = new Color(0.75f, 0.85f, 0.78f, 0f);
        so.FindProperty("hideHipFireReticle").boolValue = true;
        so.FindProperty("reticleColor").colorValue = new Color(0.94f, 0.94f, 0.9f, 0.95f);
        SerializedProperty curve = so.FindProperty("transitionCurve");
        if (curve != null)
            curve.animationCurveValue = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(scope);
        return scope;
    }

    private static bool AssignToDmr(ScopeDefinition scope)
    {
        WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponDefinitionPath);
        if (definition == null)
            return false;

        SerializedObject so = new SerializedObject(definition);
        SerializedProperty property = so.FindProperty("scopePresentation");
        if (property == null)
            return false;

        property.objectReferenceValue = scope;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return true;
    }

    private static bool AddOverlayToPlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
            return false;

        try
        {
            if (root.GetComponent<PlayerScopeOverlay>() == null)
                root.AddComponent<PlayerScopeOverlay>();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite WriteSprite(string assetPath, Texture2D texture)
    {
        string absolute = ToAbsolute(assetPath);
        File.WriteAllBytes(absolute, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static string ToAbsolute(string assetPath)
    {
        string project = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(project, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }
}
