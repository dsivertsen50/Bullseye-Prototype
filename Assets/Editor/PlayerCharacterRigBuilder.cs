using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a prototype Humanoid rig for Player Character V1 without
/// overwriting the original unrigged FBX. Menu: Bullseye/Rebuild Player Character Rig
/// </summary>
public static class PlayerCharacterRigBuilder
{
    public const string SourceFbxPath = "Assets/Player/Player Character V1.fbx";
    public const string RiggedRootFolder = "Assets/Player";
    public const string MeshPath = "Assets/Player/PlayerCharacterV1_WeightedMesh.asset";
    public const string AvatarPath = "Assets/Player/PlayerCharacterV1_RiggedAvatar.asset";
    public const string ClipPath = "Assets/Player/Idle_PlayerThirdPerson.anim";
    public const string ControllerPath = "Assets/Player/AC_PlayerThirdPerson.controller";
    public const string RiggedPrefabPath = "Assets/Player/PlayerCharacterV1_Rigged.prefab";
    public const string VisualPrefabPath = "Assets/Player/PlayerCharacterV1.prefab";
    public const string RiggedPrefabGuid = "7c4e9f2a1b8d4560a3e5c719d0f2b846";
    public const long RootGameObjectFileId = 1555850018738510906;
    public const long RootTransformFileId = 3507150726923045233;
    public const float VisualScale = 1.1418551f;
    public const float VisualYOffset = -0.013023615f;

    [MenuItem("Bullseye/Rebuild Player Character Rig")]
    public static void RebuildFromMenu()
    {
        string report = Build();
        Debug.Log(report);
    }

    public static string Build()
    {
        EnsureFolders();

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFbxPath);
        if (source == null)
            return "FAILED: missing " + SourceFbxPath;

        MeshFilter sourceFilter = source.GetComponentInChildren<MeshFilter>();
        MeshRenderer sourceRenderer = source.GetComponentInChildren<MeshRenderer>();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return "FAILED: source mesh missing";

        Mesh sourceMesh = sourceFilter.sharedMesh;
        Vector3[] vertices = sourceMesh.vertices;
        Landmarks landmarks = Measure(vertices);

