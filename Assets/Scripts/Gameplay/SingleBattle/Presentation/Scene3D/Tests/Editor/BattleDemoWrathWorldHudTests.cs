using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using NUnit.Framework;
using Poker.SingleBattle.Editor;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleDemoWrathWorldHudTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly string copy;
            private readonly UnityEngine.SceneManagement.Scene scene;
            private TextAsset bindingFixture;
            private Transform worldRoot;
            public readonly SingleBattleSceneAdapter Host;
            public readonly BattleDemoEnemyWorldHud World;

            public Fixture(bool wrath = true, bool suppressBinding = false, bool concept = false)
            {
                copy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/WrathWorldHudTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset(WrathBossSetup.SourceScenePath, copy));
                scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
                Host = WrathBossSetup.ConfigureScene(scene);
                var content = AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml");
                Assert.NotNull(content);
                if (suppressBinding)
                {
                    // Keep player windows observable without binding automatically advancing the session.
                    var xml = XDocument.Parse(content.text);
                    xml.Root.Element("Enemies").Elements("Enemy")
                        .Single(enemy => (string)enemy.Attribute("id") == "wrath-binding")
                        .Descendants("Effects").First().ReplaceNodes(XElement.Parse(
                            "<Effect id='wait' kind='GainBlock' amount='0' target='Self'/>"));
                    bindingFixture = new TextAsset(xml.ToString()); content = bindingFixture;
                }
                if (!wrath) Host.ConfigureActors(Host.Players.ToArray(), Host.Enemies.Take(1).ToArray());
                Host.ConfigureContent(content, wrath ? "single-chapter-wrath" : "single-chapter-battle-1");
                Host.StartBattle();
                World = Host.GetComponent<BattleDemoEnemyWorldHud>();
                Assert.NotNull(World, "The authored remote scene owns its world HUD.");
                World.ConfigureConceptLayout(concept);
                Invoke(World, "Start");
                worldRoot = scene.GetRootGameObjects().Single(root => root.name == "BattleDemo Enemy World UI").transform;
                Edit(new BattleAdminPatch(vitals: new[] {
                    new BattleAdminVitals(Host.Player.ParticipantId, health: 60000, maxHealth: 60000) }));
            }

            public Transform View(string id) => worldRoot.Find("Enemy World HUD " +
                Host.Enemies.Single(actor => actor.DisplayId == id).name);
            public BattleDemoEnemyBadgeTooltip[] Intents(string id) => View(id).Find("Intents")
                .GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>();
            public void Edit(BattleAdminPatch patch, bool refreshWorld = true)
            {
                var result = Host.Session.ApplyAdmin(Host.AuthorityState.Revision, patch);
                Assert.IsTrue(result.Accepted, result.Error.ToString());
                SynchronizeHost();
                if (refreshWorld) RefreshWorldOnly();
            }
            public void Round()
            {
                var player = Host.Player.ParticipantId;
                var result = Host.Session.Execute(Host.AuthorityState.Revision, player, CardDuelAction.EndTurn);
                Assert.IsTrue(result.Accepted, result.Error.ToString());
                if (Host.AuthorityState.FlowFor(player).Window == BattlePlayerWindow.Ending)
                {
                    result = Host.Session.Execute(Host.AuthorityState.Revision, player, CardDuelAction.FinishTurn);
                    Assert.IsTrue(result.Accepted, result.Error.ToString());
                }
                Refresh();
            }
            public void Refresh()
            {
                SynchronizeHost(); RefreshWorldOnly();
            }
            private void SynchronizeHost()
            {
                Host.SynchronizeBattle();
                Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
            }
            public void RefreshWorldOnly()
            {
                Invoke(World, "Refresh"); Invoke(World, "LateUpdate");
            }
            public void Dispose()
            {
                if (Host != null) { Host.SnapToState(); Host.Actions.Hand.StopSession(); }
                if (worldRoot != null) UnityEngine.Object.DestroyImmediate(worldRoot.gameObject);
                EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(copy);
                if (bindingFixture != null) UnityEngine.Object.DestroyImmediate(bindingFixture);
            }
            private static void Invoke(object target, string method) => target.GetType()
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        [Test]
        public void WrathShellUsesTheExistingImmunityBadgeWithItsRemainingHitCount()
        {
            var badge = BattleDemoEnemyHudModel.Status("wrath-shell", 2);
            Assert.AreEqual(BattleDemoEnemySymbol.Immunity, badge.Symbol);
            Assert.AreEqual("2", badge.Value);
            StringAssert.Contains("伤害免疫", badge.Description);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnemyHudKeepsScreenSizeWhenArtworkMovesFurtherFromCamera(bool concept)
        {
            using (var fixture = new Fixture(wrath: false, concept: concept))
            {
                var actor = fixture.Host.Enemies.Single();
                var view = (RectTransform)fixture.View(actor.DisplayId);
                var canvas = view.GetComponentInParent<Canvas>();
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                var camera = (Camera)typeof(BattleDemoEnemyWorldHud).GetField("viewCamera", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fixture.World);
                camera.orthographic = false;
                fixture.RefreshWorldOnly();
                var health = view.Find("Health Bar").GetComponent<RectTransform>();
                var corners = new Vector3[4]; health.GetWorldCorners(corners);
                float originalWidth = Vector3.Distance(corners[0], corners[3]);
                float originalFont = health.GetComponentInChildren<TMP_Text>().fontSize;
                actor.transform.position += camera.transform.forward * 5;
                fixture.RefreshWorldOnly(); health.GetWorldCorners(corners);
                Assert.That(Vector3.Distance(corners[0], corners[3]), Is.EqualTo(originalWidth).Within(.001f));
                Assert.AreEqual(originalFont, health.GetComponentInChildren<TMP_Text>().fontSize);
                Assert.AreEqual(Vector3.one, view.localScale);
                Assert.AreEqual(1, view.GetComponent<CanvasGroup>().alpha);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SeveredHandShowsOnlyCountdownAndRestoresHudAfterRevival(bool concept)
        {
            using (var fixture = new Fixture(concept: concept))
            {
                var actor = fixture.Host.Enemies.Single(enemy => enemy.DisplayId == "hand-1");
                var position = actor.transform.position;
                var view = fixture.View("hand-1");
                fixture.Edit(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(actor.ParticipantId, health: 0) }));
                Assert.IsTrue(view.gameObject.activeSelf, "A regenerating hand keeps its existing remote world UI.");
                Assert.AreEqual(8, fixture.Host.Display.Resource("hand-1", "wrath.reviveRemaining").Value);
                Assert.AreEqual("复活倒计时 8", view.Find("Revive Countdown").GetComponent<TMP_Text>().text);
                Assert.IsFalse(view.Find("Intents").gameObject.activeSelf);
                Assert.IsTrue(view.Find("Revive Countdown").gameObject.activeInHierarchy);
                Assert.IsFalse(view.Find("Health Bar").gameObject.activeSelf);
                Assert.IsFalse(view.Find("Statuses").gameObject.activeSelf);
                Assert.IsFalse(view.Find("Current Block").gameObject.activeSelf);
                Assert.AreEqual(6, view.parent.childCount, "The countdown reuses an existing enemy view.");
                fixture.Round();
                Assert.AreSame(view, fixture.View("hand-1"));
                Assert.AreEqual("复活倒计时 7", view.Find("Revive Countdown").GetComponent<TMP_Text>().text);
                for (int round = 1; round < 8; round++) fixture.Round();
                Assert.IsTrue(fixture.Host.Display.Unit("hand-1").IsAlive);
                Assert.IsTrue(view.Find("Health Bar").gameObject.activeInHierarchy);
                Assert.IsTrue(view.Find("Intents").gameObject.activeInHierarchy);
                Assert.IsFalse(view.Find("Revive Countdown").gameObject.activeSelf);
                Assert.Less(Vector3.Distance(position, actor.transform.position), .00001f);
            }
        }

        [Test]
        public void OrdinaryDeadEnemyStillHidesItsEntireWorldView()
        {
            using (var fixture = new Fixture(wrath: false))
            {
                var actor = fixture.Host.Enemies.Single();
                var view = fixture.View(actor.DisplayId);
                Assert.IsTrue(view.gameObject.activeSelf);
                fixture.Edit(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(actor.ParticipantId, health: 0) }));
                Assert.IsFalse(view.gameObject.activeSelf);
            }
        }

        [Test]
        public void WrathChargeUsesAnExistingBuffBadgeInsteadOfClaimingZeroShield()
        {
            using (var fixture = new Fixture())
            {
                for (int round = 0; round < 3; round++) fixture.Round();
                Assert.AreEqual(3, fixture.Host.Display.CompletedRounds);
                var badge = fixture.Intents("enemy").Single();
                Assert.AreEqual(BattleDemoEnemySymbol.Buff, badge.GetComponentInChildren<BattleDemoEnemyIcon>().Symbol);
                Assert.AreEqual("+10", badge.GetComponentInChildren<TMP_Text>().text);
                StringAssert.Contains("蓄力", badge.Description);
                StringAssert.DoesNotContain("护盾", badge.Description);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UltimateWarningKeepsDamageForecastWithoutAnExtraAttackExclamationBadge(bool concept)
        {
            using (var fixture = new Fixture(suppressBinding: true, concept: concept))
            {
                fixture.Round();
                Assert.AreEqual(1, fixture.Host.Display.CompletedRounds);
                Assert.AreEqual(1, fixture.Host.Display.Resource("enemy", "wrath.warning").Value);
                var intents = fixture.Intents("enemy");
                Assert.IsFalse(intents.Any(badge => badge.Description.Contains("大招预警")),
                    "The timed warning and artwork glow own the warning, not a third attack plate.");
                Assert.IsTrue(intents.Any(badge => badge.Description.Contains("33")), "Keep the regular damage forecast.");
                Assert.IsTrue(intents.Any(badge => badge.Description.Contains("99")), "Keep the actual ultimate damage forecast.");
                fixture.Edit(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(new ParticipantId("hand-1"), health: 0) }));
                Assert.AreEqual(0, fixture.Host.Display.Resource("enemy", "wrath.warning").Value);
                Assert.IsFalse(fixture.Intents("enemy").Any(badge => badge.Description.Contains("大招预警")));
                Assert.IsFalse(fixture.Intents("enemy").Any(badge => badge.Description.Contains("99")),
                    "Interrupting the ultimate must also remove its damage forecast.");
            }
        }

        [Test]
        public void BindingWaitKeepsItsExistingZeroBlockBadge()
        {
            using (var fixture = new Fixture())
            {
                var badge = fixture.Intents("hand-5").Single();
                Assert.AreEqual(BattleDemoEnemySymbol.Shield, badge.GetComponentInChildren<BattleDemoEnemyIcon>().Symbol);
                Assert.AreEqual("0", badge.GetComponentInChildren<TMP_Text>().text);
                Assert.AreEqual("防御：获得 0 护盾", badge.Description);
            }
        }

        [Test]
        public void SeveredHandClosesItsOpenIntentTooltip()
        {
            using (var fixture = new Fixture())
            {
                var badge = fixture.Intents("hand-1").Single();
                var tooltip = fixture.View("hand-1").Find("Enemy Badge Tooltip");
                badge.OnPointerEnter(null);
                Assert.IsTrue(tooltip.gameObject.activeSelf);
                fixture.Edit(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(new ParticipantId("hand-1"), health: 0) }));
                Assert.IsFalse(tooltip.gameObject.activeSelf);
                Assert.IsFalse(fixture.View("hand-1").Find("Intents").gameObject.activeSelf);
            }
        }

        [Test]
        public void UnchangedAttackTooltipSurvivesWorldHudRefreshAndChangesToAnotherBadge()
        {
            using (var fixture = new Fixture(wrath: false))
            {
                var actor = fixture.Host.Enemies.Single();
                // Existing ordinary previews include the enemy-turn ClearBlock before its attack
                // when it currently has block. Establish that final row before starting hover.
                fixture.Edit(new BattleAdminPatch(vitals: new[] {
                    new BattleAdminVitals(actor.ParticipantId, block: 1) }));
                var badge = fixture.Intents(actor.DisplayId).Single(item => item.Description == "预计伤害：15（格挡前）");
                var tooltip = fixture.View(actor.DisplayId).Find("Enemy Badge Tooltip");
                badge.OnPointerEnter(null);
                fixture.RefreshWorldOnly();
                Assert.IsTrue(tooltip.gameObject.activeSelf, "An unchanged refresh must not flicker the hovered explanation.");
                var block = fixture.View(actor.DisplayId).Find("Current Block");
                var blockTooltip = block.GetComponent<BattleDemoEnemyBadgeTooltip>();
                fixture.Edit(new BattleAdminPatch(vitals: new[] {
                    new BattleAdminVitals(actor.ParticipantId, block: 10) }), refreshWorld: false);
                Assert.AreEqual("1", block.GetComponentInChildren<TMP_Text>().text,
                    "The world HUD has not rendered the new block yet.");
                // Start hover after host recovery so only the WorldHud update is under test.
                badge.OnPointerEnter(null);
                fixture.RefreshWorldOnly();
                Assert.IsTrue(block.gameObject.activeSelf);
                Assert.AreEqual("10", block.GetComponentInChildren<TMP_Text>().text);
                Assert.AreEqual("当前护盾：10", blockTooltip.Description);
                Assert.IsTrue(tooltip.gameObject.activeSelf, "A shield change must not close the unchanged attack explanation.");
                blockTooltip.OnPointerExit(null);
                Assert.IsTrue(tooltip.gameObject.activeSelf, "Another badge's pointer exit must not close the hovered explanation.");
                Assert.AreEqual("预计伤害：15（格挡前）", tooltip.GetComponentInChildren<TMP_Text>().text);
            }
        }

        [Test]
        public void OrdinaryAttackKeepsItsExistingSwordValueAndDescription()
        {
            using (var fixture = new Fixture(wrath: false))
            {
                var actor = fixture.Host.Enemies.Single();
                var badge = fixture.Intents(actor.DisplayId).Single();
                Assert.AreEqual(BattleDemoEnemySymbol.Sword, badge.GetComponentInChildren<BattleDemoEnemyIcon>().Symbol);
                Assert.AreEqual("15", badge.GetComponentInChildren<TMP_Text>().text);
                Assert.AreEqual("预计伤害：15（格挡前）", badge.Description);
                Assert.AreEqual(BattleDemoEnemyHudModel.AttackColor, badge.GetComponentInChildren<BattleDemoEnemyIcon>().color);
            }
        }
    }
}
