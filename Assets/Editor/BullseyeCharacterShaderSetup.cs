using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class BullseyeCharacterShaderSetup
{
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    public const string ShaderGraphPath = "Assets/Shaders/PlayerBullseyeLit.shadergraph";
    public const string ShaderGraphSourcePath = "Assets/Shaders/PlayerBullseyeLitBasic.shadergraph";
    public const string MaterialPath = "Assets/Player/PlayerBullseyeLit.mat";
    public const string RegionMeshPath = "Assets/Player/SM_StickMan_BullseyeRegions.asset";
    public const string DecalTexturePath = "Assets/Player/BullseyeSurfaceDecal.png";
    public const string CustomFunctionMarker = "c0ffee58b0014a0aa11e001b001e0001";

    [MenuItem("Bullseye/REQ-058 Apply Character Material Bullseye")]
    public static void ApplyFromMenu()
    {
        Debug.Log(Apply());
    }

    public static string Apply()
    {
        string graphResult = EnsureShaderGraph();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DecalTexturePath);
        Material material = EnsureCharacterMaterial(texture);
        Mesh regionMesh = EnsureRegionMesh();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
            return "FAILED: missing " + PlayerPrefabPath;

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            WirePlayer(root, material, texture, regionMesh);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return "REQ-058 character-material bullseye applied. " + graphResult;
    }

    public static string EnsureShaderGraph()
    {
        if (!File.Exists(ShaderGraphSourcePath) && !File.Exists(ShaderGraphPath))
            return "FAILED: missing Shader Graph source";

        string sourcePath = File.Exists(ShaderGraphSourcePath) ? ShaderGraphSourcePath : ShaderGraphPath;
        string text = File.ReadAllText(sourcePath);
        if (!text.Contains(CustomFunctionMarker))
            text = PatchShaderGraph(text);

        if (sourcePath != ShaderGraphPath)
        {
            File.Copy(sourcePath, ShaderGraphPath, true);
        }

        File.WriteAllText(ShaderGraphPath, text);
        AssetDatabase.ImportAsset(ShaderGraphPath, ImportAssetOptions.ForceUpdate);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderGraphPath);
        return "shader=" + (shader != null ? shader.name : "null");
    }

    public static Material EnsureCharacterMaterial(Texture2D bullseyeTexture)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderGraphPath);
        if (shader == null)
            shader = Shader.Find("HDRP/PlayerBullseyeLit");
        if (shader == null)
            shader = Shader.Find("Lit/PlayerBullseyeLit");
        if (shader == null)
            shader = Shader.Find("Lit/PlayerBullseyeLitBasic");
        if (shader == null)
            throw new System.InvalidOperationException("Player bullseye shader was not imported.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", new Color(0.538f, 0.538f, 0.538f, 1f));
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", new Color(0.538f, 0.538f, 0.538f, 1f));
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.86f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (bullseyeTexture != null && material.HasProperty("_BullseyeTexture"))
            material.SetTexture("_BullseyeTexture", bullseyeTexture);
        if (material.HasProperty("_BullseyeCenterEnabled"))
            material.SetVector("_BullseyeCenterEnabled", new Vector4(0f, 1f, 0f, 0f));

        EditorUtility.SetDirty(material);
        return material;
    }

    public static UnityEngine.Mesh EnsureRegionMesh()
    {
        UnityEngine.Mesh source = FindSourceCharacterMesh();
        if (source == null)
            throw new System.InvalidOperationException("Player character mesh was not found.");

        UnityEngine.Mesh mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(RegionMeshPath);
        if (mesh == null)
        {
            mesh = new UnityEngine.Mesh { name = "SM_StickMan_BullseyeRegions" };
            AssetDatabase.CreateAsset(mesh, RegionMeshPath);
        }

        CopyMesh(source, mesh);
        string[] boneNames = FindSourceBoneNames();
        mesh.SetUVs(2, BuildRegionCoords(source, boneNames));
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    private static UnityEngine.Mesh FindSourceCharacterMesh()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player Character - Rigged - T-Pose.fbx");
        if (fbx != null)
        {
            SkinnedMeshRenderer smr = fbx.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.sharedMesh != null && smr.sharedMesh.vertexCount > 0)
                return smr.sharedMesh;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            SkinnedMeshRenderer skinned = FindCharacterRenderer(root);
            if (skinned != null && skinned.sharedMesh != null && skinned.sharedMesh.vertexCount > 0)
                return skinned.sharedMesh;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return null;
    }

    private static string[] FindSourceBoneNames()
    {
        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Player/Player Character - Rigged - T-Pose.fbx");
        if (fbx != null)
        {
            SkinnedMeshRenderer smr = fbx.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null)
            {
                var names = new string[smr.bones.Length];
                for (int i = 0; i < smr.bones.Length; i++)
                    names[i] = smr.bones[i] != null ? smr.bones[i].name : "";
                return names;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            SkinnedMeshRenderer skinned = FindCharacterRenderer(root);
            if (skinned == null || skinned.bones == null)
                return System.Array.Empty<string>();

            var names = new string[skinned.bones.Length];
            for (int i = 0; i < skinned.bones.Length; i++)
                names[i] = skinned.bones[i] != null ? skinned.bones[i].name : "";
            return names;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    public static void WirePlayer(GameObject root, Material material, Texture2D texture, Mesh regionMesh)
    {
        SkinnedMeshRenderer skinned = FindCharacterRenderer(root);
        if (skinned != null)
        {
            if (regionMesh != null)
                skinned.sharedMesh = regionMesh;
            skinned.sharedMaterial = material;
        }

        BullseyeSurfaceVisual visual = root.GetComponent<BullseyeSurfaceVisual>();
        if (visual == null)
            visual = root.AddComponent<BullseyeSurfaceVisual>();

        SerializedObject visualSo = new SerializedObject(visual);
        visualSo.FindProperty("characterRenderer").objectReferenceValue = skinned;
        visualSo.FindProperty("bullseyeTexture").objectReferenceValue = texture;
        visualSo.FindProperty("stampRadius").floatValue = 0.13f;
        visualSo.ApplyModifiedPropertiesWithoutUndo();

        Transform system = root.transform.Find("BullseyeSystem");
        if (system != null)
        {
            DisableChild(system, "AttachedVisual");
            DisableChild(system, "AttachedSticker");
            DisableChild(system, "StampOverlay");
        }

        BullseyeMover mover = root.GetComponent<BullseyeMover>();
        if (mover != null)
        {
            SerializedObject moverSo = new SerializedObject(mover);
            moverSo.FindProperty("surfaceVisual").objectReferenceValue = visual;
            moverSo.FindProperty("hideFromOwnerCameraDistance").floatValue = 0f;
            moverSo.FindProperty("bullseyeSize").floatValue = 0.26f;
            moverSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void DisableChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
            return;

        child.gameObject.SetActive(false);
        Renderer renderer = child.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
            renderer.forceRenderingOff = true;
        }

        DecalProjector projector = child.GetComponent<DecalProjector>();
        if (projector != null)
            projector.enabled = false;
    }

    private static SkinnedMeshRenderer FindCharacterRenderer(GameObject root)
    {
        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            if (renderers[i].gameObject.name.IndexOf("StickMan", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return renderers[i];
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].gameObject.name != "StampOverlay")
                return renderers[i];
        }

        return null;
    }

    private static void CopyMesh(UnityEngine.Mesh source, UnityEngine.Mesh dest)
    {
        dest.Clear();
        dest.name = "SM_StickMan_BullseyeRegions";
        dest.indexFormat = source.indexFormat;
        dest.vertices = source.vertices;
        dest.normals = source.normals;
        dest.tangents = source.tangents;
        dest.uv = source.uv;
        dest.uv2 = source.uv2;
        dest.boneWeights = source.boneWeights;
        dest.bindposes = source.bindposes;
        dest.subMeshCount = source.subMeshCount;
        for (int i = 0; i < source.subMeshCount; i++)
            dest.SetTriangles(source.GetTriangles(i), i);
        dest.RecalculateBounds();
    }

    private static List<Vector4> BuildRegionCoords(UnityEngine.Mesh mesh, string[] boneNames)
    {
        Vector3[] normals = mesh.normals;
        BoneWeight[] weights = mesh.boneWeights;
        var coords = new List<Vector4>(mesh.vertexCount);
        for (int i = 0; i < mesh.vertexCount; i++)
        {
            float family = FamilyFromWeight(weights[i], boneNames);
            float facing = i < normals.Length ? Mathf.Clamp(normals[i].z, -1f, 1f) : 1f;
            coords.Add(new Vector4(family + 1f, facing, 1f, 0f));
        }

        return coords;
    }

    private static float FamilyFromWeight(BoneWeight weight, string[] boneNames)
    {
        float best = 0f;
        float bestScore = -1f;
        Accumulate(weight.boneIndex0, weight.weight0, boneNames, ref best, ref bestScore);
        Accumulate(weight.boneIndex1, weight.weight1, boneNames, ref best, ref bestScore);
        Accumulate(weight.boneIndex2, weight.weight2, boneNames, ref best, ref bestScore);
        Accumulate(weight.boneIndex3, weight.weight3, boneNames, ref best, ref bestScore);
        return bestScore >= 0f ? best : BullseyeSurfaceFamilies.Torso;
    }

    private static void Accumulate(int boneIndex, float weight, string[] boneNames, ref float best, ref float bestScore)
    {
        if (weight <= 0.0001f || boneNames == null || boneIndex < 0 || boneIndex >= boneNames.Length)
            return;

        float family = BullseyeSurfaceFamilies.FromBoneName(boneNames[boneIndex]);
        if (weight > bestScore)
        {
            bestScore = weight;
            best = family;
        }
    }

    private static string PatchShaderGraph(string text)
    {
        text = text.Replace(
            "            \"m_Id\": \"e9e1c13a37fc4c90aa91f90de4bc28f0\"\n        }",
            "            \"m_Id\": \"e9e1c13a37fc4c90aa91f90de4bc28f0\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000001\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000002\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000003\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000004\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000005\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000006\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000007\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000008\"\n        }");

        text = text.Replace(
            "            \"m_Id\": \"5ec06cadc9e24d6da593bb6ddd8e0772\"\n        }",
            "            \"m_Id\": \"5ec06cadc9e24d6da593bb6ddd8e0772\"\n        },\n        {\n            \"m_Id\": \"" + CustomFunctionMarker + "\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000001\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000002\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000003\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000004\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000005\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000006\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000007\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000008\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b001000000000009\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b00100000000000a\"\n        },\n        {\n            \"m_Id\": \"b001node00014aa0b00100000000000b\"\n        }");

        text = text.Replace(
            "                    \"m_Id\": \"ea558b80dfbd4f6bb3f07cbdbfc6f0aa\"\n                },\n                \"m_SlotId\": 0",
            "                    \"m_Id\": \"" + CustomFunctionMarker + "\"\n                },\n                \"m_SlotId\": 0");

        const string edgeAnchor =
            "                    \"m_Id\": \"" + CustomFunctionMarker + "\"\n                },\n                \"m_SlotId\": 0\n            }\n        }";
        text = text.Replace(edgeAnchor, edgeAnchor + ",\n" + BuildGraphEdgesJson());

        text = text.Replace(
            "            \"m_Id\": \"22d8814e29224e60bf33e303d4aa3eb5\"\n        }\n    ]\n}",
            "            \"m_Id\": \"22d8814e29224e60bf33e303d4aa3eb5\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000001\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000002\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000003\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000004\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000005\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000006\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000007\"\n        },\n        {\n            \"m_Id\": \"b001ce0000014aa0b001000000000008\"\n        }\n    ]\n}");

        var extra = new StringBuilder();
        extra.AppendLine();
        extra.Append(BuildPropertiesAndNodes());
        text += extra.ToString();
        return text;
    }

    private static string BuildGraphEdgesJson()
    {
        var sb = new StringBuilder();
        void Edge(string from, int fromSlot, string to, int toSlot)
        {
            if (sb.Length > 0)
                sb.Append(",\n");
            sb.Append("        {\n            \"m_OutputSlot\": {\n                \"m_Node\": {\n                    \"m_Id\": \"").Append(from)
                .Append("\"\n                },\n                \"m_SlotId\": ").Append(fromSlot)
                .Append("\n            },\n            \"m_InputSlot\": {\n                \"m_Node\": {\n                    \"m_Id\": \"")
                .Append(to).Append("\"\n                },\n                \"m_SlotId\": ").Append(toSlot)
                .Append("\n            }\n        }");
        }

        Edge(CustomFunctionMarker, 12, "ea558b80dfbd4f6bb3f07cbdbfc6f0aa", 0);
        Edge("b001node00014aa0b001000000000001", 0, CustomFunctionMarker, 1);
        Edge("b001node00014aa0b001000000000002", 0, CustomFunctionMarker, 2);
        Edge("b001node00014aa0b001000000000003", 0, CustomFunctionMarker, 3);
        Edge("b001node00014aa0b00100000000000b", 0, CustomFunctionMarker, 4);
        Edge("b001node00014aa0b001000000000004", 0, CustomFunctionMarker, 5);
        Edge("b001node00014aa0b001000000000005", 0, CustomFunctionMarker, 6);
        Edge("b001node00014aa0b001000000000006", 0, CustomFunctionMarker, 7);
        Edge("b001node00014aa0b001000000000007", 0, CustomFunctionMarker, 8);
        Edge("b001node00014aa0b001000000000008", 0, CustomFunctionMarker, 9);
        Edge("b001node00014aa0b001000000000009", 0, CustomFunctionMarker, 10);
        Edge("b001node00014aa0b00100000000000a", 0, CustomFunctionMarker, 11);
        return sb.ToString();
    }

    private static string BuildPropertiesAndNodes()
    {
        var sb = new StringBuilder();
        sb.Append(Vector4Property("b001ce0000014aa0b001000000000001", "a0010001-0001-4000-8000-000000000001", "Bullseye Center Enabled", "_BullseyeCenterEnabled", "0.0", "0.0", "0.0", "0.0"));
        sb.Append(Vector4Property("b001ce0000014aa0b001000000000002", "a0010001-0001-4000-8000-000000000002", "Bullseye Normal Radius", "_BullseyeNormalRadius", "0.0", "0.0", "1.0", "0.13"));
        sb.Append(Vector4Property("b001ce0000014aa0b001000000000003", "a0010001-0001-4000-8000-000000000003", "Bullseye Tangent", "_BullseyeTangentWS", "1.0", "0.0", "0.0", "0.0"));
        sb.Append(Vector4Property("b001ce0000014aa0b001000000000004", "a0010001-0001-4000-8000-000000000004", "Bullseye Bitangent", "_BullseyeBitangentWS", "0.0", "1.0", "0.0", "0.0"));
        sb.Append(Vector4Property("b001ce0000014aa0b001000000000005", "a0010001-0001-4000-8000-000000000005", "Bullseye Wrap Axis Radius", "_BullseyeWrapAxisRadius", "0.0", "1.0", "0.0", "0.12"));
        sb.Append(Vector4Property("b001ce0000014aa0b001000000000006", "a0010001-0001-4000-8000-000000000006", "Bullseye Region State", "_BullseyeRegionState", "2.0", "2.0", "1.0", "1.0"));
        sb.Append(Vector4Property("b001ce0000014aa0b001000000000007", "a0010001-0001-4000-8000-000000000007", "Bullseye Wrap Flash", "_BullseyeWrapFlash", "0.0", "0.0", "0.0", "0.0"));
        sb.Append(TextureProperty("b001ce0000014aa0b001000000000008", "a0010001-0001-4000-8000-000000000008", "Bullseye Texture", "_BullseyeTexture"));

        sb.Append(PositionNode());
        sb.Append(NormalNode());
        sb.Append(UvNode());
        sb.Append(PropertyNode("b001node00014aa0b001000000000004", "b001slot00014aa0b001000000000004", "b001ce0000014aa0b001000000000001", 4));
        sb.Append(PropertyNode("b001node00014aa0b001000000000005", "b001slot00014aa0b001000000000005", "b001ce0000014aa0b001000000000002", 4));
        sb.Append(PropertyNode("b001node00014aa0b001000000000006", "b001slot00014aa0b001000000000006", "b001ce0000014aa0b001000000000003", 4));
        sb.Append(PropertyNode("b001node00014aa0b001000000000007", "b001slot00014aa0b001000000000007", "b001ce0000014aa0b001000000000004", 4));
        sb.Append(PropertyNode("b001node00014aa0b001000000000008", "b001slot00014aa0b001000000000008", "b001ce0000014aa0b001000000000005", 4));
        sb.Append(PropertyNode("b001node00014aa0b001000000000009", "b001slot00014aa0b001000000000009", "b001ce0000014aa0b001000000000006", 4));
        sb.Append(PropertyNode("b001node00014aa0b00100000000000a", "b001slot00014aa0b00100000000000a", "b001ce0000014aa0b001000000000007", 4));
        sb.Append(TexturePropertyNode());
        sb.Append(CustomFunctionNode());
        return sb.ToString();
    }

    private static string Vector4Property(string id, string guid, string name, string reference, string x, string y, string z, string w)
    {
        return $@"
{{
    ""m_SGVersion"": 1,
    ""m_Type"": ""UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty"",
    ""m_ObjectId"": ""{id}"",
    ""m_Guid"": {{
        ""m_GuidSerialized"": ""{guid}""
    }},
    ""m_Name"": ""{name}"",
    ""m_DefaultRefNameVersion"": 1,
    ""m_RefNameGeneratedByDisplayName"": ""{name}"",
    ""m_DefaultReferenceName"": ""{reference}"",
    ""m_OverrideReferenceName"": ""{reference}"",
    ""m_GeneratePropertyBlock"": true,
    ""m_UseCustomSlotLabel"": false,
    ""m_CustomSlotLabel"": """",
    ""m_DismissedVersion"": 0,
    ""m_Precision"": 0,
    ""overrideHLSLDeclaration"": false,
    ""hlslDeclarationOverride"": 0,
    ""m_Hidden"": false,
    ""m_PerRendererData"": false,
    ""m_customAttributes"": [],
    ""m_Value"": {{
        ""x"": {x},
        ""y"": {y},
        ""z"": {z},
        ""w"": {w}
    }}
}}
";
    }

    private static string TextureProperty(string id, string guid, string name, string reference)
    {
        return $@"
{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty"",
    ""m_ObjectId"": ""{id}"",
    ""m_Guid"": {{
        ""m_GuidSerialized"": ""{guid}""
    }},
    ""m_Name"": ""{name}"",
    ""m_DefaultRefNameVersion"": 1,
    ""m_RefNameGeneratedByDisplayName"": ""{name}"",
    ""m_DefaultReferenceName"": ""{reference}"",
    ""m_OverrideReferenceName"": ""{reference}"",
    ""m_GeneratePropertyBlock"": true,
    ""m_UseCustomSlotLabel"": false,
    ""m_CustomSlotLabel"": """",
    ""m_DismissedVersion"": 0,
    ""m_Precision"": 0,
    ""overrideHLSLDeclaration"": false,
    ""hlslDeclarationOverride"": 0,
    ""m_Hidden"": false,
    ""m_PerRendererData"": false,
    ""m_customAttributes"": [],
    ""m_Value"": {{
        ""m_SerializedTexture"": """",
        ""m_Guid"": """"
    }},
    ""isMainTexture"": false,
    ""useTilingAndOffset"": false,
    ""useTexelSize"": false,
    ""m_Modifiable"": true,
    ""m_DefaultType"": 0
}}
";
    }

    private static string PropertyNode(string nodeId, string slotId, string propertyId, int vectorSize)
    {
        string slotType = vectorSize == 2
            ? "UnityEditor.ShaderGraph.Vector2MaterialSlot"
            : vectorSize == 3
                ? "UnityEditor.ShaderGraph.Vector3MaterialSlot"
                : "UnityEditor.ShaderGraph.Vector4MaterialSlot";
        string value = vectorSize == 2
            ? @"""m_Value"": { ""x"": 0.0, ""y"": 0.0 }, ""m_DefaultValue"": { ""x"": 0.0, ""y"": 0.0 }"
            : vectorSize == 3
                ? @"""m_Value"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0 }, ""m_DefaultValue"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0 }"
                : @"""m_Value"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0 }, ""m_DefaultValue"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0 }";

        return $@"
{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.PropertyNode"",
    ""m_ObjectId"": ""{nodeId}"",
    ""m_Group"": {{ ""m_Id"": """" }},
    ""m_Name"": ""Property"",
    ""m_DrawState"": {{
        ""m_Expanded"": true,
        ""m_Position"": {{ ""serializedVersion"": ""2"", ""x"": -720.0, ""y"": 80.0, ""width"": 180.0, ""height"": 34.0 }}
    }},
    ""m_Slots"": [ {{ ""m_Id"": ""{slotId}"" }} ],
    ""synonyms"": [],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": true,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": {{ ""m_SerializableColors"": [] }},
    ""m_Property"": {{ ""m_Id"": ""{propertyId}"" }}
}}

