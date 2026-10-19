using System;
using System.Collections.Generic;
using System.Linq;
using CardArt;
using global::BattlePresentation;
using NUnit.Framework;
using Poker.Battle2DValidation.Editor;
using Poker.BattleHosting;
using global::BattlePresentation.Unity;
using PokerEngine;
using RetroRendering;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Poker.Battle2DValidation.Tests
{
    [TestFixture]
    public sealed class Battle2DValidationTests
    {
        private static readonly ParticipantId Player = new ParticipantId("player");
        private static readonly ParticipantId Enemy = new ParticipantId("enemy");

        [OneTimeSetUp] public void EnsureValidationAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Battle2DValidationSetup.CardPrefabPath) == null ||
                AssetDatabase.LoadAssetAtPath<Simple2DRendererData>(Battle2DValidationSetup.RendererPath) == null)
                Battle2DValidationSetup.EnsureAssets();
        }

        private static BattleSession Create(int playerHealth = 40, int enemyHealth = 10000, int energy = 3, int deckSize = 52)
        {
            var engine = new CardDuelEngine(new BattleRules("2d.test", 20));
            var rules = new BattlePlayerRules(maxEnergy: energy, initialEnergy: energy, unlimitedDiscards: true,
                damagePolicy: new ScoreDamagePolicy(5),
                dealPolicy: new CardDuelDealPolicy(CardDuelRefillTiming.AfterAction, CardDuelRefillTiming.AfterAction));
            var deck = StandardDeck.Cards().Take(deckSize).Select((card, index) => new DeckEntry("card/" + index, card.Id)).ToArray();
            var session = new BattleSession(engine, engine.Create("2d-seeded", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(Player, new CombatantDefinition("hero", playerHealth), deck, rules: rules) },
                new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("monster", enemyHealth), 7) }));
            Assert.IsTrue(session.Execute(session.State.Revision, Player, CardDuelAction.Start).Accepted);
            return session;
        }

        [Test] public void XmlSourceUsesTheSharedEncounterAndMatchesHeadlessCommands()
        {
            using (var f = new Fixture())
            {
                var source = (XmlBattleSessionSource)f.Host.SceneHost.SessionSource;
                Assert.AreEqual(Battle2DTimelineSetup.ContentPath, AssetDatabase.GetAssetPath(source.ContentXml));
                Assert.AreEqual("single-battle-new", source.EncounterId);
                var session = source.Build("xml-2d", HmacDeckShuffler.FromSeed(new byte[32]));
                var baseline = source.Build("xml-2d", HmacDeckShuffler.FromSeed(new byte[32]));
                baseline.Execute(baseline.State.Revision, Player, CardDuelAction.Start);
                f.Host.SceneHost.AttachSession(session); f.Host.Playback.Start(false);
                SameState(baseline.State, session.State);
                Assert.AreEqual(800, session.State.Duel.Combat.Get(Player).MaxHealth);
                Assert.AreEqual(1200, session.State.Duel.Combat.Get(Enemy).MaxHealth);
                foreach (var action in new[] { CardDuelAction.Play, CardDuelAction.Discard, CardDuelAction.EndTurn })
                {
                    var cards = action == CardDuelAction.EndTurn ? Array.Empty<CardInstanceId>() : baseline.State.Zone(CardZoneKinds.Hand, Player).Cards.Take(1).ToArray();
                    foreach (var id in cards) f.View.ToggleSelection(id.Value);
                    Assert.IsTrue(baseline.Execute(baseline.State.Revision, Player, action, cards).Accepted);
                    var result = action == CardDuelAction.Play ? f.Host.PlaySelected() : action == CardDuelAction.Discard ? f.Host.DiscardSelected() : f.Host.EndTurn();
                    Assert.IsTrue(result.Accepted); SameState(baseline.State, session.State);
                    Battle2DDisplayAssertions.Match(session.State, f.View.PresentedState);
                }
                var before = session.State; source.Configure(source.ContentXml, "missing-encounter");
                Assert.Throws<InvalidOperationException>(() => f.Host.SceneHost.RestartBattle());
                Assert.AreSame(before, f.Host.AuthorityState);
            }
        }

        [Test] public void SceneRecipeOwnsSignalsAndReconfigurationPreservesAuthoredChanges()
        {
            using (var f = new Fixture())
            {
                var director = f.View.SceneDirector;
                var play = director.Timeline.Single(g => g.ruleAction == "Play");
                var attack = play.nodes.Single(n => n.id == "attack");
                Assert.AreEqual("ReadyToAttack", attack.after.Single().marker);
                Assert.AreEqual("Impact", play.checkpoint.Single().marker);
                Assert.IsInstanceOf<Battle2DCardTimelineAction>(play.nodes.Single(n => n.id == "cards").action);
                Assert.IsInstanceOf<TransformTimelineAction>(attack.action);
                Assert.IsFalse(typeof(BaseBattleHost).IsAssignableFrom(typeof(Battle2DSceneAdapter)));
                Assert.IsNull(typeof(Battle2DView).GetMethod("BuildPlan", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                Assert.IsFalse(typeof(Battle2DSceneAdapter).Assembly.GetReferencedAssemblies().Any(a => a.Name == "Poker.BattleConfiguration.Runtime"));
                attack.after = new[] { new SceneActionDependency { actionId = "cards", kind = DependencyKind.Completed } };
                int actions = f.View.GetComponentsInChildren<PresentationActionBehaviour>().Length;
                Battle2DTimelineSetup.ConfigureTimeline(f.View, director);
                Assert.AreEqual(DependencyKind.Completed, director.Timeline.Single(g => g.ruleAction == "Play").nodes.Single(n => n.id == "attack").after.Single().kind);
                Assert.AreEqual(actions, f.View.GetComponentsInChildren<PresentationActionBehaviour>().Length);
            }
        }

        [Test] public void CardActionCancellationReleasesOnlyItsOffsetAndUsesTheLatestSlot()
        {
            using (var f = new Fixture())
            {
                var card = f.View.Cards[0]; var owner = new object();
                Assert.IsTrue(card.TryOwnAction(owner)); Assert.IsFalse(card.TryOwnAction(new object()));
                card.SetActionOffset(owner, Vector3.up * .7f);
                var nextSlot = new Vector3(3, 4, 0); card.Place(nextSlot, 1);
                card.ShowSelection(true, false);
                Assert.Less(Vector3.Distance(nextSlot + Vector3.up * .86f, card.transform.localPosition), .00001f);
                card.ReleaseAction(owner);
                Assert.Less(Vector3.Distance(nextSlot + Vector3.up * .16f, card.transform.localPosition), .00001f);
                card.ShowSelection(false, false); Assert.AreEqual(nextSlot, card.transform.localPosition);
            }
        }

        [Test] public void SharedLifecycleSynchronizesAndRestartsWithReadyButtons()
        {
            using (var f = new Fixture())
            {
                var session = f.Host.Session; var committed = session.State;
                f.Host.SceneHost.SynchronizeBattle(); Assert.AreSame(committed, f.Host.AuthorityState);
                f.Host.SceneHost.RestartBattle();
                Assert.AreNotSame(session, f.Host.Session); Assert.AreSame(committed, session.State);
                Assert.AreSame(f.Host.Session, f.Host.SceneHost.Lifecycle.Session);
                Assert.AreSame(f.Host.Playback, f.Host.SceneHost.Lifecycle.Playback);
                Assert.IsTrue(f.Host.Session.State.HasStarted); Assert.IsFalse(f.View.IsPresenting);
                Assert.IsTrue(f.Host.CanEndTurn); Assert.IsTrue(f.View.EndTurnButton.interactable);
            }
        }

        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly Battle2DSceneAdapter Host;
            public readonly Battle2DView View;
            public readonly EventSystem Events;
            public readonly GameObject Prefab;
            private readonly Transform cardRoot;
            private readonly TMP_Text status, details, log;
            private readonly Button play, discard, endTurn, restart;

            public Fixture(BattleSession session = null)
            {
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Battle2DValidationSetup.CardPrefabPath);
                Assert.IsNotNull(Prefab);
                var root = New("2D test battle");
                cardRoot = New("Cards", root.transform).transform;
                var canvas = New("UI", root.transform, typeof(RectTransform), typeof(Canvas));
                status = Text("Status", canvas.transform); details = Text("Details", canvas.transform); log = Text("Log", canvas.transform);
                play = Button("Play", canvas.transform); discard = Button("Discard", canvas.transform);
                endTurn = Button("End turn", canvas.transform); restart = Button("Restart", canvas.transform);
                Events = New("Events", root.transform).AddComponent<EventSystem>();
                View = root.AddComponent<Battle2DView>(); ConfigureView();
                View.ConfigureActors(New("Player", root.transform).transform, New("Enemy", root.transform).transform);
                var lifecycle = Battle2DTimelineSetup.ConfigureBattle(View);
                Host = (Battle2DSceneAdapter)lifecycle.SceneAdapter;
                Host.SceneHost.AttachSession(session ?? Create());
            }

            private GameObject New(string name, Transform parent = null, params Type[] components)
            {
                var value = new GameObject(name, components); SceneManager.MoveGameObjectToScene(value, Scene);
                if (parent != null) value.transform.SetParent(parent, false);
                return value;
            }
            private TMP_Text Text(string name, Transform parent)
            {
                var value = New(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                value.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/FusionPixelFont/TMP/FusionPixel12pxProportional.asset");
                return value;
            }
            private Button Button(string name, Transform parent)
            {
                var value = New(name, parent, typeof(RectTransform), typeof(Image), typeof(Button));
                var button = value.GetComponent<Button>(); button.targetGraphic = value.GetComponent<Image>(); return button;
            }
            public void ConfigureView() => View.Configure(Prefab, cardRoot, status, details, log, play, discard, endTurn, restart);
            public void Click(GameObject target, PointerEventData.InputButton button = PointerEventData.InputButton.Left) =>
                ExecuteEvents.Execute(target, new PointerEventData(Events) { button = button }, ExecuteEvents.pointerClickHandler);
            public void Dispose()
            {
                EditorSceneManager.ClosePreviewScene(Scene);
            }
        }

        private static void SameState(CardDuelState expected, CardDuelState actual)
        {
            Assert.AreEqual(expected.Revision, actual.Revision); Assert.AreEqual(expected.Duel.Revision, actual.Duel.Revision);
            Assert.AreEqual(expected.Duel.Phase, actual.Duel.Phase); Assert.AreEqual(expected.Duel.CompletedRounds, actual.Duel.CompletedRounds);
            Assert.AreEqual(expected.Duel.Player(Player).Energy.Energy, actual.Duel.Player(Player).Energy.Energy);
            Assert.AreEqual(expected.DiscardsFor(Player), actual.DiscardsFor(Player));
            CollectionAssert.AreEqual(expected.Random.StateBytes, actual.Random.StateBytes);
            CollectionAssert.AreEqual(expected.Duel.Actions.Select(action => action.Actor + "/" + action.Kind),
                actual.Duel.Actions.Select(action => action.Actor + "/" + action.Kind));
            CollectionAssert.AreEqual(expected.Duel.Scores.Select(score => score.Total), actual.Duel.Scores.Select(score => score.Total));
            foreach (var unit in expected.Duel.Combat.Combatants)
            {
                Assert.AreEqual(unit.Health, actual.Duel.Combat.Get(unit.Id).Health);
                Assert.AreEqual(unit.Block, actual.Duel.Combat.Get(unit.Id).Block);
            }
            foreach (var zone in expected.Table.Zones) CollectionAssert.AreEqual(zone.Cards, actual.Table.GetZone(zone.Id).Cards);
        }

        [TestCase(false)] [TestCase(true)]
        public void CardPointerAndActionButtonUseTheSharedSessionAndImmediatelyShowItsResult(bool discard)
        {
            using (var f = new Fixture())
            {
                var session = f.Host.Session;
                Assert.AreSame(session, f.Host.Playback.Session); Assert.AreSame(f.Host, f.Host.Playback.Presentation);
                Battle2DDisplayAssertions.Match(session.State, f.View.PresentedState); Assert.IsFalse(((IBattlePresentationFrontend)f.View).IsPresenting);
                CollectionAssert.AreEqual(session.State.Zone(CardZoneKinds.Hand, Player).Cards.Select(id => id.Value), f.View.Cards.Select(card => card.Id));
                Assert.IsFalse(f.View.PlayButton.interactable); Assert.IsFalse(f.View.DiscardButton.interactable);
                // Reconfiguration must remove the old button subscription before binding it again.
                f.ConfigureView(); f.ConfigureView();
                var card = f.View.Cards[0];
                f.Click(card.gameObject, PointerEventData.InputButton.Right); Assert.IsEmpty(f.View.Selected);
                f.Click(card.gameObject); Assert.AreEqual(card.Id, f.View.Selected.Single());
                Assert.IsTrue(card.IsSelected); Assert.IsTrue(f.View.PlayButton.interactable); Assert.IsTrue(f.View.DiscardButton.interactable);
                var expected = new BattleSession(session.Engine, session.State);
                var expectedResult = expected.Execute(expected.State.Revision, Player,
                    discard ? CardDuelAction.Discard : CardDuelAction.Play, new[] { new CardInstanceId(card.Id) });
                Assert.IsTrue(expectedResult.Accepted);
                f.Click((discard ? f.View.DiscardButton : f.View.PlayButton).gameObject);
                SameState(expected.State, session.State);
                Assert.AreSame(session, f.Host.Session); Battle2DDisplayAssertions.Match(session.State, f.View.PresentedState);
                Assert.IsFalse(f.View.IsPresenting); Assert.IsEmpty(f.View.Selected);
                Assert.AreEqual(2, session.State.Duel.Player(Player).Energy.Energy);
                CollectionAssert.AreEqual(session.State.Zone(CardZoneKinds.Hand, Player).Cards.Select(id => id.Value), f.View.Cards.Select(item => item.Id));
                StringAssert.Contains(discard ? "弃牌 1 张" : "出牌 1 张", f.View.LogText);
            }
        }

        [Test] public void RejectedAndInvalidSelectionLeaveTheSessionAndCardInstancesUnchanged()
        {
            using (var f = new Fixture())
            {
                var before = f.Host.Session.State; var cards = f.View.Cards.ToArray();
                f.View.ToggleSelection("foreign"); Assert.IsEmpty(f.View.Selected);
                var play = f.Host.PlaySelected(); var discard = f.Host.DiscardSelected();
                Assert.AreEqual(CardDuelError.InvalidSelection, play.Error); Assert.AreEqual(CardDuelError.InvalidSelection, discard.Error);
                Assert.IsEmpty(play.Steps); Assert.IsEmpty(discard.Steps);
                Assert.AreSame(before, f.Host.Session.State); Battle2DDisplayAssertions.Match(before, f.View.PresentedState);
                CollectionAssert.AreEqual(cards, f.View.Cards); StringAssert.Contains("操作未执行", f.View.LogText);
            }
        }

        [TestCase(true)] [TestCase(false)]
        public void RealButtonsReachWinOrLossGateInputAndRestartWithANewSession(bool win)
        {
            using (var f = new Fixture(Create(playerHealth: win ? 40 : 1, enemyHealth: win ? 1 : 10000)))
            {
                var session = f.Host.Session;
                if (win) { f.Click(f.View.Cards[0].gameObject); f.Click(f.View.PlayButton.gameObject); }
                else f.Click(f.View.EndTurnButton.gameObject);
                Assert.AreEqual(win ? ScoredDuelPhase.Won : ScoredDuelPhase.Lost, session.State.Duel.Phase);
                Battle2DDisplayAssertions.Match(session.State, f.View.PresentedState); Assert.IsFalse(f.View.IsPresenting);
                Assert.AreEqual(win ? 40 : 0, session.State.Duel.Combat.Get(Player).Health);
                Assert.IsFalse(f.View.PlayButton.interactable); Assert.IsFalse(f.View.DiscardButton.interactable);
                Assert.IsFalse(f.View.EndTurnButton.interactable); Assert.IsTrue(f.View.RestartButton.interactable);
                var terminal = session.State;
                f.Click(f.View.Cards[0].gameObject); f.Click(f.View.EndTurnButton.gameObject);
                Assert.IsEmpty(f.View.Selected); Assert.AreSame(terminal, session.State);
                Assert.IsFalse(f.Host.EndTurn().Accepted); Assert.AreSame(terminal, session.State);
                f.Click(f.View.RestartButton.gameObject);
                Assert.AreNotSame(session, f.Host.Session); Assert.AreEqual(ScoredDuelPhase.AwaitingPlayerHand, f.Host.Session.State.Duel.Phase);
                Assert.AreEqual(0, f.Host.Session.State.Duel.CompletedRounds); Assert.AreEqual(8, f.View.Cards.Count);
                Battle2DDisplayAssertions.Match(f.Host.Session.State, f.View.PresentedState); Assert.IsTrue(f.View.EndTurnButton.interactable);
                Assert.IsEmpty(f.View.Selected); Assert.AreEqual("开局 · 发牌完成", f.View.LogText);
            }
        }

        [Test] public void AttachingAnAlreadyStartedSessionAfterDefeatRefreshesTheNewCommandPort()
        {
            using (var f = new Fixture(Create(playerHealth: 1)))
            {
                f.Host.EndTurn();
                var finished = f.Host.Session;
                Assert.AreEqual(ScoredDuelPhase.Lost, finished.State.Duel.Phase);
                Assert.IsFalse(f.View.EndTurnButton.interactable);

                var next = Create(); var before = next.State;
                f.Host.SceneHost.AttachSession(next);
                Assert.AreSame(before, next.State);
                Assert.AreSame(next, f.Host.Playback.Session);
                Battle2DDisplayAssertions.Match(before, f.View.PresentedState);
                Assert.IsTrue(f.View.EndTurnButton.interactable);
                f.Click(f.View.Cards[0].gameObject);
                Assert.IsTrue(f.View.PlayButton.interactable);
                Assert.IsTrue(f.View.DiscardButton.interactable);
                f.Click(f.View.DiscardButton.gameObject);
                Assert.AreNotSame(before, next.State);
                Assert.AreEqual(ScoredDuelPhase.Lost, finished.State.Duel.Phase);
            }
        }

        [Test] public void Seeded2DCommandsMatchHeadlessRulesAcrossRefillsAndEnemyTurns()
        {
            var baseline = Create(playerHealth: 1000, enemyHealth: 1000000, deckSize: 10);
            using (var f = new Fixture(Create(playerHealth: 1000, enemyHealth: 1000000, deckSize: 10)))
            {
                for (int index = 0; index < 15; index++)
                {
                    var action = index % 3 == 0 ? CardDuelAction.Play : index % 3 == 1 ? CardDuelAction.Discard : CardDuelAction.EndTurn;
                    var cards = action == CardDuelAction.EndTurn ? Array.Empty<CardInstanceId>() :
                        baseline.State.Zone(CardZoneKinds.Hand, Player).Cards.Take(1).ToArray();
                    foreach (var id in cards) f.View.ToggleSelection(id.Value);
                    var expected = baseline.Execute(baseline.State.Revision, Player, action, cards);
                    var actual = action == CardDuelAction.Play ? f.Host.PlaySelected() :
                        action == CardDuelAction.Discard ? f.Host.DiscardSelected() : f.Host.EndTurn();
                    Assert.IsTrue(expected.Accepted); Assert.IsTrue(actual.Accepted);
                    CollectionAssert.AreEqual(expected.Steps.Select(step => step.Action), actual.Steps.Select(step => step.Action));
                    SameState(baseline.State, f.Host.Session.State);
                    Battle2DDisplayAssertions.Match(f.Host.Session.State, f.View.PresentedState); Assert.IsFalse(f.View.IsPresenting);
                    CollectionAssert.AreEqual(actual.State.Zone(CardZoneKinds.Hand, Player).Cards.Select(id => id.Value), f.View.Cards.Select(card => card.Id));
                }
                Assert.AreEqual(5, f.Host.Session.State.Duel.CompletedRounds);
            }
        }

        [Test] public void CardArtworkSharesResourcesAndUsesIndependentPropertyBlocksWithoutChanging3DAssets()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab").GetComponentInChildren<CardView>(true);
            var sourceMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            string original = EditorJsonUtility.ToJson(sourceMaterial);
            using (var f = new Fixture())
            {
                var prefab = f.Prefab.GetComponentInChildren<CardView>(true);
                var material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
                string before = EditorJsonUtility.ToJson(material);
                var classicAtlas = AssetDatabase.LoadAssetAtPath<CardAtlasData>("Assets/Art/CardAtlases/Classic.asset");
                Assert.IsNotNull(classicAtlas);
                Assert.AreNotSame(sourceMaterial, material); Assert.AreSame(classicAtlas, prefab.Atlas);
                Assert.AreSame(source.GetComponent<MeshFilter>().sharedMesh, prefab.GetComponent<MeshFilter>().sharedMesh);
                Assert.AreEqual("CardArt/Validation Unlit", material.shader.name);
                foreach (var card in f.View.Cards)
                {
                    Assert.AreSame(material, card.Surface.GetComponent<MeshRenderer>().sharedMaterial);
                    Assert.AreSame(prefab.GetComponent<MeshFilter>().sharedMesh, card.Surface.GetComponent<MeshFilter>().sharedMesh);
                    Assert.AreSame(prefab.Atlas, card.Surface.Atlas);
                }
                var first = f.View.Cards[0]; var second = f.View.Cards[1];
                f.Click(first.gameObject);
                var block = new MaterialPropertyBlock(); first.Surface.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.That(Vector4.Distance(first.Surface.Appearance.Tint, block.GetColor(CardShaderProperties.ColorTint)), Is.LessThan(0.00001f));
                Assert.AreNotEqual(Color.white, block.GetColor(CardShaderProperties.ColorTint));
                Assert.IsTrue(prefab.Atlas.TryGetCard(first.Surface.CardId, out var face));
                Assert.AreEqual(face.uv, block.GetVector(CardShaderProperties.FrontUVRect));
                second.Surface.GetComponent<MeshRenderer>().GetPropertyBlock(block);
                Assert.AreEqual(Color.white, block.GetColor(CardShaderProperties.ColorTint)); Assert.IsFalse(second.IsSelected);
                Assert.AreEqual(before, EditorJsonUtility.ToJson(material));
                Assert.AreEqual(original, EditorJsonUtility.ToJson(sourceMaterial));
            }
            Assert.AreEqual(original, EditorJsonUtility.ToJson(sourceMaterial));
        }

        [Test] public void CandidateSwitchAndClearReuseCardsWithoutChangingAuthority()
        {
            using (var f = new Fixture())
            {
                var authority = f.Host.AuthorityState;
                var first = f.View.Cards[0]; var second = f.View.Cards[1];
                var firstSlot = first.transform.localPosition; var secondSlot = second.transform.localPosition;
                Assert.AreEqual(CandidateState.Empty, f.View.Candidate.State);
                f.Click(first.gameObject);
                Assert.AreEqual(CandidateState.Valid, f.View.Candidate.State);
                long version = f.View.Candidate.SelectionVersion;
                Assert.That(first.transform.localPosition.y, Is.GreaterThan(firstSlot.y));
                f.Click(second.gameObject);
                Assert.Greater(f.View.Candidate.SelectionVersion, version);
                CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, f.View.Candidate.SelectedIds);
                f.Click(first.gameObject); f.Click(second.gameObject);
                Assert.AreEqual(CandidateState.Empty, f.View.Candidate.State);
                Assert.IsEmpty(f.View.Selected); Assert.IsFalse(f.View.PlayButton.interactable);
                Assert.AreSame(authority, f.Host.AuthorityState);
                Assert.AreSame(first, f.View.Cards[0]); Assert.AreSame(second, f.View.Cards[1]);
                Assert.AreEqual(firstSlot, first.transform.localPosition); Assert.AreEqual(secondSlot, second.transform.localPosition);
            }
        }

        [Test] public void DisablingViewClearsSelectionAndReenablingCannotRestoreOldFeedback()
        {
            using (var f = new Fixture())
            {
                var authority = f.Host.AuthorityState;
                f.Click(f.View.Cards[0].gameObject); Assert.IsNotEmpty(f.View.Selected);
                f.View.enabled = false;
                // PreviewScene EditMode does not send lifecycle callbacks to this runtime component.
                typeof(Battle2DView).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(f.View, null);
                Assert.IsEmpty(f.View.Selected); Assert.IsFalse(f.View.IsPresenting);
                f.View.enabled = true;
                typeof(Battle2DView).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(f.View, null);
                Assert.IsEmpty(f.View.Selected); Assert.AreEqual(CandidateState.Empty, f.View.Candidate.State);
                Assert.IsFalse(f.View.PlayButton.interactable); Assert.IsTrue(f.View.EndTurnButton.interactable);
                Assert.AreSame(authority, f.Host.AuthorityState);
            }
        }

        [Test] public void NeutralSnapshotReordersExistingCardsWithoutACommandOrCapturedPositionRestore()
        {
            using (var f = new Fixture())
            {
                var authority = f.Host.AuthorityState;
                var snapshot = f.View.PresentedState;
                var originalCards = f.View.Cards.ToDictionary(card => card.Id);
                var zones = snapshot.Zones.Select(zone => zone.Kind == "hand" && zone.OwnerId == snapshot.ObserverId
                    ? new CardZoneDisplayData(zone.Id, zone.OwnerId, zone.Kind, zone.Cards.Reverse()) : zone);
                var reordered = new BattleDisplaySnapshot(snapshot.BattleId, snapshot.ObserverId, snapshot.RuleRevision,
                    snapshot.Phase, snapshot.Round, snapshot.Result, snapshot.Units, zones, snapshot.Resources,
                    snapshot.Cursor, hasStarted: snapshot.HasStarted, completedRounds: snapshot.CompletedRounds);
                f.View.Synchronize(reordered);
                CollectionAssert.AreEqual(snapshot.Zones.Single(zone => zone.Kind == "hand" && zone.OwnerId == snapshot.ObserverId)
                    .Cards.Reverse().Select(card => card.CardId), f.View.Cards.Select(card => card.Id));
                foreach (var card in f.View.Cards) Assert.AreSame(originalCards[card.Id], card);
                Assert.AreSame(authority, f.Host.AuthorityState); Assert.IsEmpty(f.View.Selected);
            }
        }

        [Test] public void ViewAndCardPublicContractsDoNotExposeRuleObjects()
        {
            var types = new[] { typeof(Battle2DView), typeof(Battle2DCard) };
            foreach (var type in types)
            {
                var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                var methods = type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
                Assert.IsFalse(properties.Any(property => ContainsRules(property.PropertyType)), type.Name + " property leaks a rule type.");
                Assert.IsFalse(methods.Any(method => ContainsRules(method.ReturnType) || method.GetParameters().Any(parameter => ContainsRules(parameter.ParameterType))),
                    type.Name + " method leaks a rule type.");
            }
            bool ContainsRules(Type type) => type.Namespace == "PokerEngine" || type.IsGenericType && type.GetGenericArguments().Any(ContainsRules);
        }

        [Test] public void ValidationCameraSelectsSimple2DRendererWithinTheUnchangedMainPipeline()
        {
            var quality = QualitySettings.renderPipeline; var graphics = GraphicsSettings.defaultRenderPipeline;
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Battle2DValidationSetup.PipelinePath);
            Assert.IsNotNull(pipeline); Assert.AreEqual("Assets/Settings/MainURP.asset", AssetDatabase.GetAssetPath(pipeline));
            Assert.AreSame(pipeline, graphics); Assert.AreSame(pipeline, quality);
            var renderer = AssetDatabase.LoadAssetAtPath<Simple2DRendererData>(Battle2DValidationSetup.RendererPath);
            Assert.IsNotNull(renderer); Assert.AreEqual("Assets/Settings/Battle2DRenderer.asset", AssetDatabase.GetAssetPath(renderer));
            Assert.IsEmpty(renderer.rendererFeatures); Assert.IsFalse(renderer.useNativeRenderPass);
            var serialized = new SerializedObject(pipeline); var renderers = serialized.FindProperty("m_RendererDataList");
            Assert.GreaterOrEqual(renderers.arraySize, 3);
            Assert.AreEqual("Assets/Settings/MainDeferredRenderer.asset", AssetDatabase.GetAssetPath(renderers.GetArrayElementAtIndex(0).objectReferenceValue));
            Assert.AreEqual("Assets/Settings/UIRenderer.asset", AssetDatabase.GetAssetPath(renderers.GetArrayElementAtIndex(1).objectReferenceValue));
            Assert.AreEqual(0, serialized.FindProperty("m_DefaultRendererIndex").intValue);
            var slots = Enumerable.Range(0, renderers.arraySize)
                .Where(index => renderers.GetArrayElementAtIndex(index).objectReferenceValue == renderer).ToArray();
            Assert.AreEqual(1, slots.Length); Assert.Greater(slots[0], 1);
            using (var f = new Fixture())
            {
                var cameraObject = new GameObject("2D renderer test camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, f.Scene);
                var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true;
                var data = camera.GetUniversalAdditionalCameraData(); data.SetRenderer(slots[0]);
                Assert.IsInstanceOf<Simple2DRenderer>(data.scriptableRenderer);
                Assert.AreSame(quality, QualitySettings.renderPipeline); Assert.AreSame(graphics, GraphicsSettings.defaultRenderPipeline);
                Object.DestroyImmediate(cameraObject);
                Assert.AreSame(quality, QualitySettings.renderPipeline); Assert.AreSame(graphics, GraphicsSettings.defaultRenderPipeline);
                Assert.GreaterOrEqual(f.Prefab.GetComponentInChildren<CardView>(true).GetComponent<MeshRenderer>().sharedMaterial.FindPass("ForwardUnlit"), 0);
            }
        }
    }
}

