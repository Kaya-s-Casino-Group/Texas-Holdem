using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using global::BattlePresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class CardProjectileTests
    {
        private sealed class Markers : IMarkerSink
        { internal readonly List<string> Values = new List<string>(); public bool Emit(string value) { Values.Add(value); return true; } }

        [TestCase("HighCard")][TestCase("OnePair")][TestCase("TwoPair")][TestCase("ThreeOfAKind")]
        [TestCase("Straight")][TestCase("Flush")][TestCase("FullHouse")][TestCase("FourOfAKind")]
        [TestCase("StraightFlush")][TestCase("FiveOfAKind")]
        public void EveryTrajectoryArrivesExactlyAtTheTargetAndFlattensTheCard(string category)
        {
            var start = new Vector3(-1, 1, -2); var destination = new Vector3(2, 2, 0);
            for (int i = 0; i < 5; i++)
            {
                Assert.Less(Vector3.Distance(start, BattleCardProjectileAction.Trajectory(category, 0, i, 5, start, destination, Vector3.up, Vector3.right, 1)), .00001f);
                Assert.Less(Vector3.Distance(destination, BattleCardProjectileAction.Trajectory(category, 1, i, 5, start, destination, Vector3.up, Vector3.right, 1)), .00001f);
                var points = Enumerable.Range(0, 5).Select(step => BattleCardProjectileAction.Trajectory(category,
                    step * .25f, i, 5, start, destination, Vector3.up, Vector3.right, 1)).ToArray();
                var acceleration = points[2] - 2 * points[1] + points[0];
                for (int step = 1; step < 3; step++)
                    Assert.Less(Vector3.Distance(acceleration, points[step + 2] - 2 * points[step + 1] + points[step]), .00001f,
                        "Every hand category must trace a parabola with constant acceleration.");
                Assert.Greater(Vector3.Dot(points[2] - Vector3.Lerp(start, destination, .5f), Vector3.up), 0);
            }
            var rotation = BattleCardProjectileAction.FlightRotation(Quaternion.Euler(90, 0, 0), destination - start, 1);
            Assert.Greater(Vector3.Dot(rotation * Vector3.up, Vector3.up), .9999f);
        }

        [TestCase(false)][TestCase(true)]
        public void ProjectileOwnsItsTailWaitsForAllArrivalsAndRestoresHiddenCardsOnCancellation(bool cancel)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var scope = new PresentationScope("projectile-test");
            try
            {
                var root = new GameObject("Projectile test"); SceneManager.MoveGameObjectToScene(root, scene);
                var hand = root.AddComponent<HandZoneView>();
                hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var cameraRoot = new GameObject("Camera"); cameraRoot.transform.SetParent(root.transform);
                cameraRoot.transform.position = new Vector3(0, 0, -10);
                hand.SetCameraFacing(true, cameraRoot.AddComponent<Camera>());
                var world = root.AddComponent<WorldCardZonePresentation>();
                var actionRoot = new GameObject("Action"); actionRoot.transform.SetParent(root.transform);
                var action = actionRoot.AddComponent<BattleCardProjectileAction>();
                action.Configure(world, AssetDatabase.LoadAssetAtPath<Material>("Assets/GameAssets/SingleBattle/Presentation/Scene3D/CardProjectileTail.mat"));
                IEnumerator routine = null;
                action.StartPresentationRoutine = value => { routine = value; return () => routine = null; };
                action.DeltaTimeProvider = () => .02f;
                var zone = new CardZoneDisplayData("hand", "player", "hand", Enumerable.Range(0, 3)
                    .Select(i => new CardDisplayData("view/" + i, "card/" + i, null, "hearts_q")));
                world.Synchronize(zone);
                var markers = new Markers();
                var op = world.Play(new CardZonePresentationRequest(BattleCardProjectileAction.ActionKey, "flight", scope,
                    zone, zone, zone.Cards.Select(card => card.ViewId), parameters: new Dictionary<string, string> {
                        ["destination.x"] = "2", ["destination.y"] = "1", ["destination.z"] = "0", ["handCategory"] = "Straight"
                    }), markers);
                Assert.AreEqual(PresentationStatus.Pending, op.Status, op.Error?.ToString());
                Assert.AreEqual(3, action.GetComponentsInChildren<TrailRenderer>().Length);
                foreach (var tail in action.GetComponentsInChildren<TrailRenderer>())
                {
                    var mesh = tail.transform.parent.GetComponent<MeshFilter>().sharedMesh;
                    Assert.AreEqual(mesh.bounds.size.x * Mathf.Abs(tail.transform.parent.lossyScale.x) * .55f, tail.widthMultiplier, .00001f);
                    Assert.AreEqual(LineAlignment.TransformZ, tail.alignment);
                    Assert.Greater(Mathf.Abs(Vector3.Dot(tail.transform.forward, tail.transform.parent.up)), .9999f);
                }
                for (int i = 0; i < 20; i++) Assert.IsTrue(routine.MoveNext());
                CollectionAssert.DoesNotContain(markers.Values, "Impact");
                Assert.IsTrue(action.GetComponentsInChildren<TrailRenderer>().All(tail => tail.transform.parent.GetComponent<MeshRenderer>() != null));
                Assert.IsTrue(hand.Views.All(card => card.Surface.GetComponent<Renderer>().forceRenderingOff));
                if (cancel)
                {
                    scope.Dispose();
                    Assert.AreEqual(PresentationStatus.Cancelled, op.Status);
                    CollectionAssert.DoesNotContain(markers.Values, "Impact");
                }
                else
                {
                    for (int i = 0; i < 60 && op.Status == PresentationStatus.Pending; i++) routine.MoveNext();
                    Assert.AreEqual(PresentationStatus.Completed, op.Status, op.Error?.ToString());
                    CollectionAssert.AreEqual(new[] { "Released", "Impact", "AllArrived" }, markers.Values);
                    scope.Dispose();
                }
                Assert.IsEmpty(action.GetComponentsInChildren<TrailRenderer>());
                Assert.IsTrue(hand.Views.All(card => !card.Surface.GetComponent<Renderer>().forceRenderingOff));
            }
            finally { scope.Dispose(); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
