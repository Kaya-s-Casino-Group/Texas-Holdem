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
            public readonly CardZonePlayHost Hand;
            public readonly HandZoneView HandView;
            public readonly HandActionPresenter Actions;
            private readonly List<ScriptableObject> profiles = new List<ScriptableObject>();
            public Fixture(bool start = true, int playerCount = 1, int enemyCount = 1)
            {
                var root = New("Match"); Match = root.AddComponent<CardMatchHost>();
                Match.ConfigureRuntimeManaged();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardArt/Prefabs/Card.prefab");
                var hand = New("Hand"); HandView = hand.AddComponent<HandZoneView>(); HandView.Configure(prefab, CardZoneLayoutSettings.Default);
                Hand = hand.AddComponent<CardZonePlayHost>(); Hand.ConfigureRuntimeBinding(Match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CardPresentation/Prefabs/HandActionPanel.prefab"), Scene);
                var panel = ui.GetComponent<HandActionPanelView>(); panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(), panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                Actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); Actions.Configure(Hand, panel);
                Host = root.AddComponent<SingleBattleHost>(); Host.Configure(Match, Actions, null, null);
                var rules = ScriptableObject.CreateInstance<BattleRulesProfile>(); profiles.Add(rules);
                var damage = ScriptableObject.CreateInstance<ScoreDamageProfile>(); profiles.Add(damage);
                Host.ConfigureRules(rules);
                Player = New("Player").AddComponent<PlayerBattleActor>(); Enemy = New("Enemy").AddComponent<EnemyBattleActor>();
                var visual = New("Enemy Visual").transform; visual.SetParent(Enemy.transform, false);
                visual.localScale = new Vector3(1, 2, 3); Player.Configure(Host); Enemy.Configure(Host, visual);
                var presenter = visual.gameObject.AddComponent<BattleActorPresenter>(); presenter.Configure(visual); Enemy.ConfigurePresentation(presenter);
                var players = new List<PlayerBattleActor> { Player }; var enemies = new List<EnemyBattleActor> { Enemy };
                for (int i = 1; i < playerCount; i++) { var actor = New("Player " + i).AddComponent<PlayerBattleActor>(); actor.Configure(Host); players.Add(actor); }
                for (int i = 1; i < enemyCount; i++) { var actor = New("Enemy " + i).AddComponent<EnemyBattleActor>(); actor.Configure(Host); enemies.Add(actor); }
                foreach (var actor in players) { var profile = ScriptableObject.CreateInstance<PlayerBattleProfile>(); profiles.Add(profile); profile.ConfigureDamage(damage); actor.ConfigureProfile(profile); }
                foreach (var actor in enemies) { var profile = ScriptableObject.CreateInstance<EnemyBattleProfile>(); profiles.Add(profile); actor.ConfigureProfile(profile); SetProperty(profile, "maxHealth", 75); }
                Host.ConfigureActors(players, enemies); if (start) Host.StartBattle();
            }
            private GameObject New(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, Scene); return go; }
            public void Select()
            {
                foreach (var card in Hand.Cards) Hand.SetInteraction(card.Id, default);
                Hand.SetInteraction(Hand.Cards[0].Id, new CardInteractionState(isSelected: true));
                Hand.Publish(); HandView.CompleteTransitions(); Actions.Refresh();
            }
            private static void SetProperty(ScriptableObject profile, string field, int value)
            { var serialized = new SerializedObject(profile); serialized.FindProperty(field).intValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            public void Set(string field, int value)
            {
                if (field == "enemyHealth" || field == "enemyDamage") SetProperty(Enemy.Profile, field == "enemyHealth" ? "maxHealth" : "attackDamage", value);
                else SetProperty(Player.Profile, field == "playerHealth" ? "maxHealth" : field, value);
            }
            public void Dispose()
            {
                Host.enabled = false; Player.enabled = false; Enemy.enabled = false;
                Hand.StopSession(); EditorSceneManager.ClosePreviewScene(Scene);
                foreach (var profile in profiles) UnityEngine.Object.DestroyImmediate(profile);
            }
        }
        [Test] public void DiscardCapturesActualRewardBatchAndRoutesGainAndBlockHitCues()
        {
            using (var f = new Fixture())
            {
                var reward = ScriptableObject.CreateInstance<PerCardBlockRewardProfile>();
                try
                {
                    f.Player.Profile.ConfigureDiscardReward(reward); f.Host.StartBattle(); f.Select();
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
                finally { f.Player.Profile.ConfigureDiscardReward(null); UnityEngine.Object.DestroyImmediate(reward); }
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
        [Test] public void ScoreAssetDrivesBattleAndHoverWithoutMutatingAnExistingSnapshot()
        {
            var profile = ScriptableObject.CreateInstance<Poker.Scoring.PokerScoreProfile>();
            try
            {
                var config = new SerializedObject(profile);
                var ranks = config.FindProperty("ranks");
                for (int i = 0; i < ranks.arraySize; i++) ranks.GetArrayElementAtIndex(i).FindPropertyRelative("chips").longValue = 42;
                config.ApplyModifiedPropertiesWithoutUndo();
                using (var f = new Fixture(start: false, playerCount: 2))
                {
                    var rules = new SerializedObject(f.Host.RulesProfile);
                    rules.FindProperty("scoring").objectReferenceValue = profile; rules.ApplyModifiedPropertiesWithoutUndo();
                    f.Host.StartBattle(); f.Select();
                    var card = f.Hand.Cards[0].Card.Id;
                    Assert.IsTrue(f.Player.TryGetCardBasePoints(card, out var points)); Assert.AreEqual(42, points);
                    var otherCard = f.Host.State.Zone(CardZoneKinds.Hand, f.Host.Players[1].ParticipantId).Cards[0];
                    Assert.IsFalse(f.Player.TryGetCardBasePoints(otherCard, out _));
                    f.Hand.SetInteraction(card.Value, new CardInteractionState(isHovered: true, isSelected: true));
                    f.Hand.Publish(); f.HandView.CompleteTransitions();
                    Assert.IsTrue(f.Hand.TryGetHoveredCard(out var hover)); Assert.AreEqual(42, hover.ChipPoints);
                    // Asset edits affect the next battle only, including hover queries.
                    for (int i = 0; i < ranks.arraySize; i++) ranks.GetArrayElementAtIndex(i).FindPropertyRelative("chips").longValue = 99;
                    config.ApplyModifiedPropertiesWithoutUndo();
                    Assert.IsTrue(f.Player.TryGetCardBasePoints(card, out points)); Assert.AreEqual(42, points);
                    Assert.AreEqual(1, f.Hand.PlaySelected(false));
                    Assert.AreEqual(47, f.Host.State.Duel.Scores.Single().Total);
                    Assert.AreEqual(75 - 47, f.Enemy.Unit.Health);
                    f.Host.StartBattle(); card = f.Hand.Cards[0].Card.Id;
                    Assert.IsTrue(f.Player.TryGetCardBasePoints(card, out points)); Assert.AreEqual(99, points);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        [Test] public void DemoRulesReferenceTheIndependentDefaultScoreAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Poker.Scoring.PokerScoreProfile>("Assets/PokerScoring/Profiles/DefaultPokerScore.asset");
            var rules = AssetDatabase.LoadAssetAtPath<BattleRulesProfile>("Assets/SingleBattle/Profiles/DemoBattleRules.asset");
            Assert.IsNotNull(asset); Assert.AreSame(asset, rules.Scoring);
            var built = rules.BuildScoring(); var defaults = ScoreProfile.Default;
            foreach (HandCategory category in Enum.GetValues(typeof(HandCategory)))
            {
                var actual = built.Rule(category); var expected = defaults.Rule(category);
                Assert.AreEqual(expected.BasePoints, actual.BasePoints);
                Assert.AreEqual(expected.BaseMultiplier, actual.BaseMultiplier);
                Assert.AreEqual(expected.PointsPerLevel, actual.PointsPerLevel);
                Assert.AreEqual(expected.MultiplierPerLevel, actual.MultiplierPerLevel);
            }
            foreach (var card in StandardDeck.Cards())
                Assert.AreEqual(CardScoreRules.GetBasePoints(card), built.CardPoints.GetBasePoints(new CardFace(card.Rank, card.Suit)));
            var serialized = new SerializedObject(asset);
            Assert.AreEqual(13, serialized.FindProperty("ranks").arraySize);
            Assert.AreEqual(10, serialized.FindProperty("hands").arraySize);
        }
        [Test] public void DamagePreviewMatchesRecordedPlayAndNeverPublishesOrChangesTheLiveBattle()
        {
            using (var f = new Fixture(start: false))
            {
                var profile = (ScoreDamageProfile)f.Player.Profile.Damage;
                var config = new SerializedObject(profile); config.FindProperty("version").stringValue = "equal.test.1";
                config.ApplyModifiedPropertiesWithoutUndo(); f.Host.StartBattle(); f.Select();
                int changed = 0, attacks = 0; f.Host.StateChanged += () => changed++; f.Player.ActionPresented += () => attacks++;
                var state = f.Host.State; var table = f.Match.State; var selected = f.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Id).ToArray();
                var preview = f.Host.PreviewSelectedPlay();
                Assert.IsTrue(preview.Accepted); Assert.AreEqual(preview.Score.Total, preview.Damage.Requests.Single().Amount);
                Assert.AreSame(state, f.Host.State); Assert.AreSame(table, f.Match.State); Assert.AreEqual(0, changed); Assert.AreEqual(0, attacks);
                Assert.IsNull(f.Host.LastPlayResolution);
                CollectionAssert.AreEqual(selected, f.Hand.Cards.Where(card => card.Interaction.IsSelected).Select(card => card.Id));
                config.FindProperty("version").stringValue = "equal.test.2"; config.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(preview.Damage.Requests.Single().Amount, f.Host.PreviewSelectedPlay().Damage.Requests.Single().Amount);
                Assert.AreEqual("equal.test.1", f.Host.PreviewSelectedPlay().Damage.RuleVersion);
                Assert.AreEqual(1, f.Hand.PlaySelected(false));
                var actual = f.Host.LastPlayResolution;
                Assert.AreEqual(f.Player.ParticipantId, f.Host.LastPlayedBy);
                Assert.AreEqual(preview.Score.Total, actual.Score.Total); Assert.AreEqual(preview.Damage.Explanation, actual.Damage.Explanation);
                Assert.AreEqual(preview.Events.Single().HealthLost, actual.Events.Single().HealthLost); Assert.AreEqual(1, attacks);
                f.Host.StartBattle(); Assert.IsNull(f.Host.LastPlayResolution); f.Select();
                Assert.AreEqual("equal.test.2", f.Host.PreviewSelectedPlay().Damage.RuleVersion);
            }
        }
        [Test] public void DamageConfigurationIsIndependentAndRejectsInvalidSetup()
        {
            var rules = ScriptableObject.CreateInstance<PlayerBattleProfile>();
            var damage = ScriptableObject.CreateInstance<ScoreDamageProfile>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => rules.BuildRules()); rules.ConfigureDamage(damage);
                Assert.IsInstanceOf<EqualScoreDamageRule>(rules.BuildRules().DamagePolicy);
                var serialized = new SerializedObject(damage); serialized.FindProperty("version").stringValue = "";
                serialized.ApplyModifiedPropertiesWithoutUndo(); Assert.Throws<ArgumentException>(() => rules.BuildRules());
                var demo = AssetDatabase.LoadAssetAtPath<PlayerBattleProfile>("Assets/SingleBattle/Profiles/DemoPlayer.asset");
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<ScoreDamageProfile>("Assets/SingleBattle/Profiles/DemoScoreDamage.asset"), demo.Damage);
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); UnityEngine.Object.DestroyImmediate(damage); }
        }
        [Test] public void DiscardPreviewUsesSnapshotAndRecordsOnlyTheCommittedAction()
        {
            var profile = ScriptableObject.CreateInstance<NoDiscardRewardProfile>();
            try
            {
                using (var f = new Fixture(start: false))
                {
                    f.Player.Profile.ConfigureDiscardReward(profile); f.Host.StartBattle(); f.Select();
                    var before = f.Host.State; var mirror = f.Match.State; int changes = 0, attacks = 0;
                    f.Host.StateChanged += () => changes++; f.Player.ActionPresented += () => attacks++;
                    var preview = f.Host.PreviewSelectedDiscard();
                    Assert.IsTrue(preview.Accepted); Assert.IsEmpty(preview.DiscardReward.Plan.Effects);
                    Assert.AreSame(before, f.Host.State); Assert.AreSame(mirror, f.Match.State);
                    Assert.AreEqual(0, changes); Assert.AreEqual(0, attacks); Assert.IsNull(f.Host.LastDiscardResolution);
                    var serialized = new SerializedObject(profile); serialized.FindProperty("version").stringValue = "discard.next.2";
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    Assert.AreEqual("discard.none.1", f.Host.PreviewSelectedDiscard().DiscardReward.Plan.RuleVersion);
                    Assert.AreEqual(1, f.Player.Discard(f.Hand, false));
                    var actual = f.Host.LastDiscardResolution;
                    Assert.AreEqual(f.Player.ParticipantId, f.Host.LastDiscardedBy);
                    Assert.AreEqual(preview.DiscardReward.Plan.RuleVersion, actual.DiscardReward.Plan.RuleVersion);
                    CollectionAssert.AreEqual(preview.DiscardReward.Context.Cards.Select(card => card.Id), actual.DiscardReward.Context.Cards.Select(card => card.Id));
                    Assert.IsNull(f.Host.LastPlayResolution); Assert.IsNull(f.Host.LastScore); Assert.AreEqual(0, attacks);
                    Assert.AreEqual(preview.State.DiscardsRemaining, actual.State.DiscardsRemaining);
                    f.Host.StartBattle(); f.Select(); Assert.IsNull(f.Host.LastDiscardResolution);
                    Assert.AreEqual("discard.next.2", f.Host.PreviewSelectedDiscard().DiscardReward.Plan.RuleVersion);
                    f.Player.Profile.ConfigureDiscardReward(null); f.Host.StartBattle(); f.Select();
                    Assert.AreEqual("discard.none.1", f.Host.PreviewSelectedDiscard().DiscardReward.Plan.RuleVersion);
                }
                var demo = AssetDatabase.LoadAssetAtPath<PlayerBattleProfile>("Assets/SingleBattle/Profiles/DemoPlayer.asset");
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<NoDiscardRewardProfile>("Assets/SingleBattle/Profiles/DemoDiscardReward.asset"), demo.DiscardReward);
                Assert.IsInstanceOf<NoDiscardRewardRule>(demo.BuildRules().DiscardReward);
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
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
        [Test] public void PlayerInputUpdatesLatestEnemyStateAndPresentsTargetedFeedback()
        {
            using (var f = new Fixture())
            {
                var oldEnemy = f.Enemy.Unit; int attacks = 0, hurts = 0;
                f.Player.ActionPresented += () => attacks++;
                f.Enemy.DamagePresented += effect => { Assert.That(effect.Effect.Target, Is.EqualTo(f.Enemy.ParticipantId)); hurts++; };
                f.Select(); Assert.That(f.Hand.PlaySelected(false), Is.EqualTo(1));
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
                f.Select(); f.Hand.PlaySelected(false);
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
                f.HandView.CompleteTransitions();
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
                f.Select(); Assert.That(p2.Discard(f.Hand, false), Is.EqualTo(1));
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
        [Test] public void ActorProfilesAreIndependentAndCapturedAtBattleCreation()
        {
            using (var f = new Fixture(playerCount: 2, enemyCount: 2))
            {
                var player = f.Host.Players[1]; var enemy = f.Host.Enemies[1];
                var p = new SerializedObject(player.Profile); p.FindProperty("maxHealth").intValue = 70;
                p.FindProperty("initialEnergy").intValue = 6; p.FindProperty("playEnergyCost").intValue = 2;
                p.FindProperty("handSize").intValue = 5; p.ApplyModifiedPropertiesWithoutUndo();
                var e = new SerializedObject(enemy.Profile); e.FindProperty("maxHealth").intValue = 120;
                e.FindProperty("attackDamage").intValue = 11; e.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(player.Turn.Energy, Is.EqualTo(3)); Assert.That(enemy.Unit.Health, Is.EqualTo(75));
                f.Host.StartBattle(); f.HandView.CompleteTransitions();
                Assert.That(player.Unit.Health, Is.EqualTo(70)); Assert.That(player.Turn.Energy, Is.EqualTo(6));
                Assert.That(player.Rules.PlayEnergyCost, Is.EqualTo(2)); Assert.That(enemy.Unit.Health, Is.EqualTo(120));
                Assert.That(enemy.ReadIntent().Action.Hits.Single().SettledDamage, Is.EqualTo(11)); Assert.That(f.Player.Unit.Health, Is.EqualTo(40));
                Assert.That(f.Host.SelectPlayer(player), Is.True); f.HandView.CompleteTransitions(); Assert.That(f.Hand.Cards.Count, Is.EqualTo(5));
            }
        }
        [Test] public void EnemyQueueWaitsForEachUnitsPresentationBeforeCommittingNextAttack()
        {
            using (var f = new Fixture(playerCount: 2, enemyCount: 3))
            {
                f.HandView.CompleteTransitions(); Assert.That(f.Player.EndTurn(false), Is.True); f.HandView.CompleteTransitions();
                var actors = f.Host.Players.Cast<BattleActor>().Concat(f.Host.Enemies).ToArray();
                var probes = actors.ToDictionary(actor => actor, Probe);
                var selected = f.Host.Player;
                var result = f.Host.Engine.Execute(f.Host.State, f.Host.State.Revision, selected.ParticipantId, CardDuelAction.EndTurn);
                typeof(SingleBattleHost).GetProperty("State").SetValue(f.Host, result.State);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(SingleBattleHost).GetField("busy", flags).SetValue(f.Host, true);
                var sequence = (IEnumerator)typeof(SingleBattleHost).GetMethod("RunPresentation", flags).Invoke(f.Host,
                    new object[] { typeof(SingleBattleHost).GetMethod("Present", flags).Invoke(f.Host, new object[] { result }) });
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var prepare = NextPending(f, sequence);
                        Assert.That(probes[f.Host.Enemies[i]].Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.PrepareAttack));
                        Assert.That(f.Player.Unit.Health, Is.EqualTo(40 - i * 7));
                        Assert.That(f.Host.SelectPlayer(f.Player), Is.False); prepare.Complete();
                        var attack = NextPending(f, sequence);
                        Assert.That(probes[f.Host.Enemies[i]].Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Attack));
                        Assert.That(f.Player.Unit.Health, Is.EqualTo(40 - (i + 1) * 7)); attack.Complete();
                        var hit = NextPending(f, sequence); Assert.That(probes[f.Player].Cues.Last().Kind, Is.EqualTo(BattlePresentationKind.Hit)); hit.Complete();
                    }
                    Assert.That(NextPending(f, sequence), Is.Null); Assert.That(f.Host.IsPresenting, Is.False);
                    Assert.That(f.Host.State.Duel.CompletedRounds, Is.EqualTo(1));
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
                Assert.That(f.Player.CanPlay(f.Hand), Is.True); Assert.That(f.Player.CanDiscard(f.Hand), Is.True);
                Assert.That(f.Player.Discard(f.Hand, false), Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(5)); f.Select();
                Assert.That(f.Player.Play(f.Hand, false), Is.EqualTo(1)); Assert.That(f.Player.Turn.Energy, Is.EqualTo(2)); f.Select();
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
                f.Set("playRefillTiming", (int)CardDuelRefillTiming.AfterAction);
                f.Set("discardRefillTiming", (int)CardDuelRefillTiming.NextPlayerTurn);
                f.Host.StartBattle(); f.Select(); f.Player.Play(f.Hand, false);
                Assert.That(f.Hand.Cards.Count, Is.EqualTo(8)); Assert.That(f.Host.PlayerZone(CardZoneKinds.Played).Cards, Is.Empty);
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