namespace Poker.Battle2DValidation.Tests
{
    [TestFixture]
    public sealed class Simple2DRenderingTests
    {
        [Test]
        public void FixedRendererDrawsUprightOpaqueCardsAndDepthSortedTransparentSpritesToTexture()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Battle2DValidationSetup.PipelinePath);
            var renderer = AssetDatabase.LoadAssetAtPath<Simple2DRendererData>(Battle2DValidationSetup.RendererPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Battle2DValidationSetup.CardPrefabPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Battle2DValidationSetup.ActorSpritePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Battle2DValidationSetup.ActorMaterialPath);
            Assert.IsNotNull(pipeline); Assert.IsNotNull(renderer); Assert.IsNotNull(prefab);
            Assert.IsNotNull(sprite); Assert.IsNotNull(material);
            Assert.AreSame(pipeline, GraphicsSettings.currentRenderPipeline, "Render with the shared MainURP asset without replacing the pipeline.");
            var list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            int rendererIndex = Enumerable.Range(0, list.arraySize)
                .Single(index => list.GetArrayElementAtIndex(index).objectReferenceValue == renderer);

            const int size = 256;
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var previousTarget = RenderTexture.active;
            bool previousAsyncCompilation = ShaderUtil.allowAsyncCompilation;
            Camera camera = null;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                var cameraObject = new GameObject("Simple 2D texture probe");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.nearClipPlane = .1f; camera.farClipPlane = 20;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.03f, .05f, .08f, 1);
                camera.allowHDR = false; camera.allowMSAA = false; camera.useOcclusionCulling = false;
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>(); data.SetRenderer(rendererIndex);
                data.renderPostProcessing = false; data.renderShadows = false;
                data.requiresDepthOption = CameraOverrideOption.Off; data.requiresColorOption = CameraOverrideOption.Off;
                Assert.IsInstanceOf<Simple2DRenderer>(data.scriptableRenderer);
                target.Create();

                var card = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                card.transform.SetPositionAndRotation(new Vector3(-.8f, .75f, 0), Quaternion.Euler(-90, 0, 0));
                card.transform.localScale = Vector3.one * (1.3f / card.GetComponent<BoxCollider>().size.z);
                var surface = card.GetComponentInChildren<CardView>();
                surface.SetCard("spades_a"); surface.PresentArtwork(System.Array.Empty<string>(), Color.white);

                Color[] Render()
                {
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                    RenderTexture.active = target; readback.ReadPixels(new Rect(0, 0, size, size), 0, 0); readback.Apply();
                    return readback.GetPixels();
                }
                int Pixel(float x, float y) => Mathf.RoundToInt((y + 2) * size / 4) * size + Mathf.RoundToInt((x + 2) * size / 4);
                float Difference(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

                var cardOnly = Render(); var background = cardOnly[Pixel(1.5f, -1.5f)];
                var facePixels = new List<int>();
                for (int y = 145; y < 208; y++)
                    for (int x = 55; x < 99; x++)
                        if (Difference(cardOnly[y * size + x], background) > .25f) facePixels.Add(y * size + x);
                Assert.That(facePixels.Count, Is.GreaterThan(400), "The front-facing card must appear in the expected upper-left region.");
                Assert.That(Difference(cardOnly[Pixel(-.8f, -.75f)], background), Is.LessThan(.02f),
                    "The RenderTexture must preserve the card's vertical placement rather than flip it below the camera center.");

                SpriteRenderer Rectangle(string name, Vector3 position, Vector2 scale, Color color)
                {
                    var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene);
                    value.transform.position = position; value.transform.localScale = new Vector3(scale.x, scale.y, 1);
                    var result = value.AddComponent<SpriteRenderer>(); result.sprite = sprite; result.sharedMaterial = material;
                    result.color = color; return result;
                }

                Rectangle("Behind opaque card", new Vector3(0, .75f, .3f), new Vector2(3, 1.8f), Color.red);
                var behind = Render();
                Assert.That(facePixels.Max(index => Difference(cardOnly[index], behind[index])), Is.LessThan(.04f),
                    "The opaque card must write depth so a transparent-queue sprite behind it cannot paint over its face.");
                var visibleRed = behind[Pixel(1, .9f)];
                Assert.That(visibleRed.r, Is.GreaterThan(.8f)); Assert.That(visibleRed.g + visibleRed.b, Is.LessThan(.1f));

                Rectangle("Transparent foreground", new Vector3(0, .9f, -.3f), new Vector2(2.7f, .4f), new Color(0, 0, 1, .65f));
                var front = Render(); var mixed = front[Pixel(1, .9f)];
                Assert.That(mixed.b - mixed.r, Is.GreaterThan(.15f), "The nearer blue sprite must be sorted over the red sprite.");
                Assert.That(mixed.r, Is.GreaterThan(.05f), "Foreground alpha must retain the red sprite beneath it.");
                var coveredCard = facePixels.Where(index => index / size >= 174 && index / size < 195).ToArray();
                Assert.That(coveredCard.Length, Is.GreaterThan(100));
                Assert.That(coveredCard.Average(index => Difference(front[index], behind[index])), Is.GreaterThan(.2f),
                    "The same transparent foreground must also blend over the opaque card when it is in front of its depth.");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = previousAsyncCompilation;
                RenderTexture.active = previousTarget;
                if (camera != null) camera.targetTexture = null;
                EditorSceneManager.ClosePreviewScene(scene);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(readback);
            }
        }
    }
}

