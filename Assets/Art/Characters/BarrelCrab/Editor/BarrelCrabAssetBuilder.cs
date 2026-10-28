using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HarborCharacters.Editor
{
    public sealed class BarrelCrabImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (assetPath != BarrelCrabAssetBuilder.ModelPath) return;
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
            m.isReadable = true;
            var clips = m.defaultClipAnimations;
            foreach (var c in clips)
            {
                int i = Array.FindIndex(BarrelCrabAssetBuilder.Takes, t => c.takeName.Contains(t) || c.name.Contains(t));
                if (i < 0) continue;
                c.name = "BarrelCrab_" + BarrelCrabAssetBuilder.States[i];
                c.lastFrame = c.firstFrame + BarrelCrabAssetBuilder.EndFrames[i] - 1;
                c.loopTime = i == 0 || i == 4; c.loopPose = c.loopTime;
                c.lockRootRotation = true; c.lockRootHeightY = true; c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
            }
            if (clips.Length > 0) m.clipAnimations = clips;
        }
        void OnPreprocessTexture()
        {
            if (assetPath != BarrelCrabAssetBuilder.BasePath + "/Textures/T_BarrelCrab_BaseColor.jpg") return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default; t.sRGBTexture = true;
            t.maxTextureSize = 1024; t.mipmapEnabled = true;
            t.alphaSource = TextureImporterAlphaSource.None;
        }
    }

    public static class BarrelCrabAssetBuilder
    {
        public const string BasePath = "Assets/Art/Characters/BarrelCrab";
        public const string ModelPath = BasePath + "/Models/BarrelCrab.fbx";
        public const string PrefabPath = BasePath + "/Prefabs/BarrelCrab_Visual.prefab";
        const string Output = "output/barrel-crab-unity";
        public static readonly string[] States = { "Idle", "Attack", "Hit", "Retract", "ShelledIdle", "ShelledHit", "Emerge" };
        public static readonly string[] Takes = { "BarrelCrab_Idle_Loop", "BarrelCrab_Attack_Claw", "BarrelCrab_Hit_Normal", "BarrelCrab_Retract_IntoShell", "BarrelCrab_Shelled_Idle", "BarrelCrab_Hit_Shelled", "BarrelCrab_Emerge_FromShell" };
        public static readonly int[] EndFrames = { 49, 24, 20, 20, 25, 18, 32 };
        static string ClipPath(string state) => BasePath + "/Animation/BarrelCrab_" + state + ".anim";

        [MenuItem("Tools/Harbor/Barrel Crab/Build Visual Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            try { BuildInternal(); }
            catch (Exception e) { File.WriteAllText(Output + "/build.txt", "FAILED\n" + e); Debug.LogException(e); }
        }
        static void BuildInternal()
        {
            foreach (string dir in new[] { "Animation", "Materials", "Prefabs" }) Directory.CreateDirectory(BasePath + "/" + dir);
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (!importer) throw new Exception("Missing FBX");
            importer.SaveAndReimport();
            var takes = importer.defaultClipAnimations;
            foreach (var take in takes)
            {
                int i = Array.FindIndex(Takes, t => take.name.Contains(t) || take.takeName.Contains(t));
                if (i < 0) throw new Exception("Unknown take " + take.name + "/" + take.takeName);
                take.name = "BarrelCrab_" + States[i];
                take.lastFrame = take.firstFrame + EndFrames[i] - 1;
                take.loopTime = i == 0 || i == 4; take.loopPose = take.loopTime;
                take.lockRootRotation = true; take.lockRootHeightY = true; take.lockRootPositionXZ = true;
                take.keepOriginalOrientation = true; take.keepOriginalPositionY = true; take.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = takes;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var shader = Shader.Find("Rendering/Character Deferred Outline");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Character deferred shader unavailable or has errors");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath + "/Textures/T_BarrelCrab_BaseColor.jpg");
            var bodyMat = Material("M_BarrelCrab", shader);
            bodyMat.SetTexture("_BaseMap", texture); bodyMat.SetColor("_BaseColor", Color.white);
            bodyMat.SetColor("_EmissionColor", Color.black); bodyMat.SetFloat("_Roughness", 1);
            bodyMat.SetFloat("_Metallic", 0); bodyMat.SetFloat("_Cull", 0); bodyMat.SetFloat("_OutlineWidth", .0008f);
            var eyeMat = Material("M_BarrelCrab_Eyes", shader);
            eyeMat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            eyeMat.SetColor("_BaseColor", new Color(.65f,.55f,.3f,1));
            eyeMat.SetColor("_EmissionColor", new Color(2f,1.55f,.65f,1));
            eyeMat.SetFloat("_Roughness", 1); eyeMat.SetFloat("_Metallic", 0);
            eyeMat.SetFloat("_Cull", 2); eyeMat.SetFloat("_OutlineWidth", 0);
            foreach (var mat in new[] { bodyMat, eyeMat }) { mat.enableInstancing = true; EditorUtility.SetDirty(mat); }
            var imported = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            var clips = new Dictionary<string, AnimationClip>();
            for (int i = 0; i < States.Length; i++)
            {
                string state = States[i];
                var src = imported.SingleOrDefault(c => c.name == "BarrelCrab_" + state);
                if (!src) throw new Exception("Missing take " + state + ": " + string.Join(",", imported.Select(c => c.name)));
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(state));
                if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, ClipPath(state)); }
                EditorUtility.CopySerialized(src, clip); clip.name = "BarrelCrab_" + state;
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = state == "Idle" || state == "ShelledIdle"; settings.loopBlend = settings.loopTime;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip); clips[state] = clip;
            }
            var controller = Controller(clips);
            var root = new GameObject("BarrelCrab_Visual");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                var animator = visual.GetComponent<Animator>() ?? visual.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().Single();
                if (!animator.avatar.isValid || animator.avatar.isHuman) throw new Exception("Invalid Generic avatar");
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (skins.Length != 2) throw new Exception("Expected body + eyes, got " + skins.Length);
                var eyes = skins.Single(s => s.name == "SM_BarrelCrab_Eyes");
                var body = skins.Single(s => s.name == "SM_BarrelCrab_Body");
                foreach (var skin in skins)
                {
                    skin.sharedMaterials = new[] { skin == eyes ? eyeMat : bodyMat };
                    skin.quality = SkinQuality.Bone1; skin.updateWhenOffscreen = false;
                    skin.shadowCastingMode = skin == eyes ? ShadowCastingMode.Off : ShadowCastingMode.TwoSided;
                    skin.lightProbeUsage = LightProbeUsage.BlendProbes;
                }
                animator.enabled = false; clips["Idle"].SampleAnimation(visual, 0);
                // FBX export maps the source front to Unity +Z.
                visual.transform.localRotation = Quaternion.identity;
                // Union deformed local bounds across every candidate, not just the bind pose.
                var bounds = new Dictionary<SkinnedMeshRenderer, Bounds>();
                foreach (var clip in clips.Values)
                    for (int frame = 0; frame <= Mathf.CeilToInt(clip.length * 24); frame++)
                    {
                        clip.SampleAnimation(visual, Mathf.Min(clip.length, frame / 24f));
                        foreach (var skin in skins)
                        {
                            var baked = new Mesh(); skin.BakeMesh(baked);
                            if (bounds.TryGetValue(skin, out var b)) { b.Encapsulate(baked.bounds); bounds[skin] = b; }
                            else bounds[skin] = baked.bounds;
                            Object.DestroyImmediate(baked);
                        }
                    }
                foreach (var skin in skins) { var b = bounds[skin]; b.Expand(.1f); skin.localBounds = b; }
                clips["Idle"].SampleAnimation(visual, 0); animator.enabled = true;
                foreach (var clip in clips.Values)
                {
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    if (bindings.Length == 0) throw new Exception("Empty clip: " + clip.name);
                    foreach (var binding in bindings)
                        if (binding.path.Length > 0 && !visual.transform.Find(binding.path)) throw new Exception("Unbound " + binding.path);
                }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/build.txt", "PASS\n" + string.Join("\n", clips.Select(p => p.Key + ": " + p.Value.length.ToString("F3") + "s")) +
                    "\nTwo skinned meshes; eye vertices=" + eyes.sharedMesh.vertexCount + "; Generic avatar valid; +Z facing; no root motion; roughness=1; HDR eye emission.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static Material Material(string name, Shader shader)
        {
            string path = BasePath + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader; return mat;
        }
        static AnimatorController Controller(Dictionary<string,AnimationClip> clips)
        {
            string path = BasePath + "/Animation/BarrelCrab.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            foreach (var c in sm.states) sm.RemoveState(c.state);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Shelled", AnimatorControllerParameterType.Bool);
            var states = new Dictionary<string,AnimatorState>();
            for (int i = 0; i < States.Length; i++)
            {
                var state = sm.AddState(States[i], new Vector3(i < 3 ? 240 : 560, i < 3 ? i*100 : (i-3)*100, 0));
                state.motion = clips[States[i]]; state.writeDefaultValues = false; states[States[i]] = state;
            }
            sm.defaultState = states["Idle"];
            Action<string,string,string,bool> trigger = (from,to,param,value) => {
                var t = Transition(states[from], states[to], false, .035f);
                t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, param);
            };
            trigger("Idle","Attack","Attack",true);
            foreach (string from in new[] { "Idle", "Attack", "Hit" })
            {
                trigger(from,"Retract","Shelled",true);
                var t = Transition(states[from],states["Hit"],false,.025f);
                t.AddCondition(AnimatorConditionMode.If,0,"Hit"); t.AddCondition(AnimatorConditionMode.IfNot,0,"Shelled");
            }
            foreach (string from in new[] { "Attack", "Hit" }) Transition(states[from],states["Idle"],true,.04f);
            Transition(states["Retract"],states["ShelledIdle"],true,.02f);
            foreach (string from in new[] { "ShelledIdle", "ShelledHit" })
            {
                trigger(from,"Emerge","Shelled",false);
                var t = Transition(states[from],states["ShelledHit"],false,.025f);
                t.AddCondition(AnimatorConditionMode.If,0,"Hit"); t.AddCondition(AnimatorConditionMode.If,0,"Shelled");
            }
            Transition(states["ShelledHit"],states["ShelledIdle"],true,.035f);
            Transition(states["Emerge"],states["Idle"],true,.04f);
            EditorUtility.SetDirty(controller); return controller;
        }
        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, bool exit, float duration)
        {
            var t = from.AddTransition(to); t.hasExitTime = exit; t.exitTime = 1;
            t.hasFixedDuration = true; t.duration = duration; t.canTransitionToSelf = from == to;
            return t;
        }

        [MenuItem("Tools/Harbor/Barrel Crab/Validate and Render")]
        public static void Validate()
        {
            Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(480,360,24,RenderTextureFormat.ARGBHalf);
            var pixels = new Texture2D(480,360,TextureFormat.RGB24,false);
            var previous = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                var animator = root.GetComponentInChildren<Animator>(); var visual = animator.gameObject;
                animator.Rebind(); animator.Update(0);
                Action<float> step = seconds => { for (int i=0;i<Mathf.CeilToInt(seconds*60);i++) animator.Update(1f/60); };
                Action<string> expect = name => { if (!animator.GetCurrentAnimatorStateInfo(0).IsName(name)) throw new Exception("Expected state " + name); };
                animator.SetTrigger("Attack"); step(.1f); expect("Attack"); step(1.2f); expect("Idle");
                animator.SetTrigger("Hit"); step(.1f); expect("Hit"); step(1); expect("Idle");
                animator.SetBool("Shelled",true); step(.1f); expect("Retract"); step(1); expect("ShelledIdle");
                animator.SetTrigger("Hit"); step(.1f); expect("ShelledHit"); step(1); expect("ShelledIdle");
                animator.SetBool("Shelled",false); step(.1f); expect("Emerge"); step(1.5f); expect("Idle");
                animator.enabled = false;
                Action<string, string> checkJoin = (from, to) => {
                    var first = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(from));
                    var next = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(to));
                    first.SampleAnimation(visual, first.length);
                    var bones = visual.GetComponentsInChildren<Transform>();
                    var positions = bones.Select(t => t.localPosition).ToArray();
                    var rotations = bones.Select(t => t.localRotation).ToArray();
                    next.SampleAnimation(visual, 0);
                    for (int i = 0; i < bones.Length; i++)
                        if (Vector3.Distance(positions[i], bones[i].localPosition) > .002f ||
                            Quaternion.Angle(rotations[i], bones[i].localRotation) > .5f)
                            throw new Exception("Pose discontinuity " + from + " -> " + to + ": " + bones[i].name);
                };
                checkJoin("Attack", "Idle"); checkJoin("Hit", "Idle");
                checkJoin("Retract", "ShelledIdle"); checkJoin("ShelledHit", "ShelledIdle");
                checkJoin("Emerge", "Idle");
                var camObj = new GameObject("BarrelCrab Preview Camera"); SceneManager.MoveGameObjectToScene(camObj,scene);
                var cam = camObj.AddComponent<Camera>(); cam.scene=scene; cam.enabled=false;
                cam.transform.position=new Vector3(-1,1.2f,2.5f); cam.transform.LookAt(new Vector3(0,.36f,0));
                cam.orthographic=true; cam.orthographicSize=.58f; cam.nearClipPlane=.01f; cam.farClipPlane=20;
                cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.045f,.055f,.05f);
                cam.allowHDR=true; cam.allowMSAA=false;
                var data=cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing=false; data.SetRenderer(0);
                var lightObj=new GameObject("BarrelCrab Preview Light"); SceneManager.MoveGameObjectToScene(lightObj,scene);
                var light=lightObj.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f;
                light.transform.rotation=Quaternion.Euler(35,-140,0);
                target.Create();
                // Bake preview geometry on the CPU: several render requests in one editor
                // frame otherwise reuse the previous GPU skinning buffer.
                var previewSkins = root.GetComponentsInChildren<SkinnedMeshRenderer>();
                var previewMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
                foreach (var skin in previewSkins)
                {
                    var obj = new GameObject(skin.name + " Preview");
                    obj.transform.SetParent(skin.transform, false);
                    var mesh = new Mesh(); previewMeshes[skin] = mesh;
                    obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = obj.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = skin.sharedMaterials;
                    skin.enabled = false;
                }
                Action<string,float,string> render=(state,time,name)=>{
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(state)).SampleAnimation(visual,time);
                    foreach (var pair in previewMeshes)
                    {
                        pair.Key.BakeMesh(pair.Value);
                        pair.Key.GetComponentInChildren<MeshRenderer>().sharedMaterials = pair.Key.sharedMaterials;
                    }
                    RenderPipeline.SubmitRenderRequest(cam,new UniversalRenderPipeline.StandardRequest{destination=target});
                    RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,480,360),0,0); pixels.Apply();
                    File.WriteAllBytes(Output+"/"+name+".png",pixels.EncodeToPNG());
                };
                render("Idle",0,"Idle"); render("Attack",6f/24,"Attack_Windup"); render("Attack",9f/24,"Attack_Impact");
                render("Retract",19f/24,"Shelled"); render("Hit",2f/24,"Hit"); render("ShelledHit",2f/24,"ShelledHit"); render("Emerge",31f/24,"Emerge");
                var eyes=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="SM_BarrelCrab_Eyes");
                var original=eyes.sharedMaterial; var dark=new Material(original); dark.SetColor("_EmissionColor",Color.black);
                try
                {
                    light.intensity=0; render("Idle",0,"Eyes_Emission_On");
                    Color[] on=pixels.GetPixels(); eyes.sharedMaterial=dark; render("Idle",0,"Eyes_Emission_Off");
                    Color[] off=pixels.GetPixels(); int changed=0;
                    for(int i=0;i<on.Length;i++) if(on[i].grayscale-off[i].grayscale>.03f) changed++;
                    if(changed<2) throw new Exception("Eye emission did not produce visible rendered pixels");
                    File.WriteAllText(Output+"/validation.txt","PASS\nAnimator: Attack/Hit/Retraction/ShelledHit/Emerge transitions verified\n5 pose joins verified; 7 rendered poses; eye emission pixel delta="+changed+"\nActive URP renderer: "+data.scriptableRenderer.GetType().Name+"\n");
                }
                finally { eyes.sharedMaterial=original; Object.DestroyImmediate(dark); }
                foreach(var mesh in previewMeshes.Values) Object.DestroyImmediate(mesh);
                if(ShaderUtil.ShaderHasError(Shader.Find("Rendering/Character Deferred Outline"))) throw new Exception("Shader failed after rendering");
            }
            catch(Exception e) { File.WriteAllText(Output+"/validation.txt","FAILED\n"+e); Debug.LogException(e); }
            finally
            {
                RenderTexture.active=previous; ShaderUtil.allowAsyncCompilation=async;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