        GameObject root = new GameObject("PlayerCharacterV1");
        try
        {
            Dictionary<string, Transform> bones = CreateSkeleton(root.transform, landmarks);
            SkinnedMeshRenderer skinned = CreateSkinnedMesh(
                root.transform,
                sourceMesh,
                sourceRenderer != null ? sourceRenderer.sharedMaterial : null,
                bones);
            AssignBindPoses(skinned, bones);

            PlayerVisualRig rig = root.AddComponent<PlayerVisualRig>();
            CreateSocketsAndAnchors(bones, rig);

            Avatar avatar = BuildHumanoidAvatar(root, bones);
            if (avatar == null)
                return "FAILED: AvatarBuilder returned null";

            bool avatarValid = avatar.isValid;
            bool avatarHuman = avatar.isHuman;
            int boneCount = skinned.bones != null ? skinned.bones.Length : 0;
            int weighted = CountWeightedVertices(skinned.sharedMesh);

            SaveMesh(skinned.sharedMesh);
            SaveAvatar(avatar);

            root.transform.localPosition = new Vector3(0f, VisualYOffset, 0f);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * VisualScale;

            string meshGuid = GuidFor(MeshPath);
            string avatarGuid = GuidFor(AvatarPath);
            string controllerGuid = GuidFor(ControllerPath);
            string materialGuid = MaterialGuid(sourceRenderer != null ? sourceRenderer.sharedMaterial : null);
            WriteRiggedPrefabYaml(root, skinned, meshGuid, avatarGuid, controllerGuid, materialGuid);
            string wiring = PatchPlayerPrefabYaml();

            return
                "Rig build complete.\n" +
                "Avatar valid=" + avatarValid + " human=" + avatarHuman + "\n" +
                "Bones=" + boneCount + " weightedVerts=" + weighted + "/" + vertices.Length + "\n" +
                "Prefab=" + RiggedPrefabPath + "\n" +
                wiring + "\n" +
                "Original FBX preserved at " + SourceFbxPath;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void EnsureFolders()
    {
        // Assets/Player already exists. Do not create VP-unimported subfolders.
    }

    private struct Landmarks
    {
        public Vector3 hips;
        public Vector3 spine;
        public Vector3 chest;
        public Vector3 neck;
        public Vector3 head;
        public Vector3 leftShoulder;
        public Vector3 rightShoulder;
        public Vector3 leftUpperArm;
        public Vector3 rightUpperArm;
        public Vector3 leftLowerArm;
        public Vector3 rightLowerArm;
        public Vector3 leftHand;
        public Vector3 rightHand;
        public Vector3 leftUpperLeg;
        public Vector3 rightUpperLeg;
        public Vector3 leftLowerLeg;
        public Vector3 rightLowerLeg;
        public Vector3 leftFoot;
        public Vector3 rightFoot;
    }

    private static Landmarks Measure(Vector3[] vertices)
    {
        Vector3 min = vertices[0];
        Vector3 max = vertices[0];
        for (int i = 1; i < vertices.Length; i++)
        {
            min = Vector3.Min(min, vertices[i]);
            max = Vector3.Max(max, vertices[i]);
        }

        float height = Mathf.Max(0.01f, max.y - min.y);
        Vector3 leftHand = Centroid(vertices, v => v.x < -0.55f);
        Vector3 rightHand = Centroid(vertices, v => v.x > 0.55f);
        Vector3 leftFoot = Centroid(vertices, v => v.y < min.y + height * 0.12f && v.x < -0.02f);
        Vector3 rightFoot = Centroid(vertices, v => v.y < min.y + height * 0.12f && v.x > 0.02f);
        Vector3 head = Centroid(vertices, v => v.y > min.y + height * 0.86f);
        Vector3 torso = Centroid(vertices, v => Mathf.Abs(v.x) < 0.22f && v.y > min.y + height * 0.42f && v.y < min.y + height * 0.72f);

        if (leftHand == Vector3.zero)
            leftHand = new Vector3(min.x, min.y + height * 0.66f, 0f);
        if (rightHand == Vector3.zero)
            rightHand = new Vector3(max.x, min.y + height * 0.66f, 0f);
        if (leftFoot == Vector3.zero)
            leftFoot = new Vector3(-0.1f, min.y, 0f);
        if (rightFoot == Vector3.zero)
            rightFoot = new Vector3(0.1f, min.y, 0f);
        if (head == Vector3.zero)
            head = new Vector3(0f, max.y - 0.08f, 0f);
        if (torso == Vector3.zero)
            torso = new Vector3(0f, min.y + height * 0.55f, 0f);

        float hipY = min.y + height * 0.51f;
        float chestY = min.y + height * 0.70f;
        float shoulderY = min.y + height * 0.76f;
        float shoulderX = 0.16f;
        Vector3 hips = new Vector3(0f, hipY, torso.z);

        Landmarks l = new Landmarks
        {
            hips = hips,
            spine = new Vector3(0f, Mathf.Lerp(hipY, chestY, 0.45f), torso.z),
            chest = new Vector3(0f, chestY, torso.z),
            neck = new Vector3(0f, min.y + height * 0.84f, head.z),
            head = head,
            leftShoulder = new Vector3(-shoulderX, shoulderY, torso.z),
            rightShoulder = new Vector3(shoulderX, shoulderY, torso.z),
            leftHand = leftHand,
            rightHand = rightHand,
            leftFoot = leftFoot,
            rightFoot = rightFoot
        };

        l.leftUpperArm = Vector3.Lerp(l.leftShoulder, l.leftHand, 0.18f);
        l.rightUpperArm = Vector3.Lerp(l.rightShoulder, l.rightHand, 0.18f);
        l.leftLowerArm = Vector3.Lerp(l.leftShoulder, l.leftHand, 0.55f);
        l.rightLowerArm = Vector3.Lerp(l.rightShoulder, l.rightHand, 0.55f);
        l.leftUpperLeg = new Vector3(l.leftFoot.x, hipY - 0.02f, hips.z);
        l.rightUpperLeg = new Vector3(l.rightFoot.x, hipY - 0.02f, hips.z);
        l.leftLowerLeg = Vector3.Lerp(l.leftUpperLeg, l.leftFoot, 0.52f);
        l.rightLowerLeg = Vector3.Lerp(l.rightUpperLeg, l.rightFoot, 0.52f);
        return l;
    }

    private static Vector3 Centroid(Vector3[] vertices, System.Func<Vector3, bool> match)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (!match(vertices[i]))
                continue;
            sum += vertices[i];
            count++;
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    private static Dictionary<string, Transform> CreateSkeleton(Transform root, Landmarks l)
    {
        var bones = new Dictionary<string, Transform>();
        Transform hips = CreateBone(root, bones, "Hips", l.hips, null);
        Transform spine = CreateBone(hips, bones, "Spine", l.spine, hips);
        Transform chest = CreateBone(spine, bones, "Chest", l.chest, spine);
        Transform neck = CreateBone(chest, bones, "Neck", l.neck, chest);
        CreateBone(neck, bones, "Head", l.head, neck);

        Transform leftShoulder = CreateBone(chest, bones, "LeftShoulder", l.leftShoulder, chest);
        Transform leftUpperArm = CreateBone(leftShoulder, bones, "LeftUpperArm", l.leftUpperArm, leftShoulder);
        Transform leftLowerArm = CreateBone(leftUpperArm, bones, "LeftLowerArm", l.leftLowerArm, leftUpperArm);
        CreateBone(leftLowerArm, bones, "LeftHand", l.leftHand, leftLowerArm);

        Transform rightShoulder = CreateBone(chest, bones, "RightShoulder", l.rightShoulder, chest);
        Transform rightUpperArm = CreateBone(rightShoulder, bones, "RightUpperArm", l.rightUpperArm, rightShoulder);
        Transform rightLowerArm = CreateBone(rightUpperArm, bones, "RightLowerArm", l.rightLowerArm, rightUpperArm);
        CreateBone(rightLowerArm, bones, "RightHand", l.rightHand, rightLowerArm);

        Transform leftUpperLeg = CreateBone(hips, bones, "LeftUpperLeg", l.leftUpperLeg, hips);
        Transform leftLowerLeg = CreateBone(leftUpperLeg, bones, "LeftLowerLeg", l.leftLowerLeg, leftUpperLeg);
        CreateBone(leftLowerLeg, bones, "LeftFoot", l.leftFoot, leftLowerLeg);

        Transform rightUpperLeg = CreateBone(hips, bones, "RightUpperLeg", l.rightUpperLeg, hips);
        Transform rightLowerLeg = CreateBone(rightUpperLeg, bones, "RightLowerLeg", l.rightLowerLeg, rightUpperLeg);
        CreateBone(rightLowerLeg, bones, "RightFoot", l.rightFoot, rightLowerLeg);

        OrientChain(bones["Hips"], bones["Spine"]);
        OrientChain(bones["Spine"], bones["Chest"]);
        OrientChain(bones["Chest"], bones["Neck"]);
        OrientChain(bones["Neck"], bones["Head"]);
        OrientChain(bones["LeftShoulder"], bones["LeftUpperArm"]);
        OrientChain(bones["LeftUpperArm"], bones["LeftLowerArm"]);
        OrientChain(bones["LeftLowerArm"], bones["LeftHand"]);
        OrientChain(bones["RightShoulder"], bones["RightUpperArm"]);
        OrientChain(bones["RightUpperArm"], bones["RightLowerArm"]);
        OrientChain(bones["RightLowerArm"], bones["RightHand"]);
        OrientChain(bones["LeftUpperLeg"], bones["LeftLowerLeg"]);
        OrientChain(bones["LeftLowerLeg"], bones["LeftFoot"]);
        OrientChain(bones["RightUpperLeg"], bones["RightLowerLeg"]);
        OrientChain(bones["RightLowerLeg"], bones["RightFoot"]);
        return bones;
    }

    private static Transform CreateBone(
        Transform parent,
        Dictionary<string, Transform> bones,
        string name,
        Vector3 worldPosition,
        Transform orientFrom)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.identity;
        bones[name] = go.transform;
        return go.transform;
    }

