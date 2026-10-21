using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using global::BattlePresentation;
using CardInteraction;
using CardPresentation;
using NUnit.Framework;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class BattleActorTests
    {
        private sealed class Fixture : IDisposable
        {
            public readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            public readonly CardMatchHost Match;
            public readonly SingleBattleSceneAdapter Host;
            public readonly PlayerBattleActor Player;
            public readonly EnemyBattleActor Enemy;
            public readonly CardZonePlayHost Hand;
            public readonly HandZoneView HandView;
            public readonly HandActionPresenter Actions;
            public readonly BattleTestContent Content;
            public Fixture(bool start = true, int playerCount = 1, int enemyCount = 1)
            {
                var root = New("Match"); Match = root.AddComponent<CardMatchHost>();
                Match.ConfigureRuntimeManaged();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab");
                var hand = New("Hand"); HandView = hand.AddComponent<HandZoneView>(); HandView.Configure(prefab, CardZoneLayoutSettings.Default);
                Hand = hand.AddComponent<CardZonePlayHost>(); Hand.ConfigureRuntimeBinding(Match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab"), Scene);
                var panel = ui.GetComponent<HandActionPanelView>(); panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(), panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                Actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); Actions.Configure(Hand, panel);
                Host = root.AddComponent<SingleBattleSceneAdapter>(); Host.Configure(Match, Actions, null, null);
                Player = New("Player").AddComponent<PlayerBattleActor>(); Enemy = New("Enemy").AddComponent<EnemyBattleActor>();
                var visual = New("Enemy Visual").transform; visual.SetParent(Enemy.transform, false);
                visual.localScale = new Vector3(1, 2, 3); Player.Configure(Host); Enemy.Configure(Host, visual);
                var presenter = visual.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(visual); Enemy.ConfigurePresentation(presenter);
                var players = new List<PlayerBattleActor> { Player }; var enemies = new List<EnemyBattleActor> { Enemy };
                for (int i = 1; i < playerCount; i++) { var actor = New("Player " + i).AddComponent<PlayerBattleActor>(); actor.Configure(Host); players.Add(actor); }
                for (int i = 1; i < enemyCount; i++) { var actor = New("Enemy " + i).AddComponent<EnemyBattleActor>(); actor.Configure(Host); enemies.Add(actor); }
                Host.ConfigureActors(players, enemies);
                Content = BattleTestContent.Create(Host, playerCount, enemyCount);
                Poker.SingleBattle.Editor.BattlePresentationSceneSetup.ConfigureHost(Host, includeFeedback: false);
                if (start) Host.StartBattle();
            }
            private GameObject New(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, Scene); return go; }
            public void Select()
            {
                foreach (var card in Hand.Cards) Hand.SetInteraction(card.Id, default);
                Hand.SetInteraction(Hand.Cards[0].Id, new CardInteractionState(isSelected: true));
                Hand.Publish(); HandView.CompleteTransitions(); Actions.Refresh();
            }
            public void Set(string field, int value) => Content.Set(field, value);
            public void Dispose()
            {
                Host.enabled = false; Player.enabled = false; Enemy.enabled = false;
                Hand.StopSession(); EditorSceneManager.ClosePreviewScene(Scene);
                Content.Dispose();
            }
        }
        private sealed class AdventureCommandProbe : IBattleSessionCommands
        {
            private readonly BattleSession authority;
            public BattleSession Session => new BattleSession(authority.Engine, authority.State);
            public long Revision { get; private set; } = 500;
            public int Calls;
            public AdventureCommandProbe(BattleSession session) { authority = session; }
            public BattleSessionResult Execute(long revision, ParticipantId actor, CardDuelAction action,
                IEnumerable<CardInstanceId> selected = null, IEnumerable<ParticipantId> targets = null,
                BattleTargetBindings targetBindings = null, int drawCount = 0)
            {
                Assert.AreEqual(Revision, revision); Calls++;
                var result = authority.Execute(authority.State.Revision, actor, action, selected, targets, targetBindings, drawCount);
                if (result.Accepted) Revision++; return result;
            }
            public BattleSessionResult UseItem(long revision, ParticipantId actor, string instanceId, string targetInstanceId = null)
            { Assert.AreEqual(Revision, revision); return authority.UseItem(authority.State.Revision, actor, instanceId, targetInstanceId); }
            public BattleSessionResult ApplyAdmin(long revision, BattleAdminPatch patch)
            { Assert.AreEqual(Revision, revision); return authority.ApplyAdmin(authority.State.Revision, patch); }
        }

        [Test] public void ExternalAuthorityConsumesOpeningWithoutRestartAndKeepsAdminDraftValidAcrossReadWrappers()
        {
            using (var f = new Fixture(start: false))
            {
                var session = f.Host.BuildConfiguredSession();
                var opening = session.Execute(session.State.Revision, session.State.Duel.PlayerId, CardDuelAction.Start);
                var commands = new AdventureCommandProbe(session);
                f.Host.AttachCommands(commands, opening, false);
                Assert.AreEqual(0, commands.Calls); Assert.AreSame(opening.State, f.Host.PresentedState);
                Assert.IsTrue(f.Host.UsesExternalCommands);
                var draft = new BattleAdminDraft(f.Host.Session);
                Assert.IsTrue(draft.IsCurrent(f.Host.Session));
                var state = session.State;
                f.Host.StartBattle(); f.Host.RestartBattle(); f.Host.SceneHost.StartBattle(); f.Host.SceneHost.RestartBattle();
                Assert.AreSame(state, f.Host.AuthorityState);
                f.Select(); Assert.AreEqual(1, f.Hand.PlaySelected(false));
                Assert.AreEqual(1, commands.Calls); Assert.IsFalse(draft.IsCurrent(f.Host.Session));
                Assert.AreSame(session.State, f.Host.PresentedState);
            }
        }

        [Test] public void ExternalSummonerBindsSpawnedEnemiesAndReusesRosterWithoutLeakingIntoNextBattle()
        {
            using (var f = new Fixture(start: false))
            {
                var nextBattle = f.Host.BuildConfiguredSession();
                var xml = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath,
                    "../External/BattleToolkit/Engine/Content/Templates/08_ArmoredSummoner.xml"));
                var compiled = BattleContentCompiler.CompileXml(xml);
                Assert.IsTrue(compiled.Success, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
                var session = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest("08_ArmoredSummoner",
                    "scene-summon", HmacDeckShuffler.FromSeed(new byte[32]))).Session;
                var opening = session.Execute(session.State.Revision, session.State.Duel.PlayerId, CardDuelAction.Start);
                f.Host.AttachCommands(new AdventureCommandProbe(session), opening, false);
                f.HandView.CompleteTransitions();
                if (session.State.FlowFor(session.State.Duel.PlayerId).Window == BattlePlayerWindow.Draw)
                    Assert.IsTrue(f.Host.SkipDraw(false));
                Assert.IsTrue(f.Host.EndTurn(false));
                if (session.State.FlowFor(session.State.Duel.PlayerId).Window == BattlePlayerWindow.Ending)
                    Assert.IsTrue(f.Host.EndTurn(false));
                Assert.AreEqual(3, session.State.Duel.Enemies.Count); Assert.AreEqual(3, f.Host.Enemies.Count);
                var spawned = f.Host.Enemies.Skip(1).ToArray();
                foreach (var actor in f.Host.Enemies)
                {
                    Assert.IsTrue(actor.IsBound); Assert.IsNotNull(actor.ReadIntent());
                    Assert.AreEqual(actor.ParticipantId.Value, actor.DisplayUnit.Id);
                }
                f.Host.SynchronizeBattle();
                CollectionAssert.AreEqual(spawned, f.Host.Enemies.Skip(1));
                var nextOpening = nextBattle.Execute(nextBattle.State.Revision, nextBattle.State.Duel.PlayerId, CardDuelAction.Start);
                f.Host.AttachCommands(new AdventureCommandProbe(nextBattle), nextOpening, false);
                Assert.AreEqual(1, f.Host.Enemies.Count); Assert.IsTrue(f.Host.Enemy.IsBound);
                Assert.IsTrue(spawned.All(actor => !actor.gameObject.activeSelf));
                Assert.AreSame(nextBattle.State, f.Host.PresentedState);
            }
        }
        [Test] public void SharedLifecycleRestartsFromCurrentXmlAndSynchronizesWithoutAdvancingRules()
        {
            using (var f = new Fixture())
            {
                var session = f.Host.Session; var committed = session.State;
                f.Host.SynchronizeBattle();
                Assert.AreSame(committed, f.Host.AuthorityState); Assert.AreSame(committed, f.Host.State);
                f.Set("playerHealth", 123); f.Host.RestartBattle();
                Assert.AreNotSame(session, f.Host.Session); Assert.AreSame(committed, session.State);
                Assert.AreSame(f.Host.Session, f.Host.Lifecycle.Session);
                Assert.AreSame(f.Host.Playback, f.Host.Lifecycle.Playback);
                Assert.AreEqual(123, f.Host.Player.Unit.Health); Assert.IsTrue(f.Host.State.HasStarted);
                Assert.AreEqual(f.Host.PlayerZone(CardZoneKinds.Hand).Cards.Count, f.Hand.Cards.Count);
            }
        }

        [Test] public void DiscardCapturesActualRewardBatchAndRoutesGainAndBlockHitCues()
        {
            using (var f = new Fixture())
            {
                // Runtime XML enables the discard reward for this scenario.
                try
                {
                    f.Content.SetReward(8); f.Host.StartBattle(); f.Select();
                    string id = f.Hand.Cards.First(card => card.Interaction.IsSelected).Id;
                    Assert.AreEqual(1, f.Hand.DiscardSelected(false));
                    var batch = f.Player.Shields.Batches.Single(); Assert.AreEqual(8,batch.InitialBlock);
                    Assert.AreEqual(id,batch.Cards.Single().Id); Assert.AreEqual(8,f.Player.Unit.Block);
                    var probe=Probe(f.Player);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    var present=typeof(BattleActor).GetMethod("PresentEffect",flags);
                    var gain=f.Host.LastDiscardResolution.Events.Single(e=>e.Effect.Kind==CombatEffectKind.GainBlock);
                    var operation=(BattlePresentationOperation)present.Invoke(f.Player,new object[]{gain,true,f.Player.Shields});
                    Assert.AreEqual(BattlePresentationKind.GainBlock,probe.Cues.Last().Kind);operation.Complete();
                    var blocked=CombatEngine.Resolve(f.Host.State.Duel.Combat,f.Host.State.Duel.Combat.Revision,
                        new[]{new CombatEffect(CombatEffectKind.Damage,f.Enemy.ParticipantId,f.Player.ParticipantId,3)});
                    operation=(BattlePresentationOperation)present.Invoke(f.Player,new object[]{blocked.Events[0],true,f.Player.Shields});
                    Assert.AreEqual(BattlePresentationKind.BlockHit,probe.Cues.Last().Kind);Assert.AreEqual(0,probe.Cues.Last().Effect.HealthLost);operation.Complete();
                    var partial=CombatEngine.Resolve(blocked.State,blocked.State.Revision,
                        new[]{new CombatEffect(CombatEffectKind.Damage,f.Enemy.ParticipantId,f.Player.ParticipantId,10)});
                    operation=(BattlePresentationOperation)present.Invoke(f.Player,new object[]{partial.Events[0],true,f.Player.Shields});
                    Assert.AreEqual(BattlePresentationKind.BlockHit,probe.Cues.Last().Kind);Assert.Greater(probe.Cues.Last().Effect.HealthLost,0);operation.Complete();
                    f.Host.StartBattle();Assert.AreEqual(0,f.Player.Shields.TotalBlock);
                }
                finally { f.Content.SetReward(null); }
            }
        }

        [Test] public void PlayButtonWorksWithoutPlayedViewOrTransferRoute()
        {
            using (var f = new Fixture())
            {
                Assert.That(f.Actions.PlayRoute, Is.Null); Assert.That(f.Actions.Played, Is.Null);
                Assert.That(f.Scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayedZoneView>(true)), Is.Empty);
                Assert.That(f.Actions.CanPlay, Is.False);
                f.Select(); var selected = f.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Card.Id).ToArray();
                var energy = f.Player.Turn.Energy;
                Assert.That(f.Actions.CanPlay, Is.True);
                f.Actions.Play();
                Assert.That(f.Host.LastPlayResolution, Is.Not.Null); Assert.That(f.Host.LastPlayResolution.Accepted, Is.True);
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(energy - f.Player.Rules.PlayEnergyCost));
                Assert.That(f.Host.PlayerZone(CardZoneKinds.Hand).Cards.Intersect(selected), Is.Empty);
                Assert.That(f.Host.PlayerZone(CardZoneKinds.Played).Cards, Is.Empty);
                Assert.That(f.Host.IsPresenting, Is.False);
                CollectionAssert.AreEquivalent(f.Hand.Cards.Select(card => card.Id), f.HandView.Views.Select(view => view.ViewId));
                f.Host.SnapToState(); Assert.That(f.Host.IsPresenting, Is.False);
            }
        }

        [Test] public void DirectPlayWithoutSelectionLeavesBattleAndHandUnchanged()
        {
            using (var f = new Fixture())
            {
                var state = f.Host.State; var snapshot = f.HandView.Data;
                Assert.That(f.Hand.PlaySelected(false), Is.Zero);
                Assert.That(f.Host.State, Is.SameAs(state)); Assert.That(f.HandView.Data, Is.SameAs(snapshot));
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void DataCommandUsesCapturedCardsAfterTheUiSelectionChanges(bool discard)
        {
            using (var f = new Fixture())
            {
                f.Select();
                var request = f.Hand.CaptureSelection(); var original = request.Cards.Single();
                var replacement = f.Hand.Cards.First(card => card.Card.Id != original).Card.Id;
                f.Hand.SetInteraction(original.Value, default);
                f.Hand.SetInteraction(replacement.Value, new CardInteractionState(isSelected: true));
                f.Hand.Publish(); f.HandView.CompleteTransitions();
                ICardMatchCommands commands = f.Player;
                Assert.AreEqual(1, discard ? commands.Discard(request, false) : commands.Play(request, false));
                Assert.IsFalse(f.Host.PlayerZone(CardZoneKinds.Hand).Cards.Contains(original));
                Assert.IsTrue(f.Host.PlayerZone(CardZoneKinds.Hand).Cards.Contains(replacement));
                Assert.AreEqual(0, discard ? commands.Discard(request, false) : commands.Play(request, false));
            }
        }

        [Test] public void DataCommandsRejectForeignMatchPlayerZoneAndCardsWithoutChangingState()
        {
            using (var f = new Fixture(playerCount: 2))
            {
                f.Select(); var input = f.Hand.CaptureSelection();
                var requests = new[] {
                    new CardSelectionRequest(new MatchId("foreign"), input.ZoneId, input.ControllerId, input.Cards),
                    new CardSelectionRequest(input.MatchId, input.ZoneId, f.Host.Players[1].ParticipantId, input.Cards),
                    new CardSelectionRequest(input.MatchId, f.Hand.DeckZone.Id, input.ControllerId, input.Cards),
                    new CardSelectionRequest(input.MatchId, input.ZoneId, input.ControllerId, new[] { f.Hand.DeckCards[0].Card.Id })
                };
                var state = f.Host.State; var authority = f.Host.AuthorityState; var mirror = f.Match.State; var visual = f.HandView.Data;
                foreach (var request in requests)
                {
                    Assert.IsFalse(f.Player.CanPlay(request)); Assert.IsFalse(f.Player.CanDiscard(request));
                    Assert.AreEqual(0, f.Player.Play(request, false)); Assert.AreEqual(0, f.Player.Discard(request, false));
                    Assert.AreSame(state, f.Host.State); Assert.AreSame(mirror, f.Match.State); Assert.AreSame(visual, f.HandView.Data);
                    Assert.AreSame(authority, f.Host.Session.State);
                }
            }
        }

        [Test] public void OpeningAndPlayerSwitchAutomaticallySortWithoutChangingAuthoritativeCards()
        {
            using (var f = new Fixture(playerCount: 2))
            {
                Assert.AreEqual(CardZoneSortOrder.RankFirst, f.Hand.SortOrder);
                AssertSorted();
                Assert.IsTrue(f.Host.SelectPlayer(f.Host.Players[1]));
                AssertSorted();
                f.Hand.SetSortOrder(CardZoneSortOrder.SuitFirst);
                Assert.IsTrue(f.Host.SelectPlayer(f.Host.Players[0]));
                AssertSorted();

                void AssertSorted()
                {
                    var state = f.Host.State;
                    var hand = f.Host.PlayerZone(CardZoneKinds.Hand);
                    Assert.IsTrue(f.Hand.HasPendingSort);
                    f.HandView.CompleteTransitions();
                    Assert.IsTrue(f.Hand.TryCompletePendingSort());
                    CardDefinition Face(CardInstanceId id) => f.Host.Engine.Face(state.Table.GetCard(id));
                    var expected = f.Hand.SortOrder == CardZoneSortOrder.RankFirst ? hand.SortByRank(Face) : hand.SortBySuit(Face);
                    CollectionAssert.AreEqual(expected.Cards, f.Hand.Zone.Cards);
                    Assert.AreSame(state, f.Host.State);
                    Assert.IsFalse(f.Hand.HasPendingSort);
                    f.HandView.CompleteTransitions();
                }
            }
        }
        [Test] public void XmlScoreDrivesBattleAndHoverWithoutMutatingAnExistingSnapshot()
        {
            using (var f = new Fixture(start: false, playerCount: 2))
            {
                f.Content.SetCardPoints(42); f.Host.StartBattle(); f.Select();
                var card = f.Hand.Cards[0].Card.Id;
                Assert.IsTrue(f.Player.TryGetCardBasePoints(card, out var points)); Assert.AreEqual(42, points);
                var other = f.Host.State.Zone(CardZoneKinds.Hand, f.Host.Players[1].ParticipantId).Cards[0];
                Assert.IsFalse(f.Player.TryGetCardBasePoints(other, out _));
                f.Hand.SetInteraction(card.Value, new CardInteractionState(isHovered: true, isSelected: true));
                f.Hand.Publish(); f.HandView.CompleteTransitions();
                Assert.IsTrue(f.Hand.TryGetHoveredCard(out var hover)); Assert.AreEqual(42, hover.ChipPoints);
                f.Content.SetCardPoints(99);
                Assert.IsTrue(f.Player.TryGetCardBasePoints(card, out points)); Assert.AreEqual(42, points);
                Assert.AreEqual(1, f.Hand.PlaySelected(false));
                Assert.AreEqual(47, f.Host.State.Duel.Scores.Single().Total); Assert.AreEqual(28, f.Enemy.Unit.Health);
                f.Host.StartBattle(); card = f.Hand.Cards[0].Card.Id;
                Assert.IsTrue(f.Player.TryGetCardBasePoints(card, out points)); Assert.AreEqual(99, points);
            }
        }
        [Test] public void SceneXmlScoringPreservesTheDefaultScoreTable()
        {
            var xml = AssetDatabase.LoadAssetAtPath<TextAsset>(Editor.SingleBattleSetup.SceneBattleContentPath);
            var compiled = BattleContentCompiler.CompileXml(xml.text);
            Assert.IsTrue(compiled.Success);
            var built = compiled.Content.ScoringProfiles["single-battle-score"]; var defaults = ScoreProfile.Default;
            foreach (HandCategory category in Enum.GetValues(typeof(HandCategory)))
            {
                var actual = built.Rule(category); var expected = defaults.Rule(category);
                Assert.AreEqual(expected.BasePoints, actual.BasePoints); Assert.AreEqual(expected.BaseMultiplier, actual.BaseMultiplier);
                Assert.AreEqual(expected.PointsPerLevel, actual.PointsPerLevel); Assert.AreEqual(expected.MultiplierPerLevel, actual.MultiplierPerLevel);
            }
            foreach (var card in StandardDeck.Cards())
                Assert.AreEqual(CardScoreRules.GetBasePoints(card), built.CardPoints.GetBasePoints(new CardFace(card.Rank, card.Suit)));
        }
        [Test] public void DamagePreviewMatchesRecordedPlayAndNeverPublishesOrChangesTheLiveBattle()
        {
            using (var f = new Fixture(start: false))
            {
                f.Host.StartBattle(); f.Select();
                int changed = 0, attacks = 0; f.Host.StateChanged += () => changed++; f.Player.ActionPresented += () => attacks++;
                var state = f.Host.State; var table = f.Match.State; var selected = f.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Id).ToArray();
                var preview = f.Host.PreviewSelectedPlay();
                Assert.IsTrue(preview.Accepted); Assert.AreEqual(preview.Score.Total, preview.Damage.Requests.Single().Amount);
                Assert.AreSame(state, f.Host.State); Assert.AreSame(table, f.Match.State); Assert.AreEqual(0, changed); Assert.AreEqual(0, attacks);
                Assert.IsNull(f.Host.LastPlayResolution);
                CollectionAssert.AreEqual(selected, f.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Id));
                f.Content.Set("pointsPerDamage", 2);
                Assert.AreEqual(preview.Damage.Requests.Single().Amount, f.Host.PreviewSelectedPlay().Damage.Requests.Single().Amount);
                Assert.IsInstanceOf<EqualScoreDamageRule>(f.Player.Rules.DamagePolicy);
                Assert.AreEqual(1, f.Hand.PlaySelected(false));
                var actual = f.Host.LastPlayResolution;
                Assert.AreEqual(f.Player.ParticipantId, f.Host.LastPlayedBy);
                Assert.AreEqual(preview.Score.Total, actual.Score.Total); Assert.AreEqual(preview.Damage.Explanation, actual.Damage.Explanation);
                Assert.AreEqual(preview.Events.Single().HealthLost, actual.Events.Single().HealthLost); Assert.AreEqual(0, attacks, "Instant synchronization does not replay attack cues.");
                f.Host.StartBattle(); Assert.IsNull(f.Host.LastPlayResolution); f.Select();
                Assert.IsInstanceOf<ScoreDamagePolicy>(f.Player.Rules.DamagePolicy);
            }
        }
        [Test] public void InvalidXmlDamageConfigurationCannotReplaceAnExistingSession()
        {
            using (var f = new Fixture())
            {
                var session = f.Host.Session; var state = session.State;
                f.Content.Set("pointsPerDamage", 0);
                Assert.Throws<InvalidOperationException>(() => f.Host.StartBattle());
                Assert.AreSame(session, f.Host.Session); Assert.AreSame(state, f.Host.AuthorityState);
            }
        }
        [Test] public void DiscardPreviewUsesSnapshotAndRecordsOnlyTheCommittedAction()
        {

                using (var f = new Fixture(start: false))
                {
                    f.Host.StartBattle(); f.Select();
                    var before = f.Host.State; var mirror = f.Match.State; int changes = 0, attacks = 0;
                    f.Host.StateChanged += () => changes++; f.Player.ActionPresented += () => attacks++;
                    var preview = f.Host.PreviewSelectedDiscard();
                    Assert.IsTrue(preview.Accepted); Assert.IsEmpty(preview.DiscardReward.Plan.Effects);
                    Assert.AreSame(before, f.Host.State); Assert.AreSame(mirror, f.Match.State);
                    Assert.AreEqual(0, changes); Assert.AreEqual(0, attacks); Assert.IsNull(f.Host.LastDiscardResolution);
                    f.Content.SetReward(8);
                    Assert.AreEqual("discard.none.1", f.Host.PreviewSelectedDiscard().DiscardReward.Plan.RuleVersion);
                    Assert.AreEqual(1, f.Player.Discard(f.Hand.CaptureSelection(), false));
                    var actual = f.Host.LastDiscardResolution;
                    Assert.AreEqual(f.Player.ParticipantId, f.Host.LastDiscardedBy);
                    Assert.AreEqual(preview.DiscardReward.Plan.RuleVersion, actual.DiscardReward.Plan.RuleVersion);
                    CollectionAssert.AreEqual(preview.DiscardReward.Context.Cards.Select(card => card.Id), actual.DiscardReward.Context.Cards.Select(card => card.Id));
                    Assert.IsNull(f.Host.LastPlayResolution); Assert.IsNull(f.Host.LastScore); Assert.AreEqual(0, attacks);
                    Assert.AreEqual(preview.State.DiscardsRemaining, actual.State.DiscardsRemaining);
                    f.Host.StartBattle(); f.Select(); Assert.IsNull(f.Host.LastDiscardResolution);
                    Assert.AreEqual("discard.block-per-card.1", f.Host.PreviewSelectedDiscard().DiscardReward.Plan.RuleVersion);
                    f.Content.SetReward(null); f.Host.StartBattle(); f.Select();
                    Assert.AreEqual("discard.none.1", f.Host.PreviewSelectedDiscard().DiscardReward.Plan.RuleVersion);
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
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(3)); Assert.That(f.Enemy.ReadIntent().Action.Hits.Single().SettledDamage, Is.EqualTo(7));
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
                Assert.That(f.Match.InitialState.Zones, Is.Empty); Assert.That(f.Hand.HasSnapshot, Is.False);
                Assert.Throws<InvalidOperationException>(() => f.Match.StartMatch());
                Assert.Throws<InvalidOperationException>(() => f.Match.ConfigureInitial(Array.Empty<CardMatchZoneSnapshot>()));
                Assert.Throws<InvalidOperationException>(() => f.Match.PrepareDebugEdit(Array.Empty<CardMatchZoneSnapshot>(), true));
                CardDuelState initial = null;
                f.Match.Restarted += () => initial = f.Host.State;
                f.Host.StartBattle();
                Assert.That(initial, Is.Not.Null); Assert.That(initial.HasStarted, Is.False); Assert.That(initial.Zone(CardZoneKinds.Hand).Cards, Is.Empty);
                Assert.That(initial.Zone(CardZoneKinds.Deck).Cards.Count, Is.EqualTo(52));
                Assert.That(f.Host.State.HasStarted, Is.True); Assert.That(f.Hand.Cards.Count, Is.EqualTo(8));
                Assert.That(f.Host.PlayerZone(CardZoneKinds.Played).Cards, Is.Empty); Assert.That(f.Host.State.Duel.PlayerTurn.Energy, Is.EqualTo(3));
                foreach (var zone in f.Host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
                Assert.Throws<InvalidOperationException>(() => f.Match.RestartMatch());
                Assert.Throws<InvalidOperationException>(() => f.Match.PreparePreviewPlay(f.Hand.Zone.Id, f.Host.PlayerZone(CardZoneKinds.Played).Id, f.Hand.Zone.Cards.Take(1), 5));
                f.Host.StartBattle(); Assert.That(f.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(f.Host.State.Zone(CardZoneKinds.Deck).Cards.Count, Is.EqualTo(44));
            }
        }
        [Test] public void ScenePresentationAttachesAnAlreadyStartedSessionAndRoutesRealPlayerCommandsToIt()
        {
            using (var f = new Fixture(start: false))
            {
                var encounter = f.Content.Document.Root.Element("Encounters").Element("Encounter");
                encounter.Element("Player").SetAttributeValue("id", "external/player");
                encounter.Element("Enemy").SetAttributeValue("id", "external/enemy"); f.Content.Apply();
                var playerId = new ParticipantId("external/player"); var enemyId = new ParticipantId("external/enemy");
                var engine = new CardDuelEngine(new BattleRules("external.test", 8));
                var rules = new BattlePlayerRules(damagePolicy: new ScoreDamagePolicy(5));
                var initial = engine.Create("external", HmacDeckShuffler.FromSeed(new byte[32]),
                    new[] { new BattlePlayerSetup(playerId, new CombatantDefinition("hero", 40), rules: rules) },
                    new[] { new BattleEnemySetup(enemyId, new CombatantDefinition("monster", 1000), 7) });
                var session = new BattleSession(engine, initial);
                Assert.IsTrue(session.Execute(initial.Revision, playerId, CardDuelAction.Start).Accepted);
                var opening = session.State;
                f.Host.AttachSession(session);
                Assert.AreSame(session, f.Host.Session); Assert.AreSame(engine, f.Host.Engine);
                Assert.AreSame(opening, f.Host.AuthorityState); Assert.AreSame(opening, f.Host.PresentedState);
                Assert.AreEqual(playerId, f.Player.ParticipantId); Assert.AreEqual(enemyId, f.Enemy.ParticipantId);
                Assert.AreSame(f.Player, f.Match.Commands); Assert.AreEqual(8, f.Hand.Cards.Count);

                f.Select(); var card = f.Hand.CaptureSelection().Cards.Single();
                Assert.AreEqual(1, f.Hand.PlaySelected(false));
                Assert.AreSame(session, f.Host.Session); Assert.Greater(session.State.Revision, opening.Revision);
                Assert.AreEqual(2, session.State.Duel.Player(playerId).Energy.Energy);
                Assert.Less(session.State.Duel.Combat.Get(enemyId).Health, 1000);
                Assert.IsFalse(session.State.Zone(CardZoneKinds.Hand, playerId).Cards.Contains(card));
                Assert.AreSame(session.State, f.Host.PresentedState);

                f.HandView.CompleteTransitions();
                Assert.IsTrue(f.Host.EndTurn(false));
                Assert.AreSame(session, f.Host.Session); Assert.AreSame(session.State, f.Host.AuthorityState);
                Assert.AreSame(session.State, f.Host.PresentedState);
                Assert.AreEqual(1, session.State.Duel.CompletedRounds); Assert.AreEqual(33, f.Player.Unit.Health);
                foreach (var zone in session.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
            }
        }
        [Test] public void SharedPresentationBusyGateCoversHandFlightsAndActorPlaybackUntilSynchronization()
        {
            using (var f = new Fixture())
            {
                IBattleSessionPresentation presentation = f.Host;
                f.HandView.CompleteTransitions(); Assert.IsFalse(presentation.IsPresenting);
                var authority = f.Host.AuthorityState; var hand = f.HandView.Data;
                f.HandView.Present(new CardZoneViewData(hand.ZoneId, Array.Empty<CardVisualData>()), false);
                f.HandView.Present(hand, true);
                Assert.IsTrue(f.HandView.IsTransitioning); Assert.IsTrue(f.Host.IsPresenting);
                Assert.IsTrue(presentation.IsPresenting); Assert.IsFalse(f.Player.CanInteract);
                presentation.Synchronize(authority);
                Assert.IsFalse(f.HandView.IsTransitioning); Assert.IsFalse(presentation.IsPresenting);
                Assert.AreSame(authority, f.Host.Session.State);

                Probe(f.Player); Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play);
                try
                {
                    var pending = NextPending(f, sequence); authority = f.Host.AuthorityState;
                    Assert.IsTrue(presentation.IsPresenting);
                    presentation.Synchronize(authority);
                    Assert.IsFalse(presentation.IsPresenting); Assert.AreSame(authority, f.Host.Session.State);
                    Assert.AreEqual(BattlePresentationStatus.Cancelled, pending.Status);
                }
                finally { (sequence as IDisposable)?.Dispose(); }
            }
        }
        [Test] public void PlayerInputUpdatesLatestEnemyStateAndPresentsTargetedFeedback()
        {
            using (var f = new Fixture())
            {
                var oldEnemy = f.Enemy.Unit; int attacks = 0, hurts = 0;
                f.Player.ActionPresented += () => attacks++;
                f.Enemy.DamagePresented += effect => { Assert.That(effect.Effect.Target, Is.EqualTo(f.Enemy.ParticipantId)); hurts++; };
                f.Select(); Assert.That(f.Hand.PlaySelected(false), Is.EqualTo(1));
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(2)); Assert.That(attacks, Is.Zero); Assert.That(hurts, Is.Zero);
                Assert.AreEqual(f.Enemy.Unit.Health, f.Enemy.DisplayUnit.Health, "Instant updates the neutral display without playing feedback.");
                Assert.That(f.Enemy.Unit.Health, Is.LessThan(oldEnemy.Health)); Assert.That(oldEnemy.Health, Is.EqualTo(75));
                Assert.That(f.Enemy.Unit, Is.SameAs(f.Host.State.Duel.Combat.Get(f.Enemy.ParticipantId)));
                Assert.That(f.Player.Unit.Health, Is.EqualTo(40)); Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero);
            }
        }
        [Test] public void SessionSettlesEnemyActionAndPresentsItExactlyOnceWhenPlayerEndsTurn()
        {
            using (var f = new Fixture())
            {
                int attacks = 0, playerHurts = 0; f.Enemy.ActionPresented += () => attacks++; f.Player.DamagePresented += effect => playerHurts++;
                Assert.That(f.Player.EndTurn(false), Is.True);
                Assert.That(attacks, Is.Zero); Assert.That(playerHurts, Is.Zero); Assert.That(f.Player.Unit.Health, Is.EqualTo(33));
                Assert.AreEqual(33, f.Player.DisplayUnit.Health);
                Assert.That(f.Player.Turn.Energy, Is.EqualTo(3)); Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
                Assert.That(f.Enemy.CanAct, Is.False); Assert.That(f.Enemy.ReadIntent().ExpectedRevision, Is.EqualTo(f.Host.State.Revision));
                Assert.AreSame(f.Host.Session.State, f.Host.PresentedState);
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
        [Test] public void DeathFeedbackAndRestartResetVisualAndRetainAuthoredParticipantIdentityInANewBattle()
        {
            using (var f = new Fixture())
            {
                f.Set("enemyHealth", 1); f.Host.StartBattle(); var oldId = f.Enemy.ParticipantId; var oldBattle = f.Host.State.Table.MatchId; var rest = f.Enemy.Visual.localScale;
                var rootPosition = f.Enemy.transform.position; var rootRotation = f.Enemy.transform.rotation; var rootScale = f.Enemy.transform.localScale;
                int deaths = 0; f.Enemy.DeathPresented += effect => deaths++;
                f.Select(); f.Hand.PlaySelected(false);
                Assert.That(f.Enemy.IsAlive, Is.False); Assert.That(deaths, Is.Zero, "Instant synchronization establishes the dead pose without playing a death action."); Assert.That(f.Enemy.Visual.localScale, Is.EqualTo(rest * .75f));
                Assert.That(f.Enemy.transform.position, Is.EqualTo(rootPosition)); Assert.That(f.Enemy.transform.rotation, Is.EqualTo(rootRotation));
                Assert.That(f.Enemy.transform.localScale, Is.EqualTo(rootScale));
                Assert.That(f.Player.CanInteract, Is.False); Assert.That(f.Enemy.ReadIntent(), Is.Null);
                f.Set("enemyHealth", 75); f.Host.StartBattle();
                Assert.That(f.Enemy.ParticipantId, Is.EqualTo(oldId)); Assert.That(f.Host.State.Table.MatchId, Is.Not.EqualTo(oldBattle)); Assert.That(f.Enemy.Unit.Health, Is.EqualTo(75));
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
        private static BattleSessionResult Prepare(Fixture f, CardDuelAction action)
        {
            f.HandView.CompleteTransitions();
            var actor = f.Host.Player.ParticipantId;
            var ids = action == CardDuelAction.Play || action == CardDuelAction.Discard
                ? new[] { f.Host.AuthorityState.Zone(CardZoneKinds.Hand, actor).Cards[0] } : Array.Empty<CardInstanceId>();
            var result = f.Host.Session.Execute(f.Host.AuthorityState.Revision, actor, action, ids);
            Assert.That(result.Accepted, Is.True);
            return result;
        }
        /// <summary>Owns the clock only; the real host bridge, authored scene recipes, director and presenters execute unchanged.</summary>
        private sealed class ManualPlayback : IEnumerator, IDisposable
        {
            private sealed class Routine
            {
                public readonly IEnumerator Enumerator;
                public object Waiting;
                public bool Alive = true;
                public Routine(IEnumerator enumerator) { Enumerator = enumerator; }
                public void Advance()
                {
                    if (!Alive || Waiting is CustomYieldInstruction wait && wait.keepWaiting) return;
                    if (!Enumerator.MoveNext()) { Cancel(); return; }
                    Waiting = Enumerator.Current;
                }
                public void Cancel() { if (!Alive) return; Alive = false; (Enumerator as IDisposable)?.Dispose(); }
            }
            private readonly Fixture fixture;
            private readonly List<Routine> routines = new List<Routine>();
            public object Current { get; private set; }
            public ManualPlayback(Fixture fixture) { this.fixture = fixture; fixture.Host.StartPresentationRoutine = Start; }
            private Action Start(IEnumerator enumerator)
            {
                var routine = new Routine(enumerator); routines.Add(routine); routine.Advance(); return routine.Cancel;
            }
            public void Present(BattleSessionResult result) => ((IBattleSessionPresentation)fixture.Host).Present(result, true);
            public void Step(float cardSeconds = .25f)
            {
                foreach (var probe in Probes()) probe.EmitCompletedMarkers();
                if (cardSeconds > 0) fixture.HandView.AdvanceMotion(cardSeconds);
                foreach (var routine in routines.ToArray()) routine.Advance();
                routines.RemoveAll(routine => !routine.Alive);
                fixture.Host.PresentationDirector.Tick();
            }
            private IEnumerable<ManualBattlePresentation> Probes() => fixture.Host.Players.Cast<BattleActor>().Concat(fixture.Host.Enemies)
                .Select(actor => actor.Presentation).OfType<ManualBattlePresentation>();
            public bool MoveNext()
            {
                Step();
                Current = Probes().SelectMany(probe => probe.Operations).FirstOrDefault(operation => operation.keepWaiting);
                return fixture.Host.IsPresenting;
            }
            public void Reset() => throw new NotSupportedException();
            public void Dispose()
            {
                fixture.Host.SnapToState();
                foreach (var routine in routines.ToArray()) routine.Cancel();
                routines.Clear(); fixture.Host.StartPresentationRoutine = null;
            }
        }
        private static ManualPlayback Sequence(Fixture f, CardDuelAction action)
        {
            var sequence = new ManualPlayback(f);
            sequence.Present(Prepare(f, action));
            return sequence;
        }
        private static BattlePresentationOperation NextPending(Fixture f, IEnumerator sequence)
        {
            for (int i = 0; i < 100; ++i)
            {
                if (!sequence.MoveNext()) return null;
                if (sequence.Current is BattlePresentationOperation operation && operation.keepWaiting) return operation;
            }
            throw new AssertionException("The director failed to reach an actual presentation checkpoint.");
        }
        [TestCase(CardDuelAction.Play)] [TestCase(CardDuelAction.Discard)]
        public void ActorPresentationWaitsForTheActualCardPreparationMilestone(CardDuelAction action)
        {
            using (var f = new Fixture(start: false))
            {
                // Runtime XML enables the discard reward for this scenario.
                try
                {
                    f.Content.SetReward(8);
                    f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                    var player = Probe(f.Player); Probe(f.Enemy);
                    using (var sequence = Sequence(f, action))
                    {
                        var authority = f.Host.AuthorityState;
                        Assert.AreEqual(1, authority.Duel.CompletedRounds);
                        Assert.AreEqual(0, f.Host.PresentedState.Duel.CompletedRounds);
                        sequence.Step(0);
                        Assert.IsEmpty(player.Cues); Assert.IsTrue(f.Host.IsPresenting);
                        f.HandView.AdvanceMotion(.01f); sequence.Step(0);
                        Assert.IsEmpty(player.Cues, "Before the real card marker, no actor action can start.");
                        f.HandView.AdvanceMotion(.3f); sequence.Step(0);
                        Assert.IsNotNull(NextPending(f, sequence));
                        Assert.AreEqual(action == CardDuelAction.Play ? BattlePresentationKind.Attack : BattlePresentationKind.GainBlock, player.Cues.First().Kind);
                        Assert.AreSame(authority, f.Host.Session.State);
                    }
                }
                finally { f.Content.SetReward(null); }
            }
        }

        [Test] public void HostPresentsEachAttackHitAndPreparationAfterSessionHasSettledTheTurn()
        {
            using (var f = new Fixture())
            {
                f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                var player = Probe(f.Player); var enemy = Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play);
                var authority = f.Host.AuthorityState;
                Assert.That(authority.Duel.CompletedRounds, Is.EqualTo(1));
                Assert.That(authority.Duel.Combat.Get(f.Player.ParticipantId).Health, Is.EqualTo(33));
                Assert.That(f.Host.PresentedState.Duel.CompletedRounds, Is.Zero);
                try
                {
                    var attack = NextPending(f, sequence); Assert.That(player.Cues.Single().Kind, Is.EqualTo(BattlePresentationKind.Attack));
                    Assert.That(enemy.Cues, Is.Empty); Assert.That(f.Host.IsPresenting, Is.True); Assert.That(f.Player.CanInteract, Is.False);
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); attack.Complete();
                    Assert.That(f.Host.Session.State, Is.SameAs(authority));
                    var hit = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Hit));
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); hit.Complete();
                    var prepare = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.PrepareAttack));
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); prepare.Complete();
                    Assert.That(f.Host.Session.State, Is.SameAs(authority));
                    var enemyAttack = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Attack));
                    Assert.That(player.Cues.Count, Is.EqualTo(1)); Assert.That(f.Host.IsPresenting, Is.True); enemyAttack.Complete();
                    var playerHit = NextPending(f, sequence); Assert.That(player.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Hit));
                    Assert.That(f.Host.IsPresenting, Is.True); Assert.That(f.Player.CanInteract, Is.False); playerHit.Complete();
                    Assert.That(NextPending(f, sequence), Is.Null); Assert.That(f.Host.IsPresenting, Is.False);
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1)); Assert.That(f.Player.CanInteract, Is.True);
                    Assert.That(f.Host.Session.State, Is.SameAs(authority)); Assert.That(f.Host.PresentedState, Is.SameAs(authority));
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
                var authority = f.Host.AuthorityState;
                Assert.That(authority.Duel.Phase, Is.EqualTo(ScoredDuelPhase.Won));
                try
                {
                    NextPending(f, sequence).Complete(); NextPending(f, sequence).Complete();
                    var death = NextPending(f, sequence); Assert.That(enemy.Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Death));
                    Assert.That(f.Enemy.DisplayUnit.IsAlive, Is.False); Assert.That(f.Host.IsPresenting, Is.True);
                    Assert.That(f.Player.CanInteract, Is.False); Assert.That(f.Player.Unit.Health, Is.EqualTo(40));
                    death.Complete(); Assert.That(NextPending(f, sequence), Is.Null); Assert.That(f.Host.IsPresenting, Is.False);
                    Assert.That(player.Cues.Count, Is.EqualTo(1)); Assert.That(enemy.Cues.Any(c => c.Kind == BattlePresentationKind.Attack), Is.False);
                    Assert.That(f.Host.Session.State, Is.SameAs(authority));
                }
                finally { (sequence as IDisposable)?.Dispose(); }
            }
        }
        [Test] public void CancelledPresentationCannotCompleteANewBattleAndRestartResetsBothImplementations()
        {
            using (var f = new Fixture())
            {
                f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                var player = Probe(f.Player); var enemy = Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play); var pending = NextPending(f, sequence);
                var authority = f.Host.AuthorityState;
                (sequence as IDisposable)?.Dispose(); f.Host.SnapToState();
                Assert.That(pending.Status, Is.EqualTo(BattlePresentationStatus.Cancelled)); Assert.That(pending.Complete(), Is.False);
                Assert.That(f.Host.IsPresenting, Is.False); long revision = f.Host.State.Revision;
                Assert.That(pending.Cancel(), Is.False); Assert.That(f.Host.State.Revision, Is.EqualTo(revision));
                Assert.That(f.Host.Session.State, Is.SameAs(authority)); Assert.That(f.Host.PresentedState, Is.SameAs(authority));
                int playerResets = player.Resets, enemyResets = enemy.Resets; f.Host.StartBattle();
                var restarted = f.Host.AuthorityState;
                Assert.That(pending.Complete(), Is.False); Assert.That(f.Host.Session.State, Is.SameAs(restarted));
                Assert.That(player.Resets, Is.GreaterThan(playerResets)); Assert.That(enemy.Resets, Is.GreaterThan(enemyResets));
                Assert.That(f.Host.State.Revision, Is.EqualTo(1)); Assert.That(f.Player.Unit.Health, Is.EqualTo(40));
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void SnappingOrDisablingDuringPlaybackOnlySynchronizesTheSettledState(bool disable)
        {
            using (var f = new Fixture())
            {
                f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                Probe(f.Player); Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play);
                try
                {
                    var pending = NextPending(f, sequence); var authority = f.Host.AuthorityState;
                    Assert.That(f.Host.PresentedState.Duel.CompletedRounds, Is.Zero);
                    Assert.That(authority.Duel.CompletedRounds, Is.EqualTo(1));
                    if (disable)
                    {
                        f.Host.enabled = false;
                        // The EditMode test owns the playback and lifecycle clocks.
                        typeof(SingleBattleSceneAdapter).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(f.Host, null);
                    }
                    else f.Host.SnapToState();
                    Assert.That(f.Host.Session.State, Is.SameAs(authority));
                    Assert.That(f.Host.PresentedState, Is.SameAs(authority));
                    Assert.That(f.Host.IsPresenting, Is.False); Assert.That(pending.Status, Is.EqualTo(BattlePresentationStatus.Cancelled));
                    Assert.That(pending.Complete(), Is.False);
                    f.Host.SnapToState();
                    Assert.That(f.Host.Session.State, Is.SameAs(authority));
                    foreach (var zone in authority.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
                }
                finally { (sequence as IDisposable)?.Dispose(); }
            }
        }
        [Test] public void FailedActorPlaybackRecoversWithoutRepeatingAnyRuleStep()
        {
            using (var f = new Fixture())
            {
                f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                Probe(f.Player); Probe(f.Enemy);
                var sequence = Sequence(f, CardDuelAction.Play);
                try
                {
                    var pending = NextPending(f, sequence); var authority = f.Host.AuthorityState;
                    Assert.That(authority.Duel.CompletedRounds, Is.EqualTo(1));
                    pending.Fail(new InvalidOperationException("Test presentation failure"));
                    LogAssert.Expect(LogType.Error, new Regex("Battle presentation:.*"));
                    Assert.That(NextPending(f, sequence), Is.Null);
                    Assert.That(f.Host.Session.State, Is.SameAs(authority)); Assert.That(f.Host.PresentedState, Is.SameAs(authority));
                    Assert.That(f.Host.IsPresenting, Is.False); Assert.That(f.Player.Unit.Health, Is.EqualTo(33));
                    Assert.That(pending.Complete(), Is.False);
                    foreach (var zone in authority.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
                }
                finally { (sequence as IDisposable)?.Dispose(); }
            }
        }
        [Test] public void SkippingDiscardPlaybackPreservesTheSettledShieldRemainder()
        {
            using (var f = new Fixture(start: false))
            {
                // Runtime XML enables the discard reward for this scenario.
                IEnumerator sequence = null;
                try
                {
                    f.Content.SetReward(8);
                    f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                    Probe(f.Player); Probe(f.Enemy);
                    sequence = Sequence(f, CardDuelAction.Discard);
                    var authority = f.Host.AuthorityState;
                    Assert.That(authority.Duel.CompletedRounds, Is.EqualTo(1));
                    Assert.That(authority.Duel.Combat.Get(f.Player.ParticipantId).Block, Is.EqualTo(1));
                    var pending = NextPending(f, sequence);
                    Assert.That(f.Player.Unit.Block, Is.EqualTo(8)); Assert.That(f.Player.Shields.TotalBlock, Is.EqualTo(8));
                    f.Host.SnapToState();
                    Assert.That(pending.Status, Is.EqualTo(BattlePresentationStatus.Cancelled));
                    Assert.That(f.Host.Session.State, Is.SameAs(authority)); Assert.That(f.Host.PresentedState, Is.SameAs(authority));
                    Assert.That(f.Player.Unit.Block, Is.EqualTo(1)); Assert.That(f.Player.Shields.TotalBlock, Is.EqualTo(1));
                    Assert.That(f.Player.Shields.Batches.Single().Cards.Count, Is.EqualTo(1));
                    f.Host.SnapToState(); Assert.That(f.Player.Shields.TotalBlock, Is.EqualTo(1));
                    Assert.That(f.Host.Session.State, Is.SameAs(authority));
                }
                finally
                {
                    (sequence as IDisposable)?.Dispose();
                    // The fixture owns and releases all temporary XML assets.
                }
            }
        }
        [Test] public void SwitchingObserverKeepsIndependentCardsAndCommandAuthority()
        {
            using (var f = new Fixture(playerCount: 2, enemyCount: 3))
            {
                var p2 = f.Host.Players[1]; var hand1 = f.Host.State.Zone(CardZoneKinds.Hand, f.Player.ParticipantId).Cards.ToArray();
                var hand2 = f.Host.State.Zone(CardZoneKinds.Hand, p2.ParticipantId).Cards.ToArray();
                f.HandView.CompleteTransitions(); long revision = f.Host.State.Revision;
                Assert.That(f.Host.SelectPlayer(p2), Is.True); f.HandView.CompleteTransitions();
                Assert.That(f.Match.Commands, Is.SameAs(p2)); Assert.That(f.Player.CanInteract, Is.False); Assert.That(p2.CanInteract, Is.True);
                Assert.That(f.Host.State.Revision, Is.EqualTo(revision)); CollectionAssert.AreEqual(hand2, f.Hand.Zone.Cards);
                f.Select(); Assert.That(p2.Discard(f.Hand.CaptureSelection(), false), Is.EqualTo(1));
                Assert.That(p2.Turn.Energy, Is.EqualTo(2)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(3));
                CollectionAssert.AreEqual(hand1, f.Host.State.Zone(CardZoneKinds.Hand, f.Player.ParticipantId).Cards);
                Assert.That(f.Host.SelectPlayer(f.Player), Is.True); f.HandView.CompleteTransitions();
                CollectionAssert.AreEqual(hand1, f.Hand.Zone.Cards);
                Assert.That(f.Player.EndTurn(false), Is.True); f.HandView.CompleteTransitions();
                Assert.That(f.Host.Player, Is.SameAs(p2)); Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero);
                Assert.That(p2.EndTurn(false), Is.True); f.HandView.CompleteTransitions();
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1)); Assert.That(f.Player.Unit.Health, Is.EqualTo(19));
                foreach (var zone in f.Host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
            }
        }
        [Test] public void ActorXmlDefinitionsAreIndependentAndCapturedAtBattleCreation()
        {
            using (var f = new Fixture(playerCount: 2, enemyCount: 2))
            {
                var player = f.Host.Players[1]; var enemy = f.Host.Enemies[1];
                var p = f.Content.Actor(index: 1); p.SetAttributeValue("maxHealth", 70);
                p.SetAttributeValue("initialEnergy", 6); p.SetAttributeValue("playEnergyCost", 2); p.SetAttributeValue("handSize", 5);
                var e = f.Content.Actor(enemy: true, index: 1); e.SetAttributeValue("maxHealth", 120); e.SetAttributeValue("damage", 11);
                f.Content.Apply();
                Assert.That(player.Turn.Energy, Is.EqualTo(3)); Assert.That(enemy.Unit.Health, Is.EqualTo(75));
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                Assert.That(player.Unit.Health, Is.EqualTo(70)); Assert.That(player.Turn.Energy, Is.EqualTo(6));
                Assert.That(player.Rules.PlayEnergyCost, Is.EqualTo(2)); Assert.That(enemy.Unit.Health, Is.EqualTo(120));
                Assert.That(enemy.ReadIntent().Action.Hits.Single().SettledDamage, Is.EqualTo(11)); Assert.That(f.Player.Unit.Health, Is.EqualTo(40));
                Assert.That(f.Host.SelectPlayer(player), Is.True); f.HandView.CompleteTransitions(); Assert.That(f.Hand.Cards.Count, Is.EqualTo(5));
            }
        }
        [Test] public void EnemyQueuePresentsEachUnitsSavedSnapshotAfterAllAttacksHaveSettled()
        {
            using (var f = new Fixture(playerCount: 2, enemyCount: 3))
            {
                f.HandView.CompleteTransitions(); Assert.That(f.Player.EndTurn(false), Is.True); f.HandView.CompleteTransitions();
                var actors = f.Host.Players.Cast<BattleActor>().Concat(f.Host.Enemies).ToArray();
                var probes = actors.ToDictionary(actor => actor, Probe);
                var sequence = Sequence(f, CardDuelAction.EndTurn);
                var authority = f.Host.AuthorityState;
                Assert.That(authority.Duel.CompletedRounds, Is.EqualTo(1));
                Assert.That(authority.Duel.Combat.Get(f.Player.ParticipantId).Health, Is.EqualTo(19));
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var prepare = NextPending(f, sequence);
                        Assert.That(probes[f.Host.Enemies[i]].Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.PrepareAttack));
                        Assert.That(f.Player.Unit.Health, Is.EqualTo(40 - i * 7));
                        Assert.That(f.Host.Session.State, Is.SameAs(authority));
                        Assert.That(f.Host.SelectPlayer(f.Player), Is.False); prepare.Complete();
                        var attack = NextPending(f, sequence);
                        Assert.That(probes[f.Host.Enemies[i]].Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Attack));
                        Assert.That(f.Player.Unit.Health, Is.EqualTo(40 - i * 7)); attack.Complete();
                        var hit = NextPending(f, sequence); Assert.That(probes[f.Player].Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Hit));
                        Assert.That(f.Player.Unit.Health, Is.EqualTo(40 - (i + 1) * 7)); hit.Complete();
                    }
                    Assert.That(NextPending(f, sequence), Is.Null); Assert.That(f.Host.IsPresenting, Is.False);
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
                    Assert.That(f.Host.Session.State, Is.SameAs(authority)); Assert.That(f.Host.PresentedState, Is.SameAs(authority));
                    Assert.That(f.Host.Players.All(actor => actor.Turn.Energy == 3), Is.True);
                }
                finally { (sequence as IDisposable)?.Dispose(); }
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
                Assert.That(f.Player.CanPlay(f.Hand.CaptureSelection()), Is.True); Assert.That(f.Player.CanDiscard(f.Hand.CaptureSelection()), Is.True);
                Assert.That(f.Player.Discard(f.Hand.CaptureSelection(), false), Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(5)); f.Select();
                Assert.That(f.Player.Play(f.Hand.CaptureSelection(), false), Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(2)); f.Select();
                Assert.That(f.Player.CanPlay(f.Hand.CaptureSelection()), Is.False); Assert.That(f.Player.CanDiscard(f.Hand.CaptureSelection()), Is.True);
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero);
                Assert.That(f.Player.Discard(f.Hand.CaptureSelection(), false), Is.EqualTo(1));
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(3));
                Assert.That(f.Player.Unit.Health, Is.EqualTo(33)); Assert.That(f.Host.State.DiscardsRemaining, Is.EqualTo(1));
            }
        }
        [Test] public void HostProjectsImmediatePlayRefillAndDeferredDiscardRefill()
        {
            using (var f = new Fixture())
            {
                f.Set("playRefillTiming", (int)CardDuelRefillTiming.AfterAction);
                f.Set("discardRefillTiming", (int)CardDuelRefillTiming.NextPlayerTurn);
                f.Host.StartBattle(); f.Select(); f.Player.Play(f.Hand.CaptureSelection(), false);
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(f.Host.PlayerZone(CardZoneKinds.Played).Cards, Is.Empty);
                f.Select(); f.Player.Discard(f.Hand.CaptureSelection(), false); Assert.That(f.Hand.Cards.Count, Is.EqualTo(7));
                Assert.That(f.Host.State.Duel.CompletedRounds, Is.Zero); Assert.That(f.Player.Turn.Energy, Is.EqualTo(1));
                f.Player.EndTurn(false); Assert.That(f.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
                foreach (var zone in f.Host.State.Table.Zones) CollectionAssert.AreEquivalent(zone.Cards, f.Match.State.GetZone(zone.Id).Cards);
            }
        }
        [Test] public void InstantPlaySynchronizesTheLastScoredStepSummaryWithoutPlayingAttacks()
        {
            using (var f = new Fixture(start: false))
            {
                f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle(); f.Select();
                int attacks = 0;
                f.Player.ActionPresented += () => attacks++;
                f.Enemy.ActionPresented += () => attacks++;
                Assert.AreEqual(1, f.Player.Play(f.Hand.CaptureSelection(), false));
                var batch = f.Host.LastResolution;
                var scored = batch.Steps.Last(step => step.Transition.Score != null).Transition;
                long expectedDamage = scored.Events.Sum(effect => (long)effect.HealthLost);
                Assert.Greater(batch.Steps.Count, 1, "The summary belongs to the scored step, even when the batch also resolves an enemy turn.");
                Assert.AreSame(scored.Score, f.Host.LastScore);
                Assert.AreEqual(expectedDamage, f.Host.LastDamage);
                Assert.AreEqual(SingleBattleHud.HandName(scored.Score.Category) + " · " + scored.Score.Total + " 分 → " + expectedDamage + " 伤害", f.Host.Message);
                Assert.AreSame(batch.State, f.Host.State);
                Assert.AreSame(batch.State, f.Host.AuthorityState);
                Assert.AreEqual(0, attacks, "Instant synchronization must not replay actor feedback.");
            }
        }
        [Test] public void SnapBeforeActionResolvedSynchronizesSummaryWithoutReplayingRulesOrAttacks()
        {
            using (var f = new Fixture(start: false))
            {
                f.Set("maxEnergy", 1); f.Set("initialEnergy", 1); f.Host.StartBattle();
                Probe(f.Player); Probe(f.Enemy);
                int attacks = 0;
                f.Player.ActionPresented += () => attacks++;
                f.Enemy.ActionPresented += () => attacks++;
                using (var sequence = Sequence(f, CardDuelAction.Play))
                {
                    var attack = NextPending(f, sequence);
                    var batch = f.Host.LastResolution;
                    var committed = f.Host.AuthorityState;
                    var scored = batch.Steps.Last(step => step.Transition.Score != null).Transition;
                    long expectedDamage = scored.Events.Sum(effect => (long)effect.HealthLost);
                    Assert.NotNull(attack);
                    Assert.IsNull(f.Host.LastScore);
                    Assert.Less(f.Host.Display.Cursor.Sequence, batch.Timeline.First(fact => fact.Kind == BattleTimelineKind.ActionResolved).Sequence);
                    int alreadyPlayedAttacks = attacks;
                    f.Host.SnapToState();
                    Assert.AreSame(scored.Score, f.Host.LastScore);
                    Assert.AreEqual(expectedDamage, f.Host.LastDamage);
                    Assert.AreEqual(SingleBattleHud.HandName(scored.Score.Category) + " · " + scored.Score.Total + " 分 → " + expectedDamage + " 伤害", f.Host.Message);
                    Assert.AreSame(committed, f.Host.AuthorityState);
                    Assert.AreSame(committed, f.Host.State);
                    Assert.AreEqual(BattlePresentationStatus.Cancelled, attack.Status);
                    Assert.IsFalse(attack.Complete());
                    sequence.Step();
                    Assert.AreEqual(alreadyPlayedAttacks, attacks);
                    Assert.AreSame(committed, f.Host.AuthorityState, "A stale completion cannot submit or repeat a rule command.");
                    Assert.IsFalse(f.Host.IsPresenting);
                }
            }
        }
        [Test] public void DisabledSceneDirectorCompletesNewCommandsInstantlyWithoutAnUpdateLoop()
        {
            using (var f = new Fixture())
            {
                var player = Probe(f.Player); var enemy = Probe(f.Enemy);
                using (var sequence = new ManualPlayback(f))
                {
                    f.Host.PresentationDirector.enabled = false;
                    f.Select();
                    Assert.AreEqual(1, f.Player.Play(f.Hand.CaptureSelection(), true));
                    var batch = f.Host.LastResolution;
                    Assert.IsFalse(f.Host.IsPresenting, "A disabled frontend has no Update loop to drain an animated plan.");
                    Assert.AreSame(batch.State, f.Host.State);
                    Assert.AreSame(batch.State, f.Host.AuthorityState);
                    Assert.AreEqual(batch.Timeline.Last().Sequence, f.Host.Display.Cursor.Sequence);
                    Assert.AreEqual(f.Enemy.Unit.Health, f.Host.Display.Unit(f.Enemy.ParticipantId.Value).Health);
                    Assert.IsEmpty(player.Cues); Assert.IsEmpty(enemy.Cues);
                    Assert.IsTrue(f.Player.CanInteract, "Instant completion restores the next decision window without a Tick.");
                }
            }
        }
        private sealed class BlockThenFullHealthHealReward : IDiscardRewardRule
        {
            public string Version => "display.no-op-shield-test.1";
            public DiscardRewardPlan Resolve(DiscardRewardContext context) => new DiscardRewardPlan(Version,
                "Gain block, then heal an already healthy player.", new[] {
                    new CombatEffect(CombatEffectKind.GainBlock, context.Source, context.Source, 8),
                    new CombatEffect(CombatEffectKind.Heal, context.Source, context.Source, 4)
                });
        }
        [TestCase(false)] [TestCase(true)]
        public void NoOpEffectBetweenBlockGainAndDamagePreservesShieldAttributionOnCompletionOrRecovery(bool recover)
        {
            using (var f = new Fixture(start: false))
            {
                var encounter = f.Content.Document.Root.Element("Encounters").Element("Encounter");
                encounter.Element("Player").SetAttributeValue("id", "shield-order/player");
                encounter.Element("Enemy").SetAttributeValue("id", "shield-order/enemy"); f.Content.Apply();
                var playerId = new ParticipantId("shield-order/player");
                var enemyId = new ParticipantId("shield-order/enemy");
                var engine = new CardDuelEngine(new BattleRules("shield-order.1", 8));
                var rules = new BattlePlayerRules(maxEnergy: 1, initialEnergy: 1, discardReward: new BlockThenFullHealthHealReward());
                var initial = engine.Create("shield-order", HmacDeckShuffler.FromSeed(new byte[32]),
                    new[] { new BattlePlayerSetup(playerId, new CombatantDefinition("hero", 40), rules: rules) },
                    new[] { new BattleEnemySetup(enemyId, new CombatantDefinition("monster", 1000), 7) });
                var session = new BattleSession(engine, initial);
                Assert.IsTrue(session.Execute(initial.Revision, playerId, CardDuelAction.Start).Accepted);
                f.Host.AttachSession(session);
                string cardId = session.State.Zone(CardZoneKinds.Hand, playerId).Cards[0].Value;
                var player = Probe(f.Player); Probe(f.Enemy);
                using (var sequence = Sequence(f, CardDuelAction.Discard))
                {
                    var committed = f.Host.AuthorityState;
                    var effects = f.Host.LastResolution.Timeline.Where(fact => fact.RuleEvent is CombatEvent)
                        .Select(fact => (CombatEvent)fact.RuleEvent).ToArray();
                    CollectionAssert.AreEqual(new[] { CombatEffectKind.GainBlock, CombatEffectKind.Heal, CombatEffectKind.Damage }, effects.Select(effect => effect.Effect.Kind));
                    Assert.AreEqual(8, effects[1].Before.Block); Assert.AreEqual(8, effects[1].After.Block);
                    Assert.AreEqual(effects[1].Before.Health, effects[1].After.Health, "The heal must be a real fact without a reaction animation.");
                    Assert.AreEqual(1, committed.Duel.Combat.Get(playerId).Block);
                    var operation = NextPending(f, sequence);
                    for (int i = 0; i < 8 && operation != null && player.Cues.Last().Kind != BattlePresentationKind.BlockHit; i++)
                    { operation.Complete(); operation = NextPending(f, sequence); }
                    Assert.NotNull(operation); Assert.AreEqual(BattlePresentationKind.BlockHit, player.Cues.Last().Kind);
                    if (recover) {
                        f.Host.SnapToState();
                        Assert.AreEqual(BattlePresentationStatus.Cancelled, operation.Status);
                        sequence.Step();
                    }
                    else { operation.Complete(); Assert.IsNull(NextPending(f, sequence)); }
                    Assert.AreSame(committed, f.Host.AuthorityState); Assert.AreSame(committed, f.Host.State);
                    Assert.AreEqual(1, f.Player.Shields.TotalBlock, "Revisiting an unrecorded no-op must not resurrect earlier block.");
                    Assert.AreEqual(cardId, f.Player.Shields.Batches.Single().Cards.Single().Id,
                        "Recovery must keep the surviving block attached to its discarded card, not replace it with unknown block.");
                    Assert.IsFalse(f.Host.IsPresenting);
                }
            }
        }
    }
    public sealed class ManualBattlePresentation : MonoBehaviour, IBattleActorPresentation, IBattleActorDisplayPresentation
    {
        private readonly HashSet<int> emitted = new HashSet<int>();
        public UnitDisplayData DisplayUnit { get; private set; }
        public readonly List<BattlePresentationCue> Cues = new List<BattlePresentationCue>();
        public readonly List<BattlePresentationOperation> Operations = new List<BattlePresentationOperation>();
        public int Resets;
        public BattlePresentationOperation Play(BattlePresentationCue cue)
        { Cues.Add(cue); var operation = new BattlePresentationOperation(); Operations.Add(operation); return operation; }
        public void EmitCompletedMarkers()
        {
            for (int index = 0; index < Operations.Count; index++)
                if (Operations[index].Status == BattlePresentationStatus.Completed && Cues[index].Kind == BattlePresentationKind.Attack && emitted.Add(index))
                    Cues[index].Markers?.Emit("Impact");
        }
        public void Snap(BattlePresentationCue cue) { }
        public void Synchronize(CombatantState unit) { }
        public void SynchronizeDisplay(UnitDisplayData unit) { DisplayUnit = unit; }
        public void CancelPresentation() { foreach (var operation in Operations) operation.Cancel(); }
        public void ResetPresentation() { CancelPresentation(); Resets++; }
    }
}
