using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Tests
{
    [TestFixture]
    public sealed class LowResolutionSSAOTests
    {
        [Test]
        public void InstalledBeforeRetroWithPersistentShaderAndNoUIFeature()
        {
            var main = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
            var ao = main.rendererFeatures.OfType<LowResolutionSSAOFeature>().Single();
            Assert.IsTrue(ao.isActive);
            Assert.IsNotNull(ao.AOShader);
            Assert.AreEqual("Assets/GameAssets/Rendering/Shaders/LowResolutionSSAO.shader",AssetDatabase.GetAssetPath(ao.AOShader));
            Assert.Less(main.rendererFeatures.IndexOf(ao),main.rendererFeatures.FindIndex(item => item is RetroPostProcessFeature));
            var ui = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UIRenderer.asset");
            Assert.IsFalse(ui.rendererFeatures.OfType<LowResolutionSSAOFeature>().Any());
        }

        [Test]
        public void UsesDepthOnlyBeforeUpscalingForBaseAndSkipsOverlayZeroAndExcludedCameras()
        {
            var go = new GameObject("SSAO scope test");
            var feature = ScriptableObject.CreateInstance<LowResolutionSSAOFeature>();
            UniversalRenderer renderer = null;
            var target = new RenderTexture(64,64,24);
            try
            {
                feature.AOShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/GameAssets/Rendering/Shaders/LowResolutionSSAO.shader");
                feature.Create();
                renderer = new UniversalRenderer(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UIRenderer.asset"));
                var data = new RenderingData { cameraData = new CameraData { camera = go.AddComponent<Camera>(),
                    cameraType = CameraType.Game,renderType = CameraRenderType.Base,resolveFinalTarget = false,
                    cameraTargetDescriptor = target.descriptor } };
                var queue = (List<ScriptableRenderPass>)typeof(ScriptableRenderer)
                    .GetProperty("activeRenderPassQueue",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(renderer);
                feature.AddRenderPasses(renderer,ref data);
                Assert.AreEqual(1,queue.Count);
                Assert.AreEqual(RenderPassEvent.BeforeUpscaling,queue[0].renderPassEvent);
                Assert.AreEqual(ScriptableRenderPassInput.Depth,queue[0].input);
                Assert.IsTrue(queue[0].requiresIntermediateTexture);
                queue.Clear();
                data.cameraData.renderType = CameraRenderType.Overlay;
                feature.AddRenderPasses(renderer,ref data); Assert.IsEmpty(queue);
                data.cameraData.renderType = CameraRenderType.Base;
                feature.settings.intensity = 0;
                feature.AddRenderPasses(renderer,ref data); Assert.IsEmpty(queue);
                feature.settings.intensity = 1;
                data.cameraData.targetTexture = target; feature.settings.applyToRenderTextures = false;
                feature.AddRenderPasses(renderer,ref data); Assert.IsEmpty(queue);
                data.cameraData.targetTexture = null; data.cameraData.cameraType = CameraType.Preview;
                feature.AddRenderPasses(renderer,ref data); Assert.IsEmpty(queue);
            }
            finally
            {
                renderer?.Dispose(); feature.Dispose();
                Object.DestroyImmediate(feature); Object.DestroyImmediate(go); Object.DestroyImmediate(target);
            }
        }
    }
}
