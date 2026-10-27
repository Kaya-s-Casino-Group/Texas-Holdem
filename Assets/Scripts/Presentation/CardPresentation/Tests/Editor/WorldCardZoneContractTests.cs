using System;
using System.Collections.Generic;
using System.Linq;
using BattlePresentation;
using BattlePresentation.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    public sealed class WorldCardZoneContractTests
    {
        private GameObject root;
        private HandZoneView hand;
        private WorldCardZonePresentation view;
        private readonly List<PresentationScope> scopes = new List<PresentationScope>();
        private sealed class Markers : IMarkerSink
        {
            internal readonly List<string> Values = new List<string>();
            public bool Emit(string marker) { Values.Add(marker); return true; }
        }
        [SetUp] public void SetUp()
        {
            root = new GameObject("World card contract test");
            hand = root.AddComponent<HandZoneView>();
            hand.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
            view = root.AddComponent<WorldCardZonePresentation>();
        }
        [TearDown] public void TearDown()
        {
            foreach (var scope in scopes) scope.Dispose(); scopes.Clear();
            Object.DestroyImmediate(root);
        }
        private static CardDisplayData Card(string id, bool hidden = false, bool selected = false) =>
            new CardDisplayData(id, id, "hearts_q", "hearts_q", hidden, properties:
                new Dictionary<string, string> { ["selected"] = selected.ToString() });
        private static CardZoneDisplayData Zone(params CardDisplayData[] cards) => new CardZoneDisplayData("hand/p", "p", "hand", cards);
        private IPresentationOperation Play(string key, CardZoneDisplayData before, CardZoneDisplayData after,
            Markers markers, string[] ids = null, bool observe = false, PlaybackMode mode = PlaybackMode.Animated)
        {
            var scope = new PresentationScope("test/" + scopes.Count); scopes.Add(scope);
            return view.Play(new CardZonePresentationRequest(key, scope.Id, scope, before, after, ids, mode, observe), markers);
        }
        private void Advance(float seconds = 3) { view.AdvancePreview(seconds); }

        [TestCase(2, 0f)] [TestCase(3, .08f)] [TestCase(8, .08f)]
        public void MaximumCardSpacingKeepsSparseAndCurvedRowsCompact(int count, float arc)
        {
            var layout = CardZoneLayoutSettings.Default;
            layout.Size = new Vector2(2, 1); layout.ArcHeight = arc; layout.MaxCardSpacing = .08f;
            var size = new Vector3(.2f, .001f, .3f);
            var poses = CardZoneRowLayout.CalculatePoses(layout, size, count);
            for (int i = 1; i < count; i++)
            {
                var delta = poses[i].Position - poses[i - 1].Position; delta.y = 0;
                Assert.LessOrEqual(delta.magnitude, .080001f);
                Assert.Greater(poses[i].Position.x, poses[i - 1].Position.x);
            }
            Assert.AreEqual(0, poses[0].Position.x + poses[count - 1].Position.x, .000001f);
            Assert.Less(poses[count - 1].Position.x - poses[0].Position.x, layout.Size.x * .5f);
            Assert.AreEqual(0, CardZoneRowLayout.CalculatePoses(layout, size, 1)[0].Position.x);
            Assert.IsEmpty(CardZoneRowLayout.CalculatePoses(layout, size, 0));
            layout.MaxCardSpacing = 0; layout.ArcHeight = 0;
            var legacy = CardZoneRowLayout.CalculatePoses(layout, size, 2);
            Assert.AreEqual(size.x + layout.PreferredGap, legacy[1].Position.x - legacy[0].Position.x, .000001f);
        }

        [Test] public void DrawWaitsForRealArrivalAndPreservesProjectionIdentity()
        {
            var before = Zone(); var after = Zone(Card("a"), Card("b")); view.Synchronize(before);
            var markers = new Markers(); var op = Play(CardZoneActions.Draw, before, after, markers);
            Assert.AreEqual(PresentationStatus.Pending, op.Status); Assert.IsEmpty(markers.Values);
            Assert.AreSame(after, view.Current); Assert.IsTrue(view.IsPresenting);
            Advance(); Assert.AreEqual(PresentationStatus.Completed, op.Status);
            CollectionAssert.AreEqual(new[] { "AllArrived" }, markers.Values);
        }
        [Test] public void PreparationMarkerDoesNotRemoveCardsAndOldScopeCannotUndoDiscard()
        {
            var before = Zone(Card("a"), Card("b")); view.Synchronize(before);
            var markers = new Markers(); var prepare = Play(CardZoneActions.Prepare, before, before, markers, new[] { "a" });
            Assert.IsEmpty(markers.Values); Advance(.1f);
            CollectionAssert.AreEqual(new[] { "Prepared" }, markers.Values);
            Assert.AreEqual(2, view.Current.Cards.Count);
            var after = Zone(Card("b")); var discarded = new Markers();
            var discard = Play(CardZoneActions.Discard, before, after, discarded, new[] { "a" });
            scopes[0].Dispose(); Advance();
            Assert.AreEqual(PresentationStatus.Completed, discard.Status);
            Assert.AreSame(after, view.Current);
            CollectionAssert.AreEqual(new[] { "Released", "AllArrived" }, discarded.Values);
        }
        [Test] public void InstantPreparationCompletesWithoutCancellingItsOwnOperation()
        {
            var snapshot = Zone(Card("a")); view.Synchronize(snapshot);
            var markers = new Markers();
            var op = Play(CardZoneActions.Prepare, snapshot, snapshot, markers, new[] { "a" }, mode: PlaybackMode.Instant);
            Assert.AreEqual(PresentationStatus.Completed, op.Status);
            CollectionAssert.AreEqual(new[] { "Prepared" }, markers.Values);
        }
        [TestCase(false)] [TestCase(true)]
        public void ObservationWaitsForOrderAndStateProjection(bool stateOnly)
        {
            var before = Zone(Card("a"), Card("b"));
            var after = stateOnly ? Zone(Card("a", selected: true), Card("b")) : Zone(Card("b"), Card("a"));
            view.Synchronize(before); var markers = new Markers();
            var wait = Play(CardZoneActions.Settle, before, after, markers, observe: true);
            Advance(); Assert.AreEqual(PresentationStatus.Pending, wait.Status); Assert.IsEmpty(markers.Values);
            Play(CardZoneActions.State, before, after, new Markers()); Advance();
            Assert.AreEqual(PresentationStatus.Completed, wait.Status);
            CollectionAssert.AreEqual(new[] { "Settled" }, markers.Values);
        }
        [Test] public void SynchronizeCancelsPendingWorkAndSuppressesLateMarkers()
        {
            var before = Zone(); var after = Zone(Card("a")); view.Synchronize(before);
            var markers = new Markers(); var op = Play(CardZoneActions.Draw, before, after, markers);
            view.Synchronize(before); Advance();
            Assert.AreEqual(PresentationStatus.Cancelled, op.Status); Assert.IsEmpty(markers.Values);
            Assert.AreSame(before, view.Current); Assert.IsEmpty(hand.Views);
        }
        [Test] public void HiddenReuseClearsOldFaceAndRuleIdentity()
        {
            view.Synchronize(Zone(Card("a"))); var old = hand.Views[0];
            view.Synchronize(Zone(Card("a", hidden: true)));
            Assert.AreSame(old, hand.Views[0]); Assert.IsNull(old.Data.CardId);
            Assert.AreEqual("card_back", old.Surface.CardId); Assert.IsFalse(old.Data.FaceUp);
        }
        [Test] public void InstantObservationPublishesItsTargetWithoutWaitingForExternalAnimation()
        {
            var before = Zone(Card("a")); var after = Zone(Card("b")); view.Synchronize(before);
            var markers = new Markers();
            var op = Play(CardZoneActions.Settle, before, after, markers, observe: true, mode: PlaybackMode.Instant);
            Assert.AreEqual(PresentationStatus.Completed, op.Status); Assert.AreSame(after, view.Current);
            Assert.IsFalse(view.IsPresenting); CollectionAssert.AreEqual(new[] { "Settled" }, markers.Values);
        }
        [Test] public void DisabledAdapterCancelsWaitersWithoutEmittingCompletion()
        {
            var before = Zone(); var after = Zone(Card("a")); view.Synchronize(before);
            var markers = new Markers(); var op = Play(CardZoneActions.Settle, before, after, markers, observe: true);
            view.enabled = false; Advance();
            Assert.AreEqual(PresentationStatus.Cancelled, op.Status); Assert.IsEmpty(markers.Values);
        }
        [Test] public void SelectionReplacementContinuesFromCurrentPoseAndIsInputBlocking()
        {
            var before = Zone(Card("a")); var selected = Zone(Card("a", selected: true)); view.Synchronize(before);
            var op = Play(CardZoneActions.Selection, before, selected, new Markers()); Advance(.03f);
            Assert.IsTrue(view.IsPresenting);
            var next = Play(CardZoneActions.Selection, selected, before, new Markers());
            Assert.AreEqual(PresentationStatus.Cancelled, op.Status);
            Advance(); Assert.AreEqual(PresentationStatus.Completed, next.Status);
            Assert.AreSame(before, view.Current); Assert.IsFalse(view.IsPresenting);
        }
        private static BattleDisplaySnapshot Snapshot(CardZoneDisplayData zone, DisplayCursor cursor = null) =>
            new BattleDisplaySnapshot("battle", "p", 1, "play", 1, null, null, new[] { zone }, cursor: cursor);

        [Test] public void ConsecutiveCheckpointsRememberEarlierProjectionUntilMotionSettles()
        {
            var director = root.AddComponent<ScenePresentationDirector>();
            root.AddComponent<WorldCardZoneDisplayBinding>().Configure(director, "hand/p");
            var action = root.AddComponent<CardZoneTimelineAction>(); action.Configure(view, CardZoneActions.Settle, "hand/p", true);
            director.Configure(_ => new ActionPlan(new[] { new ActionNode("settle", action) }, Array.Empty<ActionDependency>()));
            var before = Snapshot(Zone());
            var first = Snapshot(Zone(Card("a")), new DisplayCursor("batch", 0));
            var last = Snapshot(Zone(Card("a"), Card("b")), new DisplayCursor("batch", 1));
            director.Initialize(before);
            var op = director.Present(new PresentationBatch("batch", before, new[] {
                new BattleDisplayChange(0, before.Cursor, first, "CardsDealt", before: before),
                new BattleDisplayChange(1, first.Cursor, last, "CardsDealt", before: first)
            }, last), PlaybackMode.Animated);
            Assert.AreSame(last.Zones[0], view.Current);
            Advance(); director.Tick();
            Assert.AreEqual(PresentationStatus.Completed, op.Status);
            Assert.IsFalse(director.IsPresenting);
        }

        [Test] public void CandidateFeedbackPreservesFactsAndFlightAndClearsOnSynchronize()
        {
            var before = Zone(); var after = Zone(Card("a")); view.Synchronize(before);
            var draw = Play(CardZoneActions.Draw, before, after, new Markers());
            view.PresentInteraction(new[] { new KeyValuePair<string, CardInteractionState>("a", new CardInteractionState(true, true)) });
            Assert.AreSame(after, view.Current);
            Assert.IsTrue(hand.Views[0].IsEntering);
            Assert.IsTrue(hand.Views[0].Data.Interaction.IsSelected);
            Advance(); Assert.AreEqual(PresentationStatus.Completed, draw.Status);
            view.Synchronize(after);
            Assert.IsFalse(hand.Views[0].Data.Interaction.IsSelected);
            Assert.IsFalse(hand.Views[0].Data.Interaction.IsHovered);
        }

        [Test] public void LocalArrangementPreservesFactsAndReconcilesNewMembership()
        {
            CardDisplayData Ranked(string id, int rank, string suit) => new CardDisplayData(id, id, "hearts_q", "hearts_q",
                properties: new Dictionary<string, string> { ["rank"] = rank.ToString(), ["suit"] = suit });
            var a = Ranked("a", 2, "Spades"); var b = Ranked("b", 14, "Hearts"); var c = Ranked("c", 13, "Clubs");
            var snapshot = Zone(a, b, c); view.Synchronize(snapshot);
            view.PresentInteraction(new[] { new KeyValuePair<string, CardInteractionState>("a", new CardInteractionState(false, true)) });
            Assert.IsTrue(view.SortByRank());
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, view.VisualOrder);
            Assert.AreSame(snapshot, view.Current);
            Assert.IsTrue(hand.Views.Last().Data.Interaction.IsSelected);
            Assert.IsTrue(view.SortBySuit());
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, view.VisualOrder);
            Assert.IsTrue(view.MoveToIndex("c", 0));
            var after = Zone(a, c, Ranked("d", 3, "Diamonds")); view.Synchronize(after);
            CollectionAssert.AreEqual(new[] { "c", "a", "d" }, view.VisualOrder);
            CollectionAssert.AreEqual(new[] { "a", "c", "d" }, view.Current.Cards.Select(card => card.ViewId));
            view.Synchronize(new CardZoneDisplayData("new-hand", "p", "hand", new[] { a, c }));
            CollectionAssert.AreEqual(new[] { "a", "c" }, view.VisualOrder);
        }

        [Test] public void DragMakesInsertionSlotAndCancellationRestoresOrder()
        {
            var snapshot = Zone(Card("a"), Card("b"), Card("c")); view.Synchronize(snapshot);
            var card = hand.Views[0];
            var center = card.Surface.GetComponent<Renderer>().bounds.center;
            var ray = new Ray(center + Vector3.up * 2, Vector3.down);
            Assert.IsTrue(view.BeginReorder("a", ray));
            var lastX = hand.Views[2].transform.localPosition.x;
            var target = center; target.x = lastX;
            Assert.IsTrue(view.UpdateReorder(new Ray(target + Vector3.up * 2, Vector3.down)));
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, view.VisualOrder);
            Assert.Greater(card.Surface.GetComponent<Renderer>().bounds.center.y, center.y + .04f);
            Assert.AreSame(snapshot, view.Current);
            view.EndReorder(true, false);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, view.VisualOrder);
            Assert.AreEqual(Vector3.zero, card.DragRoot.localPosition);
            Assert.IsFalse(view.IsReordering);
        }

        [TestCase(PlaybackMode.Animated)] [TestCase(PlaybackMode.Instant)]
        public void DirectorCheckpointBindingRespectsAnimatedAndInstantModes(PlaybackMode mode)
        {
            var director = root.AddComponent<ScenePresentationDirector>();
            root.AddComponent<WorldCardZoneDisplayBinding>().Configure(director, "hand/p");
            var action = root.AddComponent<CardZoneTimelineAction>(); action.Configure(view, CardZoneActions.Settle, "hand/p", true);
            director.Configure(_ => new ActionPlan(new[] { new ActionNode("settle", action) }, Array.Empty<ActionDependency>()));
            var before = Snapshot(Zone()); var after = Snapshot(Zone(Card("a")), new DisplayCursor("batch", 0));
            director.Initialize(before);
            var change = new BattleDisplayChange(0, before.Cursor, after, "CardsDealt", before: before);
            var op = director.Present(new PresentationBatch("batch", before, new[] { change }, after), mode);
            director.Tick();
            Assert.AreSame(after.Zones[0], view.Current);
            if (mode == PlaybackMode.Animated)
            {
                Assert.AreEqual(PresentationStatus.Pending, op.Status);
                Advance(); director.Tick();
            }
            Assert.AreEqual(PresentationStatus.Completed, op.Status);
            Assert.IsFalse(view.IsPresenting); Assert.IsFalse(hand.IsTransitioning);
        }
    }
}
