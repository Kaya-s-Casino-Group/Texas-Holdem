using System;
using System.Linq;
using CardInteraction;
using CardPresentation.Editor;
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
    public sealed class CardMouseInteractionTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly HandZoneView Zone;
            public readonly CardZonePreviewModel Model = new CardZonePreviewModel();
            public readonly CardZonePointerController Pointer;
            public Fixture(int count = 2)
            {
                Scene = EditorSceneManager.NewPreviewScene();
                Root = new GameObject("Mouse Test") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(Root, Scene); Zone = Root.AddComponent<HandZoneView>();
                var lift = CardZoneLiftSettings.Default; lift.Enabled = false; Zone.SetLiftSettings(lift);
                var layout = CardZoneLayoutSettings.Default; layout.ArcHeight = 0; layout.Size.x = 0.6f;
                Zone.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), layout);
                var motion = Zone.MotionSettings; motion.Enabled = false; Zone.SetMotionSettings(motion);
                var transitions = Zone.TransitionSettings; transitions.Enabled = false; Zone.SetTransitionSettings(transitions);
                for (int i = 0; i < count; i++) Model.Add(i % 2 == 0 ? "spades_a" : "hearts_q");
                Publish(); Pointer = new CardZonePointerController(Zone, Model); Pointer.StateChanged += Publish;
            }
            public void Publish() => Zone.Present(Model.BuildZone(), Model.Project);
            public Ray RayAt(CardInstanceView view)
            {
                var mesh = view.Surface.GetComponent<MeshFilter>().sharedMesh;
                var center = view.Surface.transform.TransformPoint(mesh.bounds.center);
                return new Ray(center + Zone.transform.up * 2, -Zone.transform.up);
            }
            public void Click(Ray ray) { Pointer.ProcessPointer(ray, pressed: true); Pointer.ProcessPointer(ray, released: true); }
            public void Dispose()
            {
                Pointer.CancelPointer(); Zone.Clear(); Object.DestroyImmediate(Root); EditorSceneManager.ClosePreviewScene(Scene);
            }
        }

        [Test] public void ClickedSelectionOvershootsThenSettlesWithoutPointerRefreshCancellingIt()
        {
            using (var f = new Fixture(1))
            {
                var tuning = CardZoneLiftSettings.Default; f.Zone.SetLiftSettings(tuning);
                var card = f.Zone.Views[0];
                f.Pointer.ProcessPointer(f.RayAt(card)); f.Zone.AdvanceMotion(tuning.RaiseSeconds + 0.001f);
                Assert.AreEqual(f.Zone.HoveredOffset, card.InteractionOffset);
                f.Click(f.RayAt(card));
                Assert.IsTrue(f.Model.Cards[0].IsSelected); Assert.AreEqual(f.Zone.HoveredOffset, card.InteractionOffset);
                f.Zone.AdvanceMotion(tuning.RaiseSeconds * 0.5f);
                float step = f.Zone.SelectedOffset.z - f.Zone.HoveredOffset.z;
                Assert.Greater(card.InteractionOffset.z, f.Zone.SelectedOffset.z + step * 0.2f);
                Assert.Less(card.InteractionOffset.z, f.Zone.SelectedOffset.z + step * 0.3f);
                var peak = card.InteractionOffset;
                f.Pointer.ProcessPointer(f.RayAt(card)); f.Pointer.CancelPointer(); f.Publish();
                Assert.IsTrue(card.IsLifting); Assert.AreEqual(peak, card.InteractionOffset);
                Assert.IsTrue(f.Model.Cards[0].IsSelected);
                f.Zone.AdvanceMotion(tuning.RaiseSeconds * 0.5f + 0.001f);
                Assert.AreEqual(f.Zone.SelectedOffset, card.InteractionOffset); Assert.IsFalse(card.IsLifting);
                f.Zone.AdvanceMotion(1); Assert.AreEqual(f.Zone.SelectedOffset, card.InteractionOffset);
            }
        }

        [Test] public void SelectionSoundCuesOnlyFollowSuccessfulSelectionAndCanRepeatAfterDeselecting()
        {
            using (var f = new Fixture())
            {
                int cues = 0; f.Zone.CardSelected += _ => cues++;
                var first = f.Zone.Views[0]; var second = f.Zone.Views[1];
                f.Pointer.ProcessPointer(f.RayAt(first)); Assert.AreEqual(0, cues);
                f.Pointer.ProcessPointer(f.RayAt(first), pressed: true); Assert.AreEqual(0, cues);
                f.Pointer.ProcessPointer(f.RayAt(first), released: true); Assert.AreEqual(1, cues);
                f.Pointer.CancelPointer(); f.Publish(); Assert.AreEqual(1, cues);
                f.Click(f.RayAt(first)); Assert.IsFalse(f.Model.Cards[0].IsSelected); Assert.AreEqual(1, cues);
                f.Click(f.RayAt(first)); Assert.IsTrue(f.Model.Cards[0].IsSelected); Assert.AreEqual(2, cues);
                f.Model.Cards[1].IsDisabled = true; f.Publish(); f.Click(f.RayAt(second)); Assert.AreEqual(2, cues);
                f.Model.Cards[1].IsDisabled = false; f.Model.Cards[1].Effects = CardEffects.Ineffective; f.Publish();
                f.Pointer.ProcessPointer(f.RayAt(second), pressed: true);
                f.Pointer.ProcessPointer(default(Ray), blocked: true);
                f.Pointer.ProcessPointer(f.RayAt(second), released: true); Assert.AreEqual(2, cues);
                f.Click(f.RayAt(second)); Assert.AreEqual(3, cues);
                f.Model.Reverse(); f.Publish(); f.Zone.RefreshLayout(); Assert.AreEqual(3, cues);
            }
        }

        [Test] public void MouseWritesCardStateAndSelectedLiftRemainsHigherWhenPointerLeaves()
        {
            using (var f = new Fixture())
            {
                var first = f.Zone.Views[0]; var source = f.Model.Cards[0]; var rootPose = first.transform.localPosition;
                var ray = f.RayAt(first); var ids = f.Model.BuildZone().Cards.ToArray();
                f.Pointer.ProcessPointer(ray);
                Assert.IsTrue(source.IsHovered); Assert.IsFalse(source.IsSelected);
                Assert.IsTrue(first.Data.Interaction.IsHovered);
                Assert.AreEqual(Vector3.zero, f.Zone.HoveredOffset);
                Assert.AreEqual(1.2f, first.HoverScale * first.HoverScale, 0.00001f);
                var expected = f.Zone.transform.TransformVector(f.Zone.HoveredOffset);
                Assert.Less(Vector3.Distance(first.FaceRoot.position - first.transform.position, expected), 0.00001f);
                f.Click(ray); Assert.IsTrue(source.IsSelected);
                expected = f.Zone.transform.TransformVector(f.Zone.SelectedOffset);
                f.Pointer.CancelPointer(); Assert.IsFalse(source.IsHovered); Assert.IsTrue(source.IsSelected);
                Assert.Less(Vector3.Distance(first.FaceRoot.position - first.transform.position, expected), 0.00001f);
                Assert.AreEqual(rootPose, first.transform.localPosition);
                f.Click(f.RayAt(first)); f.Pointer.CancelPointer(); Assert.IsFalse(source.IsSelected);
                Assert.Less(Vector3.Distance(first.FaceRoot.position, first.transform.position), 0.00001f);
                CollectionAssert.AreEqual(ids, f.Model.BuildZone().Cards); // Preparing selection never plays a card.
            }
        }

        [Test] public void HoverRetainsRestingFootprintAndNearestRenderedCardWinsOverlap()
        {
            using (var f = new Fixture())
            {
                f.Zone.SetRaisedOffset(new Vector3(0, 0, 0.2f)); var ray = f.RayAt(f.Zone.Views[0]);
                f.Pointer.ProcessPointer(ray);
                for (int i = 0; i < 5; i++) f.Pointer.ProcessPointer(ray);
                Assert.IsTrue(f.Model.Cards[0].IsHovered); Assert.IsFalse(f.Model.Cards[1].IsHovered);
                f.Pointer.CancelPointer();
                var first = f.Zone.Views[0]; var second = f.Zone.Views[1];
                second.transform.localPosition = first.transform.localPosition + new Vector3(0, 0.01f, 0);
                Assert.IsTrue(CardZoneRaycaster.TryHit(f.Zone, f.RayAt(first), null, out var hit));
                Assert.AreSame(second, hit);
                second.Surface.GetComponent<MeshRenderer>().enabled = false;
                Assert.IsTrue(CardZoneRaycaster.TryHit(f.Zone, f.RayAt(first), null, out hit)); Assert.AreSame(first, hit);
            }
        }

        [Test] public void HoverHitFollowsEnlargedCardEdgeAndExitRestoresOriginalSize()
        {
            using (var f = new Fixture(1))
            {
                var card = f.Zone.Views[0]; var surface = card.Surface.transform;
                var bounds = card.Surface.GetComponent<MeshFilter>().sharedMesh.bounds;
                // A point just outside the original width, inside the final 1.0954 scale.
                var point = surface.TransformPoint(bounds.center + new Vector3(bounds.extents.x * 1.05f, 0, 0));
                var edgeRay = new Ray(point + f.Zone.transform.up * 2, -f.Zone.transform.up);
                Assert.IsFalse(CardZoneRaycaster.TryHit(f.Zone, edgeRay, null, out _));
                f.Pointer.ProcessPointer(f.RayAt(card));
                f.Pointer.ProcessPointer(edgeRay);
                Assert.IsTrue(f.Model.Cards[0].IsHovered); Assert.AreEqual(Vector3.zero, card.InteractionOffset);
                f.Pointer.CancelPointer(); Assert.AreEqual(1, card.HoverScale);
                Assert.IsFalse(CardZoneRaycaster.TryHit(f.Zone, edgeRay, null, out _));
            }
        }

        [Test] public void DisabledCardsCanHoverAndDeselectButIneffectiveCardsRemainSelectable()
        {
            using (var f = new Fixture())
            {
                var card = f.Model.Cards[0]; card.IsDisabled = true; f.Publish();
                f.Click(f.RayAt(f.Zone.Views[0])); Assert.IsTrue(card.IsHovered); Assert.IsFalse(card.IsSelected);
                card.IsDisabled = false; card.Effects = CardEffects.Ineffective; f.Publish();
                f.Click(f.RayAt(f.Zone.Views[0])); Assert.IsTrue(card.IsSelected);
                card.IsDisabled = true; f.Publish(); f.Click(f.RayAt(f.Zone.Views[0]));
                Assert.IsFalse(card.IsSelected); Assert.IsTrue(card.IsHovered);
            }
        }

        [Test] public void PointerExitBlockingAndCrossCardReleaseCancelClickWithoutClearingSelection()
        {
            using (var f = new Fixture())
            {
                f.Click(f.RayAt(f.Zone.Views[0]));
                f.Pointer.ProcessPointer(f.RayAt(f.Zone.Views[1]), pressed: true);
                f.Pointer.ProcessPointer(f.RayAt(f.Zone.Views[0]), released: true);
                Assert.IsTrue(f.Model.Cards[0].IsSelected); Assert.IsFalse(f.Model.Cards[1].IsSelected);
                f.Pointer.ProcessPointer(f.RayAt(f.Zone.Views[1]), pressed: true);
                f.Pointer.ProcessPointer(default(Ray), blocked: true);
                f.Pointer.ProcessPointer(f.RayAt(f.Zone.Views[1]), released: true);
                Assert.IsFalse(f.Model.Cards[1].IsSelected);
                f.Pointer.ProcessPointer(new Ray(Vector3.one * 100, Vector3.up));
                Assert.IsFalse(f.Model.Cards.Any(c => c.IsHovered)); Assert.IsTrue(f.Model.Cards[0].IsSelected);
            }
        }

        [Test] public void SelectionSurvivesSortingAndRemovedIdentityCannotCompleteAStaleClick()
        {
            using (var f = new Fixture(3))
            {
                var selected = f.Model.Cards[1]; f.Click(f.RayAt(f.Zone.Views[1]));
                f.Model.SortByRank(); f.Publish(); f.Pointer.CancelPointer();
                CollectionAssert.AreEqual(new[] { selected.Id }, f.Pointer.GetSelectedViewIds());
                int index = f.Model.Cards.ToList().IndexOf(selected);
                f.Pointer.ProcessPointer(f.RayAt(f.Zone.Views[index]), pressed: true);
                f.Model.RemoveAt(index); f.Publish();
                f.Pointer.ProcessPointer(f.RayAt(f.Zone.Views[0]), released: true);
                Assert.IsFalse(f.Model.Cards.Any(c => c.IsSelected));
                f.Model.Clear(); f.Publish(); f.Pointer.ProcessPointer(default(Ray));
                Assert.IsNull(f.Pointer.HoveredViewId); Assert.AreEqual(0, f.Pointer.GetSelectedViewIds().Count);
            }
        }

        [Test] public void RaycastAndZLiftFollowRotatedScaledZoneIncludingFaceDownCards()
        {
            using (var f = new Fixture(1))
            {
                f.Root.transform.SetPositionAndRotation(new Vector3(1, 3, -2), Quaternion.Euler(50, 35, 14));
                f.Root.transform.localScale = new Vector3(2, 0.7f, 1.4f);
                f.Model.Cards[0].FaceUp = false; f.Publish(); var card = f.Zone.Views[0];
                f.Pointer.ProcessPointer(f.RayAt(card)); Assert.IsTrue(f.Model.Cards[0].IsHovered);
                var expected = f.Zone.transform.TransformVector(f.Zone.HoveredOffset);
                Assert.Less(Vector3.Distance(card.FaceRoot.position - card.transform.position, expected), 0.00001f);
                Assert.IsNull(card.Data.CardId); Assert.IsNull(card.Data.FaceArtId);
            }
        }

        [Test] public void IncomingCardsCannotReceiveMouseUntilLandedAndRuntimeUnbindClearsOnlyHover()
        {
            using (var f = new Fixture(1))
            {
                var settings = f.Zone.TransitionSettings; settings.Enabled = true; f.Zone.SetTransitionSettings(settings);
                f.Model.Add("clubs_k"); f.Publish(); var incoming = f.Zone.Views[1];
                Assert.IsTrue(incoming.IsEntering);
                Assert.IsFalse(CardZoneRaycaster.TryHit(f.Zone, f.RayAt(incoming), null, out _));
                f.Zone.CompleteTransitions();
                Assert.IsTrue(CardZoneRaycaster.TryHit(f.Zone, f.RayAt(incoming), null, out var hit)); Assert.AreSame(incoming, hit);
                var input = f.Root.AddComponent<CardZoneMouseInput>(); input.StateChanged += f.Publish; input.Bind(f.Zone, f.Model);
                input.Controller.ProcessPointer(f.RayAt(incoming), pressed: true);
                input.Controller.ProcessPointer(f.RayAt(incoming), released: true);
                Assert.IsTrue(f.Model.Cards[1].IsSelected); Assert.IsTrue(f.Model.Cards[1].IsHovered);
                input.Unbind(); Assert.IsNull(input.Controller);
                Assert.IsTrue(f.Model.Cards[1].IsSelected); Assert.IsFalse(f.Model.Cards[1].IsHovered);
            }
        }
    }
}
