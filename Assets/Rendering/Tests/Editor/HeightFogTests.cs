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
    public sealed class HeightFogTests
    {
        [Test]
        public void InstalledDependenciesAndOrderPreserveCustomRendererAndUI()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
            var fog = data.rendererFeatures.OfType<HeightFogFeature>().Single();
            var light = data.rendererFeatures.OfType<VolumetricLightFeature>().Single();
            Assert.AreSame(fog, light.heightFog);
            Assert.IsTrue(fog.isActive && light.isActive);
            Assert.IsNotNull(fog.FogShader); Assert.IsNotNull(light.ScatteringShader);
            Assert.Less(data.rendererFeatures.FindIndex(x => x is LowResolutionSSAOFeature), data.rendererFeatures.IndexOf(fog));
            Assert.Less(data.rendererFeatures.IndexOf(fog), data.rendererFeatures.IndexOf(light));
            Assert.Less(data.rendererFeatures.IndexOf(light), data.rendererFeatures.FindIndex(x => x is RetroPostProcessFeature));
            Assert.IsTrue(data.lowResolution.enabled);
            Assert.AreEqual(540, data.lowResolution.maximumHeight);
            Assert.AreEqual(RenderingMode.Deferred, data.renderingMode);
            var ui = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UIRenderer.asset");
            Assert.IsEmpty(ui.rendererFeatures);
        }

        [Test]
        public void DisabledGroundDoesNotInvalidateFogAndInvalidHeightRangeDoes()
        {
            var settings = new HeightFogFeature.Settings { groundHeight = 100, enableGround = false };
            Assert.IsTrue(settings.IsValid);
            settings.enableGround = true; Assert.IsFalse(settings.IsValid);
            settings.enableGround = false; settings.maximumHeight = settings.baseHeight; Assert.IsFalse(settings.IsValid);
        }

        [Test]
        public void DependencyRequiresSameCameraRendererAndPrecedingFogPass()
        {
            var fog = ScriptableObject.CreateInstance<HeightFogFeature>();
            var light = ScriptableObject.CreateInstance<VolumetricLightFeature>();
            var go = new GameObject("Height fog scope test");
            UniversalRenderer renderer = null, other = null;
            try
            {
                fog.FogShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Rendering/Shaders/HeightFog.shader"); fog.Create();
                light.ScatteringShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Rendering/Shaders/VolumetricLight.shader");
                light.heightFog = fog; light.Create();
                var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/UIRenderer.asset");
                renderer = new UniversalRenderer(rendererData); other = new UniversalRenderer(rendererData);
                var camera = go.AddComponent<Camera>();
                var data = new RenderingData { cameraData = new CameraData { camera = camera, cameraType = CameraType.Game,
                    renderType = CameraRenderType.Base, resolveFinalTarget = false,
                    cameraTargetDescriptor = new RenderTextureDescriptor(64, 64) },
                    lightData = new LightData { mainLightIndex = 0 } };
                List<ScriptableRenderPass> Queue(ScriptableRenderer r) => (List<ScriptableRenderPass>)typeof(ScriptableRenderer)
                    .GetProperty("activeRenderPassQueue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(r);
                var queue = Queue(renderer);
                light.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                fog.AddRenderPasses(renderer, ref data); light.AddRenderPasses(renderer, ref data);
                Assert.AreEqual(2, queue.Count);
                foreach (var pass in queue)
                {
                    Assert.AreEqual(RenderPassEvent.BeforeUpscaling, pass.renderPassEvent);
                    Assert.AreEqual(ScriptableRenderPassInput.Depth, pass.input);
                    Assert.IsTrue(pass.requiresIntermediateTexture);
                }
                light.AddRenderPasses(other, ref data); Assert.IsEmpty(Queue(other));
                queue.Clear(); fog.SetActive(false);
                light.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                fog.SetActive(true);
                data.cameraData.renderType = CameraRenderType.Overlay;
                fog.AddRenderPasses(renderer, ref data); light.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                data.cameraData.renderType = CameraRenderType.Base; fog.settings.density = 0;
                fog.AddRenderPasses(renderer, ref data); light.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                fog.settings.density = 1; data.cameraData.cameraType = CameraType.Preview;
                fog.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
                data.cameraData.cameraType = CameraType.Game;
                fog.AddRenderPasses(renderer, ref data); queue.Clear();
                fog.OnCameraPreCull(renderer, in data.cameraData);
                light.AddRenderPasses(renderer, ref data); Assert.IsEmpty(queue);
            }
            finally
            {
                renderer?.Dispose(); other?.Dispose(); fog.Dispose(); light.Dispose();
                Object.DestroyImmediate(fog); Object.DestroyImmediate(light); Object.DestroyImmediate(go);
            }
        }
    }
}