namespace Poker.Battle2DValidation.Tests
{
    internal static class Battle2DDisplayAssertions
    {
        public static void Match(CardDuelState expected, BattleDisplaySnapshot actual)
        {
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Revision, actual.RuleRevision);
            Assert.AreEqual(expected.Duel.CompletedRounds, actual.Round);
            var phase = expected.Duel.Phase == ScoredDuelPhase.Won ? "won" :
                expected.Duel.Phase == ScoredDuelPhase.Lost ? "lost" :
                expected.Duel.Phase == ScoredDuelPhase.AwaitingEnemyAction ? "enemy" : "player";
            Assert.AreEqual(phase, actual.Phase);
            foreach (var unit in expected.Duel.Combat.Combatants)
            {
                var displayed = actual.Unit(unit.Id.Value);
                Assert.IsNotNull(displayed);
                Assert.AreEqual(unit.Health, displayed.Health);
                Assert.AreEqual(unit.MaxHealth, displayed.MaxHealth);
                Assert.AreEqual(unit.Block, displayed.Block);
            }
            var hand = actual.Zones.Single(zone => zone.OwnerId == expected.Duel.PlayerId.Value && zone.Kind == "hand");
            CollectionAssert.AreEqual(expected.Zone(CardZoneKinds.Hand, expected.Duel.PlayerId).Cards.Select(id => id.Value),
                hand.Cards.Select(card => card.CardId));
        }
    }
}
