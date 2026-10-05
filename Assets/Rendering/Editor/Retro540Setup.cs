using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace RetroRendering.Editor
{
    public static class Retro540Setup
    {
        public const string RendererPath = "Assets/Settings/MainDeferredRenderer.asset";
        public const string ShaderPath = "Assets/Rendering/Shaders/RetroPost.shader";
        public const string OutputPath = "ArtSource/Cards/Retro540Validation";

        public static RetroPostProcessFeature EnsureFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (data == null) throw new InvalidOperationException("Main Deferred Renderer was not found.");
            var feature = data.rendererFeatures.OfType<RetroPostProcessFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RetroPostProcessFeature>();
                feature.name = "复古后处理";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
            }
            feature.name = "复古后处理";
            feature.PostShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (feature.PostShader == null) throw new InvalidOperationException("Retro post shader was not imported.");
            feature.Create();
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            // Keep URP's recovery map in sync with the persistent renderer-feature subassets.
            var serialized = new SerializedObject(data);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < data.rendererFeatures.Count; ++i)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return feature;
        }

        [MenuItem("Tools/Rendering/Install Retro 540p and Migrate Card Scene")]
        public static void InstallAndMigrate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode before migrating the scene.");
            EnsureFeature();
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            rendererData.lowResolution.enabled = true;
            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path == "Assets/Scenes/CardRenderingValidation.unity")
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    camera.targetTexture = null;
                    camera.allowMSAA = false;
                    camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.None;
                    EditorUtility.SetDirty(camera);
                }
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "Output Camera — full resolution UI only" && root.GetComponent<Camera>() != null)
                        UnityEngine.Object.DestroyImmediate(root);
                    else if (root.name == "Point Upscale — 960×540 to screen" &&
                             root.GetComponentInChildren<RawImage>()?.texture is RenderTexture)
                        UnityEngine.Object.DestroyImmediate(root);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            Debug.Log("Retro 540p installed. Native camera output, aspect-preserving 540p targets, point final blit.");
        }

        [MenuItem("Tools/Rendering/Capture Retro 540p Validation")]
        public static void CaptureValidation()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/CardRenderingValidation.unity")
                throw new InvalidOperationException("Open CardRenderingValidation to capture.");
            var feature = EnsureFeature();
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (!rendererData.lowResolution.enabled || !rendererData.lowResolution.applyToRenderTextures) throw new InvalidOperationException("Enable Apply To Render Textures for capture requests.");
            Directory.CreateDirectory(OutputPath);
            var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1080), new Vector2Int(1080, 1920), new Vector2Int(800, 450) };
            var rows = new CaptureRow[sizes.Length];
            for (int i = 0; i < sizes.Length; ++i)
            {
                Capture(Camera.main, sizes[i], Path.Combine(OutputPath, $"retro_{sizes[i].x}x{sizes[i].y}.png"));
                var renderer = Camera.main.GetUniversalAdditionalCameraData().scriptableRenderer;
                var actualMode = renderer.GetType().GetProperty("renderingModeActual", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(renderer)?.ToString();
                var deferred = renderer.GetType().GetField("m_DeferredLights", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(renderer);
                var gBuffers = deferred?.GetType().GetProperty("GbufferAttachments", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(deferred) as RTHandle[];
                rows[i] = new CaptureRow
                {
                    frame = ((UniversalRenderer)renderer).lastCameraRenderTargetInfo,
                    renderingMode = actualMode,
                    postSourceSize = feature.LastSourceSize,
                    postEvent = feature.LastExecutionEvent.ToString(),
                    gBuffers = gBuffers?.Where(h => h?.rt != null).Select(h => new Vector2Int(h.rt.width, h.rt.height)).ToArray() ?? Array.Empty<Vector2Int>()
                };
                var expected = LowResolutionSettings.CalculateRenderSize(sizes[i].x, sizes[i].y, rendererData.lowResolution.maximumHeight);
                if (feature.LastSourceSize != expected || feature.LastExecutionEvent != feature.InjectionPoint ||
                    rows[i].frame.renderSize != expected || rows[i].frame.colorTargetSize != expected ||
                    rows[i].frame.depthTargetSize != expected || rows[i].frame.finalFilter != "Point" || actualMode != "Deferred" ||
                    rows[i].gBuffers.Length < 3 || rows[i].gBuffers.Any(s => s != expected))
                    throw new InvalidOperationException("Unexpected render target dimensions or renderer mode: " + JsonUtility.ToJson(rows[i]));
            }
            var errors = ShaderUtil.GetShaderMessages(feature.PostShader).Where(m => m.severity.ToString() == "Error").Select(m => m.message).ToArray();
            File.WriteAllText(Path.Combine(OutputPath, "validation.json"), JsonUtility.ToJson(new CaptureReport
            { device = SystemInfo.graphicsDeviceType.ToString(), rows = rows, shaderErrors = errors, cameraUsesOutputTexture = Camera.main.targetTexture != null }, true));
            if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors));
            Debug.Log("Retro 540p validation passed: real Deferred color/depth/GBuffer sizes and Point output across four output sizes.");
        }

        [MenuItem("Tools/Rendering/Validate Before Upscaling Event")]
        public static void ValidateUpscalingEvent()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/CardRenderingValidation.unity")
                throw new InvalidOperationException("Open CardRenderingValidation to validate.");
            var feature = EnsureFeature();
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var cameraData = Camera.main.GetUniversalAdditionalCameraData();
            var lowEnabled = data.lowResolution.enabled;
            var lowRT = data.lowResolution.applyToRenderTextures;
            var postData = data.postProcessData;
            var cameraPost = cameraData.renderPostProcessing;
            var cameraAA = cameraData.antialiasing;
            var oldEvent = feature.InjectionPoint;
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var oldScale = pipeline.renderScale;
            var oldFilter = pipeline.upscalingFilter;
            var rows = new System.Collections.Generic.List<UpscalingEventRow>();
            Directory.CreateDirectory(OutputPath);
            try
            {
                data.lowResolution.applyToRenderTextures = true;
                data.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if (data.postProcessData == null) throw new InvalidOperationException("URP post-processing resources were not found.");
                cameraData.renderPostProcessing = true;
                var events = new[] { RenderPassEvent.BeforeUpscaling, RenderPassEvent.AfterRenderingPostProcessing,
                    RenderPassEvent.BeforeRenderingPostProcessing, RenderPassEvent.AfterRenderingTransparents, RenderPassEvent.AfterRendering };
                foreach (var injection in events)
                {
                    // Also cover normal URP post + FXAA, whose final post pass performs upscaling.
                    for (int mode = 0; mode < (injection == RenderPassEvent.BeforeUpscaling ? 3 : 1); ++mode)
                    {
                        data.lowResolution.enabled = mode == 0;
                        // Exercise actual upscaling even if the user's normal Render Scale is 1.
                        pipeline.renderScale = mode == 0 ? oldScale : 0.5f;
                        pipeline.upscalingFilter = mode == 2 ? UpscalingFilterSelection.Point : UpscalingFilterSelection.Linear;
                        cameraData.antialiasing = mode == 2 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
                        feature.InjectionPoint = injection;
                        data.SetDirty();
                        string scenario = injection + "_" + (mode == 0 ? "point" : mode == 1 ? "urp_post" : "urp_final_post");
                        Capture(Camera.main, new Vector2Int(1920, 1080), Path.Combine(OutputPath, scenario + ".png"));
                        var renderer = (UniversalRenderer)cameraData.scriptableRenderer;
                        var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                        ScriptableRenderPass final;
                        if (mode == 2)
                        {
                            var pp = typeof(UniversalRenderer).GetField("m_PostProcessPasses", fields).GetValue(renderer);
                            final = (ScriptableRenderPass)pp.GetType().GetProperty("finalPostProcessPass").GetValue(pp);
                        }
                        else final = (ScriptableRenderPass)typeof(UniversalRenderer).GetField("m_FinalBlitPass", fields).GetValue(renderer);
                        var ui = (ScriptableRenderPass)typeof(UniversalRenderer).GetField("m_DrawOverlayUIPass", fields).GetValue(renderer);
                        var frame = renderer.lastCameraRenderTargetInfo;
                        if (feature.LastExecutionEvent != injection || feature.LastSourceSize != frame.renderSize ||
                            final.renderPassEvent <= RenderPassEvent.BeforeUpscaling || ui.renderPassEvent <= final.renderPassEvent)
                            throw new InvalidOperationException("Incorrect pre-upscale ordering or source dimensions: " + scenario);
                        rows.Add(new UpscalingEventRow { scenario = scenario, sourceSize = feature.LastSourceSize,
                            selectedEvent = (int)injection, finalPass = final.GetType().Name,
                            finalEvent = (int)final.renderPassEvent, overlayUIEvent = (int)ui.renderPassEvent });
                    }
                }
                File.WriteAllText(Path.Combine(OutputPath, "upscaling_event.json"), JsonUtility.ToJson(new UpscalingEventReport { rows = rows.ToArray() }, true));
                Debug.Log("BeforeUpscaling event validated: Point, URP post, final post/FXAA and all Inspector injection choices.");
            }
            finally
            {
                data.lowResolution.enabled = lowEnabled;
                data.lowResolution.applyToRenderTextures = lowRT;
                data.postProcessData = postData;
                cameraData.renderPostProcessing = cameraPost;
                cameraData.antialiasing = cameraAA;
                feature.InjectionPoint = oldEvent;
                pipeline.renderScale = oldScale;
                pipeline.upscalingFilter = oldFilter;
                data.SetDirty();
            }
        }

        [Serializable] sealed class UpscalingEventRow
        {
            public string scenario, finalPass;
            public Vector2Int sourceSize;
            public int selectedEvent, finalEvent, overlayUIEvent;
        }
        [Serializable] sealed class UpscalingEventReport { public UpscalingEventRow[] rows; }

        [MenuItem("Tools/Rendering/Validate Renderer and Post Independence")]
        public static void ValidateIndependence()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/CardRenderingValidation.unity")
                throw new InvalidOperationException("Open CardRenderingValidation to validate.");
            var feature = EnsureFeature();
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var settings = data.lowResolution;
            var oldHeight = settings.maximumHeight;
            var oldEnabled = settings.enabled;
            var oldRT = settings.applyToRenderTextures;
            var oldActive = feature.isActive;
            int featureIndex = data.rendererFeatures.IndexOf(feature);
            var rows = new System.Collections.Generic.List<IndependenceRow>();
            Directory.CreateDirectory(OutputPath);
            void Check(string label, int height, bool low, bool active, bool attached)
            {
                settings.enabled = low;
                settings.maximumHeight = height;
                settings.applyToRenderTextures = true;
                feature.SetActive(active);
                if (attached && !data.rendererFeatures.Contains(feature)) data.rendererFeatures.Insert(featureIndex, feature);
                if (!attached) data.rendererFeatures.Remove(feature);
                data.SetDirty();
                var size = new Vector2Int(1920, 1080);
                Capture(Camera.main, size, Path.Combine(OutputPath, label + ".png"));
                var renderer = (UniversalRenderer)Camera.main.GetUniversalAdditionalCameraData().scriptableRenderer;
                var frame = renderer.lastCameraRenderTargetInfo;
                // When disabled, preserve the user's normal URP Render Scale (it need not be 1).
                var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
                float scale = Mathf.Abs(1 - pipeline.renderScale) < 0.05f ? 1 : pipeline.renderScale;
                var expected = low ? LowResolutionSettings.CalculateRenderSize(size.x, size.y, height)
                    : new Vector2Int(Mathf.Max(1, (int)(size.x * scale)), Mathf.Max(1, (int)(size.y * scale)));
                if (frame.renderSize != expected || frame.colorTargetSize != expected || frame.depthTargetSize != expected ||
                    frame.lowResolution != low || (low && frame.finalFilter != "Point"))
                    throw new InvalidOperationException(label + ": " + JsonUtility.ToJson(frame));
                rows.Add(new IndependenceRow { scenario = label, postAttached = attached, postEnabled = active, frame = frame });
            }
            try
            {
                Check("post_disabled_540", 540, true, false, true);
                Check("post_removed_540", 540, true, false, false);
                Check("renderer_360", 360, true, false, false);
                Check("renderer_720", 720, true, false, false);
                Check("pipeline_with_post", 540, false, true, true);
                Check("pipeline_without_post", 540, false, false, false);
                // Disabled and removed post-processing must have exactly the same output.
                if (!File.ReadAllBytes(Path.Combine(OutputPath, "post_disabled_540.png")).SequenceEqual(
                    File.ReadAllBytes(Path.Combine(OutputPath, "post_removed_540.png"))))
                    throw new InvalidOperationException("Disabling and removing the post feature gave different output.");
                File.WriteAllText(Path.Combine(OutputPath, "independence.json"), JsonUtility.ToJson(new IndependenceReport { rows = rows.ToArray() }, true));
                Debug.Log("Renderer/post independence passed: feature disabled/removed, configurable 360/720p, and normal URP rendering with/without post.");
            }
            finally
            {
                settings.maximumHeight = oldHeight;
                settings.enabled = oldEnabled;
                settings.applyToRenderTextures = oldRT;
                if (!data.rendererFeatures.Contains(feature)) data.rendererFeatures.Insert(featureIndex, feature);
                feature.SetActive(oldActive);
                data.SetDirty();
            }
        }

        [Serializable] sealed class IndependenceRow
        {
            public string scenario;
            public bool postAttached, postEnabled;
            public CameraRenderTargetInfo frame;
        }
        [Serializable] sealed class IndependenceReport { public IndependenceRow[] rows; }

        static void Capture(Camera camera, Vector2Int size, string path)
        {
            var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Point, antiAliasing = 1, useDynamicScale = false };
            var image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            var aspect = camera.aspect;
            try
            {
                target.Create();
                camera.aspect = (float)size.x / size.y;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                image.Apply();
                var frame = ((UniversalRenderer)camera.GetUniversalAdditionalCameraData().scriptableRenderer).lastCameraRenderTargetInfo;
                if (frame.lowResolution && size.x % frame.renderSize.x == 0 && size.y % frame.renderSize.y == 0)
                {
                    int sx = size.x / frame.renderSize.x, sy = size.y / frame.renderSize.y;
                    var pixels = image.GetPixels32();
                    for (int y = 0; y < size.y; ++y)
                        for (int x = 0; x < size.x; ++x)
                            if (!pixels[y * size.x + x].Equals(pixels[(y / sy * sy) * size.x + x / sx * sx]))
                                throw new InvalidOperationException("Point output contains mixed pixel blocks: " + path);
                }
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.aspect = aspect;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        [MenuItem("Tools/Rendering/Capture Retro Game Output")]
        public static void CaptureGameOutput()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode to capture the screen output.");
            Directory.CreateDirectory(OutputPath);
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("A main camera is required.");
            void OnCameraRendered(ScriptableRenderContext context, Camera rendered)
            {
                if (rendered != camera) return;
                RenderPipelineManager.endCameraRendering -= OnCameraRendered;
                var renderer = (UniversalRenderer)camera.GetUniversalAdditionalCameraData().scriptableRenderer;
                File.WriteAllText(Path.Combine(OutputPath, "game_frame.json"), JsonUtility.ToJson(renderer.lastCameraRenderTargetInfo, true));
                ScreenCapture.CaptureScreenshot(Path.Combine(OutputPath, "game_output.png"));
            }
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
        }

        [Serializable] sealed class CaptureRow
        {
            public CameraRenderTargetInfo frame;
            public string renderingMode;
            public Vector2Int postSourceSize;
            public string postEvent;
            public Vector2Int[] gBuffers;
        }
        [Serializable] sealed class CaptureReport
        {
            public string device;
            public CaptureRow[] rows;
            public string[] shaderErrors;
            public bool cameraUsesOutputTexture;
        }
    }
}
