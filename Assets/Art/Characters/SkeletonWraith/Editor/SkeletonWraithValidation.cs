using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SkeletonWraith.Editor
{
    [InitializeOnLoad]
    public static class SkeletonWraithValidation
    {
        const string Output = "blender_output/unity_preview";
        static SkeletonWraithValidation() { EditorApplication.update += Poll; }
        static void Poll()
        {
            const string request = "tmp/SkeletonWraith.preview.request";
            if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(request); Directory.CreateDirectory(Output);
            try { Validate(); }
            catch (Exception e) { File.WriteAllText(Output + "/validation.txt", "FAILED\n" + e); Debug.LogException(e); }
        }
        [MenuItem("Tools/Skeleton Wraith/Validate Animation and Render Previews")]
        public static void Validate()
        {
            Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(640, 480, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                string folder = SkeletonWraithAssetBuilder.BasePath;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Prefabs/SkeletonWraith.prefab");
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var animator = root.GetComponentInChildren<Animator>();
                var visual = animator.gameObject;
                animator.Rebind(); animator.Update(0);
                var report = new List<string>();
                foreach (string trigger in new[] { "Attack", "Hit", "PowerUp" })
                {
                    animator.Play("Idle", 0, 0); animator.Update(0);
                    animator.SetTrigger(trigger); animator.Update(.01f); animator.Update(.1f);
                    if (!animator.GetCurrentAnimatorStateInfo(0).IsName(trigger)) throw new Exception("Trigger did not enter " + trigger);
                    for (int i = 0; i < 240; i++) animator.Update(1f / 60);
                    if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle")) throw new Exception(trigger + " did not return to Idle");
                    report.Add(trigger + " trigger and return to Idle: PASS");
                }
                animator.enabled = false;
                var attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Animation/SkeletonWraith_Attack.anim");
                var hand = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "hand.L");
                attack.SampleAnimation(visual, 16f / 24); float high = hand.position.y;
                attack.SampleAnimation(visual, 26f / 24); float low = hand.position.y;
                if (high - low < .3f) throw new Exception("Downstrike was not preserved: " + high + " -> " + low);
                report.Add("Attack wrist height: " + high.ToString("F3") + " -> " + low.ToString("F3") + "m");
                var cameraObject = new GameObject("SkeletonWraith Preview Camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
                camera.transform.position = new Vector3(2, 1.6f, 3);
                camera.transform.LookAt(new Vector3(0, .57f, 0));
                camera.orthographic = true; camera.orthographicSize = .73f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f,.09f,.12f);
                camera.allowHDR = false; camera.allowMSAA = false; camera.enabled = false;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var lightObject = new GameObject("SkeletonWraith Preview Light"); SceneManager.MoveGameObjectToScene(lightObject, scene);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f;
                light.transform.rotation = Quaternion.Euler(35, -145, 0);
                target.Create();
                var samples = new[] { ("Idle",0f,"Idle"), ("Attack",16f/24,"Attack_Raised"), ("Attack",26f/24,"Attack_Down"), ("Hit",4f/24,"Hit"), ("PowerUp",34f/24,"PowerUp") };
                foreach (var sample in samples)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/Animation/SkeletonWraith_" + sample.Item1 + ".anim");
                    clip.SampleAnimation(visual, sample.Item2);
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,640,480),0,0); pixels.Apply();
                    File.WriteAllBytes(Output + "/" + sample.Item3 + ".png", pixels.EncodeToPNG());
                }
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(folder + "/Shaders/SkeletonWraithDeferred.shader");
                if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader failed after rendering variants.");
                report.Add("Five Unity URP rendered poses: PASS; shader compilation: PASS");
                File.WriteAllText(Output + "/validation.txt", "PASS\n" + string.Join("\n", report));
            }
            finally
            {
                RenderTexture.active = previous; ShaderUtil.allowAsyncCompilation = asyncShaders;
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
