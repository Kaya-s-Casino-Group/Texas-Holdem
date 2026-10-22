using System.Linq;
using NUnit.Framework;
using Poker.SingleBattle.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class WrathBossArtworkTests
    {
        [TestCase(1.333333f)]
        [TestCase(1.777778f)]
        [TestCase(2.164103f)]
        public void SavedSeparatePlanesRetainPsdPixelScaleAndOverlapAtDifferentAspectRatios(float aspect)
        {
            string copy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/WrathArtworkTest.unity");
            Assert.IsTrue(AssetDatabase.CopyAsset(WrathBossSetup.ScenePath, copy));
            var scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
            try
            {
                var host = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleSceneAdapter>(true)).Single();
                var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Single(value => value.CompareTag("MainCamera"));
                Assert.AreEqual(1, camera.GetComponents<MonoBehaviour>().Count(component => component != null &&
                    component.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData"),
                    "Duplicate URP camera data prevents SceneView and PlayMode previews.");
                camera.aspect = aspect;
                var composition = host.GetComponent<WrathBossArtworkLayout>();
                Assert.IsNotNull(composition); composition.Preview();
                float pixelScale = -1;
                foreach (var actor in host.Enemies)
                {
                    var part = composition.PartFor(actor);
                    Assert.IsNotNull(part);
                    var surface = actor.GetComponentInChildren<BattleDemoAshPresenter>().Artwork;
                    Assert.IsTrue(part.Material == surface.sharedMaterial);
                    Assert.AreEqual(part.Order, surface.sortingOrder);
                    var lower = camera.WorldToViewportPoint(surface.transform.TransformPoint(new Vector3(-.5f, -.5f, 0)));
                    var upper = camera.WorldToViewportPoint(surface.transform.TransformPoint(new Vector3(.5f, .5f, 0)));
                    var canvas = composition.CanvasViewport;
                    Assert.That(lower.x, Is.EqualTo(canvas.x + part.Pixels.x / 5000 * canvas.width).Within(.00001f));
                    Assert.That(upper.y, Is.EqualTo(canvas.yMax - part.Pixels.y / 5000 * canvas.height).Within(.00001f));
                    float scale = surface.bounds.size.y / part.Pixels.height;
                    if (pixelScale < 0) pixelScale = scale;
                    Assert.That(scale, Is.EqualTo(pixelScale).Within(.00001f), "Every layer must use one shared PSD pixel scale.");
                    Assert.That((upper.x - lower.x) * aspect / (upper.y - lower.y),
                        Is.EqualTo(part.Pixels.width / part.Pixels.height).Within(.0001f));
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(copy); }
        }

        [Test]
        public void TransparentHandPixelsPassClicksToLowerLayersWhileOpaquePixelsRemainSelectable()
        {
            var artwork = AssetDatabase.LoadAssetAtPath<WrathBossArtwork>(WrathBossArtworkSetup.DefinitionPath);
            var cameraObject = new GameObject("Alpha test camera");
            var plane = new GameObject("Alpha test plane");
            var filterObject = new GameObject("Alpha test filter");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 1;
                plane.transform.position = Vector3.forward * 2;
                var filter = filterObject.AddComponent<BattleArtworkRaycastFilter>();
                foreach (var part in artwork.Parts)
                {
                    var texture = part.Texture;
                    Assert.IsTrue(texture.isReadable, part.Name);
                    filter.Configure(plane.transform, texture, camera);
                    bool clear = false, solid = false;
                    for (int y = 1; y < 16 && !(clear && solid); y++)
                    for (int x = 1; x < 16 && !(clear && solid); x++)
                    {
                        float u = x / 16f, v = y / 16f;
                        float alpha = texture.GetPixelBilinear(u, v).a;
                        var point = camera.WorldToScreenPoint(plane.transform.TransformPoint(new Vector3(u - .5f, v - .5f)));
                        if (alpha < .01f) { Assert.IsFalse(filter.IsRaycastLocationValid(point, camera), part.Name); clear = true; }
                        else if (alpha > .99f) { Assert.IsTrue(filter.IsRaycastLocationValid(point, camera), part.Name); solid = true; }
                    }
                    Assert.IsTrue(clear && solid, part.Name + " must exercise both transparent and opaque source pixels.");
                }
            }
            finally
            { Object.DestroyImmediate(filterObject); Object.DestroyImmediate(plane); Object.DestroyImmediate(cameraObject); }
        }
    }
}
