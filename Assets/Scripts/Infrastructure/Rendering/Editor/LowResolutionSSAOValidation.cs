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
    /// <summary>Real GPU regression in an isolated preview scene; preserves renderer settings and the open scene.</summary>
    public static class LowResolutionSSAOValidation
    {
        const string Output = "Library/LowResolutionSSAOValidation";
        [Serializable] sealed class Report { public string status; public Row[] rows; }
        [Serializable] sealed class Row
        {
            public int width, height, changedPixels;
            public bool orthographic;
            public Vector2Int aoSize;
            public float maximumDarkening;
        }
        [MenuItem("工具/渲染/环境光遮蔽/验证低分辨率效果")]
        public static void Validate()
        {
            if (Application.isPlaying) throw new InvalidOperationException("请退出 Play 后验证 SSAO。");
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(LowResolutionSSAOSetup.RendererPath);
            var feature = data.rendererFeatures.OfType<LowResolutionSSAOFeature>().Single();
            var active = data.rendererFeatures.Select(item => item.isActive).ToArray();
            var settings = feature.settings;
            var lowResolution = data.lowResolution.enabled;
            var renderTextures = data.lowResolution.applyToRenderTextures;
            var maxHeight = data.lowResolution.maximumHeight;
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var item in data.rendererFeatures) item.SetActive(false);
                feature.settings = new LowResolutionSSAOFeature.Settings { intensity = 2, radius = .12f };
                data.lowResolution.enabled = data.lowResolution.applyToRenderTextures = true;
                data.lowResolution.maximumHeight = 540;
                var rows = new[] { Capture(feature,1920,1080,false), Capture(feature,800,1200,false), Capture(feature,800,450,true) };
                if (ShaderUtil.GetShaderMessages(feature.AOShader).Any(message => message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                    throw new InvalidOperationException("SSAO Shader compilation failed.");
                File.WriteAllText(Output+"/report.json",JsonUtility.ToJson(new Report { status = "Passed", rows = rows },true));
                Debug.Log("Low resolution SSAO GPU validation passed: " + Output + "/report.json");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = asyncCompilation;
                feature.settings = settings;
                for (int i = 0; i < active.Length; ++i) data.rendererFeatures[i].SetActive(active[i]);
                data.lowResolution.enabled = lowResolution; data.lowResolution.applyToRenderTextures = renderTextures;
                data.lowResolution.maximumHeight = maxHeight;
            }
        }

        static Row Capture(LowResolutionSSAOFeature feature, int width, int height, bool orthographic)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(width,height,TextureFormat.RGBA32,false);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                material.color = Color.white;
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",new Color(.7f,.7f,.7f));
                material.SetFloat("_Smoothness",0);
                GameObject Primitive(string name, Vector3 position, Vector3 scale)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                    SceneManager.MoveGameObjectToScene(go,scene);
                    go.transform.position = position; go.transform.localScale = scale;
                    go.GetComponent<Renderer>().sharedMaterial = material; return go;
                }
                Primitive("Floor",new Vector3(0,-.025f,0),new Vector3(2,.05f,2));
                Primitive("Contact cube",new Vector3(0,.075f,0),Vector3.one*.15f);
                var root = new GameObject("SSAO GPU test"); SceneManager.MoveGameObjectToScene(root,scene);
                camera = root.AddComponent<Camera>(); camera.scene = scene;
                camera.transform.position = new Vector3(.4f,.32f,-.45f); camera.transform.LookAt(new Vector3(0,.06f,0));
                camera.nearClipPlane = .01f; camera.farClipPlane = 5; camera.fieldOfView = 50;
                camera.orthographic = orthographic; camera.orthographicSize = .35f;
                camera.allowHDR = camera.allowMSAA = false;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.targetTexture = target; camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                target.Create();
                Color32[] Render(bool enabled, string suffix)
                {
                    feature.SetActive(enabled);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                    File.WriteAllBytes($"{Output}/{width}x{height}_{suffix}.png",pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                var baseline = Render(false,"off");
                var enabledPixels = Render(true,"on");
                var row = new Row { width = width,height = height,orthographic = orthographic,aoSize = feature.LastAOSize };
                var expected = LowResolutionSettings.CalculateRenderSize(width,height,540);
                if (row.aoSize != expected || feature.LastExecutionEvent != RenderPassEvent.BeforeUpscaling)
                    throw new InvalidOperationException("AO resolution/event mismatch.");
                for (int i = 0; i < baseline.Length; ++i)
                {
                    int delta = baseline[i].r-enabledPixels[i].r;
                    if (delta > 2) row.changedPixels++;
                    row.maximumDarkening = Mathf.Max(row.maximumDarkening,delta/255f);
                    if (delta < -1 || baseline[i].a != enabledPixels[i].a)
                        throw new InvalidOperationException("AO brightened the image or modified alpha.");
                }
                if (row.changedPixels < 20 || row.maximumDarkening < .02f)
                    throw new InvalidOperationException("AO did not darken contact geometry.");
                feature.settings.intensity = 0;
                var zero = Render(true,"zero");
                feature.settings.intensity = 2;
                if (!baseline.SequenceEqual(zero)) throw new InvalidOperationException("Zero intensity must match disabled AO exactly.");
                if (width == 1920 && height == 1080)
                    for (int y = 0; y < height; y += 2)
                    for (int x = 0; x < width; x += 2)
                    {
                        var c = enabledPixels[y*width+x];
                        if (!c.Equals(enabledPixels[y*width+x+1]) || !c.Equals(enabledPixels[(y+1)*width+x]) || !c.Equals(enabledPixels[(y+1)*width+x+1]))
                            throw new InvalidOperationException("AO broke Point upscale pixel blocks.");
                    }
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
    }
}
