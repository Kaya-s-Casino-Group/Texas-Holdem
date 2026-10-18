using System;
using System.Linq;
using NUnit.Framework;
using global::BattlePresentation;
using PokerEngine;

namespace Poker.BattlePresentation.Tests
{
    [TestFixture]
    public sealed class BattlePresentationBridgeTests
    {
        private static readonly ParticipantId Player = new ParticipantId("bridge-player"), Other = new ParticipantId("bridge-other"), Enemy = new ParticipantId("bridge-enemy");
        private sealed class Frontend : IBattlePresentationFrontend
        {
            public readonly DisplayStore Store = new DisplayStore();
            public PresentationBatch Batch;
            public bool IsPresenting { get; private set; }
            public void Initialize(BattleDisplaySnapshot snapshot) => Store.Initialize(snapshot);
            public IPresentationOperation Present(PresentationBatch batch, PlaybackMode mode)
            {
                Store.Validate(batch); Batch = batch;
                if (mode == PlaybackMode.Instant) { Store.ApplyInstant(batch); return PresentationOperation.Completed(); }
                IsPresenting = true; return new PresentationOperation(() => IsPresenting = false);
            }
            public void Synchronize(BattleDisplaySnapshot snapshot) { IsPresenting = false; Store.Synchronize(snapshot); }
        }
        private sealed class Fixture
        {
            public readonly CardDuelEngine Engine;
            public readonly BattleSession Session;
            public readonly Frontend Frontend = new Frontend();
            public readonly BattlePresentationBridge Bridge;
            public Fixture(int energy = 3, bool secondPlayer = false, bool disabledDeck = false, int deckSize = 52)
            {
                Engine = new CardDuelEngine(new BattleRules("bridge.1", 5));
                var players = new[] { Player, Other }.Take(secondPlayer ? 2 : 1).Select(id => new BattlePlayerSetup(id,
                    new CombatantDefinition("hero", 100), rules: new BattlePlayerRules(maxEnergy: 3, initialEnergy: energy),
                    deck: StandardDeck.Cards().Take(deckSize).Select((card, i) => new DeckEntry("card/" + i, card.Id,
                        effects: disabledDeck ? CardEffects.Enhanced : CardEffects.None, isDisabled: disabledDeck))));
                Session = new BattleSession(Engine, Engine.Create("bridge", HmacDeckShuffler.FromSeed(new byte[32]), players,
                    new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 10000), 7) }));
                Bridge = new BattlePresentationBridge(Engine, Player, Frontend); Bridge.Initialize(Session.State);
            }
            public BattleSessionResult Execute(CardDuelAction action, int count = 0)
            {
                var result = Session.Execute(Session.State.Revision, Player, action,
                    count == 0 ? null : Session.State.Zone(CardZoneKinds.Hand, Player).Cards.Take(count));
                Assert.IsTrue(result.Accepted, result.Error.ToString()); return result;
            }
            public void Start() { Bridge.Present(Execute(CardDuelAction.Start), false); }
        }

        [Test] public void PresentBuildsContinuousImmutableCheckpointsWithoutPublishingFinalStateEarly()
        {
            var f = new Fixture(); f.Start(); var before = f.Frontend.Store.Current;
            var batch = f.Execute(CardDuelAction.Play, 1);
            int publications = 0; f.Frontend.Store.Changed += _ => publications++;
            f.Bridge.Present(batch, true);
            Assert.AreSame(before, f.Frontend.Store.Current);
            Assert.AreEqual(0, publications);
            var projected = f.Frontend.Batch;
            var cursor = projected.Initial.Cursor;
            foreach (var change in projected.Changes)
            {
                Assert.AreEqual(cursor, change.ExpectedCursor);
                Assert.AreEqual("bridge", change.After.BattleId); Assert.AreEqual(Player.Value, change.After.ObserverId);
                Assert.AreSame(change.Before.Cursor, change.ExpectedCursor);
                cursor = new DisplayCursor(projected.Id, change.Sequence);
                Assert.AreEqual(cursor, change.After.Cursor);
                f.Frontend.Store.Apply(projected.Id, change);
                Assert.AreEqual(change.After.Unit(Enemy.Value).Health, f.Frontend.Store.Current.Unit(Enemy.Value).Health);
            }
            Assert.AreEqual(projected.FinalSnapshot.RuleRevision, f.Frontend.Store.Current.RuleRevision);
            Assert.AreEqual(batch.State.Duel.Combat.Get(Enemy).Health, f.Frontend.Store.Current.Unit(Enemy.Value).Health);
            Assert.AreEqual(projected.Changes.Count, publications);
        }

        [Test] public void FactResolutionNeverUsesTheFinalFallbackForAnInitialOrUnknownCursor()
        {
            var f = new Fixture(); f.Start(); var beforeState = f.Session.State; var oldCursor = f.Frontend.Store.Current.Cursor;
            f.Bridge.Present(f.Execute(CardDuelAction.Play, 1), true);
            Assert.AreSame(beforeState, f.Bridge.StateAt(oldCursor), "The currently displayed cursor still means the pre-command state.");
            Assert.Throws<InvalidOperationException>(() => f.Bridge.StateAt(new DisplayCursor("unknown-batch", 1000)));
            foreach (var change in f.Frontend.Batch.Changes)
                Assert.AreEqual(change.After.Unit(Enemy.Value).Health, f.Bridge.StateAt(change.After.Cursor).Duel.Combat.Get(Enemy).Health);
        }

        [Test] public void SynchronizeCancelsPlaybackAndReplacesDisplayWithCommittedSnapshot()
        {
            var f = new Fixture(); f.Start(); var accepted = f.Execute(CardDuelAction.Play, 1);
            f.Bridge.Present(accepted, true); Assert.IsTrue(f.Bridge.IsPresenting);
            f.Bridge.Synchronize(accepted.State);
            Assert.IsFalse(f.Bridge.IsPresenting); Assert.AreEqual(DisplayCursor.Empty, f.Frontend.Store.Current.Cursor);
            Assert.AreEqual(accepted.State.Revision, f.Frontend.Store.Current.RuleRevision);
            Assert.AreEqual(accepted.State.Duel.Combat.Get(Enemy).Health, f.Frontend.Store.Current.Unit(Enemy.Value).Health);
        }

        [Test] public void HiddenDeckAndOtherHandDiscloseNoFaceOrRuleIdentityOrAttributes()
        {
            var f = new Fixture(secondPlayer: true, disabledDeck: true); f.Start();
            var display = f.Frontend.Store.Current;
            var ownHand = display.Zones.Single(x => x.Kind == CardZoneKinds.Hand && x.OwnerId == Player.Value);
            Assert.IsTrue(ownHand.Cards.All(x => !x.Hidden && x.CardId != null && x.FaceKey != null));
            var hidden = display.Zones.Where(x => x.Kind == CardZoneKinds.Deck || x.Kind == CardZoneKinds.Hand && x.OwnerId != Player.Value).SelectMany(x => x.Cards).ToArray();
            Assert.IsNotEmpty(hidden);
            foreach (var card in hidden)
            {
                Assert.IsTrue(card.Hidden); Assert.IsNull(card.CardId); Assert.IsNull(card.FaceKey);
                Assert.IsTrue(card.ArtKey == null || card.ArtKey == "back"); Assert.IsEmpty(card.Properties); Assert.IsNull(card.DisabledReason);
                Assert.IsFalse(f.Session.State.Table.Cards.Any(actual => actual.Id.Value == card.ViewId));
            }
        }

        [Test] public void HiddenDealActionPayloadDoesNotDiscloseOtherPlayersCardIdentities()
        {
            var f = new Fixture(secondPlayer: true); f.Start();
            var hiddenIds = f.Session.State.Zone(CardZoneKinds.Hand, Other).Cards.Select(id => id.Value).ToArray();
            Assert.IsTrue(f.Frontend.Batch.Changes.SelectMany(change => change.CardIds).All(id => !hiddenIds.Contains(id)));
            var visibleDeal = f.Frontend.Batch.Changes.First(change => change.SemanticKey == "CardsDealt");
            Assert.AreEqual(8, visibleDeal.CardIds.Count);
        }

        [Test] public void RecyclePayloadMayNameVisibleSourceCardsButNeverTheirPrivateShuffleOrder()
        {
            var f = new Fixture(deckSize: 10); f.Start();
            var accepted = f.Execute(CardDuelAction.Discard, 5); f.Bridge.Present(accepted, false);
            var recycle = f.Frontend.Batch.Changes.Single(change => change.SemanticKey == "DeckRecycled");
            var sourceOrder = recycle.Before.Zones.Single(zone => zone.Kind == CardZoneKinds.Discard).Cards.Select(card => card.CardId);
            CollectionAssert.AreEqual(sourceOrder, recycle.CardIds);
            Assert.IsTrue(recycle.After.Zones.Single(zone => zone.Kind == CardZoneKinds.Deck).Cards.All(card => card.CardId == null));
        }

        [Test] public void CandidateRecognizesPatternIndependentlyOfCostAndDoesNotCommitPreview()
        {
            var f = new Fixture(energy: 0); f.Start(); var state = f.Session.State;
            var selected = state.Zone(CardZoneKinds.Hand, Player).Cards.Take(1).ToArray();
            var result = BattleInteractionQueries.Query(state, f.Engine, Player, selected, true, 7);
            Assert.AreEqual(CandidateState.Valid, result.State); Assert.IsNotEmpty(result.PatternKey);
            Assert.IsFalse(result.CanPlay); Assert.IsFalse(result.CanDiscard); Assert.IsNotEmpty(result.Reason);
            Assert.AreEqual(7, result.SelectionVersion); Assert.AreSame(state, f.Session.State);
            Assert.AreEqual(state.Revision, result.RuleRevision);
        }

        [Test] public void CandidateRejectsDuplicateForeignAndUnavailableCardsInsteadOfRepairingInput()
        {
            var f = new Fixture(secondPlayer: true); f.Start(); var state = f.Session.State;
            var selected = state.Zone(CardZoneKinds.Hand, Player).Cards.First();
            var foreign = state.Zone(CardZoneKinds.Hand, Other).Cards.First();
            foreach (var ids in new[] { new[] { selected, selected }, new[] { foreign }, new[] { new CardInstanceId("absent") } })
            {
                var result = BattleInteractionQueries.Query(state, f.Engine, Player, ids, true);
                Assert.AreEqual(CandidateState.Invalid, result.State); Assert.IsFalse(result.CanPlay); Assert.IsFalse(result.CanDiscard);
            }
            var closed = BattleInteractionQueries.Query(state, f.Engine, Player, new[] { selected }, false);
            Assert.AreEqual(CandidateState.Closed, closed.State); Assert.IsFalse(closed.CanPlay);
            var empty = BattleInteractionQueries.Query(state, f.Engine, Player, null, true);
            Assert.AreEqual(CandidateState.Empty, empty.State); Assert.IsFalse(empty.CanPlay);
        }

        [Test] public void RejectDoesNotInvokeFrontendOrAcceptedHandoff()
        {
            var f = new Fixture(); f.Start(); var previous = f.Frontend.Batch; int handoffs = 0;
            f.Bridge.BeforePresent = _ => handoffs++;
            var rejected = f.Session.Execute(-1, Player, CardDuelAction.EndTurn);
            Assert.Throws<ArgumentException>(() => f.Bridge.Present(rejected, true));
            Assert.AreSame(previous, f.Frontend.Batch); Assert.AreEqual(0, handoffs);
        }
    }
}
