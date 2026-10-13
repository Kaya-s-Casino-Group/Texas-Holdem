using System;
using System.Linq;
using System.Reflection;
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
    public sealed class CardZoneDrawExperimentTests
    {
        [Serializable] private sealed class LabState
        {
            public CardZonePreviewModel Hand = new CardZonePreviewModel();
            public CardZoneDrawExperiment Experiment = new CardZoneDrawExperiment();
        }

        [Test] public void TopFirstRowsMatchSingleAndBatchDrawsInEdit() => AssertTopFirstDraws(false);
        [Test] public void TopFirstRowsMatchSingleAndBatchDrawsInPlayCopy() => AssertTopFirstDraws(true);

        private static void AssertTopFirstDraws(bool inPlay)
        {
            var store = new CardZoneDrawExperimentStore(); var edit = store.Get("rows", false);
            edit.PrepareDeck(new[] { "clubs_j", "hearts_q", "spades_a", "diamonds_k", "hearts_02" }, 5);
            edit.Deck.Move(1, 4); edit.DeckCards.Last().FaceUp = false;
            edit.DeckCards.Last().Effects = CardEffects.Laminated;
            string original = JsonUtility.ToJson(edit);
            var singles = store.Get("rows", inPlay);
            var batch = JsonUtility.FromJson<CardZoneDrawExperiment>(JsonUtility.ToJson(singles));
            var visible = new CardZonePreviewList(singles.Deck, topFirst: true).Cards.ToArray();
            var oneHand = new CardZonePreviewModel(); oneHand.Add("clubs_02");
            var batchHand = JsonUtility.FromJson<CardZonePreviewModel>(JsonUtility.ToJson(oneHand));
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(visible[i].Id, new CardZonePreviewList(singles.Deck, topFirst: true).Cards[0].Id);
                singles.Draw(oneHand, 1);
                Assert.AreEqual(visible[i].Id, singles.Drawn.Single());
            }
            batch.Draw(batchHand, 3);
            CollectionAssert.AreEqual(visible.Take(3).Select(card => card.Id), batch.Drawn);
            Assert.AreEqual(JsonUtility.ToJson(oneHand), JsonUtility.ToJson(batchHand));
            CollectionAssert.AreEqual(visible.Skip(3).Select(card => card.Id),
                new CardZonePreviewList(batch.Deck, topFirst: true).Cards.Select(card => card.Id));
            Assert.IsFalse(batchHand.Cards[1].FaceUp); Assert.AreEqual(CardEffects.Laminated, batchHand.Cards[1].Effects);
            if (inPlay) Assert.AreEqual(original, JsonUtility.ToJson(store.Get("rows", false)));
        }

        [Test] public void TopFirstEditingMapsVisibleMovesInsertDuplicateDeleteAndSortToTheNextDraw()
        {
            var experiment = new CardZoneDrawExperiment(); experiment.PrepareDeck(new[] { "clubs_j", "hearts_q", "spades_a", "hearts_02" }, 4);
            var display = new CardZonePreviewList(experiment.Deck, topFirst: true);
            var raised = display.Cards[2]; raised.FaceUp = false; raised.Effects = CardEffects.Enhanced;
            display.Move(2, 0); Assert.AreSame(raised, display.Cards[0]);
            display.DuplicateAfter(0); Assert.AreSame(raised, display.Cards[0]);
            var duplicate = display.Cards[1]; Assert.AreNotEqual(raised.Id, duplicate.Id);
            Assert.AreEqual(raised.ArtId, duplicate.ArtId); Assert.AreEqual(raised.Effects, duplicate.Effects); Assert.IsFalse(duplicate.FaceUp);
            display.Insert(0, "diamonds_k"); Assert.AreEqual("diamonds_k", display.Cards[0].ArtId);
            display.Insert(display.Cards.Count, "clubs_03"); Assert.AreEqual("clubs_03", display.Cards.Last().ArtId);
            display.Move(0, 2); Assert.AreEqual("diamonds_k", display.Cards[2].ArtId);
            display.RemoveAt(1); Assert.IsFalse(display.Cards.Any(card => card.Id == duplicate.Id));
            display.Insert(display.Cards.Count, "joker_big", 2);
            var expected = JsonUtility.FromJson<CardZonePreviewModel>(JsonUtility.ToJson(experiment.Deck));
            expected.Reverse(); // Reference sort starts from the same visible order, including tied artwork.
            expected.SortByRank(); display.SortByRank();
            CollectionAssert.AreEqual(expected.Cards.Select(card => card.Id), display.Cards.Select(card => card.Id));
            expected.SortBySuit(); display.SortBySuit();
            CollectionAssert.AreEqual(expected.Cards.Select(card => card.Id), display.Cards.Select(card => card.Id));
            var next = display.Cards.Take(3).Select(card => card.Id).ToArray();
            experiment.Draw(new CardZonePreviewModel(), 3); CollectionAssert.AreEqual(next, experiment.Drawn);

            var hand = new CardZonePreviewModel(); var straight = new CardZonePreviewList(hand);
            straight.Insert(0, "spades_a"); straight.Insert(1, "clubs_02"); var first = straight.Cards[0];
            straight.Move(0, 1); Assert.AreSame(first, hand.Cards[1]);
            straight.DuplicateAfter(0); Assert.AreEqual("clubs_02", hand.Cards[1].ArtId);
            CollectionAssert.AreEqual(hand.Cards, straight.Cards);
        }

        [Test] public void PlayDeckKeepsConfiguredCardsAcrossReloadAndConsumesOnlyItsCopy()
        {
            var store = new CardZoneDrawExperimentStore(); var edit = store.Get("hand", false);
            edit.PrepareDeck(new[] { "hearts_q", "spades_a", "clubs_j" }, 5); edit.Deck.Move(0, 4);
            var top = edit.DeckCards.Last(); top.Effects = CardEffects.Laminated | CardEffects.Enhanced;
            top.FaceUp = false; top.IsDisabled = true; top.Tint = Color.cyan; top.OriginId = "test-deck";
            string original = JsonUtility.ToJson(edit); var ids = edit.DeckCards.Select(card => card.Id).ToArray();
            store.BeginPlay();
            store = JsonUtility.FromJson<CardZoneDrawExperimentStore>(JsonUtility.ToJson(store));
            var play = store.Get("hand", true); var hand = new CardZonePreviewModel();
            CollectionAssert.AreEqual(ids, play.DeckCards.Select(card => card.Id));
            play.Draw(hand, 2);
            var drawnTop = hand.Cards.Single(card => card.Id == top.Id);
            Assert.AreEqual(top.ArtId, drawnTop.ArtId);
            Assert.AreEqual(top.Effects, drawnTop.Effects); Assert.AreEqual(top.Tint, drawnTop.Tint);
            Assert.AreEqual(top.OriginId, drawnTop.OriginId); Assert.IsTrue(drawnTop.IsDisabled); Assert.IsFalse(drawnTop.FaceUp);
            CollectionAssert.AreEqual(ids.Reverse().Take(2), play.Drawn); Assert.AreEqual(3, play.DeckCards.Count);
            Assert.AreEqual(original, JsonUtility.ToJson(store.Get("hand", false)));

            // A domain reload in Play must retain remaining cards rather than cloning the edit deck again.
            string consumed = JsonUtility.ToJson(play);
            store = JsonUtility.FromJson<CardZoneDrawExperimentStore>(JsonUtility.ToJson(store)); store.BeginPlay();
            Assert.AreEqual(consumed, JsonUtility.ToJson(store.Get("hand", true)));
            store.Get("hand", true).Draw(hand, 3); Assert.AreEqual(5, hand.Cards.Count);
            Assert.IsEmpty(store.Get("hand", true).DeckCards);
            store.EndPlay(); Assert.AreEqual(original, JsonUtility.ToJson(store.Get("hand", false)));
            store.BeginPlay(); Assert.AreEqual(original, JsonUtility.ToJson(store.Get("hand", true)));
        }

        [Test] public void IndependentPlayDecksRetainEmptyStateWhenSwitchingZonesAndRestarting()
        {
            var store = new CardZoneDrawExperimentStore();
            var left = store.Get("left", false); left.PrepareDeck(new[] { "spades_a" }, 2);
            var right = store.Get("right", false); right.PrepareDeck(new[] { "hearts_q" }, 1); right.Deck.Clear();
            var expected = left.DeckCards.Select(card => card.Id).ToArray();
            store.BeginPlay(); var active = store.Get("left", true); active.Draw(new CardZonePreviewModel(), 2);
            var empty = store.Get("right", true); empty.EnsureDeck(new[] { "diamonds_a" }); Assert.IsEmpty(empty.DeckCards);
            Assert.AreSame(active, store.Get("left", true)); active.EnsureDeck(new[] { "clubs_j" }); Assert.IsEmpty(active.DeckCards);
            Assert.Throws<ArgumentOutOfRangeException>(() => active.Draw(new CardZonePreviewModel(), 1));
            CollectionAssert.AreEqual(expected, store.Get("left", false).DeckCards.Select(card => card.Id));
            store.EndPlay(); store.BeginPlay();
            CollectionAssert.AreEqual(expected, store.Get("left", true).DeckCards.Select(card => card.Id));
            store.Get("right", true).EnsureDeck(new[] { "clubs_j" }); Assert.IsEmpty(store.Get("right", true).DeckCards);
        }

        [Test] public void DrawUsesCoreDeckTopOrderAndPreservesCardIdentityAndProperties()
        {
            var hand = new CardZonePreviewModel(); hand.Add("clubs_j");
            var experiment = new CardZoneDrawExperiment(); experiment.PrepareDeck(new[] { "hearts_q", "spades_a" }, 5);
            var deckBefore = experiment.BuildDeckZone(); var handBefore = hand.BuildZone();
            var top = experiment.DeckCards.Last(); top.Effects = CardEffects.Laminated; top.FaceUp = false;
            var expected = deckBefore.DrawTo(handBefore, 3, handBefore.Cards.Count);
            experiment.Draw(hand, 3);
            CollectionAssert.AreEqual(expected.Source.Cards, experiment.BuildDeckZone().Cards);
            CollectionAssert.AreEqual(expected.Target.Cards, hand.BuildZone().Cards);
            CollectionAssert.AreEqual(hand.Cards.Select(card => card.Id), experiment.HandAfter);
            CollectionAssert.AreEqual(expected.Cards.Select(c => c.Value), experiment.Drawn);
            CollectionAssert.AreEqual(handBefore.Cards.Select(c => c.Value), experiment.HandBefore);
            CollectionAssert.AreEqual(deckBefore.Cards.Select(c => c.Value), experiment.DeckBefore);
            Assert.AreSame(top, hand.Cards[1]); Assert.AreEqual(CardEffects.Laminated, hand.GetCard(new CardInstanceId(top.Id)).Effects);
            Assert.IsFalse(hand.Cards[1].FaceUp); Assert.IsNull(hand.Project(new CardInstanceId(top.Id)).FaceArtId);
            Assert.AreEqual(5, deckBefore.Cards.Count); Assert.AreEqual(1, handBefore.Cards.Count);
            Assert.AreEqual(2, experiment.DeckCards.Count); Assert.AreEqual(4, hand.Cards.Count);
        }

        [Test] public void PickingAnyDeckCardInsertsAtAnyHandPositionAndPreservesThatInstance()
        {
            var state = new LabState(); state.Hand.Add("clubs_j"); state.Hand.Add("diamonds_a");
            state.Experiment.PrepareDeck(new[] { "hearts_q", "spades_a" }, 5);
            var picked = state.Experiment.DeckCards[1]; picked.FaceUp = false; picked.Effects = CardEffects.Ineffective;
            var originalDeck = state.Experiment.DeckCards.Select(card => card.Id).ToArray();
            var last = state.Hand.Cards.Last();
            state.Experiment.DrawCard(state.Hand, new CardInstanceId(picked.Id), 1);
            Assert.AreSame(picked, state.Hand.Cards[1]); Assert.AreSame(last, state.Hand.Cards[2]);
            Assert.IsFalse(picked.ToCard().CanScore); Assert.IsFalse(state.Hand.Project(new CardInstanceId(picked.Id)).FaceUp);
            CollectionAssert.AreEqual(originalDeck.Where(id => id != picked.Id), state.Experiment.DeckCards.Select(card => card.Id));
            CollectionAssert.AreEqual(new[] { picked.Id }, state.Experiment.Drawn); Assert.IsFalse(state.Experiment.GeneratedDraw);
            var top = state.Experiment.DeckCards.Last();
            state.Experiment.Draw(state.Hand, 1, 0); Assert.AreSame(top, state.Hand.Cards[0]);
        }

        [Test] public void SpecifiedDrawWorksWithEmptyDeckCreatesDistinctCardsAndKeepsDeckAuditHonest()
        {
            var state = new LabState(); state.Hand.Add("clubs_j");
            state.Experiment.DrawSpecified(state.Hand, "spades_a", 3, 0, faceUp: false);
            Assert.AreEqual(0, state.Experiment.DeckCards.Count); Assert.IsTrue(state.Experiment.GeneratedDraw);
            Assert.IsEmpty(state.Experiment.DeckBefore); Assert.IsEmpty(state.Experiment.DeckAfter);
            Assert.AreEqual(4, state.Hand.Cards.Count);
            Assert.IsTrue(state.Hand.Cards.Take(3).All(card => card.ArtId == "spades_a" && !card.FaceUp));
            Assert.AreEqual(3, state.Experiment.Drawn.Distinct().Count());
            var copy = JsonUtility.FromJson<LabState>(JsonUtility.ToJson(state));
            Assert.IsTrue(copy.Experiment.GeneratedDraw); CollectionAssert.AreEqual(state.Experiment.Drawn, copy.Experiment.Drawn);
            copy.Experiment.PrepareDeck(new[] { "hearts_q" }, 2);
            var deck = copy.Experiment.DeckCards.Select(card => card.Id).ToArray();
            copy.Experiment.DrawSpecified(copy.Hand, "joker_big", 1);
            CollectionAssert.AreEqual(deck, copy.Experiment.DeckCards.Select(card => card.Id));
            CollectionAssert.AreEqual(copy.Experiment.DeckBefore, copy.Experiment.DeckAfter);
            Assert.AreEqual("joker_big", copy.Hand.Cards.Last().ArtId);
        }

        [Test] public void InvalidChosenDrawLeavesBothListsAndPreviousAuditUnchanged()
        {
            var state = new LabState(); state.Experiment.PrepareDeck(new[] { "hearts_q" }, 4); state.Experiment.Draw(state.Hand, 1);
            var before = JsonUtility.ToJson(state); var id = new CardInstanceId(state.Experiment.DeckCards[0].Id);
            Assert.Throws<ArgumentException>(() => state.Experiment.DrawCard(state.Hand, new CardInstanceId("missing")));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Experiment.DrawCard(state.Hand, id, 99));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Experiment.DrawSpecified(state.Hand, "spades_a", 2, -2));
            Assert.Throws<InvalidOperationException>(() => state.Experiment.DrawSpecified(state.Hand, "spades_a", 64));
            Assert.Throws<ArgumentException>(() => state.Hand.TakeFromDeck(state.Experiment.Deck, new[] { id, id }));
            Assert.AreEqual(before, JsonUtility.ToJson(state));
        }

        [Test] public void SerializationAndRestoreKeepBothZonesAndLastDrawAuditConsistent()
        {
            var state = new LabState(); state.Hand.Add("clubs_j");
            state.Experiment.PrepareDeck(new[] { "hearts_q", "spades_a" }, 4);
            string before = JsonUtility.ToJson(state);
            state.Experiment.Draw(state.Hand, 2);
            string after = JsonUtility.ToJson(state);
            var restored = JsonUtility.FromJson<LabState>(after);
            CollectionAssert.AreEqual(state.Hand.BuildZone().Cards, restored.Hand.BuildZone().Cards);
            CollectionAssert.AreEqual(state.Experiment.BuildDeckZone().Cards, restored.Experiment.BuildDeckZone().Cards);
            CollectionAssert.AreEqual(state.Experiment.HandBefore, restored.Experiment.HandBefore);
            CollectionAssert.AreEqual(state.Experiment.HandAfter, restored.Experiment.HandAfter);
            CollectionAssert.AreEqual(state.Experiment.Drawn, restored.Experiment.Drawn);
            restored.Experiment.EnsureDeck(new[] { "diamonds_a" });
            Assert.AreEqual(2, restored.Experiment.DeckCards.Count);
            restored.Experiment.Draw(restored.Hand, 2);
            Assert.AreEqual(0, restored.Experiment.DeckCards.Count);
            restored.Experiment.EnsureDeck(new[] { "spades_a" }); Assert.AreEqual(0, restored.Experiment.DeckCards.Count);
            // Window Undo restores these same serialized fields together, then snaps the View to the restored hand.
            JsonUtility.FromJsonOverwrite(before, restored);
            Assert.AreEqual(1, restored.Hand.Cards.Count); Assert.AreEqual(4, restored.Experiment.DeckCards.Count);
            Assert.IsFalse(restored.Experiment.HasDraw);
        }

        [Test] public void InvalidDrawAndRefillDoNotPartiallyChangeZonesOrHistory()
        {
            var state = new LabState(); state.Experiment.PrepareDeck(new[] { "hearts_q" }, 2);
            state.Experiment.Draw(state.Hand, 1); string before = JsonUtility.ToJson(state);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Experiment.Draw(state.Hand, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Experiment.Draw(state.Hand, 0));
            Assert.Throws<ArgumentException>(() => state.Experiment.PrepareDeck(new string[0]));
            Assert.AreEqual(before, JsonUtility.ToJson(state));
            while (state.Hand.Cards.Count < CardZonePreviewModel.MaximumCards) state.Hand.Add("clubs_j");
            before = JsonUtility.ToJson(state);
            Assert.Throws<InvalidOperationException>(() => state.Experiment.Draw(state.Hand, 1));
            Assert.AreEqual(before, JsonUtility.ToJson(state));
            var handIds = state.Hand.BuildZone().Cards.ToArray();
            state.Experiment.PrepareDeck(new[] { "spades_a" });
            CollectionAssert.AreEqual(handIds, state.Hand.BuildZone().Cards); Assert.AreEqual(12, state.Experiment.DeckCards.Count);
        }

        [Test] public void DrawSnapshotReachesViewWhilePausedAndCanBeSteppedWithoutFurtherListChanges()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Draw Experiment Test") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
            var view = root.AddComponent<HandZoneView>();
            try
            {
                view.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var model = new CardZonePreviewModel(); model.Add("clubs_j");
                var experiment = new CardZoneDrawExperiment(); experiment.PrepareDeck(new[] { "hearts_q", "spades_a" }, 6);
                view.Present(model.BuildZone(), model.Project, animate: false); var retained = view.Views[0];
                experiment.Draw(model, 3); view.Present(model.BuildZone(), model.Project);
                Assert.AreSame(retained, view.Views.Single(card => card.ViewId == retained.ViewId)); Assert.AreEqual(4, model.Cards.Count); Assert.AreEqual(3, experiment.DeckCards.Count);
                Assert.AreEqual(3, view.Views.Count(v => v.IsEntering));
                var first = view.Views.First(card => card.IsEntering); Assert.Less(Vector3.Distance(first.DisplayPose.Position, view.DeckLocalPosition), 0.00001f);
                var handIds = model.BuildZone().Cards.ToArray(); var deckIds = experiment.BuildDeckZone().Cards.ToArray();
                view.AdvanceMotion(0.05f);
                Assert.Greater(Vector3.Distance(first.DisplayPose.Position, view.DeckLocalPosition), 0.001f);
                CollectionAssert.AreEqual(handIds, model.BuildZone().Cards); CollectionAssert.AreEqual(deckIds, experiment.BuildDeckZone().Cards);
                // Another draw during the first animation updates the list and reuses the same views.
                experiment.Draw(model, 1); view.Present(model.BuildZone(), model.Project);
                Assert.AreSame(first, view.Views.Single(card => card.ViewId == first.ViewId)); Assert.AreEqual(5, view.Views.Count);
                view.CompleteTransitions(); Assert.IsFalse(view.IsTransitioning);
                CollectionAssert.AreEqual(model.Cards.Select(c => c.Id), view.Views.Select(v => v.ViewId));
            }
            finally { view.Clear(); Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