    private static void OrientChain(Transform bone, Transform child)
    {
        Vector3 dir = child.position - bone.position;
        if (dir.sqrMagnitude < 0.000001f)
            return;

        Vector3 up = dir.normalized;
        Vector3 forward = Vector3.forward;
        if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.95f)
            forward = Vector3.right;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        if (right.sqrMagnitude < 0.0001f)
            return;
        forward = Vector3.Cross(right, up).normalized;
        bone.rotation = Quaternion.LookRotation(forward, up);
    }

    private static SkinnedMeshRenderer CreateSkinnedMesh(
        Transform root,
        Mesh sourceMesh,
        Material material,
        Dictionary<string, Transform> bones)
    {
        GameObject meshObject = new GameObject("SM_StickMan");
        meshObject.transform.SetParent(root, false);
        meshObject.transform.localPosition = Vector3.zero;
        meshObject.transform.localRotation = Quaternion.identity;
        meshObject.transform.localScale = Vector3.one;

        Mesh skinnedMesh = UnityEngine.Object.Instantiate(sourceMesh);
        skinnedMesh.name = "PlayerCharacterV1_Skinned";

        Transform[] boneArray =
        {
            bones["Hips"], bones["Spine"], bones["Chest"], bones["Neck"], bones["Head"],
            bones["LeftShoulder"], bones["LeftUpperArm"], bones["LeftLowerArm"], bones["LeftHand"],
            bones["RightShoulder"], bones["RightUpperArm"], bones["RightLowerArm"], bones["RightHand"],
            bones["LeftUpperLeg"], bones["LeftLowerLeg"], bones["LeftFoot"],
            bones["RightUpperLeg"], bones["RightLowerLeg"], bones["RightFoot"]
        };

        Vector3[] vertices = skinnedMesh.vertices;
        BoneWeight[] weights = new BoneWeight[vertices.Length];
        Segment[] segments = BuildSegments(boneArray);

        for (int i = 0; i < vertices.Length; i++)
        {
            weights[i] = WeightVertex(vertices[i], boneArray, segments);
        }

        skinnedMesh.boneWeights = weights;
        SkinnedMeshRenderer renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = skinnedMesh;
        renderer.sharedMaterial = material;
        renderer.bones = boneArray;
        renderer.rootBone = bones["Hips"];
        renderer.updateWhenOffscreen = true;
        renderer.quality = SkinQuality.Auto;
        return renderer;
    }

    private struct Segment
    {
        public Vector3 start;
        public Vector3 end;
        public float radius;
    }

    private static Segment[] BuildSegments(Transform[] bones)
    {
        var segments = new Segment[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            Vector3 start = bone.position;
            Vector3 end = start + bone.up * 0.08f;
            if (bone.childCount > 0)
                end = bone.GetChild(0).position;
            float radius = RadiusFor(bone.name);
            segments[i] = new Segment { start = start, end = end, radius = radius };
        }

        return segments;
    }

    private static float RadiusFor(string name)
    {
        if (name.Contains("Hand") || name.Contains("Foot") || name.Contains("Head") || name.Contains("Neck"))
            return 0.11f;
        if (name.Contains("Shoulder"))
            return 0.10f;
        if (name.Contains("Arm") || name.Contains("Leg"))
            return 0.13f;
        if (name == "Hips")
            return 0.20f;
        return 0.16f;
    }

    private static BoneWeight WeightVertex(Vector3 vertex, Transform[] bones, Segment[] segments)
    {
        float w0 = 0f, w1 = 0f, w2 = 0f, w3 = 0f;
        int i0 = 0, i1 = 0, i2 = 0, i3 = 0;

        for (int i = 0; i < bones.Length; i++)
        {
            float influence = Influence(vertex, segments[i]);
            if (influence >= w0)
            {
                w3 = w2; i3 = i2;
                w2 = w1; i2 = i1;
                w1 = w0; i1 = i0;
                w0 = influence; i0 = i;
            }
            else if (influence >= w1)
            {
                w3 = w2; i3 = i2;
                w2 = w1; i2 = i1;
                w1 = influence; i1 = i;
            }
            else if (influence >= w2)
            {
                w3 = w2; i3 = i2;
                w2 = influence; i2 = i;
            }
            else if (influence >= w3)
            {
                w3 = influence; i3 = i;
            }
        }

        float sum = w0 + w1 + w2 + w3;
        if (sum < 0.0001f)
        {
            return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
        }

        return new BoneWeight
        {
            boneIndex0 = i0,
            boneIndex1 = i1,
            boneIndex2 = i2,
            boneIndex3 = i3,
            weight0 = w0 / sum,
            weight1 = w1 / sum,
            weight2 = w2 / sum,
            weight3 = w3 / sum
        };
    }

    private static float Influence(Vector3 point, Segment segment)
    {
        float distance = DistanceToSegment(point, segment.start, segment.end);
        float radius = Mathf.Max(0.04f, segment.radius);
        float t = 1f - Mathf.Clamp01(distance / radius);
        return t * t;
    }

    private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float denom = Vector3.Dot(ab, ab);
        if (denom < 0.000001f)
            return Vector3.Distance(point, a);
        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denom);
        return Vector3.Distance(point, a + ab * t);
    }

    private static void AssignBindPoses(SkinnedMeshRenderer renderer, Dictionary<string, Transform> bones)
    {
        Transform[] boneArray = renderer.bones;
        Matrix4x4[] bindPoses = new Matrix4x4[boneArray.Length];
        Matrix4x4 meshWorld = renderer.transform.localToWorldMatrix;
        for (int i = 0; i < boneArray.Length; i++)
            bindPoses[i] = boneArray[i].worldToLocalMatrix * meshWorld;
        renderer.sharedMesh.bindposes = bindPoses;
    }

    private static void CreateSocketsAndAnchors(Dictionary<string, Transform> bones, PlayerVisualRig rig)
    {
        Transform rightHandSocket = CreateChild(bones["RightHand"], "RightHandWeaponSocket", new Vector3(0f, 0.04f, 0.02f));
        Transform leftHandIk = CreateChild(bones["LeftHand"], "LeftHandIKTarget", Vector3.zero);
        Transform holster = CreateChild(bones["Hips"], "WeaponHolsterSocket", new Vector3(0.12f, 0.02f, 0.04f));
        Transform back = CreateChild(bones["Chest"], "BackWeaponSocket", new Vector3(0f, 0.05f, -0.08f));
        Transform head = CreateChild(bones["Head"], "BullseyeHeadAnchor", Vector3.zero);
        Transform upper = CreateChild(bones["Chest"], "BullseyeUpperTorsoAnchor", Vector3.zero);
        Transform lower = CreateChild(bones["Spine"], "BullseyeLowerTorsoAnchor", Vector3.zero);
        Transform leftArm = CreateChild(bones["LeftUpperArm"], "BullseyeLeftArmAnchor", Vector3.zero);
        Transform rightArm = CreateChild(bones["RightUpperArm"], "BullseyeRightArmAnchor", Vector3.zero);
        Transform leftLeg = CreateChild(bones["LeftUpperLeg"], "BullseyeLeftLegAnchor", Vector3.zero);
        Transform rightLeg = CreateChild(bones["RightUpperLeg"], "BullseyeRightLegAnchor", Vector3.zero);

        rig.Assign(
            rightHandSocket,
            leftHandIk,
            holster,
            back,
            head,
            upper,
            lower,
            leftArm,
            rightArm,
            leftLeg,
            rightLeg);
    }

    private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static Avatar BuildHumanoidAvatar(GameObject root, Dictionary<string, Transform> bones)
    {
        var human = new List<HumanBone>
        {
            Human(HumanBodyBones.Hips, "Hips"),
            Human(HumanBodyBones.Spine, "Spine"),
            Human(HumanBodyBones.Chest, "Chest"),
            Human(HumanBodyBones.Neck, "Neck"),
            Human(HumanBodyBones.Head, "Head"),
            Human(HumanBodyBones.LeftShoulder, "LeftShoulder"),
            Human(HumanBodyBones.RightShoulder, "RightShoulder"),
            Human(HumanBodyBones.LeftUpperArm, "LeftUpperArm"),
            Human(HumanBodyBones.RightUpperArm, "RightUpperArm"),
            Human(HumanBodyBones.LeftLowerArm, "LeftLowerArm"),
            Human(HumanBodyBones.RightLowerArm, "RightLowerArm"),
            Human(HumanBodyBones.LeftHand, "LeftHand"),
            Human(HumanBodyBones.RightHand, "RightHand"),
            Human(HumanBodyBones.LeftUpperLeg, "LeftUpperLeg"),
            Human(HumanBodyBones.RightUpperLeg, "RightUpperLeg"),
            Human(HumanBodyBones.LeftLowerLeg, "LeftLowerLeg"),
            Human(HumanBodyBones.RightLowerLeg, "RightLowerLeg"),
            Human(HumanBodyBones.LeftFoot, "LeftFoot"),
            Human(HumanBodyBones.RightFoot, "RightFoot")
        };

        var skeleton = new List<SkeletonBone>();
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            skeleton.Add(new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            });
        }

        HumanDescription description = new HumanDescription
        {
            human = human.ToArray(),
            skeleton = skeleton.ToArray(),
            hasTranslationDoF = false
        };
        description.armStretch = 0.05f;
        description.legStretch = 0.05f;
        description.feetSpacing = 0f;

        Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
        avatar.name = "PlayerCharacterV1Avatar";
        return avatar;
    }

    private static HumanBone Human(HumanBodyBones humanBone, string boneName)
    {
        return new HumanBone
        {
            humanName = HumanTrait.BoneName[(int)humanBone],
            boneName = boneName,
            limit = new HumanLimit { useDefaultValues = true }
        };
    }

    public static string WirePlayerPrefab()
    {
        return PatchPlayerPrefabYaml();
    }

    private static void SaveMesh(Mesh mesh)
    {
        if (mesh == null)
            return;
        if (File.Exists(AbsoluteAssetPath(MeshPath)))
            return;
        AssetDatabase.CreateAsset(mesh, MeshPath);
    }

    private static void SaveAvatar(Avatar avatar)
    {
        if (avatar == null)
            return;
        if (File.Exists(AbsoluteAssetPath(AvatarPath)))
            return;
        AssetDatabase.CreateAsset(avatar, AvatarPath);
    }

    private static int CountWeightedVertices(Mesh mesh)
    {
        if (mesh == null)
            return 0;
        BoneWeight[] weights = mesh.boneWeights;
        int count = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i].weight0 + weights[i].weight1 + weights[i].weight2 + weights[i].weight3 > 0.01f)
                count++;
        }

        return count;
    }

    private static string ProjectAssetsRoot()
    {
        string dataPath = Application.dataPath.Replace("\\", "/");
        int vp = dataPath.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
        if (vp >= 0)
            return dataPath.Substring(0, vp) + "/Assets";
        return dataPath;
    }

    private static string AbsoluteAssetPath(string assetPath)
    {
        string relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
            ? assetPath.Substring("Assets/".Length)
            : assetPath;
        return Path.Combine(ProjectAssetsRoot(), relative).Replace("\\", "/");
    }

    private static void WriteAllCopies(string assetPath, string contents)
    {
        string realPath = AbsoluteAssetPath(assetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(realPath) ?? ".");
        File.WriteAllText(realPath, contents, new UTF8Encoding(false));

        string editorPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)).Replace("\\", "/");
        if (!string.Equals(editorPath, realPath, StringComparison.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(editorPath) ?? ".");
            File.WriteAllText(editorPath, contents, new UTF8Encoding(false));
        }
    }

    private static string GuidFor(string assetPath)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (!string.IsNullOrEmpty(guid))
            return guid;

        string metaPath = AbsoluteAssetPath(assetPath) + ".meta";
        if (!File.Exists(metaPath))
            return string.Empty;

        foreach (string line in File.ReadAllLines(metaPath))
        {
            if (line.StartsWith("guid: ", StringComparison.Ordinal))
                return line.Substring(6).Trim();
        }

        return string.Empty;
    }

    private static string MaterialGuid(Material material)
    {
        if (material == null)
            return "73c176f402d2c2f4d929aa5da7585d17";
        string path = AssetDatabase.GetAssetPath(material);
        string guid = AssetDatabase.AssetPathToGUID(path);
        return string.IsNullOrEmpty(guid) ? "73c176f402d2c2f4d929aa5da7585d17" : guid;
    }

    private static void WriteRiggedPrefabYaml(
        GameObject root,
        SkinnedMeshRenderer skinned,
        string meshGuid,
        string avatarGuid,
        string controllerGuid,
        string materialGuid)
    {
        if (string.IsNullOrEmpty(meshGuid))
            meshGuid = GuidFor(MeshPath);
        if (string.IsNullOrEmpty(avatarGuid))
            avatarGuid = GuidFor(AvatarPath);
        if (string.IsNullOrEmpty(controllerGuid))
            controllerGuid = "8b7a6c5d4e3f2109a0b1c2d3e4f50607";
        if (string.IsNullOrEmpty(materialGuid))
            materialGuid = "73c176f402d2c2f4d929aa5da7585d17";

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        var goIds = new Dictionary<Transform, long>();
        var trIds = new Dictionary<Transform, long>();
        long nextId = 4100000001000000001;
        goIds[root.transform] = RootGameObjectFileId;
        trIds[root.transform] = RootTransformFileId;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == root.transform)
                continue;
            goIds[t] = nextId++;
            trIds[t] = nextId++;
        }

        long animatorId = nextId++;
        long visualRigId = nextId++;
        long smrId = nextId++;
        Transform meshTransform = skinned.transform;
        Bounds aabb = skinned.localBounds;
        if (aabb.extents.sqrMagnitude < 0.0001f && skinned.sharedMesh != null)
            aabb = skinned.sharedMesh.bounds;
        if (aabb.extents.sqrMagnitude < 0.0001f)
            aabb = new Bounds(new Vector3(0f, 0.88f, 0f), new Vector3(1.66f, 1.75f, 0.3f));

        var sb = new StringBuilder(64 * 1024);
        sb.Append("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n");

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            bool isRoot = t == root.transform;
            bool isMesh = t == meshTransform;
            sb.Append("--- !u!1 &").Append(goIds[t]).Append('\n');
            sb.Append("GameObject:\n");
            sb.Append("  m_ObjectHideFlags: 0\n");
            sb.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
            sb.Append("  m_PrefabInstance: {fileID: 0}\n");
            sb.Append("  m_PrefabAsset: {fileID: 0}\n");
            sb.Append("  serializedVersion: 6\n");
            sb.Append("  m_Component:\n");
            sb.Append("  - component: {fileID: ").Append(trIds[t]).Append("}\n");
            if (isRoot)
            {
                sb.Append("  - component: {fileID: ").Append(animatorId).Append("}\n");
                sb.Append("  - component: {fileID: ").Append(visualRigId).Append("}\n");
            }
            if (isMesh)
                sb.Append("  - component: {fileID: ").Append(smrId).Append("}\n");
            sb.Append("  m_Layer: 0\n");
            sb.Append("  m_Name: ").Append(t.name).Append('\n');
            sb.Append("  m_TagString: Untagged\n");
            sb.Append("  m_Icon: {fileID: 0}\n");
            sb.Append("  m_NavMeshLayer: 0\n");
            sb.Append("  m_StaticEditorFlags: 0\n");
            sb.Append("  m_IsActive: 1\n");

            sb.Append("--- !u!4 &").Append(trIds[t]).Append('\n');
            sb.Append("Transform:\n");
            sb.Append("  m_ObjectHideFlags: 0\n");
            sb.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
            sb.Append("  m_PrefabInstance: {fileID: 0}\n");
            sb.Append("  m_PrefabAsset: {fileID: 0}\n");
            sb.Append("  m_GameObject: {fileID: ").Append(goIds[t]).Append("}\n");
            sb.Append("  serializedVersion: 2\n");
            sb.Append("  m_LocalRotation: ").Append(Quat(t.localRotation)).Append('\n');
            sb.Append("  m_LocalPosition: ").Append(Vec(t.localPosition)).Append('\n');
            sb.Append("  m_LocalScale: ").Append(Vec(t.localScale)).Append('\n');
            sb.Append("  m_ConstrainProportionsScale: 0\n");
            sb.Append("  m_Children:\n");
            if (t.childCount == 0)
                sb.Append("  []\n");
            else
            {
                for (int c = 0; c < t.childCount; c++)
                    sb.Append("  - {fileID: ").Append(trIds[t.GetChild(c)]).Append("}\n");
            }
            sb.Append("  m_Father: {fileID: ").Append(t.parent == null ? 0 : trIds[t.parent]).Append("}\n");
            sb.Append("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n");
        }

        sb.Append("--- !u!95 &").Append(animatorId).Append('\n');
        sb.Append("Animator:\n");
        sb.Append("  serializedVersion: 7\n");
        sb.Append("  m_ObjectHideFlags: 0\n");
        sb.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
        sb.Append("  m_PrefabInstance: {fileID: 0}\n");
        sb.Append("  m_PrefabAsset: {fileID: 0}\n");
        sb.Append("  m_GameObject: {fileID: ").Append(RootGameObjectFileId).Append("}\n");
        sb.Append("  m_Enabled: 1\n");
        sb.Append("  m_Avatar: {fileID: 9000000, guid: ").Append(avatarGuid).Append(", type: 2}\n");
        sb.Append("  m_Controller: {fileID: 9100000, guid: ").Append(controllerGuid).Append(", type: 2}\n");
        sb.Append("  m_CullingMode: 0\n");
        sb.Append("  m_UpdateMode: 0\n");
        sb.Append("  m_ApplyRootMotion: 0\n");
        sb.Append("  m_LinearVelocityBlending: 0\n");
        sb.Append("  m_StabilizeFeet: 0\n");
        sb.Append("  m_AnimatePhysics: 0\n");
        sb.Append("  m_WarningMessage: \n");
        sb.Append("  m_HasTransformHierarchy: 1\n");
        sb.Append("  m_AllowConstantClipSamplingOptimization: 1\n");
        sb.Append("  m_KeepAnimatorStateOnDisable: 0\n");
        sb.Append("  m_WriteDefaultValuesOnDisable: 0\n");

        Transform FindNamed(string name)
        {
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name)
                    return transforms[i];
            }
            return null;
        }

        sb.Append("--- !u!114 &").Append(visualRigId).Append('\n');
        sb.Append("MonoBehaviour:\n");
        sb.Append("  m_ObjectHideFlags: 0\n");
        sb.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
        sb.Append("  m_PrefabInstance: {fileID: 0}\n");
        sb.Append("  m_PrefabAsset: {fileID: 0}\n");
        sb.Append("  m_GameObject: {fileID: ").Append(RootGameObjectFileId).Append("}\n");
        sb.Append("  m_Enabled: 1\n");
        sb.Append("  m_EditorHideFlags: 0\n");
        sb.Append("  m_Script: {fileID: 11500000, guid: 43f26f5967693b343bf7e82731fc48c8, type: 3}\n");
        sb.Append("  m_Name: \n");
        sb.Append("  m_EditorClassIdentifier: Assembly-CSharp::PlayerVisualRig\n");
        AppendTransformRef(sb, "rightHandWeaponSocket", FindNamed("RightHandWeaponSocket"), trIds);
        AppendTransformRef(sb, "leftHandIkTarget", FindNamed("LeftHandIKTarget"), trIds);
        AppendTransformRef(sb, "weaponHolsterSocket", FindNamed("WeaponHolsterSocket"), trIds);
        AppendTransformRef(sb, "backWeaponSocket", FindNamed("BackWeaponSocket"), trIds);
        AppendTransformRef(sb, "bullseyeHeadAnchor", FindNamed("BullseyeHeadAnchor"), trIds);
        AppendTransformRef(sb, "bullseyeUpperTorsoAnchor", FindNamed("BullseyeUpperTorsoAnchor"), trIds);
        AppendTransformRef(sb, "bullseyeLowerTorsoAnchor", FindNamed("BullseyeLowerTorsoAnchor"), trIds);
        AppendTransformRef(sb, "bullseyeLeftArmAnchor", FindNamed("BullseyeLeftArmAnchor"), trIds);
        AppendTransformRef(sb, "bullseyeRightArmAnchor", FindNamed("BullseyeRightArmAnchor"), trIds);
        AppendTransformRef(sb, "bullseyeLeftLegAnchor", FindNamed("BullseyeLeftLegAnchor"), trIds);
        AppendTransformRef(sb, "bullseyeRightLegAnchor", FindNamed("BullseyeRightLegAnchor"), trIds);

        sb.Append("--- !u!137 &").Append(smrId).Append('\n');
        sb.Append("SkinnedMeshRenderer:\n");
        sb.Append("  m_ObjectHideFlags: 0\n");
        sb.Append("  m_CorrespondingSourceObject: {fileID: 0}\n");
        sb.Append("  m_PrefabInstance: {fileID: 0}\n");
        sb.Append("  m_PrefabAsset: {fileID: 0}\n");
        sb.Append("  m_GameObject: {fileID: ").Append(goIds[meshTransform]).Append("}\n");
        sb.Append("  m_Enabled: 1\n");
        sb.Append("  m_CastShadows: 1\n");
        sb.Append("  m_ReceiveShadows: 1\n");
        sb.Append("  m_DynamicOccludee: 1\n");
        sb.Append("  m_StaticShadowCaster: 0\n");
        sb.Append("  m_MotionVectors: 1\n");
        sb.Append("  m_LightProbeUsage: 1\n");
        sb.Append("  m_ReflectionProbeUsage: 1\n");
        sb.Append("  m_RayTracingMode: 3\n");
        sb.Append("  m_RayTraceProcedural: 0\n");
        sb.Append("  m_RayTracingAccelStructBuildFlagsOverride: 0\n");
        sb.Append("  m_RayTracingAccelStructBuildFlags: 1\n");
        sb.Append("  m_SmallMeshCulling: 1\n");
        sb.Append("  m_ForceMeshLod: -1\n");
        sb.Append("  m_MeshLodSelectionBias: 0\n");
        sb.Append("  m_RenderingLayerMask: 1\n");
        sb.Append("  m_RendererPriority: 0\n");
        sb.Append("  m_Materials:\n");
        sb.Append("  - {fileID: 2100000, guid: ").Append(materialGuid).Append(", type: 2}\n");
        sb.Append("  m_StaticBatchInfo:\n");
        sb.Append("    firstSubMesh: 0\n");
        sb.Append("    subMeshCount: 0\n");
        sb.Append("  m_StaticBatchRoot: {fileID: 0}\n");
        sb.Append("  m_ProbeAnchor: {fileID: 0}\n");
        sb.Append("  m_LightProbeVolumeOverride: {fileID: 0}\n");
        sb.Append("  m_ScaleInLightmap: 1\n");
        sb.Append("  m_ReceiveGI: 1\n");
        sb.Append("  m_PreserveUVs: 0\n");
        sb.Append("  m_IgnoreNormalsForChartDetection: 0\n");
        sb.Append("  m_ImportantGI: 0\n");
        sb.Append("  m_StitchLightmapSeams: 1\n");
        sb.Append("  m_SelectedEditorRenderState: 3\n");
        sb.Append("  m_MinimumChartSize: 4\n");
        sb.Append("  m_AutoUVMaxDistance: 0.5\n");
        sb.Append("  m_AutoUVMaxAngle: 89\n");
        sb.Append("  m_LightmapParameters: {fileID: 0}\n");
        sb.Append("  m_GlobalIlluminationMeshLod: 0\n");
        sb.Append("  m_SortingLayerID: 0\n");
        sb.Append("  m_SortingLayer: 0\n");
        sb.Append("  m_SortingOrder: 0\n");
        sb.Append("  m_MaskInteraction: 0\n");
        sb.Append("  m_AdditionalVertexStreams: {fileID: 0}\n");
        sb.Append("  serializedVersion: 2\n");
        sb.Append("  m_Quality: 0\n");
        sb.Append("  m_UpdateWhenOffscreen: 1\n");
        sb.Append("  m_SkinnedMotionVectors: 1\n");
        sb.Append("  m_Mesh: {fileID: 4300000, guid: ").Append(meshGuid).Append(", type: 2}\n");
        sb.Append("  m_Bones:\n");
        Transform[] bones = skinned.bones;
        for (int i = 0; i < bones.Length; i++)
            sb.Append("  - {fileID: ").Append(trIds[bones[i]]).Append("}\n");
        sb.Append("  m_BlendShapeWeights: []\n");
        sb.Append("  m_RootBone: {fileID: ").Append(trIds[skinned.rootBone]).Append("}\n");
        sb.Append("  m_AABB:\n");
        sb.Append("    m_Center: ").Append(Vec(aabb.center)).Append('\n');
        sb.Append("    m_Extent: ").Append(Vec(aabb.extents)).Append('\n');
        sb.Append("  m_DirtyAABB: 0\n");

        WriteAllCopies(RiggedPrefabPath, sb.ToString());
        WriteAllCopies(RiggedPrefabPath + ".meta",
            "fileFormatVersion: 2\n" +
            "guid: " + RiggedPrefabGuid + "\n" +
            "PrefabImporter:\n" +
            "  externalObjects: {}\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n");
    }

    private static void AppendTransformRef(
        StringBuilder sb,
        string field,
        Transform transform,
        Dictionary<Transform, long> trIds)
    {
        sb.Append("  ").Append(field).Append(": ");
        if (transform == null)
            sb.Append("{fileID: 0}\n");
        else
            sb.Append("{fileID: ").Append(trIds[transform]).Append("}\n");
    }

    private static string Vec(Vector3 v)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{{x: {0}, y: {1}, z: {2}}}",
            v.x, v.y, v.z);
    }

    private static string Quat(Quaternion q)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{{x: {0}, y: {1}, z: {2}, w: {3}}}",
            q.x, q.y, q.z, q.w);
    }

    private static string PatchPlayerPrefabYaml()
    {
        const string playerAsset = "Assets/Player/Player.prefab";
        string path = AbsoluteAssetPath(playerAsset);
        if (!File.Exists(path))
            return "FAILED: missing Player.prefab at " + path;

        string yaml = File.ReadAllText(path);
        yaml = yaml.Replace(
            "guid: 93edeac14d0000b48881d531f5454a89",
            "guid: " + RiggedPrefabGuid);

        const string animationStateId = "3340119340010000101";
        const string thirdPersonId = "3340119340010000102";
        if (!yaml.Contains("Assembly-CSharp::PlayerAnimationState"))
        {
            yaml = yaml.Replace(
                "  - component: {fileID: 8243981151345696602}\n  m_Layer: 0\n  m_Name: Player",
                "  - component: {fileID: 8243981151345696602}\n  - component: {fileID: " +
                animationStateId + "}\n  - component: {fileID: " + thirdPersonId +
                "}\n  m_Layer: 0\n  m_Name: Player");

            string block =
                "--- !u!114 &" + animationStateId + "\n" +
                "MonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: 3422818454917793740}\n" +
                "  m_Enabled: 1\n" +
                "  m_EditorHideFlags: 0\n" +
                "  m_Script: {fileID: 11500000, guid: 42afa890c4313f94d856481f4acac928, type: 3}\n" +
                "  m_Name: \n" +
                "  m_EditorClassIdentifier: Assembly-CSharp::PlayerAnimationState\n" +
                "  ShowTopMostFoldoutHeaderGroup: 1\n" +
                "--- !u!114 &" + thirdPersonId + "\n" +
                "MonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n" +
                "  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n" +
                "  m_PrefabAsset: {fileID: 0}\n" +
                "  m_GameObject: {fileID: 3422818454917793740}\n" +
                "  m_Enabled: 1\n" +
                "  m_EditorHideFlags: 0\n" +
                "  m_Script: {fileID: 11500000, guid: 4fc147ed5a567344a98b5b88d06c76ee, type: 3}\n" +
                "  m_Name: \n" +
                "  m_EditorClassIdentifier: Assembly-CSharp::PlayerThirdPersonAnimator\n" +
                "  thirdPersonAnimator: {fileID: 0}\n" +
                "  animationState: {fileID: " + animationStateId + "}\n" +
                "  playerHealth: {fileID: 3086871669323907386}\n" +
                "  coordinator: {fileID: 3788039708433434490}\n" +
                "  firePresentationDuration: 0.12\n";

            yaml = yaml.Replace(
                "--- !u!1 &3625492656395199349",
                block + "--- !u!1 &3625492656395199349");
        }

        WriteAllCopies(playerAsset, yaml);
        bool swapped = yaml.Contains("guid: " + RiggedPrefabGuid);
        bool hasState = yaml.Contains("Assembly-CSharp::PlayerAnimationState");
        bool hasAnimator = yaml.Contains("Assembly-CSharp::PlayerThirdPersonAnimator");
        return "Player prefab patched. visual=" + swapped + " animationState=" + hasState + " thirdPerson=" + hasAnimator;
    }
}
