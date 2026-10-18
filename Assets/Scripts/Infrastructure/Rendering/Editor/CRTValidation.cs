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
    public static class CRTValidation
    {
        const string Output = "Library/CRTValidation";
        [MenuItem("工具/渲染/老电视 CRT/验证 CRT")]
        public static void Validate()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Retro540Setup.RendererPath);
            var uiRenderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CRTSetup.RendererPath);
            var retro = uiRenderer.rendererFeatures.OfType<CRTFeature>().Single();
            var active = renderer.rendererFeatures.Select(f=>f.isActive).ToArray();
            var settings = retro.settings;
            var low = renderer.lowResolution;
            bool enabled = low.enabled, rt = low.applyToRenderTextures;
            int height = low.maximumHeight;
            bool async = ShaderUtil.allowAsyncCompilation;
            Directory.CreateDirectory(Output);
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var f in renderer.rendererFeatures) f.SetActive(false);
                retro.Create();
                retro.SetActive(true);
                retro.settings = new CRTFeature.Settings();
                low.enabled = low.applyToRenderTextures = true; low.maximumHeight = 180;
                foreach (var size in new[] {new Vector2Int(720,360),new Vector2Int(360,720),new Vector2Int(802,451),new Vector2Int(320,180)})
                    Capture(retro,size.x,size.y);
                if (ShaderUtil.GetShaderMessages(retro.CRTShader).Any(m=>m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error))
                    throw new InvalidOperationException("CRT Shader compilation failed.");
                File.WriteAllText(Output+"/report.txt","Passed: landscape, portrait, noninteger scaling, native size; zero intensity bypass; neutral CRT color/alpha; native CRT allocation; curved corners; output scanlines; native overlay UI shares CRT; execution on final UI camera; bypass and native size preserved.");
                Debug.Log("CRT GPU validation passed: " + Output);
            }
            finally
            {
                retro.settings = settings;
                for(int i=0;i<active.Length;i++) renderer.rendererFeatures[i].SetActive(active[i]);
                low.enabled=enabled; low.applyToRenderTextures=rt; low.maximumHeight=height;
                ShaderUtil.allowAsyncCompilation=async;
            }
        }
        static void Capture(CRTFeature retro,int w,int h)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var target=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(w,h,TextureFormat.RGBA32,false);
            var previous=RenderTexture.active;
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            Camera camera=null;
            try
            {
                material.SetColor("_BaseColor",new Color(.8f,.2f,.1f,1));
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(cube,scene);
                cube.GetComponent<Renderer>().sharedMaterial=material;
                cube.transform.position=new Vector3(.3f,.3f,0); cube.transform.localScale=Vector3.one*.6f;
                var go=new GameObject("CRT validation camera"); SceneManager.MoveGameObjectToScene(go,scene);
                camera=go.AddComponent<Camera>(); camera.scene=scene;
                camera.transform.position=new Vector3(0,0,-3); camera.orthographic=true; camera.orthographicSize=1.5f;
                camera.nearClipPlane=.1f; camera.farClipPlane=10;
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.35f,.35f,.35f,.65f);
                camera.allowHDR=camera.allowMSAA=false; camera.targetTexture=target;
                camera.GetUniversalAdditionalCameraData().SetRenderer(0); target.Create();
                // Native-resolution UI is part of the same final CRT screen.
                var uiObject=new GameObject("Native UI validation camera"); SceneManager.MoveGameObjectToScene(uiObject,scene);
                var ui=uiObject.AddComponent<Camera>(); ui.scene=scene;
                ui.CopyFrom(camera); ui.targetTexture=null; ui.clearFlags=CameraClearFlags.Depth;
                ui.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
                ui.cullingMask=1<<5; camera.cullingMask &= ~(1<<5);
                var uiData=ui.GetUniversalAdditionalCameraData(); uiData.SetRenderer(1);
                uiData.renderType=CameraRenderType.Overlay; uiData.renderOverlayAtNativeResolution=true;
                camera.GetUniversalAdditionalCameraData().cameraStack.Add(ui);
                material.renderQueue=3000; // UI Renderer draws only the transparent queue.
                var marker=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(marker,scene);
                marker.layer=5; marker.transform.position=new Vector3(0,0,-1); marker.transform.localScale=Vector3.one*.3f;
                marker.GetComponent<Renderer>().sharedMaterial=material;

                Color32[] Render(string suffix)
                {
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest {destination=target});
                    RenderTexture.active=target; pixels.ReadPixels(new Rect(0,0,w,h),0,0); pixels.Apply();
                    File.WriteAllBytes($"{Output}/{w}x{h}_{suffix}.png",pixels.EncodeToPNG());
                    return pixels.GetPixels32();
                }
                var s=retro.settings;
                s.crtEnabled=false; var baseline=Render("off");
                s.crtEnabled=true; s.crtIntensity=0;
                if(!baseline.SequenceEqual(Render("zero"))) throw new InvalidOperationException("Zero CRT must exactly match disabled.");
                s.crtIntensity=.35f; s.crtCurvature=0; s.crtEdgeSoftness=.001f; s.crtScanlineStrength=s.crtMaskStrength=0;
                var neutral=Render("neutral");
                for(int y=4;y<h-4;y++) for(int x=4;x<w-4;x++)
                {
                    int i=y*w+x;
                    if(Math.Abs(baseline[i].r-neutral[i].r)>2 || Math.Abs(baseline[i].g-neutral[i].g)>2 || baseline[i].a!=neutral[i].a)
                        throw new InvalidOperationException($"Neutral CRT color/orientation/alpha mismatch at {w}x{h}: {x},{y}, {baseline[i]} vs {neutral[i]}");
                }
                if(retro.LastCRTSize!=new Vector2Int(w,h)) throw new InvalidOperationException($"CRT must use output resolution: expected {w}x{h}, got {retro.LastCRTSize}.");
                s.crtCurvature=.025f; s.crtEdgeSoftness=.015f; s.crtScanlineStrength=.3f; s.crtMaskStrength=.2f;
                var crt=Render("on");
                if(crt[0].r>=baseline[0].r/2) throw new InvalidOperationException("Curved screen corners were not masked.");
                if(Math.Abs(crt[(h/2)*w+w/10].r-crt[(h/2+2)*w+w/10].r)<2)
                    throw new InvalidOperationException("Missing output scanline modulation.");
                if (retro.LastPresentedCamera != ui.GetInstanceID()) throw new InvalidOperationException("CRT must execute on the final UI camera.");
                int center=(h/2)*w+w/2;
                if (baseline[center].r < 120 || baseline[center].Equals(crt[center]))
                    throw new InvalidOperationException("Native UI marker did not receive CRT.");
                // Feature-off must take the direct stack path and match the initial image exactly.
                retro.SetActive(false);
                if (!baseline.SequenceEqual(Render("feature_off"))) throw new InvalidOperationException("Disabling final CRT changed the direct output.");
                retro.SetActive(true);


            }
            finally
            {
                RenderTexture.active=previous; if(camera) camera.targetTexture=null;
                EditorSceneManager.ClosePreviewScene(scene); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(material);
            }
        }
        [MenuItem("工具/渲染/老电视 CRT/捕获当前 CRT 对照")]
        public static void CaptureCurrent()
        {
            var camera=Camera.main;
            if(!camera) throw new InvalidOperationException("No main camera.");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Retro540Setup.RendererPath);
            var uiRenderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CRTSetup.RendererPath);
            var retro=uiRenderer.rendererFeatures.OfType<CRTFeature>().Single();
            bool original=retro.settings.crtEnabled;
            var target=new RenderTexture(camera.pixelWidth,camera.pixelHeight,24,RenderTextureFormat.ARGB32);
            try
            {
                Directory.CreateDirectory(Output); target.Create();
                foreach (bool enabled in new[] {false,true})
                {
                    retro.settings.crtEnabled=enabled;
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest {destination=target});
                    var previous=RenderTexture.active;
                    var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                    try
                    {
                        RenderTexture.active=target; image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                        File.WriteAllBytes(Output+(enabled ? "/Current_on.png" : "/Current_off.png"),image.EncodeToPNG());
                    }
                    finally { RenderTexture.active=previous; Object.DestroyImmediate(image); }
                }
            }
            finally { retro.settings.crtEnabled=original; target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
