using System;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CardPresentation;
using NUnit.Framework;
using Poker.SingleBattle.Editor;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleTargetImmunityReadabilityTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly string copy;
            private readonly UnityEngine.SceneManagement.Scene scene;
            private readonly TextAsset content;
            private readonly Transform worldRoot;
            public readonly SingleBattleSceneAdapter Host;
            public readonly BattleDemoEnemyWorldHud World;
            public readonly BattleDemoTargetSelection Selector;

            public Fixture(bool concept = true)
            {
                copy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/TargetImmunityReadabilityTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset(WrathBossSetup.SourceScenePath, copy));
                scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
                Host = WrathBossSetup.ConfigureScene(scene);
                var xml = XDocument.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml").text);
                xml.Root.Element("Enemies").Elements("Enemy").Single(enemy => (string)enemy.Attribute("id") == "wrath-binding")
                    .Descendants("Effects").First().ReplaceNodes(XElement.Parse("<Effect id='wait' kind='GainBlock' amount='0' target='Self'/>"));
                content = new TextAsset(xml.ToString());
                Host.ConfigureContent(content, "single-chapter-wrath");
                Host.StartBattle();
                World = Host.GetComponent<BattleDemoEnemyWorldHud>();
                World.ConfigureConceptLayout(concept);
                Invoke(World, "Start");
                worldRoot = scene.GetRootGameObjects().Single(root => root.name == "BattleDemo Enemy World UI").transform;
                Selector = Host.GetComponent<BattleDemoTargetSelection>();
                Invoke(Selector, "Start");
                Edit(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(Host.Player.ParticipantId, health: 60000, maxHealth: 60000) }));
            }

            public Transform View(string id) => worldRoot.Find("Enemy World HUD " + Host.Enemies.Single(actor => actor.DisplayId == id).name);
            public TMP_Text Health(string id) => View(id).Find("Health Bar/Health Value").GetComponent<TMP_Text>();
            public MeshRenderer Contour(string id) => Host.Enemies.Single(actor => actor.DisplayId == id).Visual
                .GetComponentsInChildren<MeshRenderer>(true).Single(renderer => renderer.name == "Selected enemy contour");
            public BattleDemoEnemyBadgeTooltip Shell() => View("enemy").Find("Statuses")
                .GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().SingleOrDefault(badge => badge.Description.Contains("免伤剩余"));
            public void Edit(BattleAdminPatch patch)
            {
                var result = Host.Session.ApplyAdmin(Host.AuthorityState.Revision, patch);
                Assert.IsTrue(result.Accepted, result.Error.ToString()); Refresh();
            }
            public void Cards(params string[] definitions)
            {
                Edit(new BattleAdminPatch(zones: new[] { new BattleAdminZone(Host.Player.ParticipantId, CardZoneKinds.Hand,
                    definitions.Select(id => new BattleAdminCard(definitionId: id))) }));
                foreach (var card in Host.Actions.Hand.Cards.ToArray())
                    Host.Actions.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: true));
                Host.Actions.Hand.Publish(); Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                Host.RefreshCandidate(); Render();
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
            public void HitBody()
            {
                Cards("standard:1:2");
                var result = Host.Session.Execute(Host.AuthorityState.Revision, Host.Player.ParticipantId, CardDuelAction.Play,
                    Host.Session.State.Zone(CardZoneKinds.Hand, Host.Player.ParticipantId).Cards, new[] { new ParticipantId("enemy") });
                Assert.IsTrue(result.Accepted, result.Error.ToString()); Refresh();
            }
            public void Refresh()
            {
                Host.SynchronizeBattle(); Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                Host.RefreshCandidate(); Render();
            }
            public void Render() { Invoke(Selector, "LateUpdate"); Invoke(World, "Refresh"); Invoke(World, "LateUpdate"); }
            public void Dispose()
            {
                Host.SnapToState(); Host.Actions.Hand.StopSession();
                // Scene-close invokes runtime OnDestroy in EditMode; dispose our generated views immediately first.
                foreach (var rect in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(rect => rect.name.StartsWith("Target ", StringComparison.Ordinal)).ToArray())
                    UnityEngine.Object.DestroyImmediate(rect.gameObject);
                foreach (var contour in Host.Enemies.SelectMany(actor => actor.Visual.GetComponentsInChildren<MeshRenderer>(true))
                    .Where(renderer => renderer.name == "Selected enemy contour").ToArray())
                    UnityEngine.Object.DestroyImmediate(contour.gameObject);
                if (worldRoot != null) UnityEngine.Object.DestroyImmediate(worldRoot.gameObject);
                EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(copy);
                UnityEngine.Object.DestroyImmediate(content);
            }
        }

        // Catches the ambiguous stack-only label and a shield projection that fails to follow real per-hit consumption.
        [TestCase(false)]
        [TestCase(true)]
        public void BodyShellStatesShowRemainingDamageInstancesAndBodyOnlyExplanation(bool concept)
        {
            using (var fixture = new Fixture(concept))
            {
                Assert.IsNull(fixture.Shell(), "A fresh encounter must not reserve a zero-immunity badge.");
                fixture.Round(); fixture.Round(); fixture.Round();
                Assert.AreEqual(2, fixture.Host.Display.Unit("enemy").Statuses.Single(status => status.Id == "wrath-shell").Stacks);
                AssertShell(fixture, 2);
                fixture.HitBody();
                Assert.AreEqual(1, fixture.Host.Display.Unit("enemy").Statuses.Single(status => status.Id == "wrath-shell").Stacks);
                AssertShell(fixture, 1);
                fixture.HitBody();
                Assert.IsFalse(fixture.Host.Display.Unit("enemy").Statuses.Any(status => status.Id == "wrath-shell"));
                AssertShell(fixture, 0);
            }
        }

        // Catches a click that changes authority but leaves the player unable to identify the selected hand.
        [Test]
        public void SelectedHandUsesItsOwnAlphaContourAndHealthTextContainsOnlyVitals()
        {
            using (var fixture = new Fixture())
            {
                fixture.Cards("standard:1:2");
                var hand = fixture.Host.Enemies.Single(actor => actor.DisplayId == "hand-1");
                Assert.IsTrue(fixture.Selector.ChooseTarget(hand)); fixture.Render();
                Assert.AreEqual("hand-1", fixture.Host.PlayTargetChoices.Single().SelectedId);
                var contour = fixture.Contour("hand-1"); Assert.IsTrue(contour.enabled);
                var properties = new MaterialPropertyBlock(); contour.GetPropertyBlock(properties);
                Assert.AreSame(fixture.Host.GetComponent<WrathBossArtworkLayout>().PartFor(hand).Texture, properties.GetTexture("_MainTex"));
                var tint = properties.GetColor("_OutlineColor");
                Assert.That(tint.g, Is.GreaterThan(.6f), "Target selection uses gold/cyan; full-body red remains the ultimate warning.");
                StringAssert.DoesNotContain("目标", fixture.Health("hand-1").text);
                Assert.AreEqual("1 / 1", fixture.Health("hand-1").text);
                var health = fixture.Health("hand-1");
                Assert.That(health.GetPreferredValues(health.text).x, Is.LessThanOrEqualTo(health.rectTransform.rect.width),
                    "The existing health-text rectangle must contain the HP.");
                Assert.IsFalse(fixture.Host.GetComponent<WrathBossArtworkLayout>().WarningVisible);
            }
        }

        // Catches stale single-hand selection surviving a switch to an automatically targeted group attack.
        [Test]
        public void GroupAttackHighlightsResolvedLivingTargetsAndClearsSingleTargetName()
        {
            using (var fixture = new Fixture())
            {
                fixture.Cards("standard:1:2");
                Assert.IsTrue(fixture.Selector.ChooseTarget(fixture.Host.Enemies.Single(actor => actor.DisplayId == "hand-1")));
                fixture.Render();
                fixture.Cards("standard:1:7", "standard:2:7", "standard:3:7");
                Assert.IsEmpty(fixture.Host.PlayTargetChoices);
                Assert.IsFalse(fixture.Selector.ChooseTarget(fixture.Host.Enemies[0]));
                foreach (var actor in fixture.Host.Enemies.Where(actor => actor.IsAlive))
                {
                    Assert.IsTrue(fixture.Contour(actor.DisplayId).enabled, "The real group preview reaches " + actor.DisplayId);
                    StringAssert.DoesNotContain("目标：", fixture.Health(actor.DisplayId).text);
                }
            }
        }

        // Catches presentation retaining a dead hand when the rule choices reconcile to the only living body.
        [Test]
        public void LastLivingBodyReplacesDeadHandHighlightWithoutOverridingRuleSelection()
        {
            using (var fixture = new Fixture())
            {
                fixture.Cards("standard:1:2");
                Assert.IsTrue(fixture.Selector.ChooseTarget(fixture.Host.Enemies.Single(actor => actor.DisplayId == "hand-1")));
                fixture.Render();
                fixture.Edit(new BattleAdminPatch(vitals: fixture.Host.Enemies.Where(actor => actor.DisplayId != "enemy")
                    .Select(actor => new BattleAdminVitals(actor.ParticipantId, health: 0))));
                Assert.AreEqual("enemy", fixture.Host.PlayTargetChoices.Single().SelectedId);
                Assert.IsFalse(fixture.Contour("hand-1").enabled);
                Assert.IsTrue(fixture.Contour("enemy").enabled);
                StringAssert.DoesNotContain("目标", fixture.Health("enemy").text);
                foreach (var card in fixture.Host.Actions.Hand.Cards.ToArray())
                    fixture.Host.Actions.Hand.SetInteraction(card.Id, new CardInteractionState(isSelected: false));
                fixture.Host.Actions.Hand.Publish(); fixture.Host.RefreshCandidate(); fixture.Render();
                StringAssert.DoesNotContain("目标", fixture.Health("enemy").text);
                StringAssert.Contains("2000 / 2000", fixture.Health("enemy").text);
            }
        }

        private static void AssertShell(Fixture fixture, int remaining)
        {
            var badge = fixture.Shell(); Assert.NotNull(badge, "Render the current remaining-immunity count.");
            StringAssert.Contains("免伤剩余" + remaining + "次", badge.GetComponentInChildren<TMP_Text>().text.Replace(" ", ""));
            StringAssert.Contains("每段伤害", badge.Description); StringAssert.Contains("本体", badge.Description);
            StringAssert.Contains("不保护手", badge.Description);
            StringAssert.DoesNotContain("×", badge.Description);
        }
        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
