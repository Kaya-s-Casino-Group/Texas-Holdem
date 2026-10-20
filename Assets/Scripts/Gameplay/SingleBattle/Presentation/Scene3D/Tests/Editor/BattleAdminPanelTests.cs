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
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class BattleAdminPanelTests
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
        public void DraftPreviewAndApplySynchronizeLongVitalsGoldRelicsAndAllZones()
        {
            using (var f = new Fixture())
            {
                var before = f.Host.AuthorityState;
                var draft = new BattleAdminDraft(f.Host.Session); var player = draft.Players[0];
                var hero = draft.Units.Single(u => u.Id == player.Id);
                hero.Maximum = "6000000000"; hero.Health = "5000000000"; hero.Block = "3000000000"; hero.Gold = "9000000000";
                var enemy = draft.Units.Single(u => u.Gold == null); enemy.Health = "99"; enemy.Block = "22";
                var moved = player.Zones[CardZoneKinds.Hand][0];
                player.Zones[CardZoneKinds.Hand].RemoveAt(0); player.Zones[CardZoneKinds.Discard].Add(moved);
                var top = player.Zones[CardZoneKinds.Deck][0];
                player.Zones[CardZoneKinds.Deck].RemoveAt(0); top.Effects = CardEffects.Unplayable;
                player.Zones[CardZoneKinds.Hand].Add(top);
                player.Zones[CardZoneKinds.Deck].Insert(0, moved.Copy());
                player.Relics.Clear();
                player.Relics.Add(new BattleAdminDraft.Relic { DefinitionId = "11004", Level = "1", Charges = "2", Growth = "0", Cooldown = "0", Uses = "0" });
                player.Relics.Add(new BattleAdminDraft.Relic { DefinitionId = "10002", Level = "1", Charges = "1", Growth = "0", Cooldown = "0", Uses = "0" });
                Assert.AreEqual(12, f.Host.Engine.AdminRelicCatalog.Count);
                var preview = f.Host.EditBattle(draft, true);
                Assert.IsTrue(preview.Accepted, preview.Error.ToString()); Assert.AreSame(before, f.Host.AuthorityState);
                var result = f.Host.EditBattle(draft, false);
                Assert.IsTrue(result.Accepted, result.Error.ToString());
                Assert.AreEqual(5000000000L, f.Host.Player.DisplayUnit.Health);
                Assert.AreEqual(3000000000L, f.Host.Player.DisplayUnit.Block);
                Assert.AreEqual(9000000000L, result.State.GoldFor(player.Id));
                Assert.AreEqual(99, f.Host.Enemies[0].DisplayUnit.Health);
                CollectionAssert.AreEqual(new[] { "11004", "10002" }, result.State.Duel.Player(player.Id).Relics.Relics.Select(r => r.DefinitionId));
                Assert.AreEqual(2, result.State.Duel.Player(player.Id).Relics.Relics[0].RemainingCharges);
                Assert.AreEqual(moved.DefinitionId, result.State.Table.GetCard(result.State.Zone(CardZoneKinds.Deck, player.Id).Cards.Last()).DefinitionId.Value);
                Assert.AreEqual(CardEffects.Unplayable, result.State.Table.GetCard(new CardInstanceId(top.InstanceId)).Effects);
                CollectionAssert.AreEquivalent(result.State.Zone(CardZoneKinds.Hand, player.Id).Cards.Select(id => id.Value), f.Hand.Cards.Select(c => c.Id));
                CollectionAssert.AreEqual(before.Random.StateBytes, result.State.Random.StateBytes);
                Assert.AreEqual(before.FlowFor(player.Id).ActionsRemaining, result.State.FlowFor(player.Id).ActionsRemaining);
                Assert.AreEqual(before.Duel.Player(player.Id).Energy.Energy, result.State.Duel.Player(player.Id).Energy.Energy);
            }
        }

        [Test]
        public void StaleDraftAndDraftFromPreviousSessionCannotOverwriteCurrentBattle()
        {
            using (var f = new Fixture())
            {
                var draft = new BattleAdminDraft(f.Host.Session);
                Assert.IsTrue(f.Host.EditBattle(new BattleAdminDraft(f.Host.Session), false).Accepted);
                var current = f.Host.AuthorityState;
                Assert.Throws<InvalidOperationException>(() => f.Host.EditBattle(draft, false));
                Assert.AreSame(current, f.Host.AuthorityState);
                draft = new BattleAdminDraft(f.Host.Session);
                f.Host.RestartBattle(); f.HandView.CompleteTransitions(); current = f.Host.AuthorityState;
                Assert.Throws<InvalidOperationException>(() => f.Host.EditBattle(draft, false));
                Assert.AreSame(current, f.Host.AuthorityState);
            }
        }

        [Test]
        public void InvalidNumbersHealthAndDuplicateCardsAreRejectedAtomically()
        {
            using (var f = new Fixture())
            {
                var before = f.Host.AuthorityState; var draft = new BattleAdminDraft(f.Host.Session);
                draft.Units[0].Health = "9223372036854775808";
                Assert.Throws<ArgumentException>(() => f.Host.EditBattle(draft, false)); Assert.AreSame(before, f.Host.AuthorityState);
                draft = new BattleAdminDraft(f.Host.Session); draft.Units[0].Health = "999999999";
                Assert.IsFalse(f.Host.EditBattle(draft, false).Accepted); Assert.AreSame(before, f.Host.AuthorityState);
                draft = new BattleAdminDraft(f.Host.Session); var player = draft.Players[0];
                player.Zones[CardZoneKinds.Discard].Add(player.Zones[CardZoneKinds.Hand][0]);
                Assert.IsFalse(f.Host.EditBattle(draft, false).Accepted); Assert.AreSame(before, f.Host.AuthorityState);
            }
        }

        [Test]
        public void PresentationAndFinishedBattleBlockEditsAndDirectDeathDoesNotConsumeRevival()
        {
            using (var f = new Fixture())
            {
                f.Host.StartPresentationRoutine = routine => () => (routine as IDisposable)?.Dispose();
                Assert.IsTrue(f.Host.UseItem(f.Id("10002"), animate: true).Accepted);
                Assert.IsTrue(f.Host.IsPresenting);
                Assert.Throws<InvalidOperationException>(() => f.Host.EditBattle(new BattleAdminDraft(f.Host.Session), false));
                f.Host.SnapToState();
                var draft = new BattleAdminDraft(f.Host.Session);
                draft.Units.Single(u => u.Gold != null).Health = "0";
                Assert.IsTrue(f.Host.EditBattle(draft, false).Accepted);
                Assert.IsTrue(f.Host.AuthorityState.Duel.IsFinished);
                Assert.NotNull(f.Item(f.Id("13004")), "A direct state correction must not execute revival.");
                Assert.IsFalse(f.Host.CanEditBattle);
                Assert.Throws<InvalidOperationException>(() => f.Host.EditBattle(new BattleAdminDraft(f.Host.Session), true));
            }
        }

        [Test]
        public void PanelPreviewIsReadOnlyApplyUpdatesHudAndCloseDiscardsDraft()
        {
            using (var f = new Fixture())
            {
                var root = new GameObject("Administrator UI test", typeof(RectTransform), typeof(Canvas));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                SceneManager.MoveGameObjectToScene(root, f.Host.gameObject.scene);
                var panel = root.AddComponent<BattleAdminPanel>();
                panel.Configure(f.Host, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansCJK/TMP/NotoSansCJKSCRegular.asset"));
                panel.Open(); Assert.IsTrue(panel.IsOpen);
                var overlay = (RectTransform)root.transform.Find("Runtime Battle Administrator");
                Assert.AreEqual(Vector2.zero, overlay.anchorMin);
                Assert.AreEqual(Vector2.one, overlay.anchorMax);
                foreach (var tab in new[] { "遗物", "手牌", "牌库", "弃牌堆", "血量 / 护盾 / 金币" })
                    root.GetComponentsInChildren<Button>().Single(b => b.name == tab).onClick.Invoke();
                var before = f.Host.AuthorityState;
                panel.Draft.Units.Single(u => u.Gold != null).Gold = "888";
                Assert.IsTrue(panel.Submit(true), panel.Feedback); Assert.AreSame(before, f.Host.AuthorityState);
                Assert.IsTrue(panel.Submit(false), panel.Feedback); Assert.AreEqual(888, f.Host.AuthorityState.GoldFor(f.Host.Player.ParticipantId));
                panel.Draft.Units.Single(u => u.Gold != null).Gold = "999";
                panel.Close(); Assert.IsFalse(panel.IsOpen); panel.Open();
                Assert.AreEqual("888", panel.Draft.Units.Single(u => u.Gold != null).Gold);
                panel.Close();
            }
        }
    }
}
