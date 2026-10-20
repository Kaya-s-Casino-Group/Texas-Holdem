using System;
using System.Collections.Generic;
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

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class BattleActiveItemIntegrationTests
    {
        private const string StandardPath = "Packages/com.kaya.battle-engine/Content/StandardSingleBattle-2026-10-21.xml";
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            public readonly SingleBattleSceneAdapter Host;
            public readonly CardZonePlayHost Hand;
            public readonly HandZoneView HandView;
            private readonly Dictionary<string, string> itemIds;
            public Fixture()
            {
                var root = New("Active item host");
                var match = root.AddComponent<CardMatchHost>(); match.ConfigureRuntimeManaged();
                var hand = New("Hand"); HandView = hand.AddComponent<HandZoneView>();
                HandView.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                Hand = hand.AddComponent<CardZonePlayHost>(); Hand.ConfigureRuntimeBinding(match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab"), scene);
                var panel = ui.GetComponent<HandActionPanelView>();
                panel.Configure(panel.PlayButton, panel.DiscardButton, panel.RankSortButton, panel.SuitSortButton,
                    panel.PlayButton.GetComponentInChildren<TMP_Text>(), panel.DiscardButton.GetComponentInChildren<TMP_Text>(),
                    panel.RankSortButton.GetComponentInChildren<TMP_Text>(), panel.SuitSortButton.GetComponentInChildren<TMP_Text>());
                var actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); actions.Configure(Hand, panel);
                Host = root.AddComponent<SingleBattleSceneAdapter>(); Host.Configure(match, actions, null, null);
                var player = New("Player").AddComponent<PlayerBattleActor>(); player.Configure(Host);
                var enemy = New("Enemy").AddComponent<EnemyBattleActor>(); enemy.Configure(Host);
                Host.ConfigureActors(new[] { player }, new[] { enemy });
                var xml = AssetDatabase.LoadAssetAtPath<TextAsset>(StandardPath);
                Assert.NotNull(xml, "The Unity package must expose the date-versioned standard XML.");
                Host.ConfigureContent(xml, "standard-item-lab");
                Editor.BattlePresentationSceneSetup.ConfigureHost(Host, includeFeedback: false);
                Host.StartBattle(); HandView.CompleteTransitions();
                itemIds = Host.State.Duel.Player(Host.Player.ParticipantId).Relics.Relics
                    .GroupBy(item => item.DefinitionId).ToDictionary(group => group.Key, group => group.First().InstanceId);
            }
            private GameObject New(string name)
            { var value = new GameObject(name); SceneManager.MoveGameObjectToScene(value, scene); return value; }
            public string Id(string definitionId) => itemIds[definitionId];
            public BattleRelicState Item(string instanceId) =>
                Host.AuthorityState.Duel.Player(Host.Player.ParticipantId).Relics.Relics.SingleOrDefault(item => item.InstanceId == instanceId);
            public void Dispose()
            {
                Host.SnapToState(); Host.StartPresentationRoutine = null;
                Host.enabled = false; Hand.StopSession(); EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void DateStandardLoadsTheCurrentActionWindowAndChineseItemNames()
        {
            using (var f = new Fixture())
            {
                Assert.AreEqual(StandardPath, AssetDatabase.GetAssetPath(f.Host.ContentXml));
                Assert.AreEqual("standard-item-lab", f.Host.ContentEncounterId);
                Assert.AreEqual(BattlePlayerWindow.Action, f.Host.State.FlowFor(f.Host.Player.ParticipantId).Window);
                Assert.AreEqual(8, f.Hand.Cards.Count);
                Assert.AreEqual("急救药水", f.Host.ItemName("10002"));
                Assert.NotNull(BattleRelicArt.Get("10002"), "The potion icon must load through the runtime Resources mapping.");
                Assert.AreEqual("一串手雷", f.Host.ItemName("11004"));
                Assert.AreEqual("俄罗斯转盘", f.Host.ItemName("15001"));
                Assert.AreEqual("unknown-item", f.Host.ItemName("unknown-item"));
                Assert.AreEqual("", f.Host.ItemName(null));
                Assert.IsTrue(f.Host.CanUseItem(f.Id("10002")));
                Assert.IsFalse(f.Host.CanUseItem(f.Id("12001")), "A passive relic must not acquire an active-use button.");
            }
        }

        [Test]
        public void PotionPreviewIsReadOnlyAndUseHealsOnceWithoutSpendingActionPoints()
        {
            using (var f = new Fixture())
            {
                var actor = f.Host.Player.ParticipantId;
                var before = f.Host.AuthorityState;
                var preview = f.Host.PreviewItem(f.Id("10002"));
                Assert.IsTrue(preview.Accepted); Assert.AreSame(before, f.Host.AuthorityState);
                Assert.AreEqual(1, f.Item(f.Id("10002")).RemainingCharges);
                var result = f.Host.UseItem(f.Id("10002"), animate: false);
                Assert.IsTrue(result.Accepted);
                Assert.AreEqual(before.Duel.Combat.Get(actor).Health + 10, result.State.Duel.Combat.Get(actor).Health);
                Assert.AreEqual(preview.State.Duel.Combat.Get(actor).Health, f.Host.Player.DisplayUnit.Health);
                Assert.AreEqual(before.Duel.Player(actor).Energy.Energy, result.State.Duel.Player(actor).Energy.Energy);
                Assert.AreEqual(before.FlowFor(actor).ActionsRemaining, result.State.FlowFor(actor).ActionsRemaining);
                CollectionAssert.AreEqual(before.Zone(CardZoneKinds.Hand, actor).Cards, result.State.Zone(CardZoneKinds.Hand, actor).Cards);
                Assert.IsNull(f.Item(f.Id("10002"))); Assert.IsFalse(f.Host.CanUseItem(f.Id("10002")));
                var after = f.Host.AuthorityState;
                Assert.IsFalse(f.Host.UseItem(f.Id("10002"), animate: false).Accepted);
                Assert.AreSame(after, f.Host.AuthorityState);
            }
        }

        [Test]
        public void GrenadeSpendsItsOwnThreeChargesAndNeverCardActionPoints()
        {
            using (var f = new Fixture())
            {
                var actor = f.Host.Player.ParticipantId;
                var before = f.Host.AuthorityState;
                long health = f.Host.Enemy.Unit.Health;
                for (int use = 1; use <= 3; use++)
                {
                    Assert.IsTrue(f.Host.CanUseItem(f.Id("11004")));
                    var result = f.Host.UseItem(f.Id("11004"), animate: false);
                    Assert.IsTrue(result.Accepted);
                    Assert.AreEqual(health - 20 * use, f.Host.Enemy.Unit.Health);
                    Assert.AreEqual(before.Duel.Player(actor).Energy.Energy, result.State.Duel.Player(actor).Energy.Energy);
                    Assert.AreEqual(before.FlowFor(actor).ActionsRemaining, result.State.FlowFor(actor).ActionsRemaining);
                    if (use < 3) Assert.AreEqual(3 - use, f.Item(f.Id("11004")).RemainingCharges);
                }
                Assert.IsNull(f.Item(f.Id("11004"))); Assert.IsFalse(f.Host.CanUseItem(f.Id("11004")));
                Assert.AreEqual(before.Duel.CompletedRounds, f.Host.State.Duel.CompletedRounds);
                CollectionAssert.AreEqual(before.Zone(CardZoneKinds.Hand, actor).Cards, f.Host.State.Zone(CardZoneKinds.Hand, actor).Cards);
            }
        }

        [Test]
        public void AnActiveItemAnimationLocksEveryFurtherItemUntilTheDisplaySynchronizes()
        {
            using (var f = new Fixture())
            {
                var visual = new GameObject("Held item hit"); visual.transform.SetParent(f.Host.Enemy.transform, false);
                var presenter = visual.AddComponent<ManualBattlePresentation>();
                f.Host.Enemy.ConfigurePresentation(presenter);
                // The real director starts the hit immediately; this scheduler deliberately leaves its tail pending.
                f.Host.StartPresentationRoutine = routine => () => (routine as IDisposable)?.Dispose();
                var grenade = f.Host.UseItem(f.Id("11004"));
                Assert.IsTrue(grenade.Accepted);
                Assert.IsTrue(f.Host.IsPresenting);
                Assert.IsTrue(presenter.Operations.Any(operation => operation.keepWaiting));
                var committed = f.Host.AuthorityState;
                Assert.IsFalse(f.Host.CanUseItem(f.Id("10002"))); Assert.IsNull(f.Host.PreviewItem(f.Id("10002")));
                var blocked = f.Host.UseItem(f.Id("10002"));
                Assert.IsNull(blocked);
                Assert.AreSame(committed, f.Host.AuthorityState);
                Assert.AreEqual(1, f.Item(f.Id("10002")).RemainingCharges);
                Assert.AreEqual(2, f.Item(f.Id("11004")).RemainingCharges);
                f.Host.SnapToState(); f.HandView.CompleteTransitions();
                Assert.IsFalse(f.Host.IsPresenting); Assert.IsTrue(f.Host.CanUseItem(f.Id("10002")));
                Assert.IsTrue(f.Host.UseItem(f.Id("10002"), animate: false).Accepted);
            }
        }
    }
}
