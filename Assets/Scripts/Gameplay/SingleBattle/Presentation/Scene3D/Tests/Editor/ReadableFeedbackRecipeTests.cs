using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CardInteraction;
using CardPresentation;
using global::BattlePresentation;
using global::BattlePresentation.Unity;
using NUnit.Framework;
using PokerEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poker.SingleBattle.Tests
{
    public sealed class ReadableFeedbackRecipeTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene = EditorSceneManager.NewPreviewScene();
            private readonly TextAsset content;
            internal readonly SingleBattleSceneAdapter Host;
            internal Fixture(bool legacy)
            {
                var root = New("Readable recipe"); var match = root.AddComponent<CardMatchHost>(); match.ConfigureRuntimeManaged();
                var handObject = New("Hand"); var view = handObject.AddComponent<HandZoneView>();
                view.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardArt/Prefabs/Card.prefab"), CardZoneLayoutSettings.Default);
                var hand = handObject.AddComponent<CardZonePlayHost>(); hand.ConfigureRuntimeBinding(match);
                var ui = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameAssets/CardPresentation/Prefabs/HandActionPanel.prefab"), scene);
                var actions = ui.GetComponent<HandActionPresenter>() ?? ui.AddComponent<HandActionPresenter>(); actions.Configure(hand, ui.GetComponent<HandActionPanelView>());
                Host = root.AddComponent<SingleBattleSceneAdapter>(); Host.Configure(match, actions, null, null);
                var player = New("Player").AddComponent<PlayerBattleActor>(); player.Configure(Host);
                var enemy = New("Enemy").AddComponent<EnemyBattleActor>(); enemy.Configure(Host); Host.ConfigureActors(player, enemy);
                New("Feedback canvas").AddComponent<Canvas>();
                content = new TextAsset(File.ReadAllText("External/BattleToolkit/Engine/Content/" + (legacy ? "StandardSingleBattle-2026-10-21.xml" : "SingleChapterBattle.xml")));
                Host.ConfigureContent(content, legacy ? "standard-single-battle" : "single-chapter-battle-1");
                Editor.BattlePresentationSceneSetup.ConfigureHost(Host); Host.StartBattle(); view.CompleteTransitions();
            }
            private GameObject New(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
            public void Dispose() { Host.Actions.Hand.StopSession(); EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(content); }
        }
        private static ActionContext Context(PresentationScope scope)
        {
            var unit = new UnitDisplayData("enemy", "敌人", "enemy", 100, 100, 0, 0, 0);
            var snapshot = new BattleDisplaySnapshot("recipe", "player", 1, "player", 1, null, new[] { unit }, null);
            var change = new BattleDisplayChange(0, DisplayCursor.Empty, snapshot, "EffectSettled", "player", new[] { "enemy" }, values:
                new Dictionary<string, string> { ["action"] = "Play", ["damagePrevented"] = "immune", ["comboSummary"] = "3段连击·总伤害10" },
                effect: new EffectDisplayData("Damage", "player", "enemy", 0, 0, 0, unit, unit));
            return new ActionContext("recipe", 1, "recipe", change, snapshot, null, scope);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void SevenStationRecipesRenderZeroImmunityAndNonblockingComboWithoutChangingLegacyRecipes(bool legacy)
        {
            using (var f = new Fixture(legacy))
            using (var scope = new PresentationScope("readable-recipe"))
            {
                var context = Context(scope);
                var nodes = f.Host.PresentationDirector.Timeline.Where(group => group.Matches(context)).SelectMany(group => group.nodes).Where(node => node.Matches(context)).ToArray();
                var summary = nodes.Where(node => node.action is UiFeedbackTimelineAction ui && UiFeedbackTimelineAction.Format(context.Data, ui.Kind, "{value:comboSummary}") == "3段连击·总伤害10" &&
                    node.id == "ui-combo-summary").ToArray();
                var immune = nodes.Where(node => node.id == "ui-immune-damage").ToArray();
                Assert.That(summary.Length, Is.EqualTo(legacy ? 0 : 1));
                Assert.That(immune.Length, Is.EqualTo(legacy ? 0 : 1));
                if (!legacy)
                {
                    Assert.That(summary[0].blocking, Is.False, "A combo's readable tail cannot gate settlement or final results.");
                    Assert.That(immune[0].blocking, Is.False);
                    Assert.That(UiFeedbackTimelineAction.Format(context.Data, ((UiFeedbackTimelineAction)immune[0].action).Kind), Is.EqualTo("免疫"));
                }
            }
        }

        [Test]
        public void UpgradingTheSameSevenStationProfileDoesNotRepublishThePreviousEncountersDisplay()
        {
            using (var f = new Fixture(legacy: false))
            {
                int publications = 0;
                f.Host.PresentationDirector.Display.Changed += _ => publications++;
                BattleReadableFeedbackTimeline.Upgrade(f.Host, f.Host.PresentationDirector, f.Host.State);
                Assert.That(publications, Is.Zero, "An already-upgraded recipe must not synchronize old encounter identities during a new attachment.");
            }
        }
    }
}
