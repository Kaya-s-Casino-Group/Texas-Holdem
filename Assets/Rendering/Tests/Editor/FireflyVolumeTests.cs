using NUnit.Framework;
using UnityEngine;
using RetroRendering.Editor;
using UnityEngine.Rendering;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace RetroRendering.Tests
{
    public class FireflyVolumeTests
    {
        GameObject root, wall;
        FireflyVolume volume;
        [SetUp] public void SetUp()
        {
            root = new GameObject("Firefly test"); root.SetActive(false);
            root.transform.position = new Vector3(2000, 2000, 2000);
            root.transform.rotation = Quaternion.Euler(0, 25, 0);
            root.transform.localScale = new Vector3(1, 1.2f, .8f);
            volume = root.AddComponent<FireflyVolume>();
            FireflySetup.Configure(volume); volume.count = 8; volume.size = Vector3.one * 2;
            volume.speed = 5; volume.clearance = .03f;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(wall.GetComponent<Collider>());
            wall.transform.position = root.transform.position;
            wall.transform.localScale = new Vector3(.08f, 4, 4);
            root.SetActive(true); volume.Regenerate();
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); Object.DestroyImmediate(wall); }

        [Test] public void MovementStaysInRotatedScaledBoxAndDoesNotCrossColliderlessMesh()
        {
            Assert.That(volume.SpawnedCount, Is.EqualTo(8));
            var lights = root.GetComponentsInChildren<Light>();
            var sides = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) sides[i] = Mathf.Sign(lights[i].transform.position.x - wall.transform.position.x);
            Vector3 initial = lights[0].transform.position;
            var obstacle = wall.GetComponent<Renderer>().bounds;
            obstacle.Expand(volume.clearance * 2 + volume.diameter);
            for (int step = 0; step < 600; step++)
            {
                volume.Tick(1f / 30);
                for (int i = 0; i < lights.Length; i++)
                {
                    var p = lights[i].transform.position;
                    Assert.IsTrue(volume.Contains(p)); Assert.IsFalse(obstacle.Contains(p));
                    Assert.That(Mathf.Sign(p.x - wall.transform.position.x), Is.EqualTo(sides[i]), "Tunneled through wall");
                }
            }
            Assert.That(Vector3.Distance(initial, lights[0].transform.position), Is.GreaterThan(.01f));
        }

        [Test] public void ParametersPropagateAndDisableRemovesGeneratedLights()
        {
            volume.lightIntensity = .7f; volume.lightRange = .8f; volume.pulseAmount = 0;
            volume.scattering = .6f; volume.anisotropy = -.2f; volume.lightRadius = .08f;
            volume.Tick(.02f);
            foreach (var light in root.GetComponentsInChildren<Light>())
            {
                Assert.That(light.type, Is.EqualTo(LightType.Point)); Assert.That(light.intensity, Is.EqualTo(.7f));
                Assert.That(light.range, Is.EqualTo(.8f)); Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
                var scattering = light.GetComponent<VolumetricLight>();
                Assert.That(scattering.scattering, Is.EqualTo(.6f)); Assert.That(scattering.anisotropy, Is.EqualTo(-.2f));
                Assert.That(scattering.radius, Is.EqualTo(.08f));
                Assert.IsTrue(light.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_ZWrite") > .5f);
            }
            volume.enabled = false;
            Assert.That(root.GetComponentsInChildren<Light>(true), Is.Empty);
        }

        [Test] public void RegenerationIsDeterministicAndFullObstacleDoesNotSpawnEmbeddedBodies()
        {
            var before = root.GetComponentsInChildren<Light>();
            var positions = new Vector3[before.Length];
            for (int i = 0; i < before.Length; i++) positions[i] = before[i].transform.position;
            volume.Regenerate();
            var after = root.GetComponentsInChildren<Light>();
            for (int i = 0; i < after.Length; i++) Assert.That(after[i].transform.position, Is.EqualTo(positions[i]));
            wall.transform.localScale = Vector3.one * 10;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "Firefly test: 区域可用空间不足，生成 0/8 只萤火虫。请移动/扩大区域或调整障碍物 Layer。");
            volume.Regenerate(); Assert.That(volume.SpawnedCount, Is.Zero);
        }

        [Test] public void BodyPreservesHDRAndOccludesLaterGeometryWhileRespectingForegroundDepth()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/MainDeferredRenderer.asset");
            var active = data.rendererFeatures.Select(f => f.isActive).ToArray();
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            var rt = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGBHalf);
            var readback = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            var dark = new Material(volume.bodyMaterial); dark.SetColor("_BaseColor", new Color(.02f, .02f, .02f));
            // Render after the body: this also proves the body writes usable depth.
            dark.renderQueue = 2450;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var f in data.rendererFeatures) f.SetActive(false);
                var cameraObject = new GameObject("Firefly depth validation camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.allowHDR = true; camera.allowMSAA = false; camera.targetTexture = rt;
                camera.nearClipPlane = .01f; camera.farClipPlane = 10; camera.fieldOfView = 40;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); SceneManager.MoveGameObjectToScene(sphere, scene);
                sphere.transform.position = new Vector3(0, 0, 2); sphere.transform.localScale = Vector3.one * .4f;
                sphere.GetComponent<Renderer>().sharedMaterial = volume.bodyMaterial;
                var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", new Color(4, 6, 1));
                sphere.GetComponent<Renderer>().SetPropertyBlock(block);
                var occluder = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(occluder, scene);
                occluder.GetComponent<Renderer>().sharedMaterial = dark;
                Color Read(float z)
                {
                    occluder.transform.position = new Vector3(0, 0, z);
                    camera.Render(); RenderTexture.active = rt;
                    readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply();
                    return readback.GetPixel(32, 32);
                }
                Color visible = Read(3);
                Assert.That(visible.g, Is.GreaterThan(1), "HDR emission was clamped or overwritten by later background geometry");
                Color hidden = Read(1);
                Assert.That(Mathf.Max(hidden.r, hidden.g, hidden.b), Is.LessThan(.1f), "Foreground mesh did not occlude the firefly body");
            }
            finally
            {
                for (int i = 0; i < active.Length; i++) data.rendererFeatures[i].SetActive(active[i]);
                ShaderUtil.allowAsyncCompilation = asyncCompilation; RenderTexture.active = previous;
                EditorSceneManager.ClosePreviewScene(scene);
                rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(readback); Object.DestroyImmediate(dark);
            }
        }
    }
}
