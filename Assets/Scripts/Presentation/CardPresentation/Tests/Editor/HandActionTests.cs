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
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace CardPresentation.Tests
{
    [TestFixture]
    public sealed class HandActionTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene;
            public readonly GameObject Root;
            public readonly HandZoneView View;
            public readonly CardZonePlayHost Host;
            public readonly CardZonePlayHost Played;
            public readonly CardZoneTransferPresenter Route;
            public readonly HandActionPanelView Panel;
            public readonly HandActionPresenter Presenter;
            public Fixture()
            {
                Scene = EditorSceneManager.NewPreviewScene();
                Root = new GameObject("Hand actions test"); SceneManager.MoveGameObjectToScene(Root, Scene);
                View = Root.AddComponent<HandZoneView>();
                View.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var motion = View.MotionSettings; motion.Enabled = false; View.SetMotionSettings(motion);
                Host = Root.AddComponent<CardZonePlayHost>();
                Host.Capture(new CardZone(new ZoneId("actions/hand"), CardZoneKinds.Hand, null, new[] { Card("queen", "hearts_q", Suit.Hearts, Rank.Queen).Card.Id }),
                    new[] { Card("queen", "hearts_q", Suit.Hearts, Rank.Queen) }, deck: new[] {
                        Card("king", "clubs_k", Suit.Clubs, Rank.King), Card("ace", "spades_a", Suit.Spades, Rank.Ace, faceUp: false) });
                Host.BeginSession();
                var playedRoot = new GameObject("Played actions test"); SceneManager.MoveGameObjectToScene(playedRoot, Scene);
                playedRoot.transform.position = new Vector3(0, 0, 0.28f);
                var playedView = playedRoot.AddComponent<PlayedZoneView>(); playedView.Configure(View.CardPrefab, CardZoneLayoutSettings.Default);
                Played = playedRoot.AddComponent<CardZonePlayHost>();
                Played.Capture(new CardZone(new ZoneId("actions/played"), CardZoneKinds.Played, null, new CardInstanceId[0]), Array.Empty<CardZoneCardState>());
                Played.BeginSession(); Route = Root.AddComponent<CardZoneTransferPresenter>();
                Route.Configure(View, playedView, CardZoneTransferSettings.Default);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(HandActionPanelSetup.PrefabPath), Scene);
                Panel = ui.GetComponent<HandActionPanelView>();
                // Edit-mode fixtures explicitly configure event wiring; Play invokes the component's OnEnable.
                Panel.Configure(Panel.PlayButton, Panel.DiscardButton, Panel.RankSortButton, Panel.SuitSortButton,
                    Panel.PlayButton.GetComponentInChildren<TMP_Text>(), Panel.DiscardButton.GetComponentInChildren<TMP_Text>(),
                    Panel.RankSortButton.GetComponentInChildren<TMP_Text>(), Panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                Presenter = ui.AddComponent<HandActionPresenter>(); Presenter.Configure(Host, Panel, Route);
            }
            public void Dispose() { Host.StopSession(); View.Clear(); EditorSceneManager.ClosePreviewScene(Scene); }
        }
        private static CardZoneCardState Card(string id, string art, Suit suit, Rank rank, bool faceUp = true, bool disabled = false,
            CardEffects effects = CardEffects.None) => new CardZoneCardState(new CardInstance(new CardInstanceId(id), StandardDeck.CardId(suit, rank),
                originId: "test", effects: effects, isDisabled: disabled), art, faceUp, Color.cyan, default);

        [Test] public void FourButtonsUseCardDataAndRespectTheCurrentSortAfterLanding()
        {
            using (var f = new Fixture())
            {
                int arrivals = 0; f.View.CardEnteredZone += _ => arrivals++;
                var retained = f.View.Views[0];
                Assert.IsFalse(f.Panel.PlayButton.interactable); Assert.IsFalse(f.Panel.DiscardButton.interactable);
                f.Host.DrawCards(2); // Gameplay triggers draws; the player panel has no draw command.
                CollectionAssert.AreEqual(new[] { "queen", "ace", "king" }, f.Host.Cards.Select(card => card.Id));
                Assert.IsEmpty(f.Host.DeckCards); Assert.IsTrue(f.Host.HasPendingSort); Assert.IsFalse(f.Host.Cards[1].FaceUp);
                f.Panel.SuitSortButton.onClick.Invoke(); Assert.AreEqual(CardZoneSortOrder.SuitFirst, f.Host.SortOrder);
                f.Panel.RankSortButton.onClick.Invoke(); Assert.AreEqual(CardZoneSortOrder.RankFirst, f.Host.SortOrder);
                Assert.AreEqual("queen", f.Host.Cards[0].Id); Assert.IsFalse(f.Host.TryCompletePendingSort());
                f.View.AdvanceMotion(2); Assert.IsTrue(f.Host.TryCompletePendingSort()); f.Presenter.Refresh();
                CollectionAssert.AreEqual(new[] { "ace", "king", "queen" }, f.Host.Cards.Select(card => card.Id));
                Assert.AreSame(retained, f.View.Views.Single(card => card.ViewId == "queen")); Assert.AreEqual(2, arrivals);
                f.Host.SetInteraction("queen", new CardInteractionState(isSelected: true)); f.Host.Publish(); f.Presenter.Refresh();
                Assert.IsTrue(f.Panel.PlayButton.interactable); f.Panel.PlayButton.onClick.Invoke();
                CollectionAssert.AreEqual(new[] { "ace", "king" }, f.Host.Cards.Select(card => card.Id));
                Assert.AreEqual("queen", f.Played.Cards.Single().Id); Assert.AreSame(retained, f.Route.Destination.Views.Single());
                Assert.IsTrue(f.Route.IsTransferring); Assert.IsFalse(f.Played.Cards.Single().Interaction.IsSelected);
                f.Panel.PlayButton.onClick.Invoke(); Assert.AreEqual(1, f.Played.Cards.Count);
                f.Host.SetInteraction("king", new CardInteractionState(isSelected: true)); f.Host.Publish(); f.Presenter.Refresh();
                Assert.IsTrue(f.Panel.DiscardButton.interactable); f.Panel.DiscardButton.onClick.Invoke();
                CollectionAssert.AreEqual(new[] { "ace" }, f.Host.Cards.Select(card => card.Id));
                CollectionAssert.AreEqual(new[] { "king" }, f.Host.DiscardedCards.Select(card => card.Id));
                Assert.AreEqual(1, f.View.DepartingViews.Count); Assert.IsFalse(f.Panel.DiscardButton.interactable);
                f.Route.CompleteTransfers(); Assert.IsEmpty(f.Host.DeckCards);
                f.View.AdvanceMotion(3); f.Host.TryCompletePendingSort(); Assert.AreEqual(2, arrivals);
                Assert.IsEmpty(f.View.DepartingViews); Assert.IsFalse(f.Host.Cards.Single(card => card.Id == "ace").FaceUp);
                Assert.AreEqual("test", f.Played.Cards.Single().Card.OriginId);
            }
        }

        [Test] public void SortPresentationSurvivesRepeatedPresenterRefreshesAndSelectionChanges()
        {
            using (var f = new Fixture())
            {
                var rank = f.Panel.RankSortButton.GetComponentInChildren<TMP_Text>();
                var suit = f.Panel.SuitSortButton.GetComponentInChildren<TMP_Text>();
                Assert.AreEqual("点数理牌", rank.text); Assert.AreEqual("花色理牌", suit.text);
                Assert.AreEqual((Color)new Color32(255, 208, 105, 255), rank.color);
                Assert.AreEqual(Color.white, suit.color);
                f.Panel.SetSortPresentation("点数", "花色", Color.gray, Color.yellow);
                foreach (bool suitFirst in new[] { false, true, false })
                {
                    if (suitFirst) f.Presenter.SortBySuit(); else f.Presenter.SortByRank();
                    for (int frame = 0; frame < 30; frame++)
                    {
                        f.Presenter.Refresh();
                        Assert.AreEqual("点数", rank.text); Assert.AreEqual("花色", suit.text);
                        Assert.AreEqual(suitFirst ? Color.gray : Color.yellow, rank.color);
                        Assert.AreEqual(suitFirst ? Color.yellow : Color.gray, suit.color);
                    }
                }
            }
        }

        private sealed class CountingCommands : ICardMatchCommands
        {
            public bool CanInteract { get; set; } = true;
            public bool Allowed = true;
            public int PlayChecks, DiscardChecks, Plays, Discards;
            public CardSelectionRequest PlayRequest, DiscardRequest;
            public bool CanPlay(CardSelectionRequest request) { PlayChecks++; PlayRequest = request; return Allowed; }
            public bool CanDiscard(CardSelectionRequest request) { DiscardChecks++; DiscardRequest = request; return Allowed; }
            public int Play(CardSelectionRequest request, bool animate) { Plays++; return request.Cards.Count; }
            public int Discard(CardSelectionRequest request, bool animate) { Discards++; return request.Cards.Count; }
        }

        [Test] public void EmptySelectionSkipsRuleChecksAndButtonsShareOneCapturedRequest()
        {
            using (var f = new Fixture())
            {
                var commands = new CountingCommands();
                var match = f.Host.Match;
                var state = match.State;
                var cards = state.Zones.SelectMany(zone => match.ReadCards(zone.Id)).ToArray();
                match.StartExternalMatch(commands, state, cards);
                for (int frame = 0; frame < 30; frame++) f.Presenter.Refresh();
                Assert.IsFalse(f.Presenter.CanPlay); Assert.IsFalse(f.Presenter.CanDiscard);
                Assert.AreEqual(0, commands.PlayChecks); Assert.AreEqual(0, commands.DiscardChecks);
                Assert.IsFalse(f.Panel.PlayButton.interactable); Assert.IsFalse(f.Panel.DiscardButton.interactable);

                f.Host.SetInteraction("queen", new CardInteractionState(isSelected: true));
                f.Host.Publish(); f.View.CompleteTransitions(); f.Presenter.Refresh();
                Assert.AreEqual(1, commands.PlayChecks); Assert.AreEqual(1, commands.DiscardChecks);
                Assert.AreSame(commands.PlayRequest, commands.DiscardRequest,
                    "Both availability checks must consume the same immutable input captured by Refresh.");
                Assert.IsTrue(f.Panel.PlayButton.interactable); Assert.IsTrue(f.Panel.DiscardButton.interactable);

                // Submission checks current authority again, even if the last rendered buttons were enabled.
                commands.Allowed = false; f.Presenter.Play(); f.Presenter.Discard();
                Assert.Greater(commands.PlayChecks, 1); Assert.Greater(commands.DiscardChecks, 1);
                Assert.AreEqual(0, commands.Plays); Assert.AreEqual(0, commands.Discards);
                int checks = commands.PlayChecks + commands.DiscardChecks;
                commands.CanInteract = false; f.Presenter.Refresh();
                Assert.AreEqual(checks, commands.PlayChecks + commands.DiscardChecks);
                Assert.IsFalse(f.Panel.PlayButton.interactable); Assert.IsFalse(f.Panel.DiscardButton.interactable);
            }
        }

        [Test] public void RuntimeDeckIsSerializedAndRestartedWithoutRefillOrMutatingStartupData()
        {
            using (var f = new Fixture())
            {
                string snapshot = JsonUtility.ToJson(f.Host);
                var initial = f.Host.InitialDeckCards.Select(card => card.Id).ToArray();
                f.Host.DrawCards(2, animate: false);
                CollectionAssert.AreEqual(new[] { "queen", "ace", "king" }, f.Host.Cards.Select(card => card.Id));
                Assert.IsEmpty(f.Host.DeckCards); Assert.IsFalse(f.Host.CanDraw());
                Assert.AreEqual(snapshot, JsonUtility.ToJson(f.Host)); CollectionAssert.AreEqual(initial, f.Host.InitialDeckCards.Select(card => card.Id));
                f.Host.StopSession(); JsonUtility.FromJsonOverwrite(snapshot, f.Host); f.Host.Match.RestartMatch(); f.Host.BeginSession();
                CollectionAssert.AreEqual(initial, f.Host.DeckCards.Select(card => card.Id)); Assert.AreEqual(1, f.Host.Cards.Count);
                Assert.IsFalse(f.Host.HasPendingSort);
                var empty = new CardZoneDrawExperiment(); empty.LoadDeck(Array.Empty<CardZoneCardState>());
                empty.EnsureDeck(new[] { "hearts_q" }); Assert.IsEmpty(empty.DeckCards);
            }
        }

        [Test] public void BadArtOverlapAndInvalidCountsRejectWithoutConsumingTheDeck()
        {
            using (var f = new Fixture())
            {
                var hand = f.Host.Cards.ToArray(); var deck = f.Host.DeckCards.ToArray();
                Assert.Throws<ArgumentOutOfRangeException>(() => f.Host.DrawCards(0));
                Assert.Throws<ArgumentOutOfRangeException>(() => f.Host.DrawCards(3));
                Assert.Throws<ArgumentException>(() => f.Host.ReplaceCardsAndDeck(hand, deck.Concat(hand)));
                CollectionAssert.AreEqual(deck.Select(card => card.Id), f.Host.DeckCards.Select(card => card.Id));
                var bad = Card("bad", "missing-art", Suit.Clubs, Rank.Two);
                f.Host.ReplaceCardsAndDeck(hand, new[] { bad });
                Assert.Throws<ArgumentException>(() => f.Host.DrawCards());
                CollectionAssert.AreEqual(hand.Select(card => card.Id), f.Host.Cards.Select(card => card.Id));
                Assert.AreEqual("bad", f.Host.DeckCards.Single().Id); Assert.AreEqual(1, f.View.Views.Count); Assert.IsFalse(f.Host.HasPendingSort);
                Assert.Throws<ArgumentException>(() => f.Host.Capture(f.Host.Zone, hand, deck: hand));
                Assert.IsTrue(f.Host.IsRunning); Assert.AreEqual("bad", f.Host.DeckCards.Single().Id);
            }
        }

        [Test] public void DisabledSelectionBlocksDiscardAndIneffectiveCardsCanStillBeDiscarded()
        {
            using (var f = new Fixture())
            {
                var blocked = Card("blocked", "clubs_02", Suit.Clubs, Rank.Two, disabled: true);
                blocked.SetInteraction(new CardInteractionState(isSelected: true));
                f.Host.ReplaceCards(f.Host.Cards.Concat(new[] { blocked })); f.Presenter.Refresh();
                Assert.IsFalse(f.Panel.PlayButton.interactable); f.Panel.PlayButton.onClick.Invoke(); Assert.IsEmpty(f.Played.Cards);
                Assert.IsFalse(f.Panel.DiscardButton.interactable); f.Panel.DiscardButton.onClick.Invoke(); Assert.IsEmpty(f.Host.DiscardedCards);
                f.Host.ReplaceCards(new[] { Card("ineffective", "clubs_02", Suit.Clubs, Rank.Two, effects: CardEffects.Ineffective) });
                f.Host.SetInteraction("ineffective", new CardInteractionState(isSelected: true)); f.Host.Publish(); f.Presenter.Refresh();
                Assert.IsTrue(f.Panel.DiscardButton.interactable); f.Panel.DiscardButton.onClick.Invoke();
                Assert.IsEmpty(f.Host.Cards); Assert.AreEqual(CardEffects.Ineffective, f.Host.DiscardedCards.Single().Card.Effects);
                Assert.IsFalse(f.Host.DiscardedCards.Single().Interaction.IsSelected);
            }
        }

        [Test] public void RebindingDisableAndMissingRouteDoNotDuplicateCommands()
        {
            using (var f = new Fixture())
            {
                f.Host.SetInteraction("queen", new CardInteractionState(isSelected: true)); f.Host.Publish();
                f.Presenter.Configure(f.Host, f.Panel); Assert.IsFalse(f.Panel.PlayButton.interactable);
                f.Panel.PlayButton.onClick.Invoke(); Assert.AreEqual(1, f.Host.Cards.Count);
                f.Presenter.Configure(f.Host, f.Panel, f.Route); f.Presenter.Configure(f.Host, f.Panel, f.Route);
                f.Presenter.enabled = false; f.Panel.PlayButton.onClick.Invoke(); Assert.AreEqual(1, f.Host.Cards.Count);
                f.Presenter.enabled = true; f.Panel.PlayButton.onClick.Invoke(); Assert.IsEmpty(f.Host.Cards);
                Assert.AreEqual(1, f.Played.Cards.Count); Assert.AreEqual(2, f.Host.DeckCards.Count);
                f.Host.enabled = false; f.Presenter.Refresh(); Assert.IsFalse(f.Panel.PlayButton.interactable);
                Assert.IsFalse(f.Panel.SuitSortButton.interactable);
            }
        }

        [Test] public void PanelPrefabOnlyInterceptsItsFourButtonsAndSetupReusesTheSceneBinding()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandActionPanelSetup.PrefabPath);
            Assert.IsNotNull(prefab); Assert.IsTrue(prefab.GetComponent<HandActionPanelView>().IsConfigured);
            Assert.AreEqual((int)RenderMode.ScreenSpaceCamera, new SerializedObject(prefab.GetComponent<Canvas>()).FindProperty("m_RenderMode").intValue);
            Assert.IsNotNull(prefab.GetComponent<GraphicRaycaster>());
            Assert.AreEqual(4, prefab.GetComponentsInChildren<Graphic>().Count(graphic => graphic.raycastTarget));
            Assert.AreEqual(4, prefab.GetComponentsInChildren<Button>().Length);
            Assert.IsNull(prefab.transform.Find("Actions/Status")); Assert.IsNull(prefab.transform.Find("Actions/Draw"));
            CollectionAssert.AreEquivalent(new[] { "出牌", "弃牌", "点数理牌", "花色理牌" }, prefab.GetComponentsInChildren<TMP_Text>().Select(label => label.text));
            using (var f = new Fixture())
            {
                Assert.AreSame(f.Presenter, HandActionPanelSetup.Register(f.Host));
                Assert.AreSame(f.Presenter, HandActionPanelSetup.Register(f.Host));
                Assert.AreEqual(1, f.Scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HandActionPresenter>()).Count());
            }
        }

        [Test] public void PlayCapacityAndIneffectiveCardsUseTheExistingTransferRules()
        {
            using (var f = new Fixture())
            {
                var cards = Enumerable.Range(0, 6).Select(i => Card("play-" + i, "clubs_02", Suit.Clubs, Rank.Two,
                    effects: CardEffects.Ineffective)).ToArray();
                foreach (var card in cards) card.SetInteraction(new CardInteractionState(isSelected: true));
                f.Host.ReplaceCards(cards, animate: false); f.Presenter.Refresh(); Assert.IsFalse(f.Presenter.CanPlay);
                f.Panel.PlayButton.onClick.Invoke(); Assert.AreEqual(6, f.Host.Cards.Count); Assert.IsEmpty(f.Played.Cards);
                f.Host.SetInteraction(cards[5].Id, default); f.Host.Publish(); f.Presenter.Refresh();
                Assert.IsTrue(f.Presenter.CanPlay); f.Panel.PlayButton.onClick.Invoke();
                Assert.AreEqual(5, f.Played.Cards.Count); Assert.AreEqual(1, f.Host.Cards.Count);
                Assert.IsTrue(f.Played.Cards.All(card => card.Card.Effects == CardEffects.Ineffective && !card.Interaction.IsSelected));
                f.Route.CompleteTransfers(); f.Host.SetInteraction(cards[5].Id, new CardInteractionState(isSelected: true));
                f.Host.Publish(); f.Presenter.Refresh(); Assert.IsFalse(f.Presenter.CanPlay);
                f.Panel.PlayButton.onClick.Invoke(); Assert.AreEqual(1, f.Host.Cards.Count); Assert.AreEqual(5, f.Played.Cards.Count);
                CollectionAssert.AreEqual(new[] { "king", "ace" }, f.Host.DeckCards.Select(card => card.Id));
            }
        }
    }
}
