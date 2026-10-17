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
    /// <summary>GPU checks in an isolated scene, without saving or changing the user's open scene.</summary>
    public static class HeightFogValidation
    {
        const string Output = "Library/HeightFogValidation";
        [Serializable] sealed class Report { public string status; public List<Row> rows = new List<Row>(); }
        [Serializable] sealed class Row
        {
            public int width, height, fogChangedPixels, scatteringChangedPixels;
            public int mainShadowPixels, pointShadowPixels, spotShadowPixels;
            public bool orthographic;
            public Vector2Int fogSize, scatteringSize;
        }

        [MenuItem("工具/渲染/高度雾与体积光/验证效果")]
        public static void Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后验证高度雾。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(HeightFogSetup.RendererPath);
            var fog = data.rendererFeatures.OfType<HeightFogFeature>().Single();
            var light = data.rendererFeatures.OfType<VolumetricLightFeature>().Single();
            var active = data.rendererFeatures.Select(x => x.isActive).ToArray();
            var fogSettings = fog.settings; var lightSettings = light.settings;
            bool lowResolution = data.lowResolution.enabled, renderTextures = data.lowResolution.applyToRenderTextures;
            int maximumHeight = data.lowResolution.maximumHeight;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var report = new Report { status = "Running" };
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var feature in data.rendererFeatures) feature.SetActive(false);
                fog.settings = new HeightFogFeature.Settings { baseHeight = 0.2f, maximumHeight = 2,
                    density = 0.5f, distance = 3, attenuationDistance = 1, groundHeight = 100 };
                light.settings = new VolumetricLightFeature.Settings { mainScattering = 1,
                    steps = 48, jitter = 0, blurIterations = 1 };
                data.lowResolution.enabled = data.lowResolution.applyToRenderTextures = true;
                data.lowResolution.maximumHeight = 540;
                report.rows.Add(Capture(data, fog, light, 1920, 1080, false));
                report.rows.Add(Capture(data, fog, light, 800, 1200, false));
                report.rows.Add(Capture(data, fog, light, 800, 450, true));
                foreach (var shader in new[] { fog.FogShader, light.ScatteringShader })
                    if (ShaderUtil.GetShaderMessages(shader).Any(x => x.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                        throw new InvalidOperationException("Shader compilation failed: " + shader.name);
                report.status = "Passed";
                Debug.Log("Height fog GPU validation passed: " + Output + "/report.json");
            }
            catch (Exception e) { report.status = "Failed: " + e.Message; throw; }
            finally
            {
                File.WriteAllText(Output + "/report.json", JsonUtility.ToJson(report, true));
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                fog.settings = fogSettings; light.settings = lightSettings;
                for (int i = 0; i < active.Length; ++i) data.rendererFeatures[i].SetActive(active[i]);
                data.lowResolution.enabled = lowResolution; data.lowResolution.applyToRenderTextures = renderTextures;
                data.lowResolution.maximumHeight = maximumHeight;
            }
        }

        static Row Capture(UniversalRendererData data, HeightFogFeature fog, VolumetricLightFeature light,
            int width, int height, bool orthographic)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                GameObject Root(string name)
                {
                    var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
                }
                material.color = new Color(.35f, .35f, .35f);
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", new Color(.15f, .15f, .15f));
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(floor, scene);
                floor.transform.position = new Vector3(0, -.1f, 0); floor.transform.localScale = new Vector3(4, .1f, 4);
                floor.GetComponent<Renderer>().sharedMaterial = material;
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(cube, scene);
                cube.transform.position = new Vector3(0, .2f, 0); cube.transform.localScale = new Vector3(.25f, .5f, .25f);
                cube.GetComponent<Renderer>().sharedMaterial = material;
                var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(canopy, scene);
                canopy.transform.position = new Vector3(0, .95f, 0); canopy.transform.localScale = new Vector3(1.6f, .1f, 1.6f);
                canopy.GetComponent<Renderer>().sharedMaterial = material;
                var directional = Root("Fog test sun").AddComponent<Light>();
                directional.type = LightType.Directional; directional.intensity = .8f; directional.shadows = LightShadows.Hard;
                directional.transform.rotation = Quaternion.Euler(90, 0, 0);
                var point = Root("Fog test point").AddComponent<Light>(); point.type = LightType.Point;
                point.range = 2; point.intensity = .8f; point.shadows = LightShadows.Hard;
                point.transform.position = new Vector3(-.4f, .65f, -.15f);
                var custom = point.gameObject.AddComponent<VolumetricLight>(); custom.scattering = .4f;
                camera = Root("Fog test camera").AddComponent<Camera>(); camera.scene = scene;
                camera.transform.position = new Vector3(.7f, .5f, -1.2f); camera.transform.LookAt(new Vector3(0, .25f, 0));
                camera.nearClipPlane = .01f; camera.farClipPlane = 8; camera.fieldOfView = 50;
                camera.orthographic = orthographic; camera.orthographicSize = .65f;
                camera.allowHDR = camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.3f, .3f, .3f, .37f);
                camera.targetTexture = target; camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                target.Create();
                Color32[] Render(bool fogOn, bool lightOn, string suffix)
                {
                    fog.SetActive(fogOn); light.SetActive(lightOn);
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    File.WriteAllBytes($"{Output}/{width}x{height}_{suffix}.png", pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                static int Changed(Color32[] a, Color32[] b)
                {
                    int count = 0;
                    for (int i = 0; i < a.Length; ++i)
                    {
                        if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > 3) count++;
                        if (a[i].a != b[i].a) throw new InvalidOperationException("Fog changed camera alpha.");
                    }
                    return count;
                }
                void Same(Color32[] a, Color32[] b, string message)
                {
                    if (!a.SequenceEqual(b)) throw new InvalidOperationException(message);
                }
                var baseline = Render(false, false, "off");
                var fogOnly = Render(true, false, "fog");
                var both = Render(true, true, "fog_and_light");
                var row = new Row { width = width, height = height, orthographic = orthographic,
                    fogSize = fog.LastSourceSize, scatteringSize = light.LastSourceSize,
                    fogChangedPixels = Changed(baseline, fogOnly), scatteringChangedPixels = Changed(fogOnly, both) };
                var expected = LowResolutionSettings.CalculateRenderSize(width, height, 540);
                if (row.fogSize != expected || row.scatteringSize != expected ||
                    fog.LastExecutionEvent != RenderPassEvent.BeforeUpscaling || light.LastExecutionEvent != RenderPassEvent.BeforeUpscaling)
                    throw new InvalidOperationException("Fog resolution/event mismatch.");
                if (row.fogChangedPixels < 100 || row.scatteringChangedPixels < 100)
                    throw new InvalidOperationException($"Fog/scattering did not affect the image: {row.fogChangedPixels}/{row.scatteringChangedPixels}.");
                for (int i = 0; i < both.Length; ++i)
                    if (both[i].r < fogOnly[i].r || both[i].g < fogOnly[i].g || both[i].b < fogOnly[i].b)
                        throw new InvalidOperationException("Scattering applied extinction twice.");
                Same(baseline, Render(false, true, "dependency_off"), "Light must stop when fog is off.");
                float density = fog.settings.density; fog.settings.density = 0;
                Same(baseline, Render(true, true, "zero_density"), "Zero density must match disabled effects.");
                fog.settings.density = density;
                light.settings.mainLight = light.settings.additionalLights = false;
                Same(fogOnly, Render(true, true, "no_lights"), "Disabling all light contributions must preserve fog.");
                light.settings.mainLight = true;
                var mainOnly = Render(true, true, "main_only");
                if (Changed(fogOnly, mainOnly) < 50) throw new InvalidOperationException("Main light scattering missing.");
                int CheckShadow(Light source, Color32[] shadowFog, Color32[] shadowLight, string label)
                {
                    source.shadows = LightShadows.None;
                    var clearFog = Render(true, false, label + "_no_shadow_fog");
                    var clearLight = Render(true, true, label + "_no_shadow_light");
                    source.shadows = LightShadows.Hard;
                    int count = 0;
                    // Compare the added radiance, cancelling changes to surface lighting.
                    // Decode display bytes before subtraction (project renders in linear color space).
                    for (int i = 0; i < clearFog.Length; ++i)
                    {
                        float shadowRadiance = Mathf.GammaToLinearSpace(shadowLight[i].r / 255f) - Mathf.GammaToLinearSpace(shadowFog[i].r / 255f);
                        float clearRadiance = Mathf.GammaToLinearSpace(clearLight[i].r / 255f) - Mathf.GammaToLinearSpace(clearFog[i].r / 255f);
                        if (clearRadiance - shadowRadiance > .003f) count++;
                    }
                    if (count < 10) throw new InvalidOperationException(label + " shadows did not attenuate volumetric scattering.");
                    return count;
                }
                row.mainShadowPixels = CheckShadow(directional, fogOnly, mainOnly, "main");
                light.settings.mainLight = false; light.settings.additionalLights = true;
                var pointOnly = Render(true, true, "point_only");
                if (Changed(fogOnly, pointOnly) < 50) throw new InvalidOperationException("Point light scattering missing.");
                row.pointShadowPixels = CheckShadow(point, fogOnly, pointOnly, "point");
                custom.enabled = false;
                Same(fogOnly, Render(true, true, "point_excluded"), "Disabled per-light override must exclude that light.");
                custom.enabled = true;
                custom.scattering = 0;
                Same(fogOnly, Render(true, true, "point_zero"), "Zero scattering must exclude that light.");
                Object.DestroyImmediate(custom);
                Same(fogOnly, Render(true, true, "point_without_script"), "An additional light without VolumetricLight must not scatter.");
                custom = point.gameObject.AddComponent<VolumetricLight>(); custom.scattering = .4f;
                point.type = LightType.Spot; point.spotAngle = 65;
                point.transform.LookAt(new Vector3(.15f, -.1f, .2f));
                var spotFog = Render(true, false, "spot_fog");
                var spotBoth = Render(true, true, "spot_scattering");
                if (Changed(spotFog, spotBoth) < 50) throw new InvalidOperationException("Spot light scattering missing.");
                row.spotShadowPixels = CheckShadow(point, spotFog, spotBoth, "spot");
                point.type = LightType.Point; light.settings.mainLight = true;
                if (width == 1920 && height == 1080)
                    for (int y = 0; y < height; y += 2)
                    for (int x = 0; x < width; x += 2)
                    {
                        var c = both[y * width + x];
                        if (!c.Equals(both[y * width + x + 1]) || !c.Equals(both[(y + 1) * width + x]) || !c.Equals(both[(y + 1) * width + x + 1]))
                            throw new InvalidOperationException("Fog broke Point upscale pixel blocks.");
                    }
                // Disabling custom low resolution must make both passes follow the full descriptor.
                data.lowResolution.enabled = false;
                Render(true, true, "native_resolution");
                if (fog.LastSourceSize != new Vector2Int(width, height) || light.LastSourceSize != new Vector2Int(width, height))
                    throw new InvalidOperationException("Fog did not follow native renderer dimensions.");
                data.lowResolution.enabled = true;
                return row;
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                EditorSceneManager.ClosePreviewScene(scene); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
            }
        }

        [MenuItem("工具/渲染/高度雾与体积光/捕获当前场景")]
        public static void CaptureCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后捕获高度雾对照。");
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("当前场景没有 Main Camera。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(HeightFogSetup.RendererPath);
            var fog = data.rendererFeatures.OfType<HeightFogFeature>().Single();
            var light = data.rendererFeatures.OfType<VolumetricLightFeature>().Single();
            bool fogActive = fog.isActive, lightActive = light.isActive, asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false; target.Create();
                foreach (var variant in new[] { "off", "fog", "fog_and_light" })
                {
                    fog.SetActive(variant != "off"); light.SetActive(variant == "fog_and_light");
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                    File.WriteAllBytes(Output + "/current_scene_" + variant + ".png", pixels.EncodeToPNG());
                }
                Debug.Log("Current scene fog comparison saved to " + Output);
            }
            finally
            {
                fog.SetActive(fogActive); light.SetActive(lightActive); ShaderUtil.allowAsyncCompilation = asyncCompilation;
                RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
