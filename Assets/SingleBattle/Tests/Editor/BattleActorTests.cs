using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class BattleActorTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly CardMatchHost Match;
            public readonly SingleBattleHost Host;
            public readonly PlayerBattleActor Player;
            public readonly EnemyBattleActor Enemy;
            public readonly CardZonePlayHost Hand, Played;
            public readonly HandZoneView HandView;
            public readonly HandActionPresenter Actions;
            public Fixture(bool start = true)
            {
                var root = New("Match"); Match = root.AddComponent<CardMatchHost>();
                Match.ConfigureRuntimeManaged();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
                var hand = New("Hand"); HandView = hand.AddComponent<HandZoneView>(); HandView.Configure(prefab, CardZoneLayoutSettings.Default);
                Hand = hand.AddComponent<CardZonePlayHost>(); Hand.ConfigureRuntimeBinding(Match);
                var played = New("Played"); var playedView = played.AddComponent<PlayedZoneView>(); playedView.Configure(prefab, CardZoneLayoutSettings.Default);
                Played = played.AddComponent<CardZonePlayHost>(); Played.ConfigureRuntimeBinding(Match);
                var route = hand.AddComponent<CardZoneTransferPresenter>(); route.Configure(HandView, playedView, CardZoneTransferSettings.Default);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardPresentation/Prefabs/HandActionPanel.prefab"), Scene);
                var panel = ui.GetComponent<HandActionPanelView>(); panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(), panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                Actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); Actions.Configure(Hand, panel, route);
                Host = root.AddComponent<SingleBattleHost>(); Host.Configure(Match, Actions, null, null);
                Player = New("Player").AddComponent<PlayerBattleActor>(); Enemy = New("Enemy").AddComponent<EnemyBattleActor>();
                var visual = New("Enemy Visual").transform; visual.SetParent(Enemy.transform, false);
                visual.localScale = new Vector3(1, 2, 3); Player.Configure(Host); Enemy.Configure(Host, visual);
                var presenter = visual.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(visual); Enemy.ConfigurePresentation(presenter);
                Host.ConfigureActors(Player, Enemy); if (start) Host.StartBattle();
            }
            private GameObject New(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, Scene); return go; }
            public void Select()
            {
                foreach (var card in Hand.Cards) Hand.SetInteraction(card.Id, default);
                Hand.SetInteraction(Hand.Cards[0].Id, new CardInteractionState(isSelected: true));
                Hand.Publish(); HandView.CompleteTransitions(); Actions.Refresh();
            }
            public void Set(string field, int value) => typeof(SingleBattleHost).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Host, value);
            public void Dispose()
            {
                Host.enabled = false; Player.enabled = false; Enemy.enabled = false;
                Hand.StopSession(); Played.StopSession(); EditorSceneManager.ClosePreviewScene(Scene);
            }
        }
        [Test] public void DerivedActorsBindDistinctSlotsAndOnlyPlayerOwnsCardCommands()
        {
            using (var f = new Fixture())
            {
                Assert.That(f.Player, Is.InstanceOf<BattleActor>()); Assert.That(f.Enemy, Is.InstanceOf<BattleActor>());
                Assert.That(f.Match.Commands, Is.SameAs(f.Player)); Assert.That(f.Host, Is.Not.InstanceOf<ICardMatchCommands>());
                Assert.That(f.Player.Unit, Is.SameAs(f.Host.State.Duel.Combat.Get(f.Host.State.Duel.PlayerId)));
                Assert.That(f.Enemy.Unit, Is.SameAs(f.Host.State.Duel.Combat.Get(f.Host.State.Duel.EnemyId)));
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(3)); Assert.That(f.Enemy.ReadIntent().Damage, Is.EqualTo(7));
                Assert.That(f.Enemy.ReadIntent().Source, Is.EqualTo(f.Enemy.ParticipantId)); Assert.That(f.Enemy.CanAct, Is.False);
                Assert.That(typeof(EnemyBattleActor).GetProperty("Turn"), Is.Null);
                Assert.That(f.Enemy.Visual.IsChildOf(f.Enemy.transform), Is.True);
                Assert.Throws<ArgumentException>(() => f.Enemy.Configure(f.Host, f.Enemy.transform));
            }
        }
        [Test] public void RuntimeOnlySceneBindsAnEmptyBattleThenDealsAndRejectsEditorSeeds()
        {
            using (var f = new Fixture(start: false))
            {
                Assert.That(f.Match.IsRuntimeManaged, Is.True); Assert.That(f.Match.InitialState.Cards, Is.Empty);
                Assert.That(f.Match.InitialState.Zones, Is.Empty); Assert.That(f.Hand.HasSnapshot, Is.False); Assert.That(f.Played.HasSnapshot, Is.False);
                Assert.Throws<InvalidOperationException>(() => f.Match.StartMatch());
                Assert.Throws<InvalidOperationException>(() => f.Match.ConfigureInitial(Array.Empty<CardMatchZoneSnapshot>()));
                Assert.Throws<InvalidOperationException>(() => f.Match.PrepareDebugEdit(Array.Empty<CardMatchZoneSnapshot>(), true));
                CardDuelState initial = null;
                f.Match.Restarted += () => initial = f.Host.State;
                f.Host.StartBattle();
                Assert.That(initial, Is.Not.Null); Assert.That(initial.HasStarted, Is.False); Assert.That(initial.Zone(CardZoneKinds.Hand).Cards, Is.Empty);
                Assert.That(initial.Zone(CardZoneKinds.Deck).Cards.Count, Is.EqualTo(52));
                Assert.That(f.Host.State.HasStarted, Is.True); Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));
                Assert.That(f.Played.Cards, Is.Empty); Assert.That(f.Host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3));
                foreach (var zone in f.Host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
                Assert.Throws<InvalidOperationException>(() => f.Match.RestartMatch());
                Assert.Throws<InvalidOperationException>(() => f.Match.PreparePreviewPlay(f.Hand.Zone.Id, f.Played.Zone.Id, f.Hand.Zone.Cards.Take(1), 5));
                f.Host.StartBattle(); Assert.That(f.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(f.Host.State.Zone(CardZoneKinds.Deck).Cards.Count, Is.EqualTo(44));
            }
        }
        [Test] public void PlayerInputUpdatesLatestEnemyStateAndPresentsTargetedFeedback()
        {
            using (var f = new Fixture())
            {
                var oldEnemy = f.Enemy.Unit; int attacks = 0, hurts = 0;
                f.Player.ActionPresented += () => attacks++;
                f.Enemy.DamagePresented += effect => { Assert.That(effect.Effect.Target, Is.EqualTo(f.Enemy.ParticipantId)); hurts++; };
                f.Select(); Assert.That(f.Hand.PlaySelected(f.Played, f.Actions.PlayRoute, false), Is.EqualTo(1));
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(2)); Assert.That(attacks, Is.EqualTo(1)); Assert.That(hurts, Is.EqualTo(1));
                Assert.That(f.Enemy.Unit.Health, Is.LessThan(oldEnemy.Health)); Assert.That(oldEnemy.Health, Is.EqualTo(75));
                Assert.That(f.Enemy.Unit, Is.SameAs(f.Host.State.Duel.Combat.Get(f.Enemy.ParticipantId)));
                Assert.That(f.Player.Unit.Health, Is.EqualTo(40)); Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero);
            }
        }
        [Test] public void EnemyActsThroughItsControllerExactlyOnceWhenPlayerEndsTurn()
        {
            using (var f = new Fixture())
            {
                int attacks = 0, playerHurts = 0; f.Enemy.ActionPresented += () => attacks++; f.Player.DamagePresented += effect => playerHurts++;
                Assert.That(f.Player.EndTurn(false), Is.True);
                Assert.That(attacks, Is.EqualTo(1)); Assert.That(playerHurts, Is.EqualTo(1)); Assert.That(f.Player.Unit.Health, Is.EqualTo(33));
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(3)); Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
                Assert.That(f.Enemy.CanAct, Is.False); Assert.That(f.Enemy.ReadIntent().ExpectedRevision, Is.EqualTo(f.Host.State.Revision));
            }
        }
        [Test] public void WrongSlotAndForeignOwnerBindingsAreRejected()
        {
            using (var f = new Fixture())
            using (var other = new Fixture())
            {
                Assert.Throws<ArgumentException>(() => f.Player.Bind(f.Enemy.ParticipantId));
                Assert.Throws<ArgumentException>(() => f.Enemy.Bind(f.Player.ParticipantId));
                Assert.Throws<ArgumentException>(() => f.Player.Configure(other.Host));
                Assert.That(f.Player.ParticipantId, Is.EqualTo(f.Host.State.Duel.PlayerId)); Assert.That(f.Enemy.IsBound, Is.True);
                f.Player.enabled = false; Assert.That(f.Player.CanInteract, Is.False); Assert.That(f.Player.EndTurn(false), Is.False);
                f.Player.enabled = true; f.Enemy.enabled = false; Assert.That(f.Player.CanInteract, Is.False);
            }
        }
        [Test] public void DeathFeedbackAndRestartResetVisualAndRebindNewEncounterIdentity()
        {
            using (var f = new Fixture())
            {
                f.Set("enemyHealth", 1); f.Host.StartBattle(); var oldId = f.Enemy.ParticipantId; var rest = f.Enemy.Visual.localScale;
                var rootPosition = f.Enemy.transform.position; var rootRotation = f.Enemy.transform.rotation; var rootScale = f.Enemy.transform.localScale;
                int deaths = 0; f.Enemy.DeathPresented += effect => deaths++;
                f.Select(); f.Hand.PlaySelected(f.Played, f.Actions.PlayRoute, false);
                Assert.That(f.Enemy.IsAlive, Is.False); Assert.That(deaths, Is.EqualTo(1)); Assert.That(f.Enemy.Visual.localScale, Is.EqualTo(rest * .75f));
                Assert.That(f.Enemy.transform.position, Is.EqualTo(rootPosition)); Assert.That(f.Enemy.transform.rotation, Is.EqualTo(rootRotation));
                Assert.That(f.Enemy.transform.localScale, Is.EqualTo(rootScale));
                Assert.That(f.Player.CanInteract, Is.False); Assert.That(f.Enemy.ReadIntent(), Is.Null);
                f.Set("enemyHealth", 75); f.Host.StartBattle();
                Assert.That(f.Enemy.ParticipantId, Is.Not.EqualTo(oldId)); Assert.That(f.Enemy.Unit.Health, Is.EqualTo(75));
                Assert.That(f.Enemy.Visual.localScale, Is.EqualTo(rest)); Assert.That(f.Player.Unit.Health, Is.EqualTo(40));
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(3)); Assert.That(f.Match.Commands, Is.SameAs(f.Player));
            }
        }
        private static ManualBattlePresentation Probe(BattleActor actor)
        {
            var go = new GameObject("Manual presentation"); SceneManager.MoveGameObjectToScene(go, actor.gameObject.scene);
            go.transform.SetParent(actor.transform, false);
            var probe = go.AddComponent<ManualBattlePresentation>(); actor.ConfigurePresentation(probe); return probe;
        }
        private static IEnumerator Sequence(Fixture f, CardDuelAction action)
        {
            f.HandView.CompleteTransitions();
            var ids = action == CardDuelAction.Play ? new[] { f.Host.State.Zone(CardZoneKinds.Hand).Cards[0] } : Array.Empty<CardInstanceId>();
            var result = f.Host.Engine.Execute(f.Host.State, f.Host.State.Revision, action, ids); Assert.That(result.Accepted, Is.True);
            typeof(SingleBattleHost).GetProperty("State").SetValue(f.Host, result.State);
            typeof(SingleBattleHost).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(f.Host, true);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var sequence = (IEnumerator)typeof(SingleBattleHost).GetMethod("Present", flags).Invoke(f.Host, new object[] { result });
            return (IEnumerator)typeof(SingleBattleHost).GetMethod("RunPresentation", flags).Invoke(f.Host, new object[] { sequence });
        }
        private static BattlePresentationOperation NextPending(Fixture f, IEnumerator sequence)
        {
            for (int i = 0; i < 100; ++i)
            {
                if (!sequence.MoveNext()) return null;
                if (sequence.Current is BattlePresentationOperation operation && operation.keepWaiting) return operation;
                // The test owns the card-view clock; protocol operations remain manually gated.
                f.HandView.CompleteTransitions(); f.Played.GetComponent<PlayedZoneView>().CompleteTransitions();
            }
            throw new AssertionException("The sequence failed to reach a presentation checkpoint.");
        }
        [Test] public void HostWaitsForEachAttackHitAndPreparationBeforeContinuingTheTurn()
        {
            using (var f = new Fixture())
            {
                f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                var player = Probe(f.Player); var enemy = Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play);
                try
                {
                    var attack = NextPending(f, sequence); Assert.That(player.Cues.Single().Kind, Is.EqualTo(BattlePresentationKind.Attack));
                    Assert.That(enemy.Cues, Is.Empty); Assert.That(f.Host.IsPresenting, Is.True); Assert.That(f.Player.CanInteract, Is.False);
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); attack.Complete();
                    var hit = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Hit));
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); hit.Complete();
                    var prepare = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.PrepareAttack));
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); prepare.Complete();
                    var enemyAttack = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Attack));
                    Assert.That(player.Cues.Count, Is.EqualTo(1)); Assert.That(f.Host.IsPresenting, Is.True); enemyAttack.Complete();
                    var playerHit = NextPending(f, sequence); Assert.That(player.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Hit));
                    Assert.That(f.Host.IsPresenting, Is.True); Assert.That(f.Player.CanInteract, Is.False); playerHit.Complete();
                    Assert.That(NextPending(f, sequence), Is.Null); Assert.That(f.Host.IsPresenting, Is.False);
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1)); Assert.That(f.Player.CanInteract, Is.True);
                    foreach (var zone in f.Host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
                }
                finally { (sequence as IDisposable)?.Dispose(); }
            }
        }
        [Test] public void DeathCompletionGatesTerminalPresentationAndNoRetaliationOccurs()
        {
            using (var f = new Fixture())
            {
                f.Set("enemyHealth", 1); f.Host.StartBattle(); var player = Probe(f.Player); var enemy = Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play);
                try
                {
                    NextPending(f, sequence).Complete(); NextPending(f, sequence).Complete();
                    var death = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Death));
                    Assert.That(f.Host.State.Duel.Phase, Is.EqualTo(ScoredDuelPhase.Won)); Assert.That(f.Host.IsPresenting, Is.True);
                    Assert.That(f.Player.CanInteract, Is.False); Assert.That(f.Player.Unit.Health, Is.EqualTo(40));
                    death.Complete(); Assert.That(NextPending(f, sequence), Is.Null); Assert.That(f.Host.IsPresenting, Is.False);
                    Assert.That(player.Cues.Count, Is.EqualTo(1)); Assert.That(enemy.Cues.Any(c => c.Kind == BattlePresentationKind.Attack), Is.False);
                }
                finally { (sequence as IDisposable)?.Dispose(); }
            }
        }
        [Test] public void CancelledPresentationCannotCompleteANewBattleAndRestartResetsBothImplementations()
        {
            using (var f = new Fixture())
            {
                var player = Probe(f.Player); var enemy = Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play); var pending = NextPending(f, sequence);
                (sequence as IDisposable)?.Dispose(); f.Host.SnapToState();
                Assert.That(pending.Status, Is.EqualTo(BattlePresentationStatus.Cancelled)); Assert.That(pending.Complete(), Is.False);
                Assert.That(f.Host.IsPresenting, Is.False); long revision = f.Host.State.Revision;
                Assert.That(pending.Cancel(), Is.False); Assert.That(f.Host.State.Revision, Is.EqualTo(revision));
                int playerResets = player.Resets, enemyResets = enemy.Resets; f.Host.StartBattle();
                Assert.That(player.Resets, Is.GreaterThan(playerResets)); Assert.That(enemy.Resets, Is.GreaterThan(enemyResets));
                Assert.That(f.Host.State.Revision, Is.EqualTo(1)); Assert.That(f.Player.Unit.Health, Is.EqualTo(40));
            }
        }
        [Test] public void OperationHasExactlyOneTerminalOutcomeAndCarriesFailureReason()
        {
            var operation = new BattlePresentationOperation(); var reason = new InvalidOperationException("Timeline interrupted");
            Assert.That(operation.keepWaiting, Is.True); Assert.That(operation.Fail(reason), Is.True);
            Assert.That(operation.Error, Is.SameAs(reason)); Assert.That(operation.Status, Is.EqualTo(BattlePresentationStatus.Failed));
            Assert.That(operation.keepWaiting, Is.False); Assert.That(operation.Complete(), Is.False); Assert.That(operation.Cancel(), Is.False);
        }
        [Test] public void IndependentCostsAllowOverflowAndGateEachPlayerCommandSeparately()
        {
            using (var f = new Fixture())
            {
                f.Set("maxEnergy", 3); f.Set("initialEnergy", 7); f.Set("playEnergyCost", 3); f.Set("discardEnergyCost", 2); f.Host.StartBattle();
                Assert.That(f.Player.Turn.MaxEnergy, Is.EqualTo(3)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(7)); f.Select();
                Assert.That(f.Player.CanPlay(f.Hand), Is.True); Assert.That(f.Player.CanDiscard(f.Hand), Is.True);
                Assert.That(f.Player.Discard(f.Hand, false), Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(5)); f.Select();
                Assert.That(f.Player.Play(f.Hand, f.Played, f.Actions.PlayRoute, false), Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(2)); f.Select();
                Assert.That(f.Player.CanPlay(f.Hand), Is.False); Assert.That(f.Player.CanDiscard(f.Hand), Is.True);
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero);
                Assert.That(f.Player.Discard(f.Hand, false), Is.EqualTo(1));
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(3));
                Assert.That(f.Player.Unit.Health, Is.EqualTo(33)); Assert.That(f.Host.State.DiscardsRemaining, Is.EqualTo(1));
            }
        }
        [Test] public void HostProjectsImmediatePlayRefillAndDeferredDiscardRefill()
        {
            using (var f = new Fixture())
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(SingleBattleHost).GetField("playRefillTiming", flags).SetValue(f.Host, CardDuelRefillTiming.AfterAction);
                typeof(SingleBattleHost).GetField("discardRefillTiming", flags).SetValue(f.Host, CardDuelRefillTiming.NextPlayerTurn);
                f.Host.StartBattle(); f.Select(); f.Player.Play(f.Hand, f.Played, f.Actions.PlayRoute, false);
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(f.Played.Cards, Is.Empty);
                f.Select(); f.Player.Discard(f.Hand, false); Assert.That(f.Hand.Cards.Count, Is.EqualTo(7));
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); Assert.That(f.Player.Turn.Energy, Is.EqualTo(1));
                f.Player.EndTurn(false); Assert.That(f.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
                foreach (var zone in f.Host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
            }
        }
    }
    public sealed class ManualBattlePresentation : MonoBehaviour, IBattleActorPresentation
    {
        public readonly List<BattlePresentationCue> Cues = new List<BattlePresentationCue>();
        public readonly List<BattlePresentationOperation> Operations = new List<BattlePresentationOperation>();
        public int Resets;
        public BattlePresentationOperation Play(BattlePresentationCue cue)
        { Cues.Add(cue); var operation = new BattlePresentationOperation(); Operations.Add(operation); return operation; }
        public void Snap(BattlePresentationCue cue) { }
        public void Synchronize(CombatantState unit) { }
        public void CancelPresentation() { foreach (var operation in Operations) operation.Cancel(); }
        public void ResetPresentation() { CancelPresentation(); Resets++; }
    }
}
