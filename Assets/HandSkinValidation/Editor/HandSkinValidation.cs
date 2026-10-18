using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class HandSkinValidation
{
    const string Folder = "Assets/Art/Models/Characters/PokerCharacter";
    const string Model = Folder + "/Models/PokerCharacter.fbx";
    const string TestFolder = "Assets/HandSkinValidation";
    static string Normalize(string s) => s.Replace("_", "").Replace(" ", "").ToLowerInvariant().Replace("little", "pinky");

    [MenuItem("Tools/Hand Skin Validation/1 Import and Map Humanoid")]
    public static void Import()
    {
        AssetDatabase.Refresh();
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.isReadable = true;
        importer.importAnimation = false;
        importer.optimizeGameObjects = false;
        importer.skinWeights = ModelImporterSkinWeights.Custom;
        importer.maxBonesPerVertex = 8;
        importer.minBoneWeight = 0.00001f;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.SaveAndReimport();
        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        try
        {
            instance.name = AssetDatabase.LoadAssetAtPath<GameObject>(Model).name;
            var transforms = instance.GetComponentsInChildren<Transform>(true);
            var lookup = transforms.ToDictionary(t => Normalize(t.name));
            // Supply a T-pose to the Avatar definition while preserving the mesh bind poses.
            foreach (var side in new[] { "Left", "Right" })
            {
                var upper = lookup[Normalize(side + "UpperArm")];
                var lower = lookup[Normalize(side + "LowerArm")];
                var hand = lookup[Normalize(side + "Hand")];
                var outward = (upper.position.x > instance.transform.position.x ? Vector3.right : Vector3.left);
                upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, outward) * upper.rotation;
                lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, outward) * lower.rotation;
            }
            var human = HumanTrait.BoneName.Where(n => lookup.ContainsKey(Normalize(n)))
                .Select(n => new HumanBone { humanName = n, boneName = lookup[Normalize(n)].name,
                    limit = new HumanLimit { useDefaultValues = true } }).ToArray();
            var description = new HumanDescription
            {
                human = human,
                skeleton = transforms.Select(t => new SkeletonBone { name = t.name,
                    position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                armStretch = .05f, legStretch = .05f, feetSpacing = 0
            };
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.humanDescription = description;
            importer.SaveAndReimport();
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
        var report = new StringBuilder();
        report.AppendLine("Avatar: " + (avatar ? avatar.name : "MISSING"));
        report.AppendLine("isValid=" + (avatar && avatar.isValid) + ", isHuman=" + (avatar && avatar.isHuman));
        var hd = importer.humanDescription;
        foreach (var b in hd.human) report.AppendLine(b.humanName + " -> " + b.boneName);
        for (int i = 0; i < HumanTrait.BoneCount; i++)
            if (HumanTrait.RequiredBone(i) && !hd.human.Any(b => b.humanName == HumanTrait.BoneName[i]))
                report.AppendLine("MISSING REQUIRED: " + HumanTrait.BoneName[i]);
        report.AppendLine("MUSCLES:");
        report.AppendLine(string.Join("\n", HumanTrait.MuscleName));
        Directory.CreateDirectory("Art/HandSkinReview/Unity");
        File.WriteAllText("Art/HandSkinReview/Unity/avatar-mapping.txt", report.ToString());
        Debug.Log("HandSkin: " + report);
        if (!avatar || !avatar.isValid || !avatar.isHuman) throw new Exception("Invalid Humanoid Avatar");
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Tools/Hand Skin Validation/2 Build and Sample Animation")]
    public static void BuildTest()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().First();
        if (!avatar.isHuman || !avatar.isValid) throw new Exception("Avatar invalid");
        var oldScene = SceneManager.GetActiveScene();
        var previousTest = SceneManager.GetSceneByPath(TestFolder + "/HandSkinHumanoidTest.unity");
        if (previousTest.IsValid() && previousTest.isLoaded) EditorSceneManager.CloseScene(previousTest, true);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = "HandSkinHumanoidTest";
        SceneManager.SetActiveScene(scene);
        var character = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        character.name = "Optimized Humanoid - Hand Animation Test";
        foreach (var t in character.GetComponentsInChildren<Transform>()) t.gameObject.layer = 31;
        var animator = character.GetComponent<Animator>();
        if (!animator) animator = character.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        var clip = new AnimationClip { name = "Humanoid_Hands_Open_Fist_Opposition", frameRate = 30 };
        foreach (string muscle in HumanTrait.MuscleName)
        {
            float[] values = { 0, 0, 0, 0, 0 };
            if (muscle.Contains("Stretched"))
                values = muscle.Contains("Thumb") ? new[] { .5f, -.5f, .0f, -.65f, .5f }
                    : new[] { .7f, -.75f, .2f, -.25f, .7f };
            if (muscle.Contains("Thumb Spread")) values = new[] { .5f, -.3f, -.75f, -.6f, .5f };
            if (muscle.EndsWith("Arm Down-Up")) values = Enumerable.Repeat(-.35f, 5).ToArray();
            if (muscle.EndsWith("Forearm Stretch")) values = Enumerable.Repeat(.55f, 5).ToArray();
            if (muscle.Contains("Hand Down-Up")) values = new[] { 0f, -.3f, .3f, 0f, 0f };
            string property = muscle;
            if (muscle.Contains("Stretched") || muscle.Contains(" Spread"))
            {
                var parts = muscle.Split(' ');
                property = parts[0] + "Hand." + parts[1] + "." + string.Join(" ", parts.Skip(2));
            }
            clip.SetCurve("", typeof(Animator), property,
                new AnimationCurve(Enumerable.Range(0, 5).Select(i => new Keyframe(i * 2, values[i])).ToArray()));
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.DeleteAsset(TestFolder + "/HandDeformation.anim");
        AssetDatabase.CreateAsset(clip, TestFolder + "/HandDeformation.anim");
        AssetDatabase.DeleteAsset(TestFolder + "/HandDeformation.controller");
        var controller = AnimatorController.CreateAnimatorControllerAtPath(TestFolder + "/HandDeformation.controller");
        controller.AddMotion(clip); animator.runtimeAnimatorController = controller;
        var shader = Shader.Find("HandSkinValidation/NeutralReview");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(TestFolder + "/CharacterReview.mat");
        if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, TestFolder + "/CharacterReview.mat"); }
        mat.shader = shader;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .2f);
        foreach (var renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>())
        { renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => mat).ToArray(); renderer.updateWhenOffscreen = true; }
        var light = new GameObject("Review Light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.4f; light.cullingMask = 1 << 31;
        light.transform.rotation = Quaternion.Euler(35, -35, 0);
        var camera = new GameObject("Hand Review Camera").AddComponent<Camera>();
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.14f, .16f, .19f); camera.nearClipPlane = .001f;
        camera.farClipPlane = 20; camera.orthographic = true; camera.orthographicSize = .13f;
        var cameraData = camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        cameraData.SetRenderer(0); cameraData.renderPostProcessing = false;
        animator.runtimeAnimatorController = null;
        var graph = PlayableGraph.Create("HandSkin validation samples");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        var output = AnimationPlayableOutput.Create(graph, "Humanoid", animator);
        output.SetSourcePlayable(playable); graph.Play();
        var report = new StringBuilder();
        report.AppendLine("Humanoid clip=" + clip.humanMotion + "; Avatar valid=" + avatar.isValid);
        var skin = character.GetComponentInChildren<SkinnedMeshRenderer>();
        var baked = new Mesh();
        Vector3[] firstVertices = null;
        var fingerBones = Enumerable.Range((int)HumanBodyBones.LeftThumbProximal, 30).Select(i => animator.GetBoneTransform((HumanBodyBones)i)).ToArray();
        var firstRotations = new Quaternion[30];
        var maximumAngles = new float[30];
        var oldWeights = QualitySettings.skinWeights;
        try
        {
            QualitySettings.skinWeights = SkinWeights.Unlimited;
            for (int frame = 0; frame <= 240; frame++)
            {
                float time = frame / 30f;
                playable.SetTime(time); graph.Evaluate(0);
                for (int finger = 0; finger < 30; finger++)
                {
                    if (frame == 0) firstRotations[finger] = fingerBones[finger].localRotation;
                    maximumAngles[finger] = Mathf.Max(maximumAngles[finger], Quaternion.Angle(firstRotations[finger], fingerBones[finger].localRotation));
                }
                skin.BakeMesh(baked);
                var vertices = baked.vertices;
                if (vertices.Any(v => float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z)))
                    throw new Exception("Non-finite skinned vertex at " + time);
                if (frame == 0) firstVertices = vertices;
                if (frame % 60 == 0)
                {
                    float displacement = vertices.Select((v, i) => Vector3.Distance(v, firstVertices[i])).Max();
                    report.AppendLine("t=" + time + ", vertices=" + vertices.Length + ", max displacement=" + displacement);
                    report.AppendLine("Index proximal=" + animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).localRotation.eulerAngles + "; little proximal=" + animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal).localRotation.eulerAngles);
                    foreach (var side in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                    {
                        // BakeMesh is the evaluated Animator result; render it explicitly in edit mode,
                        // where GPU skinning may otherwise retain the previous editor-update pose.
                        var sideName = side == HumanBodyBones.LeftHand ? "Left_" : "Right_";
                        var handBones = skin.bones.Select(b => b.name.StartsWith(sideName) &&
                            (b.name.Contains("Hand") || b.name.Contains("Thumb") || b.name.Contains("Index") ||
                             b.name.Contains("Middle") || b.name.Contains("Ring") || b.name.Contains("Pinky"))).ToArray();
                        var weights = skin.sharedMesh.boneWeights;
                        var mask = weights.Select(w =>
                            (handBones[w.boneIndex0] ? w.weight0 : 0) + (handBones[w.boneIndex1] ? w.weight1 : 0) +
                            (handBones[w.boneIndex2] ? w.weight2 : 0) + (handBones[w.boneIndex3] ? w.weight3 : 0) > .25f).ToArray();
                        var tris = baked.triangles;
                        var handMesh = UnityEngine.Object.Instantiate(baked);
                        handMesh.subMeshCount = 1;
                        handMesh.triangles = Enumerable.Range(0, tris.Length / 3)
                            .Where(i => mask[tris[3*i]] && mask[tris[3*i+1]] && mask[tris[3*i+2]])
                            .SelectMany(i => new[] { tris[3*i], tris[3*i+1], tris[3*i+2] }).ToArray();
                        var snapshot = new GameObject("Temporary evaluated hand"); snapshot.layer = 31;
                        snapshot.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
                        snapshot.transform.localScale = Vector3.one;
                        snapshot.AddComponent<MeshFilter>().sharedMesh = handMesh;
                        snapshot.AddComponent<MeshRenderer>().sharedMaterial = mat;
                        skin.enabled = false;
                        var hand = animator.GetBoneTransform(side);
                        var middle = animator.GetBoneTransform(side == HumanBodyBones.LeftHand ? HumanBodyBones.LeftMiddleIntermediate : HumanBodyBones.RightMiddleIntermediate);
                        var target = Vector3.Lerp(hand.position, middle.position, .65f);
                        report.AppendLine(side + " hand triangles=" + handMesh.triangles.Length / 3 + "; bakedBounds=" + handMesh.bounds + "; target=" + target + "; skinScale=" + skin.transform.lossyScale);
                        var index = animator.GetBoneTransform(side == HumanBodyBones.LeftHand ? HumanBodyBones.LeftIndexProximal : HumanBodyBones.RightIndexProximal);
                        var little = animator.GetBoneTransform(side == HumanBodyBones.LeftHand ? HumanBodyBones.LeftLittleProximal : HumanBodyBones.RightLittleProximal);
                        var normal = Vector3.Cross(index.position - hand.position, little.position - hand.position).normalized;
                        for (int view = 0; view < 2; view++)
                        {
                            camera.transform.position = target + normal * (view == 0 ? .3f : -.3f);
                            camera.transform.LookAt(target, (hand.position - middle.position).normalized);
                            Capture(camera, "Art/HandSkinReview/Unity/" + side + "_" + frame + (view == 0 ? "" : "_reverse") + ".png");
                        }
                        skin.enabled = true;
                        UnityEngine.Object.DestroyImmediate(snapshot); UnityEngine.Object.DestroyImmediate(handMesh);
                    }
                }
            }
            report.AppendLine("241 frames sampled through Unity AnimationClipPlayable; no NaN/Infinity vertices.");
        }
        finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(baked); QualitySettings.skinWeights = oldWeights; }
        animator.runtimeAnimatorController = controller;
        character.AddComponent<HandSkinRuntimeProbe>();
        report.AppendLine("Finger joints moving more than 1 degree=" + maximumAngles.Count(a => a > 1) + "/30");
        for (int finger = 0; finger < 30; finger++) report.AppendLine(fingerBones[finger].name + ": " + maximumAngles[finger].ToString("F2") + " degrees");
        report.AppendLine("Mapped finger transforms=" + Enumerable.Range((int)HumanBodyBones.LeftThumbProximal, 30).Count(i => animator.GetBoneTransform((HumanBodyBones)i)));
        File.WriteAllText("Art/HandSkinReview/Unity/animation-validation.txt", report.ToString());
        PrefabUtility.SaveAsPrefabAsset(character, TestFolder + "/OptimizedHumanoid.prefab");
        EditorSceneManager.SaveScene(scene, TestFolder + "/HandSkinHumanoidTest.unity");
        SceneManager.SetActiveScene(oldScene);
        Selection.activeGameObject = character;
        AssetDatabase.SaveAssets();
        Debug.Log("HandSkin animation validation: " + report);
    }

    [MenuItem("Tools/Hand Skin Validation/3 Finalize Textured Character")]
    public static void FinalizeCharacter()
    {
        var materialPath = Folder + "/Materials/PokerCharacter.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, materialPath); }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/PokerCharacter_BaseColor_Review.png"));
        material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .25f);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "tripo_mat_dc21eaf7"), material);
        importer.SaveAndReimport();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        try
        {
            foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(go, Folder + "/Models/PokerCharacter_Visual.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        AssetDatabase.SaveAssets();
        Debug.Log("HandSkin textured prefab finalized");
    }

    static void Capture(Camera camera, string path)
    {
        var rt = RenderTexture.GetTemporary(900, 900, 24);
        var previous = RenderTexture.active;
        var texture = new Texture2D(900, 900, TextureFormat.RGB24, false);
        try
        {
            camera.cameraType = CameraType.Preview;
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { camera.cameraType = CameraType.Game; camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(texture); }
    }

    [MenuItem("Tools/Hand Skin Validation/4 Play Test Scene")]
    public static void PlayTestScene()
    {
        SessionState.SetString("HandSkinPreviousPlayScene", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestFolder + "/HandSkinHumanoidTest.unity");
        EditorApplication.isPlaying = true;
    }
    [MenuItem("Tools/Hand Skin Validation/5 Restore Play Scene Setting")]
    public static void RestorePlayScene()
    {
        string path = SessionState.GetString("HandSkinPreviousPlayScene", "");
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        SessionState.EraseString("HandSkinPreviousPlayScene");
    }
}
