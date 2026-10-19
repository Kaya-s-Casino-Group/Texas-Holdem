using System;
using System.Linq;
using System.Reflection;
using CardInteraction;
using CardPresentation.Editor;
using NUnit.Framework;
using RetroRendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CardUICameraTests
    {
        [Test] public void RendererKeepsNativeUIAndOnlyAppliesCrtToTheFinalStackOutput()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CardUICameraSetup.RendererPath);
            Assert.IsNotNull(data); Assert.AreEqual(RenderingMode.Forward, data.renderingMode);
            Assert.AreEqual(1, data.rendererFeatures.Count);
            var crt = data.rendererFeatures.Single() as CRTFeature;
            Assert.IsNotNull(crt); Assert.IsInstanceOf<ICameraStackOutputFeature>(crt);
            Assert.IsTrue(crt.isActive); Assert.IsNotNull(crt.CRTShader);
            Assert.IsNull(data.postProcessData);
            Assert.AreEqual(0, data.opaqueLayerMask.value); Assert.AreEqual(1 << LayerMask.NameToLayer("UI"), data.transparentLayerMask.value);
            Assert.IsFalse(data.lowResolution.enabled); Assert.IsFalse(data.shadowTransparentReceive);
            Assert.AreEqual(DepthPrimingMode.Disabled, data.depthPrimingMode); Assert.AreEqual(IntermediateTextureMode.Auto, data.intermediateTextureMode);
            var main = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
            Assert.AreEqual(RenderingMode.Deferred, main.renderingMode); Assert.IsTrue(main.lowResolution.enabled);
            Assert.AreEqual(1, main.rendererFeatures.OfType<RetroPostProcessFeature>().Count());
            var pipeline = new SerializedObject(GraphicsSettings.currentRenderPipeline);
            var list = pipeline.FindProperty("m_RendererDataList");
            Assert.AreEqual(1, Enumerable.Range(0, list.arraySize).Count(i => list.GetArrayElementAtIndex(i).objectReferenceValue == data));
            Assert.IsNotNull(AssetImporter.GetAtPath(CardUICameraSetup.RendererPath));
        }

        [Test] public void StackReusesUICameraPreservesOtherOverlaysAndSynchronizesProjectionForWorldUI()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Scene camera"); SceneManager.MoveGameObjectToScene(root, scene);
                var camera = root.AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.fieldOfView = 47; camera.nearClipPlane = 0.01f; camera.farClipPlane = 4;
                var oldOverlayRoot = new GameObject("Existing overlay"); SceneManager.MoveGameObjectToScene(oldOverlayRoot, scene);
                var oldOverlay = oldOverlayRoot.AddComponent<Camera>(); oldOverlay.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
                camera.GetUniversalAdditionalCameraData().cameraStack.Add(oldOverlay);
                var ui = CardUICameraSetup.Register(scene, camera); var rig = ui.GetComponent<UICameraStack>();
                Assert.AreSame(ui, CardUICameraSetup.Register(scene, camera));
                CollectionAssert.AreEqual(new[] { oldOverlay, ui }, camera.GetUniversalAdditionalCameraData().cameraStack);
                Assert.AreEqual(0, camera.cullingMask & (1 << 5)); Assert.AreEqual(1 << 5, ui.cullingMask);
                Assert.IsFalse(ui.allowHDR); Assert.IsFalse(ui.allowMSAA); Assert.IsFalse(ui.useOcclusionCulling);
                var data = ui.GetUniversalAdditionalCameraData();
                Assert.AreEqual(CameraRenderType.Overlay, data.renderType); Assert.IsTrue(data.clearDepth);
                Assert.IsTrue(data.renderOverlayAtNativeResolution);
                Assert.IsFalse(data.renderShadows); Assert.IsFalse(data.renderPostProcessing);
                Assert.AreEqual(CameraOverrideOption.Off, data.requiresDepthOption); Assert.AreEqual(CameraOverrideOption.Off, data.requiresColorOption);
                Assert.AreEqual(0, data.volumeLayerMask.value); Assert.AreEqual(VolumeFrameworkUpdateMode.ViaScripting, ui.GetVolumeFrameworkUpdateMode());
                camera.transform.SetPositionAndRotation(new Vector3(1, 2, -3), Quaternion.Euler(18, 12, 0));
                camera.fieldOfView = 67; camera.rect = new Rect(0.1f, 0.1f, 0.8f, 0.8f); rig.Synchronize();
                var point = camera.transform.position + camera.transform.forward + camera.transform.up * 0.2f;
                Assert.AreEqual(camera.WorldToViewportPoint(point), ui.WorldToViewportPoint(point));
                Assert.AreEqual(camera.rect, ui.rect); Assert.AreEqual(camera.projectionMatrix, ui.projectionMatrix);
                camera.orthographic = true; camera.orthographicSize = 0.24f; rig.Synchronize();
                Assert.IsTrue(ui.orthographic); Assert.AreEqual(camera.orthographicSize, ui.orthographicSize);
                Assert.AreEqual(camera.WorldToViewportPoint(point), ui.WorldToViewportPoint(point));
                Object.DestroyImmediate(ui.gameObject); CollectionAssert.AreEqual(new[] { oldOverlay }, camera.GetUniversalAdditionalCameraData().cameraStack);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test] public void NativeUIStackDrawsSinglePixelStripesAfterSceneResolveAndDisabledUILeavesScene()
        {
            ValidateNativeUIStack(1280,720,false);
            ValidateNativeUIStack(1920,1080,true);
            ValidateNativeUIStack(800,450,false);
        }

        [Test]
        public void OverlaysAppendedAfterUIStayBeforeNativeUIOnTheirFirstRender()
        {
            ValidateNativeUIStack(1280, 720, true, appendAfterUI: true);
            ValidateNativeUIStack(1920, 1080, true, appendAfterUI: true);
        }

        private static void ValidateNativeUIStack(int width, int height, bool sceneOverlay, bool appendAfterUI = false)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var stripe = new Texture2D(64,1,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
            var pixels = new Texture2D(width,height,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            var mainData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
            var uiData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CardUICameraSetup.RendererPath);
            var crt = uiData.rendererFeatures.OfType<CRTFeature>().Single();
            bool savedCrtEnabled = crt.settings.crtEnabled;
            Camera ui = null;
            // Off-screen preview requests need explicit Canvas geometry emission for the overlay camera.
            void EmitPreviewCanvas(ScriptableRenderContext context, Camera current)
            { if (current == ui) ScriptableRenderContext.EmitGeometryForCamera(current); }
            RenderPipelineManager.beginCameraRendering += EmitPreviewCanvas;
            try
            {
                // Inspect native UI before the intentional final-screen curvature, scanlines and mask.
                // Restore the authored CRT setting even if an assertion fails.
                crt.settings.crtEnabled = false;
                var cameraRoot = new GameObject("Native UI scene test"); SceneManager.MoveGameObjectToScene(cameraRoot,scene);
                var camera = cameraRoot.AddComponent<Camera>(); camera.scene = scene; camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f,.3f,.4f);
                camera.cullingMask = 0; camera.orthographic = true; camera.orthographicSize = height/2f;
                camera.transform.position = new Vector3(0,0,-10); camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.GetUniversalAdditionalCameraData().SetRenderer(0);
                if (sceneOverlay)
                {
                    var extraRoot = new GameObject("Existing scene overlay"); SceneManager.MoveGameObjectToScene(extraRoot,scene);
                    var extra = extraRoot.AddComponent<Camera>(); extra.scene = scene; extra.cullingMask = 0;
                    var data = extra.GetUniversalAdditionalCameraData(); data.renderType = CameraRenderType.Overlay; data.SetRenderer(1);
                    camera.GetUniversalAdditionalCameraData().cameraStack.Add(extra);
                }
                ui = CardUICameraSetup.Register(scene,camera); ui.scene = scene;
                var colors = Enumerable.Range(0,64).Select(index => index%2 == 0 ? Color.black : Color.white).ToArray();
                stripe.SetPixels(colors); stripe.Apply();
                var canvasRoot = new GameObject("Pixel probe",typeof(RectTransform),typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasRoot,scene); canvasRoot.layer = 5;
                canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                canvasRoot.GetComponent<Canvas>().worldCamera = ui;
                ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(width,height);
                var probe = new GameObject("One pixel stripes",typeof(RectTransform),typeof(CanvasRenderer),typeof(UnityEngine.UI.RawImage));
                probe.transform.SetParent(canvasRoot.transform,false); probe.layer = 5;
                ((RectTransform)probe.transform).sizeDelta = new Vector2(64,32);
                probe.GetComponent<UnityEngine.UI.RawImage>().texture = stripe;
                target.Create(); Canvas.ForceUpdateCanvases();
                if (appendAfterUI)
                {
                    // Render once, then mutate the stack without Configure/Synchronize/LateUpdate.
                    // The very next render must classify UI as native, not just reorder it after classification.
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest { destination = target });
                    var stack = camera.GetUniversalAdditionalCameraData().cameraStack;
                    var expected = stack.Where(item => item != ui).ToList();
                    for (int index = 0; index < 2; ++index)
                    {
                        var addedRoot = new GameObject("Runtime overlay " + index);
                        SceneManager.MoveGameObjectToScene(addedRoot,scene);
                        var added = addedRoot.AddComponent<Camera>(); added.scene = scene; added.cullingMask = 0;
                        var data = added.GetUniversalAdditionalCameraData();
                        data.renderType = CameraRenderType.Overlay; data.SetRenderer(1);
                        stack.Add(added); expected.Add(added);
                    }
                    expected.Add(ui);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest { destination = target });
                    CollectionAssert.AreEqual(expected,stack,"Runtime overlays must retain their order ahead of native UI.");
                }
                else
                {
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest { destination = target });
                }
                var info = ((UniversalRenderer)ui.GetUniversalAdditionalCameraData().scriptableRenderer).lastCameraRenderTargetInfo;
                Assert.AreEqual(new Vector2Int(width,height),info.renderSize); Assert.AreEqual(info.outputSize,info.colorTargetSize);
                Assert.IsFalse(info.lowResolution); Assert.AreEqual("Native",info.finalFilter);
                var mainInfo = ((UniversalRenderer)camera.GetUniversalAdditionalCameraData().scriptableRenderer).lastCameraRenderTargetInfo;
                Assert.AreEqual(LowResolutionSettings.CalculateRenderSize(width,height,mainData.lowResolution.maximumHeight),mainInfo.renderSize);
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                var line = Enumerable.Range(0,64).Select(index => pixels.GetPixel(width/2-32+index,height/2).r).ToArray();
                for (int index = 1; index < line.Length; index++) Assert.Greater(Mathf.Abs(line[index]-line[index-1]),.8f,
                    "UI must preserve one-output-pixel stripes: " + width + " x " + height + " pixel " + index);
                Assert.Greater(pixels.GetPixel(4,4).maxColorComponent,.05f,"Scene color must be preserved under UI.");
                ui.enabled = false;
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
                Assert.Less(Mathf.Abs(pixels.GetPixel(width/2,height/2).r-pixels.GetPixel(width/2+1,height/2).r),.1f);
            }
            finally
            {
                crt.settings.crtEnabled = savedCrtEnabled;
                RenderPipelineManager.beginCameraRendering -= EmitPreviewCanvas;
                RenderTexture.active = previous;
                foreach (var camera in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true))) camera.targetTexture = null;
                EditorSceneManager.ClosePreviewScene(scene); target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(stripe); Object.DestroyImmediate(pixels);
            }
        }

        [Test] public void CompactPrefabHasTwoRowsAnchoredToBottomLeftAndNoSceneCameraReference()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandActionPanelSetup.PrefabPath);
            var canvas = prefab.GetComponent<Canvas>();
            Assert.AreEqual((int)RenderMode.ScreenSpaceCamera, new SerializedObject(canvas).FindProperty("m_RenderMode").intValue);
            Assert.IsNull(canvas.worldCamera);
            var bar = (RectTransform)prefab.transform.Find("Actions");
            Assert.AreEqual(Vector2.zero, bar.anchorMin); Assert.AreEqual(Vector2.zero, bar.anchorMax); Assert.AreEqual(Vector2.zero, bar.pivot);
            Assert.AreEqual(new Vector2(16, 16), bar.anchoredPosition); Assert.AreEqual(Vector3.one * 0.75f, bar.localScale);
            Assert.AreEqual(new Vector2(316, 124), bar.sizeDelta);
            Assert.AreEqual(((RectTransform)bar.Find("Play")).anchoredPosition.y, ((RectTransform)bar.Find("Discard")).anchoredPosition.y);
            Assert.AreEqual(((RectTransform)bar.Find("RankSort")).anchoredPosition.y, ((RectTransform)bar.Find("SuitSort")).anchoredPosition.y);
        }

        [Test] public void RetroPostRunsForBaseBeforeStackResolveAndNeverGradesUIOverlay()
        {
            var scene = EditorSceneManager.NewPreviewScene(); var feature = ScriptableObject.CreateInstance<RetroPostProcessFeature>();
            UniversalRenderer renderer = null;
            try
            {
                feature.PostShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/GameAssets/Rendering/Shaders/RetroPost.shader"); feature.Create();
                renderer = new UniversalRenderer(AssetDatabase.LoadAssetAtPath<UniversalRendererData>(CardUICameraSetup.RendererPath));
                var root = new GameObject("Stack post test camera"); SceneManager.MoveGameObjectToScene(root, scene); var camera = root.AddComponent<Camera>();
                var data = new RenderingData { cameraData = new CameraData { camera = camera, cameraType = CameraType.Game,
                    renderType = CameraRenderType.Base, resolveFinalTarget = false, cameraTargetDescriptor = new RenderTextureDescriptor(64, 64) } };
                var queue = typeof(ScriptableRenderer).GetProperty("activeRenderPassQueue", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(queue);
                int Count() => ((System.Collections.ICollection)queue.GetValue(renderer)).Count;
                int before = Count(); feature.AddRenderPasses(renderer, ref data); Assert.AreEqual(before + 1, Count());
                data.cameraData.renderType = CameraRenderType.Overlay; data.cameraData.resolveFinalTarget = true;
                feature.AddRenderPasses(renderer, ref data); Assert.AreEqual(before + 1, Count());
            }
            finally { renderer?.Dispose(); Object.DestroyImmediate(feature); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
