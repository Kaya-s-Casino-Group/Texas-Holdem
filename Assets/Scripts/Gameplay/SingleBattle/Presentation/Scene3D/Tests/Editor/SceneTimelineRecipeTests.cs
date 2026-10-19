using System;
using System.Collections.Generic;
using System.Linq;
using CardInteraction;
using CardPresentation;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using NUnit.Framework;
using Poker.SingleBattle.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    [TestFixture]
    public sealed class SceneTimelineRecipeTests
    {
        private sealed class Fixture : IDisposable
        {
            internal readonly Scene Scene = EditorSceneManager.NewPreviewScene();
            internal readonly SingleBattleSceneAdapter Host;
            internal readonly ScenePresentationDirector Director;
            internal Fixture()
            {
                var root = New("Match"); var match = root.AddComponent<CardMatchHost>(); match.ConfigureRuntimeManaged();
                var handRoot = New("Hand"); handRoot.AddComponent<HandZoneView>();
                var hand = handRoot.AddComponent<CardZonePlayHost>(); hand.ConfigureRuntimeBinding(match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab"), Scene);
                var actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>();
                actions.Configure(hand, ui.GetComponent<HandActionPanelView>());
                Host = root.AddComponent<SingleBattleSceneAdapter>(); Host.Configure(match, actions, null, null);
                var camera = New("Recipe camera"); camera.tag = "MainCamera"; camera.AddComponent<Camera>();
                New("Recipe UI").AddComponent<Canvas>();
                Director = BattlePresentationSceneSetup.ConfigureHost(Host);
            }
            private GameObject New(string name) { var result = new GameObject(name); SceneManager.MoveGameObjectToScene(result, Scene); return result; }
            public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
        }
        private static UnitDisplayData Unit(string id, string team, long health, long block) => new UnitDisplayData(id, id, team, health, 100, block, 3, 3);
        private static ActionContext Context(string semantic, string action, string kind = null, long damage = 0, long absorbed = 0,
            long beforeBlock = 0, long afterBlock = 0, bool death = false, bool skipped = false, string target = "enemy")
        {
            var units = new[] { Unit("player", "player", 100, 0), Unit("enemy", "enemy", 100, beforeBlock) };
            var snapshot = new BattleDisplaySnapshot("battle", "player", 1, "player", 1, null, units, zones: null);
            var effect = kind == null ? null : new EffectDisplayData(kind, "player", target, damage + absorbed,
                damage, absorbed, Unit(target, target, 100, beforeBlock), Unit(target, target, 100 - damage, afterBlock), death);
            var change = new BattleDisplayChange(0, DisplayCursor.Empty, snapshot, semantic, "player", effect: effect,
                values: new Dictionary<string, string> { { "action", action }, { "skipped", skipped ? "true" : "false" } });
            return new ActionContext("batch", 1, "node", change, snapshot, new DictionaryTargetResolver(), new PresentationScope("recipe"));
        }
        private static ActionPlan Plan(ScenePresentationDirector director, ActionContext context)
        {
            var plan = ActionPlan.Empty;
            foreach (var group in director.Timeline.Where(group => group.Matches(context)))
                plan = new ActionPlan((group.replaceDefault ? Enumerable.Empty<ActionNode>() : plan.Nodes).Concat(group.nodes.Where(node => node.Matches(context)).Select(node => node.Build())),
                    group.explicitCheckpoint ? group.checkpoint.Select(item => item.Build()) : plan.Checkpoint);
            return plan;
        }
        private static void Validate(ActionPlan plan)
        {
            Assert.AreEqual(plan.Nodes.Count, plan.Nodes.Select(node => node.Id).Distinct().Count(), "Matched recipes must not duplicate node IDs.");
            foreach (var dependency in plan.Nodes.SelectMany(node => node.Dependencies).Concat(plan.Checkpoint ?? Array.Empty<ActionDependency>()))
            {
                var source = plan.Nodes.SingleOrDefault(node => node.Id == dependency.ActionId);
                Assert.IsNotNull(source, "No selected dependency may point at a filtered-out node: " + dependency.ActionId);
                if (dependency.Kind == DependencyKind.Marker) CollectionAssert.Contains(source.Action.Descriptor.Markers, dependency.MarkerKey);
            }
        }
        [TestCase("Damage", 12, 0, 0, 0, false, "actor.Hit")]
        [TestCase("Damage", 8, 4, 4, 0, true, "actor.BlockHit")]
        [TestCase("Damage", 0, 4, 4, 0, false, "actor.BlockHit")]
        [TestCase("GainBlock", 0, 0, 0, 8, false, "actor.GainBlock")]
        [TestCase("ClearBlock", 0, 0, 8, 0, false, "actor.ClearBlock")]
        [TestCase("Heal", 0, 0, 0, 0, false, null)]
        [TestCase("Damage", 0, 0, 0, 0, false, null)]
        [TestCase("Eliminate", 0, 0, 0, 0, true, "actor.Death")]
        public void EffectRecipesSelectRealReactionsAndNeverLeaveDanglingSignals(string kind, long damage, long absorbed,
            long beforeBlock, long afterBlock, bool death, string expectedReaction)
        {
            using (var fixture = new Fixture())
            {
                foreach (var target in new[] { "enemy", "player" })
                {
                    var context = Context("EffectSettled", "Play", kind, damage, absorbed, beforeBlock, afterBlock, death, target: target);
                    var plan = Plan(fixture.Director, context); Validate(plan);
                    var reactions = plan.Nodes.Where(node => node.Action is BattleActorTimelineAction).ToArray();
                    if (expectedReaction == null) Assert.IsEmpty(reactions);
                    else CollectionAssert.Contains(reactions.Select(node => node.Action.Descriptor.Key), expectedReaction);
                    Assert.AreEqual(target == "enemy" && (damage > 0 || absorbed > 0), plan.Nodes.Any(node => node.Id == "camera-impact"));
                    context.Scope.Dispose();
                }
            }
        }
        [Test]
        public void CardReadyAndImpactAreAuthoredAndReconnectingPreservesAnEditedDependency()
        {
            using (var fixture = new Fixture())
            {
                var context = Context("ActionStarted", "Play"); var plan = Plan(fixture.Director, context); Validate(plan);
                var attack = plan.Nodes.Single(node => node.Id == "attack");
                Assert.AreEqual("Prepared", attack.Dependencies.Single().MarkerKey);
                Assert.AreEqual("Impact", plan.Checkpoint.Single().MarkerKey);
                var configuredAttack = fixture.Director.Timeline.Where(group => group.Matches(context)).SelectMany(group => group.nodes).Single(node => node.id == "attack");
                configuredAttack.after = new[] { new SceneActionDependency { actionId = "cards", kind = DependencyKind.Completed } };
                int count = fixture.Host.GetComponentsInChildren<PresentationActionBehaviour>(true).Length;
                BattlePresentationSceneSetup.ConfigureHost(fixture.Host);
                Assert.AreEqual(count, fixture.Host.GetComponentsInChildren<PresentationActionBehaviour>(true).Length);
                Assert.AreEqual(DependencyKind.Completed, Plan(fixture.Director, context).Nodes.Single(node => node.Id == "attack").Dependencies.Single().Kind);
                var skipped = Context("ActionStarted", "Play", skipped: true);
                Assert.IsEmpty(Plan(fixture.Director, skipped).Nodes);
                context.Scope.Dispose(); skipped.Scope.Dispose();
            }
        }
    }
}
