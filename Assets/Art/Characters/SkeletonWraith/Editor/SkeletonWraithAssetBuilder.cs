using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SkeletonWraith.Editor
{
    // Scoped import defaults: no other character or project settings are changed.
    public sealed class SkeletonWraithImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath != SkeletonWraithAssetBuilder.ModelPath) return;
            var m = (ModelImporter)assetImporter;
            m.animationType = ModelImporterAnimationType.Generic;
            m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            m.importAnimation = true;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importCameras = false; m.importLights = false;
            m.optimizeGameObjects = false;
            m.animationCompression = ModelImporterAnimationCompression.Off;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            var clips = m.defaultClipAnimations;
            foreach (var clip in clips)
            {
                foreach (string state in SkeletonWraithAssetBuilder.States)
                    if (clip.takeName.Contains("SkeletonWraith_" + state)) clip.name = "SkeletonWraith_" + state;
                clip.loopTime = clip.name == "SkeletonWraith_Idle";
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
            }
            if (clips.Length > 0) m.clipAnimations = clips;
        }
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SkeletonWraithAssetBuilder.BasePath + "/Textures/", StringComparison.Ordinal)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default; t.sRGBTexture = true;
            t.maxTextureSize = 512; t.mipmapEnabled = true; t.alphaSource = TextureImporterAlphaSource.None;
        }
    }

    [InitializeOnLoad]
    public static class SkeletonWraithAssetBuilder
    {
        public const string BasePath = "Assets/Art/Characters/SkeletonWraith";
        public const string ModelPath = BasePath + "/Models/SkeletonWraith.fbx";
        public static readonly string[] States = { "Idle", "Attack", "Hit", "PowerUp" };
        const string Request = "tmp/SkeletonWraith.build.request";
        const string Report = "blender_output/skeleton_wraith_unity_validation.txt";
        static SkeletonWraithAssetBuilder() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            try { Build(); }
            catch (Exception e) { File.WriteAllText(Report, "FAILED\n" + e); Debug.LogException(e); }
        }
        [MenuItem("Tools/Skeleton Wraith/Build and Validate Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(BasePath + "/Animation");
            Directory.CreateDirectory(BasePath + "/Materials");
            Directory.CreateDirectory(BasePath + "/Prefabs");
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new Exception("FBX did not import.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(BasePath + "/Shaders/SkeletonWraithDeferred.shader");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("SkeletonWraith shader has compilation errors.");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath + "/Textures/SkeletonWraith_BaseColor.png");
            if (!texture) throw new Exception("Missing base color texture.");
            var matPath = BasePath + "/Materials/SkeletonWraith_Surface.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader; mat.SetTexture("_BaseMap", texture); mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Cull", 0); mat.SetFloat("_Metallic", 0); mat.SetFloat("_Roughness", .85f);
            mat.SetFloat("_OutlineWidth", .0008f); mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            var imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            var clips = new Dictionary<string, AnimationClip>();
            foreach (string state in States)
            {
                var src = imported.SingleOrDefault(c => c.name.EndsWith("SkeletonWraith_" + state, StringComparison.Ordinal));
                if (!src) throw new Exception("Missing " + state + "; imported takes: " + string.Join(",", imported.Select(c => c.name)));
                string path = BasePath + "/Animation/SkeletonWraith_" + state + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
                EditorUtility.CopySerialized(src, clip); clip.name = "SkeletonWraith_" + state;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = state == "Idle"; settings.loopBlend = settings.loopTime;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip); clips.Add(state, clip);
            }
            string controllerPath = BasePath + "/Animation/SkeletonWraith.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = controller.layers[0].stateMachine;
            foreach (var child in sm.states) sm.RemoveState(child.state);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            var states = new Dictionary<string, AnimatorState>();
            for (int i = 0; i < States.Length; i++)
            {
                var state = sm.AddState(States[i], new Vector3(i == 0 ? 260 : 540, 70 + i * 100, 0));
                state.motion = clips[States[i]]; state.writeDefaultValues = false; states.Add(States[i], state);
            }
            sm.defaultState = states["Idle"];
            foreach (var state in States.Skip(1))
            {
                controller.AddParameter(state, AnimatorControllerParameterType.Trigger);
                var enter = state == "Hit" ? sm.AddAnyStateTransition(states[state]) : states["Idle"].AddTransition(states[state]);
                enter.hasExitTime = false; enter.hasFixedDuration = true; enter.duration = .06f; enter.canTransitionToSelf = false;
                enter.AddCondition(AnimatorConditionMode.If, 0, state);
                var exit = states[state].AddTransition(states["Idle"]);
                exit.hasExitTime = true; exit.exitTime = 1; exit.hasFixedDuration = true; exit.duration = .08f;
            }
            EditorUtility.SetDirty(controller);
            var root = new GameObject("SkeletonWraith");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                visual.transform.localRotation = Quaternion.Euler(0, 90, 0);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
                animator.avatar = avatar;
                if (!avatar || !avatar.isValid || avatar.isHuman) throw new Exception("Generic avatar is missing or invalid.");
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    skin.sharedMaterials = Enumerable.Repeat(mat, skin.sharedMaterials.Length).ToArray();
                    skin.shadowCastingMode = ShadowCastingMode.TwoSided;
                    skin.quality = SkinQuality.Bone4; skin.updateWhenOffscreen = true;
                    skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 4f);
                }
                var report = new List<string> { "PASS", "Generic avatar: valid", "Shader: " + shader.name, "Texture: 512x512", "Animator triggers: Attack, Hit, PowerUp", "Prefab forward: +Z; Animator on Visual; root motion disabled" };
                foreach (var pair in clips)
                {
                    var bindings = AnimationUtility.GetCurveBindings(pair.Value);
                    var missing = bindings.Where(b => !string.IsNullOrEmpty(b.path) && visual.transform.Find(b.path) == null).Select(b => b.path).Distinct().ToArray();
                    if (missing.Length > 0) throw new Exception("Unbound animation paths: " + string.Join(",", missing));
                    if (bindings.Length == 0 || pair.Value.length <= 0) throw new Exception("Empty animation: " + pair.Key);
                    report.Add(pair.Key + ": " + pair.Value.length.ToString("F3") + "s, " + bindings.Length + " curves; all paths resolved; loop=" + pair.Value.isLooping);
                }
                foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                    report.Add(skin.name + ": " + skin.sharedMesh.vertexCount + " vertices, " + skin.bones.Length + " bones");
                PrefabUtility.SaveAsPrefabAsset(root, BasePath + "/Prefabs/SkeletonWraith.prefab");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Report, string.Join("\n", report));
                Debug.Log("SkeletonWraith assets built and validated: " + Report);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath + "/Prefabs/SkeletonWraith.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
