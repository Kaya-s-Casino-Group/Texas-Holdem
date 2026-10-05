using System;
using System.Linq;
using CardArt;
using DG.Tweening;
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
    public sealed class CardZoneMotionTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly HandZoneView Zone;
            public Fixture(bool previewScene = true)
            {
                Scene = previewScene ? EditorSceneManager.NewPreviewScene() : default(Scene);
                try
                {
                    var root = new GameObject("Motion Test") { hideFlags = HideFlags.HideAndDontSave };
                    if (previewScene) SceneManager.MoveGameObjectToScene(root, Scene);
                    Zone = root.AddComponent<HandZoneView>();
                    var lift = CardZoneLiftSettings.Default; lift.Enabled = false; Zone.SetLiftSettings(lift);
                    var transitions = CardZoneTransitionSettings.Default; transitions.Enabled = false; Zone.SetTransitionSettings(transitions);
                    Zone.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (Scene.IsValid())
                {
                    // Explicitly release the test's owned tweens before preview-scene teardown.
                    if (Zone != null) Zone.Clear();
                    EditorSceneManager.ClosePreviewScene(Scene);
                }
                else if (Zone != null) Object.DestroyImmediate(Zone.gameObject);
            }
        }

        private static CardVisualData Card(string id, bool visible = true, bool selected = false) =>
            new CardVisualData(id, visible ? "hearts_q" : null, interaction: new CardInteractionState(isSelected: selected));
        private static CardZoneViewData Data(params CardVisualData[] cards) => new CardZoneViewData(new ZoneId("motion"), cards);

        [Test] public void ArcHandlesOddEvenSmallAndDenseRowsWithinAvailableDepth()
        {
            var size = new Vector3(0.064f, 0.00035f, 0.096f);
            foreach (int count in new[] { 0, 1, 2, 3, 4, 9, 30 })
            foreach (float arc in new[] { 0f, 0.01f, -0.01f, 1f, -1f })
            {
                var settings = CardZoneLayoutSettings.Default; settings.ArcHeight = arc;
                var positions = CardZoneRowLayout.Calculate(settings, size, count);
                Assert.AreEqual(count, positions.Length);
                for (int i = 0; i < count; i++)
                {
                    Assert.That(positions[i].z, Is.EqualTo(positions[count - 1 - i].z).Within(0.00001));
                    Assert.LessOrEqual(Mathf.Abs(positions[i].x) + size.x / 2, settings.Size.x / 2 - settings.Padding.x + 0.00001f);
                    Assert.LessOrEqual(Mathf.Abs(positions[i].z) + size.z / 2, settings.Size.y / 2 - settings.Padding.y + 0.00001f);
                    if (count < 3 || arc == 0) Assert.AreEqual(0, positions[i].z);
                }
                if (count > 2 && arc != 0)
                {
                    float actual = positions[count / 2].z - positions[0].z;
                    Assert.AreEqual(Math.Sign(arc), Math.Sign(actual));
                    Assert.LessOrEqual(Mathf.Abs(actual), Mathf.Abs(arc) + 0.00001f);
                }
            }
        }

        [Test] public void CurvedBaseAxesMeetAtOneCircleCenterAndRotatedCornersFit()
        {
            var size = new Vector3(0.064f, 0.00035f, 0.096f);
            foreach (int count in new[] { 2, 3, 4, 9, 30 })
            foreach (float arc in new[] { 0.0001f, 0.008f, -0.008f, 1f, -1f })
            foreach (CardZoneAlignment alignment in Enum.GetValues(typeof(CardZoneAlignment)))
            {
                var settings = CardZoneLayoutSettings.Default; settings.ArcHeight = arc; settings.Alignment = alignment;
                var poses = CardZoneRowLayout.CalculatePoses(settings, size, count);
                Vector3 first = poses[0].Position, last = poses[count - 1].Position;
                Vector3 inward = poses[0].Rotation * Vector3.forward;
                float centerX = (first.x + last.x) / 2;
                float radius = (centerX - first.x) / inward.x;
                Vector3 center = first + radius * inward; center.y = 0;
                Assert.Greater(radius, 0);
                foreach (var pose in poses)
                {
                    var planar = new Vector3(pose.Position.x, 0, pose.Position.z);
                    Assert.That(Vector3.Distance(planar, center), Is.EqualTo(radius).Within(Math.Max(0.00001f, radius * 0.00001f)));
                    Assert.Greater(Vector3.Dot(pose.Rotation * Vector3.forward, (center - planar).normalized), 0.99999f);
                    Assert.Greater(Vector3.Dot(pose.Rotation * Vector3.up, Vector3.up), 0.99999f);
                    for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = pose.Position + pose.Rotation * new Vector3(x * size.x / 2, 0, z * size.z / 2);
                        Assert.LessOrEqual(Mathf.Abs(corner.x), settings.Size.x / 2 - settings.Padding.x + 0.00001f);
                        Assert.LessOrEqual(Mathf.Abs(corner.z), settings.Size.y / 2 - settings.Padding.y + 0.00001f);
                    }
                }
            }
            var straight = CardZoneLayoutSettings.Default; straight.ArcHeight = 0;
            Assert.IsTrue(CardZoneRowLayout.CalculatePoses(straight, size, 5).All(p => p.Rotation == Quaternion.identity));
            Assert.AreEqual(Quaternion.identity, CardZoneRowLayout.CalculatePoses(CardZoneLayoutSettings.Default, size, 1)[0].Rotation);
            straight.ArcHeight = 1; straight.Size = new Vector2(size.x, size.z); straight.Padding = Vector2.zero;
            Assert.IsTrue(CardZoneRowLayout.CalculatePoses(straight, size, 5).All(p => p.Rotation == Quaternion.identity));
        }

        [Test] public void ArtworkStaysUprightAcrossSignedArcsFlipAndSingleCardTransitions()
        {
            using (var f = new Fixture())
            {
                f.Zone.transform.rotation = Quaternion.Euler(17, 43, 9);
                var motion = f.Zone.MotionSettings; motion.Enabled = false; f.Zone.SetMotionSettings(motion);
                f.Zone.Present(Data(Card("a"))); var retained = f.Zone.Views[0];
                var straightArtForward = retained.SwayRoot.GetChild(0).forward;
                foreach (float arc in new[] { 0.008f, 0f, -0.008f, 0.0001f })
                foreach (int count in new[] { 1, 2, 5, 10 })
                foreach (bool visible in new[] { true, false })
                {
                    // First change the layout on existing cards, then update the data independently.
                    var layout = f.Zone.Layout; layout.ArcHeight = arc; f.Zone.SetLayout(layout);
                    foreach (var view in f.Zone.Views)
                        Assert.Greater(Vector3.Dot(view.SwayRoot.GetChild(0).forward, straightArtForward), 0);
                    f.Zone.Present(Data(Enumerable.Range(0, count).Select(i => Card(i == 0 ? "a" : "card-" + i, visible, selected: i == 0)).ToArray()));
                    Assert.AreSame(retained, f.Zone.Views[0]);
                    foreach (var view in f.Zone.Views)
                    {
                        var artwork = view.SwayRoot.GetChild(0);
                        Assert.Greater(Vector3.Dot(artwork.forward, straightArtForward), 0, "Artwork must not inherit the inward axis's half turn.");
                        Assert.Greater(Mathf.Abs(Vector3.Dot(artwork.forward, view.transform.forward)), 0.9999f);
                        if (count == 1 || arc == 0) Assert.Greater(Vector3.Dot(artwork.forward, straightArtForward), 0.9999f);
                        Assert.Greater(Vector3.Dot(artwork.up, f.Zone.transform.up) * (visible ? 1 : -1), 0.9999f);
                    }
                }
            }
        }

        [Test] public void FanReflowKeepsRaisedOffsetInZoneSpaceAndSwayPhase()
        {
            using (var f = new Fixture())
            {
                var offset = new Vector3(0.013f, 0.02f, -0.007f); f.Zone.SetRaisedOffset(offset);
                var data = Data(Card("a", selected: true), Card("b"), Card("c")); f.Zone.Present(data); f.Zone.AdvanceMotion(0.7f);
                var view = f.Zone.Views[0]; var sway = view.SwayRoot.localRotation;
                foreach (float arc in new[] { 0f, -0.012f, 0.012f })
                {
                    var layout = f.Zone.Layout; layout.ArcHeight = arc; f.Zone.SetLayout(layout);
                    Assert.Less((view.transform.localRotation * view.FaceRoot.localPosition - offset).magnitude, 0.00001f);
                    Assert.Less(Quaternion.Angle(sway, view.SwayRoot.localRotation), 0.001f);
                    f.Zone.Present(data);
                    Assert.Less((view.transform.localRotation * view.FaceRoot.localPosition - offset).magnitude, 0.00001f);
                }
            }
        }

        [Test] public void MotionPreservesIdentityAndSelectionResetsSwayBeforeResumingFromNeutral()
        {
            using (var f = new Fixture())
            {
                var data = Data(Card("a"), Card("b")); f.Zone.Present(data);
                var a = f.Zone.Views[0]; var b = f.Zone.Views[1]; var randomBefore = UnityEngine.Random.state;
                f.Zone.AdvanceMotion(0);
                var initial = a.SwayRoot.localRotation;
                Assert.Greater(Quaternion.Angle(initial, b.SwayRoot.localRotation), 0.0001f);
                f.Zone.AdvanceMotion(0.73f); var advanced = a.SwayRoot.localRotation;
                Assert.Greater(Quaternion.Angle(initial, advanced), 0.0001f);
                Assert.AreEqual(randomBefore, UnityEngine.Random.state);
                for (int i = 0; i < 3; i++) f.Zone.Present(data);
                Assert.Less(Quaternion.Angle(advanced, a.SwayRoot.localRotation), 0.001f);
                var oldTween = DOTween.TweensByTarget(a.SwayRoot).Single();
                f.Zone.Present(Data(Card("b"), Card("a", visible: false, selected: true)));
                Assert.AreSame(a, f.Zone.Views[1]); Assert.AreSame(b, f.Zone.Views[0]);
                Assert.AreEqual(Quaternion.identity, a.SwayRoot.localRotation);
                Assert.IsFalse(oldTween.IsActive());
                Assert.Less(Vector3.Distance(f.Zone.RaisedOffset,
                    f.Zone.transform.InverseTransformVector(a.FaceRoot.position - a.transform.position)), 0.00001f);
                Assert.Less((a.FaceRoot.localRotation * Vector3.up).y, -0.99f);
                f.Zone.RefreshLayout(); Assert.AreEqual(Quaternion.identity, a.SwayRoot.localRotation);
                float otherAngle = b.SwayRoot.localEulerAngles.y;
                f.Zone.AdvanceMotion(0.37f); f.Zone.Present(f.Zone.Data); f.Zone.AdvanceMotion(0.41f);
                Assert.AreEqual(Quaternion.identity, a.SwayRoot.localRotation);
                Assert.IsFalse(DOTween.IsTweening(a.SwayRoot));
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(otherAngle, b.SwayRoot.localEulerAngles.y)), 0.0001f);
                f.Zone.Present(Data(Card("b"), Card("a", visible: false)));
                Assert.AreEqual(Quaternion.identity, a.SwayRoot.localRotation);
                f.Zone.AdvanceMotion(0);
                Assert.AreEqual(Quaternion.identity, a.SwayRoot.localRotation);
                Assert.AreEqual(1, DOTween.TweensByTarget(a.SwayRoot).Count);
                f.Zone.AdvanceMotion(0.2f);
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0, a.SwayRoot.localEulerAngles.y)), 0.0001f);
                CollectionAssert.AreEqual(new[] { "a", "b" }, data.Cards.Select(c => c.ViewId));
                Assert.AreSame(a.Surface.GetComponent<MeshRenderer>().sharedMaterial, b.Surface.GetComponent<MeshRenderer>().sharedMaterial);
                f.Zone.Clear(); f.Zone.Present(data); f.Zone.AdvanceMotion(0);
                Assert.Less(Quaternion.Angle(initial, f.Zone.Views[0].SwayRoot.localRotation), 0.001f);
            }
        }

        [Test] public void RotatedMeshCornersStayInsideZoneDuringCurvedMotion()
        {
            using (var f = new Fixture())
            {
                var layout = f.Zone.Layout; layout.ArcHeight = 1; // Clamp to available depth.
                var motion = f.Zone.MotionSettings; motion.MaxSwayAngle = 10;
                f.Zone.SetLayout(layout, motion);
                f.Zone.transform.SetPositionAndRotation(new Vector3(3, 2, -1), Quaternion.Euler(15, 35, 12));
                f.Zone.Present(Data(Enumerable.Range(0, 15).Select(i => Card("card-" + i, i % 2 == 0, i % 3 == 0)).ToArray()));
                for (int step = 0; step < 40; step++)
                {
                    f.Zone.AdvanceMotion(0.2f);
                    foreach (var view in f.Zone.Views)
                    {
                        var mesh = view.Surface.GetComponent<MeshFilter>(); var bounds = mesh.sharedMesh.bounds;
                        Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(0, view.SwayRoot.localEulerAngles.y)), 10.00001f);
                        for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var point = f.Zone.transform.InverseTransformPoint(mesh.transform.TransformPoint(
                                bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
                            // Zone bounds constrain the resting row; interaction may intentionally lift it beyond that rectangle.
                            if (view.Data.Interaction.IsRaised) point -= f.Zone.RaisedOffset;
                            Assert.LessOrEqual(Mathf.Abs(point.x), layout.Size.x / 2 - layout.Padding.x + 0.00001f);
                            Assert.LessOrEqual(Mathf.Abs(point.z), layout.Size.y / 2 - layout.Padding.y + 0.00001f);
                        }
                    }
                }
            }
        }

        [Test] public void LifecycleKillsOnlyOwnedTweensAndDoesNotAdvanceForeignManualTween()
        {
            float foreignValue = 0; var foreignId = new object();
            var foreign = DOTween.To(() => foreignValue, value => foreignValue = value, 1, 10)
                .SetUpdate(UpdateType.Manual).SetAutoKill(false).SetRecyclable(false).SetId(foreignId).Play();
            Tween ownedAtDispose = null;
            try
            {
                // A temporary DontSave root exercises Unity's real enable/disable/destroy callbacks.
                using (var f = new Fixture(previewScene: false))
                {
                    var data = Data(Card("a"), Card("b")); f.Zone.Present(data); f.Zone.AdvanceMotion(0.5f);
                    Assert.AreEqual(0, foreignValue); Assert.IsTrue(foreign.IsActive());
                    var view = f.Zone.Views[0]; var owned = DOTween.TweensByTarget(view.SwayRoot).Single();
                    view.gameObject.SetActive(false); Assert.IsFalse(owned.IsActive()); Assert.AreEqual(Quaternion.identity, view.SwayRoot.localRotation);
                    view.gameObject.SetActive(true); f.Zone.AdvanceMotion(0); Assert.IsTrue(DOTween.IsTweening(view.SwayRoot));
                    f.Zone.enabled = false; Assert.IsFalse(DOTween.IsTweening(view.SwayRoot));
                    f.Zone.AdvanceMotion(1); Assert.IsFalse(DOTween.IsTweening(view.SwayRoot));
                    f.Zone.enabled = true; f.Zone.AdvanceMotion(0);
                    owned = DOTween.TweensByTarget(view.SwayRoot).Single();
                    Object.DestroyImmediate(view.gameObject); Assert.IsFalse(owned.IsActive());
                    f.Zone.Present(data); f.Zone.AdvanceMotion(0);
                    var motion = f.Zone.MotionSettings; motion.Enabled = false; f.Zone.SetMotionSettings(motion);
                    foreach (var card in f.Zone.Views) { Assert.IsFalse(DOTween.IsTweening(card.SwayRoot)); Assert.AreEqual(Quaternion.identity, card.SwayRoot.localRotation); }
                    motion.Enabled = true; f.Zone.SetMotionSettings(motion); f.Zone.AdvanceMotion(0);
                    owned = DOTween.TweensByTarget(f.Zone.Views[0].SwayRoot).Single(); f.Zone.Clear(); Assert.IsFalse(owned.IsActive());
                    Assert.IsTrue(foreign.IsActive()); Assert.AreEqual(0, foreignValue);
                    f.Zone.Present(data); f.Zone.AdvanceMotion(0); ownedAtDispose = DOTween.TweensByTarget(f.Zone.Views[0].SwayRoot).Single();
                }
                Assert.IsFalse(ownedAtDispose.IsActive()); Assert.IsTrue(foreign.IsActive());
            }
            finally { DOTween.Kill(foreignId); }
        }

        [Test] public void InvalidMotionAndArcLeaveSettingsAndLiveTweensIntact()
        {
            using (var f = new Fixture())
            {
                f.Zone.Present(Data(Card("a"))); f.Zone.AdvanceMotion(0.4f);
                var view = f.Zone.Views[0]; var pose = view.SwayRoot.localRotation;
                var settings = f.Zone.MotionSettings; var bad = settings; bad.PeriodSeconds.x = 0;
                Assert.Throws<ArgumentException>(() => f.Zone.SetMotionSettings(bad));
                bad = settings; bad.MaxSwayAngle = float.NaN; Assert.Throws<ArgumentException>(() => f.Zone.SetMotionSettings(bad));
                bad = settings; bad.PeriodSeconds.y = float.PositiveInfinity; Assert.Throws<ArgumentException>(() => f.Zone.SetMotionSettings(bad));
                bad = settings; bad.PeriodSeconds.y = 1; Assert.Throws<ArgumentException>(() => f.Zone.SetMotionSettings(bad));
                var layout = f.Zone.Layout; layout.ArcHeight = float.NaN; Assert.Throws<ArgumentException>(() => f.Zone.SetLayout(layout));
                Assert.Throws<ArgumentOutOfRangeException>(() => f.Zone.AdvanceMotion(float.NaN));
                Assert.AreEqual(settings, f.Zone.MotionSettings); Assert.Less(Quaternion.Angle(pose, view.SwayRoot.localRotation), 0.001f);
                Assert.AreEqual(1, DOTween.TweensByTarget(view.SwayRoot).Count);
                CollectionAssert.Contains(typeof(HandZoneView).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray(), "DOTween");
            }
        }
    }
}
