using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public static class BullseyeSurfaceSetup
{
    public const string PlayerPrefabPath = "Assets/Player/Player.prefab";
    public const string StampMaterialPath = "Assets/Player/BullseyeMeshStamp.mat";
    public const string DecalMaterialPath = "Assets/Player/BullseyeSurfaceDecal.mat";
    public const string DecalTexturePath = "Assets/Player/BullseyeSurfaceDecal.png";

    private static readonly Dictionary<BullseyeSurfaceRegionId, string[]> BoneAliases = new()
    {
        { BullseyeSurfaceRegionId.Head, new[] { "mixamorig:Head", "Head", "BullseyeHeadAnchor" } },
        { BullseyeSurfaceRegionId.Neck, new[] { "mixamorig:Neck", "Neck" } },
        { BullseyeSurfaceRegionId.UpperChest, new[] { "mixamorig:Spine2", "Chest", "BullseyeUpperTorsoAnchor" } },
        { BullseyeSurfaceRegionId.LowerChest, new[] { "mixamorig:Spine", "Spine", "BullseyeLowerTorsoAnchor" } },
        { BullseyeSurfaceRegionId.UpperBack, new[] { "mixamorig:Spine2", "Chest" } },
        { BullseyeSurfaceRegionId.LowerBack, new[] { "mixamorig:Spine", "Spine" } },
        { BullseyeSurfaceRegionId.LeftShoulder, new[] { "mixamorig:LeftShoulder", "LeftShoulder" } },
        { BullseyeSurfaceRegionId.RightShoulder, new[] { "mixamorig:RightShoulder", "RightShoulder" } },
        { BullseyeSurfaceRegionId.LeftUpperArm, new[] { "mixamorig:LeftArm", "LeftUpperArm", "BullseyeLeftArmAnchor" } },
        { BullseyeSurfaceRegionId.RightUpperArm, new[] { "mixamorig:RightArm", "RightUpperArm", "BullseyeRightArmAnchor" } },
        { BullseyeSurfaceRegionId.LeftForearm, new[] { "mixamorig:LeftForeArm", "LeftLowerArm" } },
        { BullseyeSurfaceRegionId.RightForearm, new[] { "mixamorig:RightForeArm", "RightLowerArm" } },
        { BullseyeSurfaceRegionId.LeftThigh, new[] { "mixamorig:LeftUpLeg", "LeftUpperLeg", "BullseyeLeftLegAnchor" } },
        { BullseyeSurfaceRegionId.RightThigh, new[] { "mixamorig:RightUpLeg", "RightUpperLeg", "BullseyeRightLegAnchor" } },
        { BullseyeSurfaceRegionId.LeftLowerLeg, new[] { "mixamorig:LeftLeg", "LeftLowerLeg" } },
        { BullseyeSurfaceRegionId.RightLowerLeg, new[] { "mixamorig:RightLeg", "RightLowerLeg" } }
    };

    public static string Apply()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
            return "FAILED: missing " + PlayerPrefabPath;

        Texture2D decalTexture = EnsureDecalTexture();
        Material stampMaterial = EnsureStampMaterial();
        Material decalMaterial = EnsureDecalMaterial(decalTexture);

        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Undo.RegisterFullObjectHierarchyUndo(root, "Bullseye Surface Setup");

            HideLegacyCapsule(root);
            MarkLocomotionCollider(root);
            Transform system = EnsureChild(root.transform, "BullseyeSystem");
            BullseyeSurfaceMap map = EnsureComponent<BullseyeSurfaceMap>(root);
            BullseyeSurfaceVisual visual = EnsureComponent<BullseyeSurfaceVisual>(root);
            BullseyeMover mover = root.GetComponent<BullseyeMover>();
            BullseyeDetachController detach = root.GetComponent<BullseyeDetachController>();
            SkinnedMeshRenderer skinned = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Transform physical = root.transform.Find("Bullseye");

            BullseyeSurfaceRegion[] regions = BullseyeSurfaceMap.CreateDefaultRegions();
            SampleRegions(root.transform, skinned, regions);
            Transform visualRoot = root.transform.Find("VisualRoot") ?? root.transform;
            map.Assign(visualRoot, regions);

            Transform hitTarget = EnsureAttachedHitTarget(system, root.GetComponent<PlayerHealth>());
            DecalProjector projector = EnsureDecalProjector(system, decalMaterial);

            if (skinned != null)
            {
                AssignStampMaterial(skinned, stampMaterial);
                EnableReceiveDecals(skinned.sharedMaterial);
            }

            visual.Configure(skinned, stampMaterial, projector, 0.14f);
            SerializedObject visualSo = new SerializedObject(visual);
            visualSo.FindProperty("stampRadius").floatValue = 0.14f;
            SerializedProperty brightness = visualSo.FindProperty("stampBrightness");
            if (brightness != null)
                brightness.floatValue = 3.8f;
            visualSo.ApplyModifiedPropertiesWithoutUndo();
            HidePhysicalWhileAuthoring(physical);

            CreateCombatHitboxes(root.transform);

            if (mover != null)
            {
                SerializedObject so = new SerializedObject(mover);
                so.FindProperty("surfaceMap").objectReferenceValue = map;
                so.FindProperty("surfaceVisual").objectReferenceValue = visual;
                so.FindProperty("attachedHitTarget").objectReferenceValue = hitTarget;
                so.FindProperty("physicalBullseye").objectReferenceValue = physical;
                so.FindProperty("bullseyeSize").floatValue = 0.28f;
                so.FindProperty("baseMovementSpeed").floatValue = 0.2f;
                so.FindProperty("pauseChance").floatValue = 0f;
                so.FindProperty("minPauseDuration").floatValue = 0f;
                so.FindProperty("maxPauseDuration").floatValue = 0.25f;
                so.FindProperty("randomDirectionWeight").floatValue = 0.35f;
                so.FindProperty("continueForwardWeight").floatValue = 2.4f;
                so.FindProperty("maxSpawnPhaseOffset").floatValue = 0f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (detach != null)
            {
                SerializedObject so = new SerializedObject(detach);
                so.FindProperty("surfaceMover").objectReferenceValue = mover;
                so.FindProperty("surfaceMap").objectReferenceValue = map;
                so.FindProperty("surfaceVisual").objectReferenceValue = visual;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return "Bullseye surface system wired on Player.prefab. Regions=" + regions.Length;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void HideLegacyCapsule(GameObject root)
    {
        Transform capsule = root.transform.Find("Capsule");
        if (capsule == null)
            return;

        MeshRenderer renderer = capsule.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
            renderer.forceRenderingOff = true;
        }

        CapsuleCollider collider = capsule.GetComponent<CapsuleCollider>();
        if (collider != null)
            collider.enabled = false;
    }

    private static void MarkLocomotionCollider(GameObject root)
    {
        CapsuleCollider locomotion = root.GetComponent<CapsuleCollider>();
        if (locomotion == null)
            return;

        if (locomotion.GetComponent<PlayerLocomotionCollider>() == null)
            locomotion.gameObject.AddComponent<PlayerLocomotionCollider>();
    }

    private static Transform EnsureAttachedHitTarget(Transform system, PlayerHealth health)
    {
        Transform hit = system.Find("AttachedHitTarget");
        if (hit == null)
        {
            GameObject created = new GameObject("AttachedHitTarget");
            created.transform.SetParent(system, false);
            hit = created.transform;
        }

        SphereCollider sphere = hit.GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = hit.gameObject.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.5f;

        BullseyeTarget target = hit.GetComponent<BullseyeTarget>();
        if (target == null)
            target = hit.gameObject.AddComponent<BullseyeTarget>();

        return hit;
    }

    private static DecalProjector EnsureDecalProjector(Transform system, Material decalMaterial)
    {
        Transform visual = system.Find("AttachedVisual");
        if (visual == null)
        {
            GameObject created = new GameObject("AttachedVisual");
            created.transform.SetParent(system, false);
            visual = created.transform;
        }

        DecalProjector projector = visual.GetComponent<DecalProjector>();
        if (projector == null)
            projector = visual.gameObject.AddComponent<DecalProjector>();

        projector.material = decalMaterial;
        projector.size = new Vector3(0.24f, 0.24f, 0.16f);
        projector.fadeFactor = 1f;
        projector.drawDistance = 80f;
        return projector;
    }

    private static void EnableReceiveDecals(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_SupportDecals"))
            material.SetFloat("_SupportDecals", 1f);
        if (material.HasProperty("_EnableDecals"))
            material.SetFloat("_EnableDecals", 1f);
        EditorUtility.SetDirty(material);
    }

    private static void AssignStampMaterial(SkinnedMeshRenderer skinned, Material stamp)
    {
        Material[] materials = skinned.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == stamp)
                return;
        }

        var next = new Material[materials.Length + 1];
        for (int i = 0; i < materials.Length; i++)
            next[i] = materials[i];
        next[materials.Length] = stamp;
        skinned.sharedMaterials = next;
    }

    private static void HidePhysicalWhileAuthoring(Transform physical)
    {
        if (physical == null)
            return;

        Renderer renderer = physical.GetComponent<Renderer>();
        if (renderer != null)
            renderer.forceRenderingOff = true;
    }

    private static void CreateCombatHitboxes(Transform root)
    {
        CreateHitbox(root, "mixamorig:Head", "Hitbox_Head", BullseyeBodyZone.Head, 0.12f, 0.18f, new Vector3(0f, 0.08f, 0f));
        CreateHitbox(root, "mixamorig:Spine2", "Hitbox_Torso", BullseyeBodyZone.Torso, 0.16f, 0.42f, Vector3.zero);
        CreateHitbox(root, "mixamorig:LeftArm", "Hitbox_LeftArm", BullseyeBodyZone.Torso, 0.07f, 0.28f, new Vector3(0f, 0.12f, 0f));
        CreateHitbox(root, "mixamorig:RightArm", "Hitbox_RightArm", BullseyeBodyZone.Torso, 0.07f, 0.28f, new Vector3(0f, 0.12f, 0f));
        CreateHitbox(root, "mixamorig:LeftUpLeg", "Hitbox_LeftLeg", BullseyeBodyZone.LowerBody, 0.09f, 0.38f, new Vector3(0f, 0.18f, 0f));
        CreateHitbox(root, "mixamorig:RightUpLeg", "Hitbox_RightLeg", BullseyeBodyZone.LowerBody, 0.09f, 0.38f, new Vector3(0f, 0.18f, 0f));
    }

    private static void CreateHitbox(
        Transform root,
        string boneName,
        string hitboxName,
        BullseyeBodyZone zone,
        float radius,
        float height,
        Vector3 localCenter)
    {
        Transform bone = FindNamed(root, boneName);
        if (bone == null)
            return;

        Transform existing = bone.Find(hitboxName);
        GameObject box = existing != null ? existing.gameObject : new GameObject(hitboxName);
        if (existing == null)
            box.transform.SetParent(bone, false);

        box.transform.localPosition = localCenter;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = Vector3.one;

        CapsuleCollider capsule = box.GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = box.AddComponent<CapsuleCollider>();
        capsule.isTrigger = true;
        capsule.radius = radius;
        capsule.height = height;
        capsule.direction = 1;

        PlayerCombatHitbox marker = box.GetComponent<PlayerCombatHitbox>();
        if (marker == null)
            marker = box.AddComponent<PlayerCombatHitbox>();
        marker.Zone = zone;
    }

    private static void SampleRegions(Transform root, SkinnedMeshRenderer skinned, BullseyeSurfaceRegion[] regions)
    {
        Mesh baked = null;
        if (skinned != null && skinned.sharedMesh != null)
        {
            baked = new Mesh();
            skinned.BakeMesh(baked, true);
        }

        Transform orientation = root.Find("VisualRoot") ?? root;
        Vector3[] vertices = baked != null ? baked.vertices : System.Array.Empty<Vector3>();
        Vector3[] normals = baked != null ? baked.normals : System.Array.Empty<Vector3>();

        for (int i = 0; i < regions.Length; i++)
        {
            BullseyeSurfaceRegion region = regions[i];
            Transform bone = FindBone(root, region.id);
            region.bone = bone != null ? bone : orientation;
            Vector3 desired = DesiredDirection(orientation, region);
            SampleRegion(skinned, vertices, normals, region, desired);
        }

        if (baked != null)
            Object.DestroyImmediate(baked);
    }

    private static void SampleRegion(
        SkinnedMeshRenderer skinned,
        Vector3[] vertices,
        Vector3[] normals,
        BullseyeSurfaceRegion region,
        Vector3 desiredWorld)
    {
        Transform bone = region.bone;
        if (bone == null)
            return;

        Vector3 fallback = desiredWorld.normalized * 0.08f;
        if (vertices == null || vertices.Length == 0 || skinned == null)
        {
            region.localPosition = bone.InverseTransformPoint(bone.position + fallback);
            region.localNormal = bone.InverseTransformDirection(desiredWorld.normalized);
            return;
        }

        Transform meshTransform = skinned.transform;
        float best = float.MinValue;
        Vector3 bestWorld = bone.position + fallback;
        Vector3 bestNormal = desiredWorld.normalized;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = meshTransform.TransformPoint(vertices[i]);
            Vector3 normal = meshTransform.TransformDirection(normals[i]).normalized;
            float toBone = Vector3.Distance(world, bone.position);
            if (toBone > 0.42f)
                continue;

            float facing = Vector3.Dot(normal, desiredWorld);
            float score = facing * 1.4f - toBone * 2.2f;
            if (region.id == BullseyeSurfaceRegionId.Head)
                score += (world.y - bone.position.y) * 6f;
            if (score <= best)
                continue;

            best = score;
            bestWorld = world;
            bestNormal = normal;
        }

        if (best < -100f)
        {
            region.localPosition = bone.InverseTransformPoint(bone.position + fallback);
            region.localNormal = bone.InverseTransformDirection(desiredWorld.normalized);
            return;
        }

        region.localPosition = bone.InverseTransformPoint(bestWorld);
        region.localNormal = bone.InverseTransformDirection(bestNormal.normalized);
    }

    private static Vector3 DesiredDirection(Transform orientation, BullseyeSurfaceRegion region)
    {
        Vector3 local = Vector3.forward;
        if (region.id == BullseyeSurfaceRegionId.Head)
            local = (Vector3.forward + Vector3.up * 0.85f).normalized;
        else if (region.facing == BullseyeFacing.Back)
            local = Vector3.back;
        else if (region.lateral < -0.55f)
            local = (Vector3.forward + Vector3.left * 0.65f).normalized;
        else if (region.lateral > 0.55f)
            local = (Vector3.forward + Vector3.right * 0.65f).normalized;

        return orientation.TransformDirection(local);
    }

    private static Transform FindBone(Transform root, BullseyeSurfaceRegionId id)
    {
        if (!BoneAliases.TryGetValue(id, out string[] names))
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindNamed(root, names[i]);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Transform FindNamed(Transform root, string name)
    {
        if (root.name == name)
            return root;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == name)
                return children[i];
        }

        return null;
    }

    private static Transform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(name);
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static T EnsureComponent<T>(GameObject root) where T : Component
    {
        T existing = root.GetComponent<T>();
        return existing != null ? existing : root.AddComponent<T>();
    }

    private static Material EnsureStampMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(StampMaterialPath);
        Shader shader = Shader.Find("Bullseye/MeshSurfaceStamp");
        if (shader == null)
            shader = Shader.Find("HDRP/Unlit");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, StampMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        if (material.HasProperty("_Brightness"))
            material.SetFloat("_Brightness", 3.8f);
        if (material.HasProperty("_RingOuter"))
            material.SetColor("_RingOuter", new Color(8f, 0.18f, 0.08f, 1f));
        if (material.HasProperty("_RingMid"))
            material.SetColor("_RingMid", new Color(6.5f, 6.5f, 6.5f, 1f));
        if (material.HasProperty("_RingInner"))
            material.SetColor("_RingInner", new Color(9f, 0.28f, 0.1f, 1f));
        if (material.HasProperty("_CenterColor"))
            material.SetColor("_CenterColor", new Color(10f, 8.5f, 1.4f, 1f));

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureDecalMaterial(Texture2D texture)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(DecalMaterialPath);
        Shader shader = Shader.Find("HDRP/Decal");
        if (shader == null)
            shader = Shader.Find("HDRP/Unlit");

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, DecalMaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        if (texture != null)
        {
            if (material.HasProperty("_BaseColorMap"))
                material.SetTexture("_BaseColorMap", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_EmissiveColorMap"))
                material.SetTexture("_EmissiveColorMap", texture);
            if (material.HasProperty("_EmissiveColor"))
                material.SetColor("_EmissiveColor", new Color(6f, 1.2f, 0.25f, 1f));
            if (material.HasProperty("_AffectAlbedo"))
                material.SetFloat("_AffectAlbedo", 1f);
            if (material.HasProperty("_AffectEmission"))
                material.SetFloat("_AffectEmission", 1f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D EnsureDecalTexture()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(0.5f, 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float n = Vector2.Distance(uv, center) * 2f;
                Color color = Color.clear;
                if (n <= 1f)
                {
                    if (n < 0.18f)
                        color = new Color(1f, 0.92f, 0.2f, 1f);
                    else if (n < 0.38f)
                        color = new Color(1f, 0.08f, 0.05f, 1f);
                    else if (n < 0.62f)
                        color = new Color(1f, 1f, 1f, 1f);
                    else
                        color = new Color(1f, 0.05f, 0.04f, 1f);

                    color.a *= 1f - Mathf.SmoothStep(0.86f, 1f, n);
                }

                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(
            DecalTexturePath,
            texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(DecalTexturePath);
        TextureImporter importer = AssetImporter.GetAtPath(DecalTexturePath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(DecalTexturePath);
    }
}
