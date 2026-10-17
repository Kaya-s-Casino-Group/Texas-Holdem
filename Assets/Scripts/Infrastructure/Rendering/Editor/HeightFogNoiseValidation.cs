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
    public static class HeightFogNoiseValidation
    {
        const string Output = "Library/HeightFogNoiseValidation";
        [Serializable] sealed class Report
        {
            public string status;
            public float constantDensityError, sharedExtinctionError, periodicError, seamError, movingDifference;
            public int scatteringPixels;
            public float curveDensityError, curveIntegralError, curveSharedError;
        }

        [MenuItem("工具/渲染/三维噪声/验证雾噪声")]
        public static void Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后验证。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(HeightFogSetup.RendererPath);
            var fog = data.rendererFeatures.OfType<HeightFogFeature>().Single();
            var volume = data.rendererFeatures.OfType<VolumetricLightFeature>().Single();
            var oldFog = fog.settings; var oldLight = volume.settings;
            var active = data.rendererFeatures.Select(f => f.isActive).ToArray();
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(160, 120, 24, RenderTextureFormat.ARGBHalf);
            var pixels = new Texture2D(160, 120, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var constant = new Texture3D(2, 2, 2, TextureFormat.RFloat, false);
            var report = new Report { status = "Running" };
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var f in data.rendererFeatures) f.SetActive(false);
                var s = new HeightFogFeature.Settings { baseHeight = .2f, maximumHeight = 2, density = .7f,
                    distance = 4, color = new Color(.04f, .06f, .09f), noiseVelocity = Vector3.zero,
                    noiseWorldSize = new Vector3(1.5f, 1, 2), noiseSteps = 32, noiseStrength = 1 };
                fog.settings = s;
                volume.settings = new VolumetricLightFeature.Settings { mainLight = true, mainScattering = .7f,
                    additionalLights = false, blurIterations = 1 };
                GameObject Root(string name)
                {
                    var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
                }
                var camera = Root("Noise validation camera").AddComponent<Camera>(); camera.scene = scene;
                camera.transform.position = new Vector3(0, .5f, -2);
                camera.nearClipPlane = .01f; camera.farClipPlane = 6; camera.fieldOfView = 45;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.4f, .3f, .2f, .37f);
                camera.allowHDR = true; camera.allowMSAA = false; camera.targetTexture = target; camera.enabled = false;
                camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(wall, scene);
                wall.transform.position = new Vector3(0, .5f, 1); wall.transform.localScale = new Vector3(1, 1, .1f);
                material.SetColor("_BaseColor", new Color(.4f, .3f, .2f)); wall.GetComponent<Renderer>().sharedMaterial = material;
                var sun = Root("Noise validation sun").AddComponent<Light>(); sun.type = LightType.Directional;
                sun.intensity = 1; sun.color = Color.black; sun.shadows = LightShadows.None;
                target.Create();
                Color[] Render(bool fogOn, bool lightOn, string label = null)
                {
                    fog.SetActive(fogOn); volume.SetActive(lightOn);
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 160, 120), 0, 0); pixels.Apply();
                    var result = pixels.GetPixels();
                    foreach (var c in result)
                        if (float.IsNaN(c.r) || float.IsInfinity(c.r) || c.r < 0 || c.g < 0 || c.b < 0)
                            throw new InvalidOperationException("Non-finite/negative fog output.");
                    if (label != null)
                    {
                        var preview = new Texture2D(160, 120, TextureFormat.RGBA32, false);
                        try { preview.SetPixels(result.Select(c => c.gamma).ToArray()); preview.Apply(); File.WriteAllBytes(Output + "/" + label + ".png", preview.EncodeToPNG()); }
                        finally { Object.DestroyImmediate(preview); }
                    }
                    return result;
                }
                float Error(Color[] a, Color[] b)
                {
                    float error = 0;
                    for (int i = 0; i < a.Length; i++)
                    {
                        if (a[i].a != b[i].a) throw new InvalidOperationException("Fog changed destination alpha.");
                        error = Mathf.Max(error, Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b));
                    }
                    return error;
                }
                void Same(Color[] a, Color[] b, string reason, float tolerance = .0015f)
                {
                    if (Error(a, b) > tolerance) throw new InvalidOperationException(reason + ": " + Error(a, b));
                }
                void Fill(float value) { constant.SetPixelData(Enumerable.Repeat(value, 8).ToArray(), 0); constant.Apply(false); }

                var clear = Render(false, false);
                var analytic = Render(true, false, "analytic");
                s.useNoiseTexture = true; s.noiseTexture = constant; Fill(.5f);
                var neutral = Render(true, false);
                report.constantDensityError = Error(analytic, neutral);
                Same(analytic, neutral, "Constant 0.5 texture must match analytic density");
                Fill(0); Same(clear, Render(true, false), "Zero texture must remove fog");
                s.noiseStrength = 0; Same(analytic, Render(true, false), "Zero strength must use analytic path", 0);
                s.noiseStrength = 1; s.noiseTexture = null;
                Same(analytic, Render(true, false), "Missing texture must use analytic path", 0);
                s.noiseTexture = constant; Fill(1);
                var doubled = Render(true, false);
                s.useNoiseTexture = false; s.density *= 2;
                Same(doubled, Render(true, false), "White texture must double density");
                s.density *= .5f; s.useNoiseTexture = true;

                s.noiseTexture = AssetDatabase.LoadAssetAtPath<Texture3D>(VolumeNoiseGenerator.DefaultPath);
                if (s.noiseTexture == null) throw new InvalidOperationException("Default periodic 3D texture is missing.");
                var textured = Render(true, false, "textured_fog");
                if (Error(analytic, textured) < .005f) throw new InvalidOperationException("3D density has no visible effect.");
                var shared = Render(true, true);
                if (!fog.LastUsedSharedNoisePass) throw new InvalidOperationException("Volumetric light did not share the noise march.");
                report.sharedExtinctionError = Error(textured, shared);
                Same(textured, shared, "Shared extinction differs from standalone fog");
                volume.settings.blurIterations = 3;
                Same(textured, Render(true, true), "Scattering blur changed extinction");
                volume.settings.mainLight = false;
                Same(textured, Render(true, true), "No light must fall back to standalone textured fog", 0);
                if (fog.LastUsedSharedNoisePass) throw new InvalidOperationException("Stale shared-pass state.");
                volume.settings.mainLight = true; volume.settings.blurIterations = 1;
                sun.color = Color.white;
                var lit = Render(true, true, "textured_scattering");
                for (int i = 0; i < lit.Length; i++) if (lit[i].r > textured[i].r + .001f) report.scatteringPixels++;
                if (report.scatteringPixels < 100) throw new InvalidOperationException("Textured scattering is missing.");
                Same(lit, Render(true, true), "Stationary noise should be temporally stable", 0);
                for (int axis = 0; axis < 3; axis++)
                {
                    var offset = Vector3.zero; offset[axis] = s.NoiseSize[axis]; s.noiseOffset = offset;
                    var wrapped = Render(true, true);
                    report.periodicError = Mathf.Max(report.periodicError, Error(lit, wrapped));
                    Same(lit, wrapped, "Full tile offset must be periodic");
                    offset[axis] = s.NoiseSize[axis] * (1 - .0001f); s.noiseOffset = offset;
                    var left = Render(true, true);
                    offset[axis] = s.NoiseSize[axis] * (1 + .0001f); s.noiseOffset = offset;
                    var right = Render(true, true);
                    report.seamError = Mathf.Max(report.seamError, Error(left, right));
                    Same(left, right, "Tile boundary has a visible discontinuity", .003f);
                }
                s.noiseOffset = new Vector3(.25f, .1f, -.2f);
                var moved = Render(true, true, "translated_scattering");
                report.movingDifference = Error(lit, moved);
                if (report.movingDifference < .005f) throw new InvalidOperationException("World-space translation did not move the noise.");

                // Check every height profile against independent scalar density and integrated-area references.
                var textureNoise = s.noiseTexture;
                var fogColor = s.color; s.color = Color.black;
                wall.SetActive(false); sun.color = Color.black;
                camera.backgroundColor = new Color(1, 1, 1, .37f);
                camera.orthographic = true; camera.orthographicSize = .0001f;
                s.heightCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(.35f, .2f), new Keyframe(1, 0));
                foreach (HeightFogFalloff mode in Enum.GetValues(typeof(HeightFogFalloff)))
                {
                    s.heightFalloff = mode; s.useNoiseTexture = false;
                    float height = s.maximumHeight - s.baseHeight;
                    camera.transform.rotation = Quaternion.identity;
                    camera.transform.position = new Vector3(0, s.baseHeight + height * .6f, -2);
                    var horizontal = Render(true, false);
                    float expectedDensityT = Mathf.Exp(-s.density * HeightFogCurveLut.Evaluate(mode, s.heightCurve, .6f) * s.distance / s.attenuationDistance);
                    float error = Mathf.Abs(horizontal[80 + 60 * 160].r - expectedDensityT);
                    report.curveDensityError = Mathf.Max(report.curveDensityError, error);
                    if (error > .0015f) throw new InvalidOperationException("Curve GPU density mismatch: " + mode + " " + error);
                    camera.transform.position = new Vector3(0, s.baseHeight - camera.nearClipPlane, -2);
                    camera.transform.rotation = Quaternion.Euler(-90, 0, 0);
                    var vertical = Render(true, false);
                    float area = HeightFogCurveLut.Bake(mode, s.heightCurve)[HeightFogCurveLut.Segments].y;
                    float expectedIntegralT = Mathf.Exp(-s.density * height * area / s.attenuationDistance);
                    error = Mathf.Abs(vertical[80 + 60 * 160].r - expectedIntegralT);
                    report.curveIntegralError = Mathf.Max(report.curveIntegralError, error);
                    if (error > .0015f) throw new InvalidOperationException("Curve GPU integral mismatch: " + mode + " " + error);
                    Fill(.5f); s.noiseTexture = constant; s.useNoiseTexture = true;
                    Same(vertical, Render(true, false), "Constant 3D noise changed height curve: " + mode);
                    s.noiseTexture = textureNoise;
                    var curveFog = Render(true, false);
                    var curveShared = Render(true, true);
                    report.curveSharedError = Mathf.Max(report.curveSharedError, Error(curveFog, curveShared));
                    Same(curveFog, curveShared, "Shared textured curve extinction mismatch: " + mode);
                }
                // Leave a custom profile enabled for the ground-clipped, tilted orthographic check below.
                s.color = fogColor; s.noiseTexture = textureNoise; wall.SetActive(true);
                camera.transform.position = new Vector3(0, .5f, -2);
                // Test orthographic depth reconstruction with a non-horizontal ray and ground clipping.
                camera.orthographic = true; camera.orthographicSize = 1;
                camera.transform.rotation = Quaternion.Euler(15, 0, 0);
                s.enableGround = true; s.groundHeight = .3f; sun.color = Color.black;
                Same(Render(true, false), Render(true, true), "Orthographic/ground shared extinction mismatch");
                sun.color = Color.white;
                Same(Render(false, false), Render(false, true), "Disabled height fog must also disable textured scattering", 0);
                foreach (var shader in new[] { fog.FogShader, volume.ScatteringShader })
                    if (ShaderUtil.GetShaderMessages(shader).Any(x => x.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                        throw new InvalidOperationException("Shader compilation failed: " + shader.name);
                report.status = "Passed";
                Debug.Log("3D fog GPU validation passed: " + Output + "/report.json");
            }
            catch (Exception e) { report.status = "Failed: " + e.Message; throw; }
            finally
            {
                fog.settings = oldFog; volume.settings = oldLight;
                for (int i = 0; i < active.Length; i++) data.rendererFeatures[i].SetActive(active[i]);
                ShaderUtil.allowAsyncCompilation = asyncCompilation; RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material); Object.DestroyImmediate(constant);
                File.WriteAllText(Output + "/report.json", JsonUtility.ToJson(report, true));
            }
        }
    }
}
