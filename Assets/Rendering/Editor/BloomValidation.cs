using System;
using System.Collections.Generic;
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
    public static class BloomValidation
    {
        const string Output = "Library/BloomValidation";
        [Serializable] sealed class Report { public string status; public List<Row> rows = new List<Row>(); }
        [Serializable] sealed class Row
        {
            public int width, height, haloPixels, mipCount;
            public Vector2Int sourceSize;
            public float peakSource, peakHalo;
        }

        [MenuItem("Tools/Rendering/Validate Bloom")]
        public static void Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后验证 Bloom。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(BloomSetup.RendererPath);
            var bloom = data.rendererFeatures.OfType<BloomFeature>().Single();
            var settings = bloom.settings;
            var active = data.rendererFeatures.Select(x => x.isActive).ToArray();
            bool low = data.lowResolution.enabled, rt = data.lowResolution.applyToRenderTextures;
            int height = data.lowResolution.maximumHeight;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var report = new Report { status = "Running" };
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var feature in data.rendererFeatures) feature.SetActive(false);
                bloom.settings = new BloomFeature.Settings { intensity = .8f, threshold = 1, softKnee = .5f, scatter = .8f, maxIterations = 6 };
                data.lowResolution.enabled = data.lowResolution.applyToRenderTextures = true;
                data.lowResolution.maximumHeight = 540;
                report.rows.Add(Capture(bloom, 1920, 1080, true));
                report.rows.Add(Capture(bloom, 800, 1200, true));
                report.rows.Add(Capture(bloom, 321, 181, true));
                report.rows.Add(Capture(bloom, 1, 1, true));
                data.lowResolution.enabled = false;
                report.rows.Add(Capture(bloom, 960, 600, false));
                if (ShaderUtil.GetShaderMessages(bloom.BloomShader).Any(x => x.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                    throw new InvalidOperationException("Bloom shader compilation failed.");
                report.status = "Passed"; Debug.Log("Bloom GPU validation passed: " + Output + "/report.json");
            }
            catch (Exception e) { report.status = "Failed: " + e.Message; throw; }
            finally
            {
                bloom.settings = settings;
                for (int i = 0; i < active.Length; ++i) data.rendererFeatures[i].SetActive(active[i]);
                data.lowResolution.enabled = low; data.lowResolution.applyToRenderTextures = rt; data.lowResolution.maximumHeight = height;
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                File.WriteAllText(Output + "/report.json", JsonUtility.ToJson(report, true));
            }
        }

        static Row Capture(BloomFeature bloom, int width, int height, bool lowResolution)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var previous = RenderTexture.active;
            try
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); SceneManager.MoveGameObjectToScene(quad, scene);
                quad.transform.localScale = Vector3.one * .18f;
                quad.GetComponent<Renderer>().sharedMaterial = material;
                material.SetColor("_BaseColor", new Color(8, 8, 8, 1));
                var root = new GameObject("Bloom GPU test camera"); SceneManager.MoveGameObjectToScene(root, scene);
                var camera = root.AddComponent<Camera>(); camera.scene = scene;
                camera.transform.position = new Vector3(0, 0, -2);
                camera.orthographic = true; camera.orthographicSize = .5f;
                camera.nearClipPlane = .01f; camera.farClipPlane = 10;
                camera.allowHDR = true; camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0, 0, 0, .37f);
                camera.targetTexture = target; camera.GetUniversalAdditionalCameraData().SetRenderer(0); target.Create();
                Color[] Render(bool enabled, string label)
                {
                    bloom.SetActive(enabled);
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    var result = pixels.GetPixels();
                    if (width > 1)
                    {
                        var preview = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        try { preview.SetPixels(result.Select(c => c.gamma).ToArray()); preview.Apply(); File.WriteAllBytes($"{Output}/{width}x{height}_{label}.png", preview.EncodeToPNG()); }
                        finally { Object.DestroyImmediate(preview); }
                    }
                    return result;
                }
                void Same(Color[] a, Color[] b, string label)
                {
                    for (int i = 0; i < a.Length; ++i)
                        if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) + Mathf.Abs(a[i].a - b[i].a) > .0001f)
                            throw new InvalidOperationException(label);
                }
                var baseline = Render(false, "off"); var enabled = Render(true, "on");
                var expected = lowResolution ? LowResolutionSettings.CalculateRenderSize(width, height, 540) : new Vector2Int(width, height);
                var row = new Row { width = width, height = height, sourceSize = bloom.LastSourceSize, mipCount = bloom.LastMipCount };
                if (row.sourceSize != expected || bloom.LastExecutionEvent != RenderPassEvent.BeforeUpscaling)
                    throw new InvalidOperationException("Bloom resolution/event mismatch.");
                for (int i = 0; i < baseline.Length; ++i)
                {
                    if (float.IsNaN(enabled[i].r) || float.IsInfinity(enabled[i].r)) throw new InvalidOperationException("Non-finite Bloom output.");
                    if (enabled[i].a != baseline[i].a || enabled[i].r + .0001f < baseline[i].r)
                        throw new InvalidOperationException("Bloom changed alpha or darkened scene color.");
                    row.peakSource = Mathf.Max(row.peakSource, baseline[i].r);
                    if (baseline[i].r < .01f && enabled[i].r > .001f)
                    { row.haloPixels++; row.peakHalo = Mathf.Max(row.peakHalo, enabled[i].r); }
                }
                if (width > 1 && (row.peakSource < 4 || row.haloPixels < 20)) throw new InvalidOperationException("HDR highlight did not produce a surrounding halo.");
                float intensity = bloom.settings.intensity; bloom.settings.intensity = 0;
                Same(baseline, Render(true, "zero"), "Zero intensity must match disabled Bloom."); bloom.settings.intensity = intensity;
                bloom.settings.threshold = 100;
                Same(baseline, Render(true, "high_threshold"), "Below-threshold pixels must not bloom."); bloom.settings.threshold = 1;
                bloom.settings.tint = Color.red; var red = Render(true, "red"); bloom.settings.tint = Color.white;
                for (int i = 0; i < baseline.Length; ++i)
                    if (red[i].g != baseline[i].g || red[i].b != baseline[i].b)
                        throw new InvalidOperationException("Red tint added green/blue light.");
                material.SetColor("_BaseColor", new Color(.2f, .2f, .2f, 1));
                Same(Render(false, "dim_off"), Render(true, "dim_on"), "Dim source below soft knee should not bloom.");
                material.SetColor("_BaseColor", new Color(8, 8, 8, 1));
                bloom.settings.maxIterations = 1; Render(true, "one_mip");
                if (bloom.LastMipCount != 1) throw new InvalidOperationException("Single-mip Bloom failed.");
                bloom.settings.maxIterations = 6;
                if (width == 1920 && height == 1080)
                    for (int y = 0; y < height; y += 2)
                    for (int x = 0; x < width; x += 2)
                    {
                        var c = enabled[y * width + x];
                        if (c != enabled[y * width + x + 1] || c != enabled[(y + 1) * width + x] || c != enabled[(y + 1) * width + x + 1])
                            throw new InvalidOperationException("Bloom broke Point upscale blocks.");
                    }
                return row;
            }
            finally
            {
                RenderTexture.active = previous; EditorSceneManager.ClosePreviewScene(scene); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
            }
        }

        [MenuItem("Tools/Rendering/Capture Current Scene Bloom")]
        public static void CaptureCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后捕获 Bloom 对照。");
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("当前场景没有 Main Camera。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(BloomSetup.RendererPath);
            var bloom = data.rendererFeatures.OfType<BloomFeature>().Single();
            bool active = bloom.isActive, asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; target.Create();
                foreach (bool enabled in new[] { false, true })
                {
                    bloom.SetActive(enabled);
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Output + (enabled ? "/current_scene_on.png" : "/current_scene_off.png"), pixels.EncodeToPNG());
                }
                Debug.Log("Current scene Bloom comparison saved to " + Output);
            }
            finally
            {
                bloom.SetActive(active); ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
