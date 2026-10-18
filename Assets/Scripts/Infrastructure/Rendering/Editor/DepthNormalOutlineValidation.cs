using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RetroRendering.Editor
{
    public static class DepthNormalOutlineValidation
    {
        const string Output = "Library/DepthNormalOutlineValidation";
        [MenuItem("工具/渲染/描边/验证屏幕像素宽度")]
        public static void Validate()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
            var outline = renderer.rendererFeatures.OfType<DepthNormalOutlineFeature>().Single();
            var active = renderer.rendererFeatures.Select(f => f.isActive).ToArray();
            var settings = outline.settings;
            var low = renderer.lowResolution;
            bool enabled = low.enabled, rt = low.applyToRenderTextures;
            int height = low.maximumHeight;
            bool async = ShaderUtil.allowAsyncCompilation;
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var f in renderer.rendererFeatures) f.SetActive(false);
                low.enabled = low.applyToRenderTextures = true; low.maximumHeight = 180;
                outline.settings = new DepthNormalOutlineFeature.Settings { color = Color.black };
                foreach (int h in new[] { 180, 360, 540, 450 })
                foreach (float width in new[] { 1f, 2f, 4f }) Capture(outline, h*2, h, width);
                if (ShaderUtil.GetShaderMessages(outline.OutlineShader).Any(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                    throw new InvalidOperationException("Outline shader compilation failed.");
                Debug.Log("Outline GPU validation passed (1/2/4 px at 1x, 2x, 3x, 2.5x). Images: " + Output);
            }
            finally
            {
                outline.settings = settings;
                for (int i=0;i<active.Length;i++) renderer.rendererFeatures[i].SetActive(active[i]);
                low.enabled = enabled; low.applyToRenderTextures = rt; low.maximumHeight = height;
                ShaderUtil.allowAsyncCompilation = async;
            }
        }
        [MenuItem("工具/渲染/描边/保存当前相机预览")]
        public static void CaptureCurrentCamera()
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No main camera in the current scene.");
            int w = Mathf.Max(1,camera.pixelWidth), h = Mathf.Max(1,camera.pixelHeight);
            var target = new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(w,h,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,w,h),0,0); pixels.Apply();
                Directory.CreateDirectory(Output);
                File.WriteAllBytes(Output + "/CurrentCamera_1px.png",pixels.EncodeToPNG());
                Debug.Log("Saved outline preview: " + Output + "/CurrentCamera_1px.png");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = async;
                RenderTexture.active = previous; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
        static void Capture(DepthNormalOutlineFeature outline, int w, int h, float lineWidth)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(w,h,TextureFormat.RGBA32,false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                material.SetColor("_BaseColor",Color.white);
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",Color.white);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(cube,scene);
                cube.GetComponent<Renderer>().sharedMaterial = material;
                var go = new GameObject("Outline validation camera"); SceneManager.MoveGameObjectToScene(go,scene);
                camera = go.AddComponent<Camera>(); camera.scene = scene;
                camera.transform.position = new Vector3(0,0,-3);
                camera.orthographic = true; camera.orthographicSize = 1.5f;
                camera.nearClipPlane = .1f; camera.farClipPlane = 10;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.allowHDR = camera.allowMSAA = false;
                camera.targetTexture = target; camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                target.Create();
                Color32[] Render(bool enabled)
                {
                    outline.SetActive(enabled);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,w,h),0,0); pixels.Apply();
                    return pixels.GetPixels32();
                }
                outline.settings.width = lineWidth;
                var baseline = Render(false);
                var drawn = Render(true);
                File.WriteAllBytes($"{Output}/{w}x{h}_{lineWidth}px.png", pixels.EncodeToPNG());
                int changed = 0;
                for (int x=0;x<w;x++) if (baseline[h/2*w+x].r - drawn[h/2*w+x].r > 5) changed++;
                // The two silhouettes each occupy width pixels, plus at most one coverage pixel.
                if (changed < 2*lineWidth || changed > 2*(lineWidth+1))
                    throw new InvalidOperationException($"Unexpected silhouette width: {w}x{h}, requested {lineWidth}px, changed {changed} pixels across two edges.");
                outline.settings.width = 0;
                if (!baseline.SequenceEqual(Render(true))) throw new InvalidOperationException("Zero width changed image.");
                outline.settings.width = lineWidth;
                outline.settings.excludedLayers = 1;
                if (!baseline.SequenceEqual(Render(true))) throw new InvalidOperationException("Excluded surface received outline.");
                outline.settings.excludedLayers = 0;
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera) camera.targetTexture = null;
                EditorSceneManager.ClosePreviewScene(scene); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
            }
        }
    }
}
