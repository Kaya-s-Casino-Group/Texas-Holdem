using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RetroRendering.Tests
{
    public sealed class BloomTests
    {
        [Test]
        public void InstalledAfterVolumetricBeforeRetroAndAbsentFromUI()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
            var bloom = data.rendererFeatures.OfType<BloomFeature>().Single();
            Assert.IsNotNull(bloom.BloomShader);
            Assert.AreEqual("Assets/Rendering/Shaders/Bloom.shader", AssetDatabase.GetAssetPath(bloom.BloomShader));
            Assert.Less(data.rendererFeatures.FindIndex(x => x is VolumetricLightFeature), data.rendererFeatures.IndexOf(bloom));
            Assert.Less(data.rendererFeatures.IndexOf(bloom), data.rendererFeatures.FindIndex(x => x is RetroPostProcessFeature));
            Assert.IsEmpty(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UIRenderer.asset").rendererFeatures);
        }

        [Test]
        public void RenamedVolumetricScriptsAndShaderRetainTheirGuids()
        {
            var feature = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath("21463d936f5aa4c40b06efe0c63f5dd8"));
            var light = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath("b77cbf9117e664170be6d7483330c63d"));
            Assert.AreEqual(typeof(VolumetricLightFeature), feature.GetClass());
            Assert.AreEqual(typeof(VolumetricLight), light.GetClass());
            Assert.AreEqual("Assets/Rendering/Shaders/VolumetricLight.shader", AssetDatabase.GUIDToAssetPath("80b009c0fb9444914a36ed0420b6022b"));
        }

        [Test]
        public void BloomNeedsColorOnlyAndSkipsZeroIntensityOverlayAndExcludedCameras()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var go = new GameObject("Bloom test camera"); SceneManager.MoveGameObjectToScene(go, scene);
            var bloom = ScriptableObject.CreateInstance<BloomFeature>();
            UniversalRenderer renderer = null;
            var target = new RenderTexture(64, 64, 24);
            try
            {
                bloom.BloomShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Rendering/Shaders/Bloom.shader"); bloom.Create();
                renderer = new UniversalRenderer(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UIRenderer.asset"));
                var data = new RenderingData { cameraData = new CameraData { camera = go.AddComponent<Camera>(),
                    cameraType = CameraType.Game, renderType = CameraRenderType.Base, resolveFinalTarget = false,
                    cameraTargetDescriptor = target.descriptor } };
                var queue = (List<ScriptableRenderPass>)typeof(ScriptableRenderer)
                    .GetProperty("activeRenderPassQueue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
                bloom.AddRenderPasses(renderer, ref data);
                Assert.AreEqual(1, queue.Count);
                Assert.AreEqual(RenderPassEvent.BeforeUpscaling, queue[0].renderPassEvent);
                Assert.AreEqual(ScriptableRenderPassInput.None, queue[0].input);
                Assert.IsTrue(queue[0].requiresIntermediateTexture); queue.Clear();
                bloom.settings.intensity = 0; bloom.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                bloom.settings.intensity = 1; data.cameraData.renderType = CameraRenderType.Overlay;
                bloom.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                data.cameraData.renderType = CameraRenderType.Base; data.cameraData.cameraType = CameraType.Preview;
                bloom.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                data.cameraData.cameraType = CameraType.Game; data.cameraData.targetTexture = target;
                bloom.settings.applyToRenderTextures = false;
                bloom.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
            }
            finally
            {
                renderer?.Dispose(); bloom.Dispose(); Object.DestroyImmediate(bloom); Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