{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""{slotType}"",
    ""m_ObjectId"": ""{slotId}"",
    ""m_Id"": 0,
    ""m_DisplayName"": ""Out"",
    ""m_SlotType"": 1,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""Out"",
    ""m_StageCapability"": 3,
    {value}
}}
";
    }

    private static string TexturePropertyNode()
    {
        return @"
{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.PropertyNode"",
    ""m_ObjectId"": ""b001node00014aa0b00100000000000b"",
    ""m_Group"": { ""m_Id"": """" },
    ""m_Name"": ""Property"",
    ""m_DrawState"": {
        ""m_Expanded"": true,
        ""m_Position"": { ""serializedVersion"": ""2"", ""x"": -720.0, ""y"": 420.0, ""width"": 180.0, ""height"": 34.0 }
    },
    ""m_Slots"": [ { ""m_Id"": ""b001slot00014aa0b00100000000000b"" } ],
    ""synonyms"": [],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": true,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": { ""m_SerializableColors"": [] },
    ""m_Property"": { ""m_Id"": ""b001ce0000014aa0b001000000000008"" }
}

{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.Texture2DMaterialSlot"",
    ""m_ObjectId"": ""b001slot00014aa0b00100000000000b"",
    ""m_Id"": 0,
    ""m_DisplayName"": ""Out"",
    ""m_SlotType"": 1,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""Out"",
    ""m_StageCapability"": 3,
    ""m_BareResource"": false
}
";
    }

    private static string PositionNode()
    {
        return @"
{
    ""m_SGVersion"": 1,
    ""m_Type"": ""UnityEditor.ShaderGraph.PositionNode"",
    ""m_ObjectId"": ""b001node00014aa0b001000000000001"",
    ""m_Group"": { ""m_Id"": """" },
    ""m_Name"": ""Position"",
    ""m_DrawState"": {
        ""m_Expanded"": true,
        ""m_Position"": { ""serializedVersion"": ""2"", ""x"": -720.0, ""y"": -80.0, ""width"": 206.0, ""height"": 130.0 }
    },
    ""m_Slots"": [ { ""m_Id"": ""b001slot00014aa0b001000000000001"" } ],
    ""synonyms"": [ ""location"" ],
    ""m_Precision"": 1,
    ""m_PreviewExpanded"": false,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 2,
    ""m_CustomColors"": { ""m_SerializableColors"": [] },
    ""m_Space"": 4,
    ""m_PositionSource"": 0
}

{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.PositionMaterialSlot"",
    ""m_ObjectId"": ""b001slot00014aa0b001000000000001"",
    ""m_Id"": 0,
    ""m_DisplayName"": ""Out"",
    ""m_SlotType"": 1,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""Out"",
    ""m_StageCapability"": 3,
    ""m_Value"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0 },
    ""m_DefaultValue"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0 },
    ""m_Labels"": [],
    ""m_Space"": 4
}
";
    }

    private static string NormalNode()
    {
        return @"
{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.NormalVectorNode"",
    ""m_ObjectId"": ""b001node00014aa0b001000000000002"",
    ""m_Group"": { ""m_Id"": """" },
    ""m_Name"": ""Normal Vector"",
    ""m_DrawState"": {
        ""m_Expanded"": true,
        ""m_Position"": { ""serializedVersion"": ""2"", ""x"": -720.0, ""y"": 60.0, ""width"": 206.0, ""height"": 130.0 }
    },
    ""m_Slots"": [ { ""m_Id"": ""b001slot00014aa0b001000000000002"" } ],
    ""synonyms"": [ ""surface direction"" ],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": false,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 2,
    ""m_CustomColors"": { ""m_SerializableColors"": [] },
    ""m_Space"": 2
}

{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.NormalMaterialSlot"",
    ""m_ObjectId"": ""b001slot00014aa0b001000000000002"",
    ""m_Id"": 0,
    ""m_DisplayName"": ""Out"",
    ""m_SlotType"": 1,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""Out"",
    ""m_StageCapability"": 3,
    ""m_Value"": { ""x"": 0.0, ""y"": 0.0, ""z"": 1.0 },
    ""m_DefaultValue"": { ""x"": 0.0, ""y"": 0.0, ""z"": 1.0 },
    ""m_Labels"": [],
    ""m_Space"": 2
}
";
    }

    private static string UvNode()
    {
        return @"
{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.UVNode"",
    ""m_ObjectId"": ""b001node00014aa0b001000000000003"",
    ""m_Group"": { ""m_Id"": """" },
    ""m_Name"": ""UV"",
    ""m_DrawState"": {
        ""m_Expanded"": true,
        ""m_Position"": { ""serializedVersion"": ""2"", ""x"": -720.0, ""y"": 200.0, ""width"": 145.0, ""height"": 128.0 }
    },
    ""m_Slots"": [ { ""m_Id"": ""b001slot00014aa0b001000000000003"" } ],
    ""synonyms"": [ ""texcoords"" ],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": false,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": { ""m_SerializableColors"": [] },
    ""m_OutputChannel"": 2
}

{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.Vector4MaterialSlot"",
    ""m_ObjectId"": ""b001slot00014aa0b001000000000003"",
    ""m_Id"": 0,
    ""m_DisplayName"": ""Out"",
    ""m_SlotType"": 1,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""Out"",
    ""m_StageCapability"": 3,
    ""m_Value"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0 },
    ""m_DefaultValue"": { ""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0 }
}
";
    }

    private static string CustomFunctionNode()
    {
        var sb = new StringBuilder();
        sb.Append(@"
{
    ""m_SGVersion"": 1,
    ""m_Type"": ""UnityEditor.ShaderGraph.CustomFunctionNode"",
    ""m_ObjectId"": """).Append(CustomFunctionMarker).Append(@""",
    ""m_Group"": { ""m_Id"": """" },
    ""m_Name"": ""EvaluateBullseyeCharacterSurface (Custom Function)"",
    ""m_DrawState"": {
        ""m_Expanded"": true,
        ""m_Position"": { ""serializedVersion"": ""2"", ""x"": -360.0, ""y"": -40.0, ""width"": 280.0, ""height"": 360.0 }
    },
    ""m_Slots"": [
        { ""m_Id"": ""b001cfs000014aa0b001000000000000"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000001"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000002"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000003"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000004"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000005"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000006"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000007"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000008"" },
        { ""m_Id"": ""b001cfs000014aa0b001000000000009"" },
        { ""m_Id"": ""b001cfs000014aa0b00100000000000a"" },
        { ""m_Id"": ""b001cfs000014aa0b00100000000000b"" },
        { ""m_Id"": ""b001cfs000014aa0b00100000000000c"" }
    ],
    ""synonyms"": [],
    ""m_Precision"": 0,
    ""m_PreviewExpanded"": false,
    ""m_DismissedVersion"": 0,
    ""m_PreviewMode"": 0,
    ""m_CustomColors"": { ""m_SerializableColors"": [] },
    ""m_SourceType"": 0,
    ""m_FunctionName"": ""EvaluateBullseyeCharacterSurface"",
    ""m_FunctionSource"": ""b7e4c1a09f2d4e6a8c3d5f1a2b9e7048"",
    ""m_FunctionSourceUsePragmas"": true,
    ""m_FunctionBody"": ""Enter function body here...""
}
");
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000000", 0, "BaseColor", 0, 3));
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000001", 1, "PositionWS", 0, 3));
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000002", 2, "NormalWS", 0, 3));
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000003", 3, "RegionCoords", 0, 4));
        sb.Append(@"
{
    ""m_SGVersion"": 0,
    ""m_Type"": ""UnityEditor.ShaderGraph.Texture2DMaterialSlot"",
    ""m_ObjectId"": ""b001cfs000014aa0b001000000000004"",
    ""m_Id"": 4,
    ""m_DisplayName"": ""BullseyeTex"",
    ""m_SlotType"": 0,
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""BullseyeTex"",
    ""m_StageCapability"": 3,
    ""m_BareResource"": false
}
");
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000005", 5, "CenterEnabled", 0, 4));
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000006", 6, "NormalRadius", 0, 4));
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000007", 7, "TangentWS", 0, 3));
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000008", 8, "BitangentWS", 0, 3));
        sb.Append(VectorSlot("b001cfs000014aa0b001000000000009", 9, "WrapAxisRadius", 0, 4));
        sb.Append(VectorSlot("b001cfs000014aa0b00100000000000a", 10, "RegionState", 0, 4));
        sb.Append(VectorSlot("b001cfs000014aa0b00100000000000b", 11, "WrapFlash", 0, 2));
        sb.Append(VectorSlot("b001cfs000014aa0b00100000000000c", 12, "OutColor", 1, 3));
        return sb.ToString();
    }

    private static string VectorSlot(string id, int slotId, string name, int slotType, int size)
    {
        string type = size == 2
            ? "UnityEditor.ShaderGraph.Vector2MaterialSlot"
            : size == 3
                ? "UnityEditor.ShaderGraph.Vector3MaterialSlot"
                : "UnityEditor.ShaderGraph.Vector4MaterialSlot";
        string value = size == 2
            ? @"""x"": 0.0, ""y"": 0.0"
            : size == 3
                ? @"""x"": 0.0, ""y"": 0.0, ""z"": 0.0"
                : @"""x"": 0.0, ""y"": 0.0, ""z"": 0.0, ""w"": 0.0";
        return $@"
{{
    ""m_SGVersion"": 0,
    ""m_Type"": ""{type}"",
    ""m_ObjectId"": ""{id}"",
    ""m_Id"": {slotId},
    ""m_DisplayName"": ""{name}"",
    ""m_SlotType"": {slotType},
    ""m_Hidden"": false,
    ""m_ShaderOutputName"": ""{name}"",
    ""m_StageCapability"": 3,
    ""m_Value"": {{ {value} }},
    ""m_DefaultValue"": {{ {value} }}
}}
";
    }
}
