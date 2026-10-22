using System.IO;
using System.Linq;
using NUnit.Framework;
using Poker.SingleBattle.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class WrathBossSceneTests
    {
        [Test]
        public void DedicatedSetupBuildsSixStableActorsWithoutChangingTheSourceScene()
        {
            const string source = "Assets/Scenes/BattleDemo.unity";
            var original = File.ReadAllBytes(source);
            // Unity 2022.3 cannot open an existing asset as a preview scene.
            // A disposable copy exercises the authored scene without modifying the user's scene.
            string copy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/WrathBossSceneTest.unity");
            Assert.IsTrue(AssetDatabase.CopyAsset(source, copy));
            var scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
            try
            {
                var host = WrathBossSetup.ConfigureScene(scene);

                Assert.AreEqual("standard-wrath-thousand-hands", host.ContentEncounterId);
                Assert.AreEqual(1, host.Players.Count);
                Assert.AreEqual(6, host.Enemies.Count);
                Assert.AreEqual(6, host.Enemies.Select(actor => actor.gameObject).Distinct().Count());
                Assert.IsTrue(host.Enemies.All(actor => actor.Battle == host && actor.Visual != null && actor.Presentation != null));
                foreach (var layout in host.Enemies.SelectMany(actor => actor.GetComponentsInChildren<BattleBillboardLayout>(true))) layout.Refresh();
                Assert.AreEqual(6, host.Enemies.Select(actor => actor.transform.position).Distinct().Count(),
                    "Copied billboard layouts must not snap the five hands back onto the body.");
                var session = host.BuildConfiguredSession("wrath-scene-test");
                Assert.AreEqual(6, session.State.Duel.Enemies.Count);
                Assert.AreEqual(1500, session.State.Duel.Combat.Get(session.State.Duel.Enemies[0].Id).MaxHealth);
                Assert.IsTrue(session.State.Duel.Enemies.Skip(1).All(hand => session.State.Duel.Combat.Get(hand.Id).MaxHealth == 1));
                Assert.AreNotEqual(host.BuildConfiguredSession().State.Table.MatchId, host.BuildConfiguredSession().State.Table.MatchId,
                    "Restart creates a new identity so the per-battle opening warning can appear again.");
                var firstActors = host.Enemies.ToArray();
                WrathBossSetup.ConfigureScene(scene);
                CollectionAssert.AreEqual(firstActors, host.Enemies, "Reopening setup must not accumulate or replace hand actors.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(copy);
            }
            CollectionAssert.AreEqual(original, File.ReadAllBytes(source));
        }

        [Test]
        public void BossHudUsesBodyIdentityForHealthAndKeepsDeadHandsInItsSnapshot()
        {
            var snapshot = new global::BattlePresentation.BattleDisplaySnapshot("boss", "player", 0, "player", 0, null,
                new[] {
                    new global::BattlePresentation.UnitDisplayData("hand", "饮血之手", "enemy", 0, 1, 0, 0, 0),
                    new global::BattlePresentation.UnitDisplayData("body", "暴怒", "enemy", 750, 1500, 0, 0, 0)
                }, zones: null, labels: new System.Collections.Generic.Dictionary<string, string> { ["wrath.body"] = "body" });

            Assert.AreEqual("body", SingleBattleHud.WrathBody(snapshot).Id);
            Assert.AreEqual(750, SingleBattleHud.WrathBody(snapshot).Health);
            Assert.AreEqual(2, snapshot.Units.Count);
        }
    }
}
