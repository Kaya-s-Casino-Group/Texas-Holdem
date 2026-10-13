using System;
using System.Linq;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.DrawBattle.Tests
{
    [TestFixture]
    public sealed class DrawBattleTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly CardMatchHost Match;
            public readonly DrawBattleHost Battle;
            public readonly CardZonePlayHost Hand, Played;
            public readonly HandZoneView HandView;
            public readonly CardZoneTransferPresenter Route;
            public readonly HandActionPresenter Actions;
            public readonly DrawEngine Engine;
            public Fixture(long target = 100000, int hands = 4, int discards = 3)
            {
                var root = New("Match"); Match = root.AddComponent<CardMatchHost>();
                var defs = StandardDeck.Cards();
                var cards = defs.Select((definition, i) => new DrawCard(new CardInstance(new CardInstanceId("test/card/" + i), definition.Id))).ToArray();
                var seedZones = new[] {
                    new CardZone(new ZoneId("deck"), CardZoneKinds.Deck, null, cards.Select(card => card.Card.Id)),
                    new CardZone(new ZoneId("hand"), CardZoneKinds.Hand, null, Array.Empty<CardInstanceId>()),
                    new CardZone(new ZoneId("played"), CardZoneKinds.Played, null, Array.Empty<CardInstanceId>()),
                    new CardZone(new ZoneId("discard"), CardZoneKinds.Discard, null, Array.Empty<CardInstanceId>()) };
                var styles = cards.ToDictionary(card => card.Card.Id, card => new CardZoneCardState(card.Card, DrawBattleHost.StandardArt(defs.Single(def => def.Id == card.Card.DefinitionId)), true, Color.white, default));
                Match.ConfigureInitial(seedZones.Select(zone => new CardMatchZoneSnapshot(zone, zone.Cards.Select(id => styles[id]))));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
                var handRoot = New("Hand"); HandView = handRoot.AddComponent<HandZoneView>(); HandView.Configure(prefab, CardZoneLayoutSettings.Default);
                Hand = handRoot.AddComponent<CardZonePlayHost>(); Hand.Bind(Match, new ZoneId("hand"), new ZoneId("deck"), new ZoneId("discard"));
                var playedRoot = New("Played"); var playedView = playedRoot.AddComponent<PlayedZoneView>(); playedView.Configure(prefab, CardZoneLayoutSettings.Default);
                Played = playedRoot.AddComponent<CardZonePlayHost>(); Played.Bind(Match, new ZoneId("played"), new ZoneId("deck"), new ZoneId("discard"));
                Route = handRoot.AddComponent<CardZoneTransferPresenter>(); Route.Configure(HandView, playedView, CardZoneTransferSettings.Default);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardPresentation/Prefabs/HandActionPanel.prefab"), Scene);
                var panel = ui.GetComponent<HandActionPanelView>(); panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(), panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                Actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); Actions.Configure(Hand, panel, Route);
                Battle = root.AddComponent<DrawBattleHost>(); Battle.Configure(Match, Actions);
                Engine = new DrawEngine(new ContentCatalog("test.1", defs, Array.Empty<AbilityDefinitionId>(), Array.Empty<StatusDefinitionId>()), new DrawDefinition("test.1", target, 8, hands, discards));
                Restore(Engine.Create("test-draw", new ParticipantId("player"), cards, HmacDeckShuffler.FromSeed(new byte[32])));
            }
            private GameObject New(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, Scene); return go; }
            public void Restore(DrawState state, Func<DrawCommand, DrawBattleTransition> submit = null) => Battle.StartEncounter(Engine, state, card => DrawBattleHost.StandardArt(Engine.Catalog.Get(card.DefinitionId)), submit);
            public CardInstanceId Select(int count = 1)
            {
                foreach (var card in Hand.Cards) Hand.SetInteraction(card.Id, default);
                foreach (var card in Hand.Cards.Take(count)) Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                Hand.Publish(); HandView.CompleteTransitions(); Actions.Refresh(); return Hand.Cards[0].Card.Id;
            }
            public void AssertLocations()
            {
                Battle.State.World.Zones.ToList().ForEach(zone => CollectionAssert.AreEquivalent(zone.Cards, Match.State.GetZone(zone.Id).Cards));
                Assert.AreEqual(Battle.State.World.Cards.Count, Match.State.Zones.Sum(zone => zone.Cards.Count));
            }
            public void Dispose() { Battle.enabled = false; Hand.StopSession(); Played.StopSession(); EditorSceneManager.ClosePreviewScene(Scene); }
        }
        [Test] public void PlayReusesVisualThenScoresDiscardsAndRefillsWithOneAuthority()
        {
            using (var f = new Fixture())
            {
                var selected = f.Select(); var visual = f.HandView.Views.First(card => card.ViewId == selected.Value); bool sawPlayed = false;
                f.Battle.EventPresented += item => {
                    if (item.Kind != DrawEventKind.CardsPlayed) return;
                    sawPlayed = true; Assert.AreSame(visual, f.Route.Destination.Views.Single()); Assert.AreEqual(selected, f.Played.Cards.Single().Card.Id);
                };
                Assert.AreEqual(1, f.Hand.PlaySelected(f.Played, f.Route, false)); Assert.IsTrue(sawPlayed);
                Assert.AreEqual(3, f.Battle.State.HandsRemaining); Assert.Greater(f.Battle.State.Score, 0); Assert.AreEqual(DrawPhase.Ready, f.Battle.State.Phase);
                Assert.AreEqual(2, f.Battle.State.Revision); Assert.AreEqual(8, f.Hand.Cards.Count); Assert.AreEqual(43, f.Hand.DeckCards.Count);
                Assert.AreEqual(selected, f.Hand.DiscardedCards.Single().Card.Id); Assert.IsEmpty(f.Played.Cards); Assert.IsFalse(f.Battle.IsPresenting); f.AssertLocations();
            }
        }
        [Test] public void DefaultBattleUsesAll52FacesRegardlessOfDuplicateDisabledPreviewCardsAndJokers()
        {
            using (var f = new Fixture())
            {
                var preview = Enumerable.Range(0, 8).Select(i => new CardZoneCardState(new CardInstance(new CardInstanceId("preview/" + i),
                    StandardDeck.CardId(Suit.Hearts, Rank.Ace), effects: CardEffects.Enhanced, isDisabled: true), "hearts_a", true, Color.white, default))
                    .Concat(new[] { new CardZoneCardState(new CardInstance(new CardInstanceId("preview/joker"), new CardDefinitionId("preview:joker")),
                        "joker_big", true, Color.white, default) }).ToArray();
                f.Hand.StopSession(); f.Played.StopSession(); f.Match.StopMatch();
                var zones = f.Match.InitialState.Zones.Select(zone => {
                    var cards = zone.Kind == CardZoneKinds.Deck ? preview : Array.Empty<CardZoneCardState>();
                    return new CardMatchZoneSnapshot(new CardZone(zone.Id, zone.Kind, zone.ControllerId, cards.Select(card => card.Card.Id)), cards);
                }).ToArray();
                f.Match.ConfigureInitial(zones); string authored = JsonUtility.ToJson(f.Match);
                f.Battle.StartBattle();
                Assert.AreEqual(52, f.Battle.State.Cards.Count);
                CollectionAssert.AreEquivalent(StandardDeck.Cards().Select(card => card.Id), f.Battle.State.Cards.Select(card => card.Card.DefinitionId));
                Assert.IsTrue(f.Battle.State.Cards.All(card => card.Card.Effects == CardEffects.None && !card.Card.IsDisabled));
                Assert.AreEqual(8, f.Hand.Cards.Count); Assert.AreEqual(44, f.Hand.DeckCards.Count);
                Assert.IsEmpty(f.Played.Cards); Assert.IsEmpty(f.Hand.DiscardedCards);
                Assert.AreEqual(authored, JsonUtility.ToJson(f.Match)); f.AssertLocations();
            }
        }
        [Test] public void DefaultDeckKeepsIdentitiesAcrossMovesAndNewBattlesCreateIndependentDecks()
        {
            using (var f = new Fixture())
            {
                f.Battle.StartBattle(); var first = f.Battle.State; var ids = first.Cards.Select(card => card.Card.Id).ToArray();
                f.Select(2); f.Hand.DiscardSelected(false); f.Select(); f.Hand.PlaySelected(f.Played, f.Route, false);
                CollectionAssert.AreEquivalent(ids, f.Battle.State.World.Zones.SelectMany(zone => zone.Cards));
                Assert.AreEqual(3, f.Hand.DiscardedCards.Count); f.AssertLocations();
                f.Battle.StartBattle();
                Assert.AreNotEqual(first.Id, f.Battle.State.Id);
                Assert.IsFalse(ids.Intersect(f.Battle.State.Cards.Select(card => card.Card.Id)).Any());
                Assert.AreEqual(52, f.Battle.State.Cards.Count); Assert.AreEqual(8, f.Hand.Cards.Count); Assert.AreEqual(44, f.Hand.DeckCards.Count);
                Assert.AreEqual(0, f.Battle.State.Score); Assert.IsEmpty(f.Hand.DiscardedCards); f.AssertLocations();
            }
        }
        [Test] public void DiscardConsumesBudgetAndDrawsReplacementWithoutScoring()
        {
            using (var f = new Fixture(discards: 1))
            {
                var selected = f.Select(2); Assert.IsTrue(f.Actions.CanDiscard); Assert.AreEqual(2, f.Hand.DiscardSelected(false));
                Assert.AreEqual(0, f.Battle.State.DiscardsRemaining); Assert.AreEqual(0, f.Battle.State.Score); Assert.AreEqual(4, f.Battle.State.HandsRemaining);
                Assert.AreEqual(8, f.Hand.Cards.Count); Assert.AreEqual(42, f.Hand.DeckCards.Count); Assert.AreEqual(2, f.Hand.DiscardedCards.Count);
                f.Select(); Assert.IsFalse(f.Actions.CanDiscard); var before = f.Battle.State; Assert.AreEqual(0, f.Hand.DiscardSelected(false)); Assert.AreSame(before, f.Battle.State); f.AssertLocations();
            }
        }
        [Test] public void SixSelectedCardsCannotBypassMaximumOrChangeTheMirror()
        {
            using (var f = new Fixture())
            {
                f.Select(6); var before = f.Battle.State; var mirror = f.Match.State;
                Assert.IsFalse(f.Actions.CanPlay); Assert.IsFalse(f.Actions.CanDiscard); Assert.AreEqual(0, f.Hand.DiscardSelected(false));
                Assert.AreSame(before, f.Battle.State); Assert.AreSame(mirror, f.Match.State);
            }
        }
        [Test] public void WonAndLostDoNotRefillOrAcceptAnotherCommand()
        {
            using (var f = new Fixture(target: 1))
            {
                f.Select(); f.Hand.PlaySelected(f.Played, f.Route, false); Assert.AreEqual(DrawPhase.Won, f.Battle.State.Phase);
                Assert.AreEqual(7, f.Hand.Cards.Count); Assert.AreEqual(44, f.Hand.DeckCards.Count); Assert.IsFalse(f.Battle.CanInteract); f.AssertLocations();
            }
            using (var f = new Fixture(hands: 1))
            {
                f.Select(); f.Hand.PlaySelected(f.Played, f.Route, false); Assert.AreEqual(DrawPhase.Lost, f.Battle.State.Phase);
                Assert.AreEqual(7, f.Hand.Cards.Count); Assert.IsFalse(f.Actions.CanDiscard); f.AssertLocations();
            }
        }
        [Test] public void SortingIsLocalAndSortedSelectionsReachTheEngineInVisualOrder()
        {
            using (var f = new Fixture())
            {
                var snapshot = DrawSnapshotCodec.Serialize(f.Battle.State, f.Engine); f.Hand.RequestSortAfterLanding(CardZoneSortOrder.SuitFirst); Assert.IsTrue(f.Hand.TryCompletePendingSort());
                f.HandView.CompleteTransitions(); Assert.AreEqual(snapshot, DrawSnapshotCodec.Serialize(f.Battle.State, f.Engine));
                f.Select(3); var selected = f.Hand.Cards.Take(3).Select(card => card.Card.Id).ToArray();
                f.Battle.EventPresented += item => { if (item.Kind == DrawEventKind.CardsPlayed) CollectionAssert.AreEqual(selected, item.Cards); };
                f.Hand.PlaySelected(f.Played, f.Route, false); f.AssertLocations();
            }
        }
        [Test] public void PreviewCommandsAndRestartCannotMutateAnExternalMatch()
        {
            using (var f = new Fixture())
            {
                var state = f.Match.State;
                Assert.IsFalse(f.Hand.CanDraw()); Assert.Throws<InvalidOperationException>(() => f.Hand.DrawCards(1, false));
                Assert.Throws<InvalidOperationException>(() => f.Match.RestartMatch());
                Assert.Throws<InvalidOperationException>(() => f.Match.PrepareReorder(f.Hand.Zone.Id, f.Hand.Zone.Cards));
                Assert.Throws<InvalidOperationException>(() => f.Match.PrepareDebugEdit(Array.Empty<CardMatchZoneSnapshot>(), false)); Assert.AreSame(state, f.Match.State);
            }
        }
        [Test] public void RestoringSnapshotPreservesPilesBudgetAndCanonicalRevision()
        {
            using (var f = new Fixture())
            {
                f.Select(2); f.Hand.DiscardSelected(false); var saved = f.Battle.State;
                f.Select(); f.Hand.PlaySelected(f.Played, f.Route, false); f.Restore(DrawSnapshotCodec.Deserialize(DrawSnapshotCodec.Serialize(saved, f.Engine), f.Engine));
                Assert.AreEqual(1, f.Battle.State.Revision); Assert.AreEqual(2, f.Battle.State.DiscardsRemaining); Assert.AreEqual(2, f.Hand.DiscardedCards.Count); Assert.AreEqual(42, f.Hand.DeckCards.Count); f.AssertLocations();
            }
        }
        [Test] public void ExternalRunSubmissionOwnsPlayAndContinueAndRejectionLeavesViewsUntouched()
        {
            using (var f = new Fixture())
            {
                var state = f.Battle.State; int calls = 0;
                f.Restore(state, command => { calls++; var result = f.Engine.Execute(state, command, state.PlayerId); state = result.State; return new DrawBattleTransition(result); });
                f.Select(); f.Hand.PlaySelected(f.Played, f.Route, false); Assert.AreEqual(2, calls); Assert.AreSame(state, f.Battle.State);
                f.Restore(state, command => new DrawBattleTransition(state, null, DrawError.StaleRevision)); f.Select(); var mirror = f.Match.State;
                Assert.AreEqual(0, f.Hand.PlaySelected(f.Played, f.Route, false)); Assert.AreSame(state, f.Battle.State); Assert.AreSame(mirror, f.Match.State); Assert.IsFalse(f.Battle.IsPresenting);
            }
        }
        [Test] public void ExternallyOwnedEncountersUseCurrentDeckAndCannotResetToStandard52()
        {
            using (var f = new Fixture())
            {
                var original = f.Battle.State.Cards.ToArray();
                foreach (int count in new[] { 53, 51 })
                {
                    var deck = Enumerable.Range(0, count).Select(i => new DrawCard(new CardInstance(new CardInstanceId("encounter-" + count + "/card/" + i),
                        original[i % original.Length].Card.DefinitionId, "run/card/" + i), bonusPoints: i == 0 ? 10 : 0)).ToArray();
                    var state = f.Engine.Create("encounter-" + count, f.Battle.State.PlayerId, deck, HmacDeckShuffler.FromSeed(new byte[32]));
                    f.Restore(state, command => {
                        var result = f.Engine.Execute(state, command, state.PlayerId); state = result.State; return new DrawBattleTransition(result);
                    });
                    Assert.AreEqual(count, f.Battle.State.Cards.Count); Assert.AreEqual(count - 8, f.Hand.DeckCards.Count);
                    CollectionAssert.AreEqual(deck.Select(card => card.Card.OriginId), f.Battle.State.Cards.Select(card => card.Card.OriginId));
                    Assert.AreEqual(10, f.Battle.State.Cards[0].BonusPoints);
                    var mirror = f.Match.State;
                    Assert.Throws<InvalidOperationException>(() => f.Battle.StartBattle());
                    Assert.AreSame(state, f.Battle.State); Assert.AreSame(mirror, f.Match.State);
                    f.Select(); f.Hand.DiscardSelected(false); Assert.AreEqual(count, f.Battle.State.Cards.Count); f.AssertLocations();
                }
            }
        }
        [Test] public void InvalidExternalRestoreRetainsAuthorityAndNextEncounterRebindsZoneIds()
        {
            using (var f = new Fixture())
            {
                var before = f.Match.State;
                Assert.Throws<ArgumentException>(() => f.Match.StartExternalMatch(f.Battle, before, Array.Empty<CardZoneCardState>()));
                Assert.AreSame(f.Battle, f.Match.Commands); Assert.AreSame(before, f.Match.State);
                var next = f.Engine.Create("next-encounter", f.Battle.State.PlayerId, f.Battle.State.Cards, HmacDeckShuffler.FromSeed(new byte[32]));
                f.Restore(next); Assert.AreEqual(DrawEngine.Zone(next.Id, CardZoneKinds.Hand), f.Hand.Zone.Id);
                Assert.AreEqual(8, f.Hand.Cards.Count); Assert.IsEmpty(f.Played.Cards); f.AssertLocations();
            }
        }
        [Test] public void DrawBridgeShipsInPlayerWithoutDependingOnEditorOrRunComposition()
        {
            var player = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.Player);
            var runtime = player.Single(assembly => assembly.name == "Poker.DrawBattle.Runtime");
            Assert.IsFalse(player.Any(assembly => assembly.name == "Poker.DrawBattle.Editor" || assembly.name == "Poker.DrawBattle.Tests.Editor"));
            Assert.IsFalse(runtime.assemblyReferences.Any(assembly => assembly.name.Contains("Editor") || assembly.name == "PokerEngine.Composition"));
            Assert.IsTrue(runtime.sourceFiles.Any(file => file.EndsWith("DrawBattleHost.cs")));
        }
    }
}
