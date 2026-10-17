using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Tests
{
    public sealed class Retro540Tests
    {
        [TestCase(1920, 1080, 960, 540)]
        [TestCase(2560, 1080, 1280, 540)]
        [TestCase(1080, 1920, 304, 540)]
        [TestCase(800, 450, 800, 450)]
        [TestCase(1, 4096, 1, 540)]
        public void CapsHeightAndPreservesOutputAspect(int width, int height, int expectedWidth, int expectedHeight)
        {
            Assert.AreEqual(new Vector2Int(expectedWidth, expectedHeight), LowResolutionSettings.CalculateRenderSize(width, height, 540));
        }

        [TestCase(360, 640, 360)]
        [TestCase(720, 1280, 720)]
        [TestCase(2160, 1920, 1080)]
        public void HeightIsConfigurableAndClampedToOutput(int height, int width, int expectedHeight)
        {
            Assert.AreEqual(new Vector2Int(width, expectedHeight), LowResolutionSettings.CalculateRenderSize(1920, 1080, height));
        }

        [Test]
        public void CameraConfigurationPreservesOutputAndProjectionAndSkipsSceneView()
        {
            var go = new GameObject("Retro Test Camera");
            var settings = new LowResolutionSettings { enabled = true };
            var target = new RenderTexture(2560, 1080, 24);
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.targetTexture = target;
                var projection = camera.projectionMatrix;
                var data = new CameraData
                {
                    camera = camera, cameraType = CameraType.Game, renderType = CameraRenderType.Base,
                    targetTexture = target, cameraTargetDescriptor = target.descriptor,
                    antialiasing = AntialiasingMode.TemporalAntiAliasing, postProcessEnabled = true
                };
                settings.ConfigureCamera(ref data);
                Assert.AreEqual(1280, data.cameraTargetDescriptor.width);
                Assert.AreEqual(540, data.cameraTargetDescriptor.height);
                Assert.AreEqual(1280, data.scaledWidth);
                Assert.AreEqual(540, data.scaledHeight);
                Assert.AreEqual(AntialiasingMode.None, data.antialiasing);
                Assert.IsFalse(data.postProcessEnabled);
                Assert.AreSame(target, camera.targetTexture);
                Assert.AreEqual(projection, camera.projectionMatrix);
                data.cameraType = CameraType.SceneView;
                data.cameraTargetDescriptor = target.descriptor;
                settings.ConfigureCamera(ref data);
                Assert.AreEqual(1080, data.cameraTargetDescriptor.height);
                data.cameraType = CameraType.Game;
                settings.applyToRenderTextures = false;
                settings.ConfigureCamera(ref data);
                Assert.AreEqual(1080, data.cameraTargetDescriptor.height);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(target);
            }
        }
    }
}
