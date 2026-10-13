using System;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class CardZoneFacingTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly HandZoneView Zone;
            public readonly Camera Camera;
            public Fixture(bool previewScene = true)
            {
                Scene = previewScene ? EditorSceneManager.NewPreviewScene() : default(Scene);
                Root = new GameObject("Facing Test") { hideFlags = HideFlags.HideAndDontSave };
                if (previewScene) SceneManager.MoveGameObjectToScene(Root, Scene);
                var zone = new GameObject("Zone"); zone.transform.SetParent(Root.transform, false);
                Zone = zone.AddComponent<HandZoneView>();
                var transitions = CardZoneTransitionSettings.Default; transitions.Enabled = false; Zone.SetTransitionSettings(transitions);
                Zone.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var camera = new GameObject("Camera"); camera.transform.SetParent(Root.transform, false);
                Camera = camera.AddComponent<Camera>(); Camera.enabled = false;
            }
            public void Dispose()
            {
                Zone.Clear(); Object.DestroyImmediate(Root);
                if (Scene.IsValid()) EditorSceneManager.ClosePreviewScene(Scene);
            }
        }

        private static void AssertFacing(HandZoneView zone, Camera camera)
        {
            var direction = (camera.transform.position - zone.transform.position).normalized;
            Assert.Greater(Vector3.Dot(zone.transform.up, direction), 0.99999f);
            var screenUp = Vector3.ProjectOnPlane(camera.transform.up, direction).normalized;
            if (screenUp.sqrMagnitude > 0.5f) Assert.Greater(Vector3.Dot(zone.transform.forward, screenUp), 0.99999f);
        }

        [Test] public void FacingTracksCameraWithoutCardsOrMotionAndRestoresAuthoredRotation()
        {
            using (var f = new Fixture())
            {
                f.Root.transform.SetPositionAndRotation(new Vector3(1, 3, -2), Quaternion.Euler(13, 47, 9));
                f.Root.transform.localScale = Vector3.one * 1.3f;
                f.Zone.transform.localPosition = new Vector3(0.2f, 0.1f, -0.3f);
                var original = Quaternion.Euler(17, 23, 11); f.Zone.transform.localRotation = original;
                var position = f.Zone.transform.position; var scale = f.Zone.transform.localScale;
                var motion = f.Zone.MotionSettings; motion.Enabled = false; f.Zone.SetMotionSettings(motion);
                Assert.IsFalse(f.Zone.FaceCamera);
                f.Camera.transform.SetPositionAndRotation(new Vector3(0, 6, -5), Quaternion.Euler(33, 7, 24));
                f.Zone.SetCameraFacing(true, f.Camera); AssertFacing(f.Zone, f.Camera);
                f.Camera.transform.position = new Vector3(-3, 4, 2);
                for (int i = 0; i < 20; i++) { f.Zone.RefreshCameraFacing(); AssertFacing(f.Zone, f.Camera); }
                Assert.AreEqual(position, f.Zone.transform.position); Assert.AreEqual(scale, f.Zone.transform.localScale);
                Assert.IsNull(f.Zone.Data); Assert.AreSame(f.Camera, f.Zone.FacingCamera);
                f.Zone.SetCameraFacing(false, f.Camera);
                Assert.Less(Quaternion.Angle(original, f.Zone.transform.localRotation), 0.001f);
                f.Zone.SetCameraFacing(true, f.Camera); f.Zone.SetCameraFacing(false, f.Camera);
                Assert.Less(Quaternion.Angle(original, f.Zone.transform.localRotation), 0.001f);
            }
        }

        [Test] public void FacingPreservesCardsBaseFanFlipAttributesAndLiveSway()
        {
            using (var f = new Fixture())
            {
                var data = new CardZoneViewData(new ZoneId("facing"), new[] {
                    new CardVisualData("a", "hearts_q", effects: CardEffects.Laminated), new CardVisualData("b") });
                f.Zone.Present(data); f.Zone.AdvanceMotion(0.5f);
                var view = f.Zone.Views[0]; var baseRotation = view.transform.localRotation;
                var sway = view.SwayRoot.localRotation; var appearance = view.Appearance;
                f.Camera.transform.position = new Vector3(2, 3, -4);
                f.Zone.SetCameraFacing(true, f.Camera); Assert.AreSame(appearance, view.Appearance);
                f.Zone.RefreshLayout(); f.Zone.Present(data);
                AssertFacing(f.Zone, f.Camera); Assert.AreSame(view, f.Zone.Views[0]); Assert.AreSame(data, f.Zone.Data);
                Assert.AreEqual(baseRotation, view.transform.localRotation);
                Assert.AreEqual(appearance.Tint, view.Appearance.Tint);
                Assert.AreEqual(appearance.GetFloat("_FoilEnabled") != 0, view.Surface.FoilEnabled);
                Assert.AreEqual(appearance.GetFloat("_FoilStrength"), view.Surface.FoilStrength);
                Assert.Less(Quaternion.Angle(sway, view.SwayRoot.localRotation), 0.001f);
                Assert.Greater(Vector3.Dot(view.FaceRoot.up, f.Zone.transform.up), 0.99999f);
                Assert.Less(Vector3.Dot(f.Zone.Views[1].FaceRoot.up, f.Zone.transform.up), -0.99999f);
                var screenUp = Vector3.ProjectOnPlane(f.Camera.transform.up, f.Zone.transform.up).normalized;
                foreach (var card in f.Zone.Views)
                    Assert.Greater(Vector3.Dot(card.SwayRoot.GetChild(0).forward, screenUp), 0.9f);
            }
        }

        [Test] public void CoincidentCameraAndParallelUpHaveFiniteStableFallbacks()
        {
            using (var f = new Fixture())
            {
                var original = Quaternion.Euler(11, 24, 7); f.Zone.transform.localRotation = original;
                f.Camera.transform.position = f.Zone.transform.position;
                f.Zone.SetCameraFacing(true, f.Camera);
                Assert.Less(Quaternion.Angle(original, f.Zone.transform.localRotation), 0.001f);
                f.Camera.transform.rotation = Quaternion.identity; f.Camera.transform.position = f.Zone.transform.position + Vector3.up;
                f.Zone.RefreshCameraFacing(); AssertFacing(f.Zone, f.Camera);
                var valid = f.Zone.transform.rotation; f.Camera.transform.position = f.Zone.transform.position;
                f.Zone.RefreshCameraFacing(); Assert.Less(Quaternion.Angle(valid, f.Zone.transform.rotation), 0.001f);
                f.Zone.SetCameraFacing(false); Assert.Less(Quaternion.Angle(original, f.Zone.transform.localRotation), 0.001f);
            }
        }

        [Test] public void DisableRestoresRotationAndReenableUsesTheNewAuthoredPose()
        {
            using (var f = new Fixture(previewScene: false))
            {
                var original = Quaternion.Euler(11, 24, 7); f.Zone.transform.localRotation = original;
                f.Camera.transform.position = new Vector3(3, 2, -4); f.Zone.SetCameraFacing(true, f.Camera);
                f.Zone.enabled = false; Assert.Less(Quaternion.Angle(original, f.Zone.transform.localRotation), 0.001f);
                var next = Quaternion.Euler(31, 7, 22); f.Zone.transform.localRotation = next;
                f.Zone.enabled = true; f.Zone.RefreshCameraFacing(); AssertFacing(f.Zone, f.Camera);
                f.Zone.gameObject.SetActive(false); Assert.Less(Quaternion.Angle(next, f.Zone.transform.localRotation), 0.001f);
            }
        }
    }
}
