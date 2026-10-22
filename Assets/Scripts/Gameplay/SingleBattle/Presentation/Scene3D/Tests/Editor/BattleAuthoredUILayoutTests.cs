using System.Linq;
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
    public sealed class BattleAuthoredUILayoutTests
    {
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

        [TestCase("single-chapter-battle-1")]
        [TestCase("single-chapter-gatekeeper")]
        [TestCase("single-chapter-wrath")]
        public void ImportedControlsKeepAuthoredGeometryAndLiveValues(string encounter)
        {
            string scratch = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/__UILayoutTest.unity");
            Assert.IsTrue(AssetDatabase.CopyAsset("Assets/Scenes/SingleChapterAdventure.unity", scratch));
            var scene = EditorSceneManager.OpenScene(scratch, OpenSceneMode.Additive);
            try
            {
                var host = All<SingleBattleSceneAdapter>(scene).Single();
                for (var t = host.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                var content = AssetDatabase.LoadAssetAtPath<TextAsset>("Packages/com.kaya.battle-engine/Content/SingleChapterBattle.xml");
                var compiled = BattleContentCompiler.CompileXml(content.text, content.name);
                var result = BattleContentFactory.Create(compiled.Content, new BattleSetupRequest(encounter, "ui-test", HmacDeckShuffler.FromSeed(new byte[32])));
                Assert.IsTrue(result.Success);
                var session = result.Session;
                var opening = session.Execute(session.State.Revision, session.State.Duel.PlayerId, CardDuelAction.Start);
                host.ConfigureContent(content, encounter); host.AttachCommands(new BattleSessionCommands(session), opening, false);
                host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                var hud = All<SingleBattleHud>(scene).Single(); hud.Refresh();
                var desk = hud.GetComponent<DeskBattleLayout>(); var authored = hud.GetComponent<BattleAuthoredUILayout>();
                Assert.NotNull(authored); Assert.NotNull(authored.Profile);
                Assert.That(host.GetComponent<BattleAuthoredLayout>().Profile,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<BattleLayoutProfile>("Assets/GameAssets/SingleBattle/BattleLayoutWorkshop.asset")));
                for (int frame = 0; frame < 2; frame++)
                {
                    desk.Apply(); authored.Apply();
                    foreach (string path in new[] { "Turn", "Player/Health Bar", "Player/Block Icon", "Concept Action Points", "Concept Action Points/Action Liquid", "Discard Pile Art" })
                    {
                        var entry = authored.Profile.nodes.Single(n => n.root == "Battle HUD" && n.pose.path == path);
                        var rect = authored.Resolve("Battle HUD", path); Assert.NotNull(rect, path);
                        Assert.That(Vector3.Distance(rect.anchoredPosition3D, entry.pose.position), Is.LessThan(.001f), path);
                        Assert.That(Vector2.Distance(rect.sizeDelta, entry.pose.sizeDelta), Is.LessThan(.001f), path);
                        Assert.That(Vector3.Distance(rect.localScale, entry.pose.scale), Is.LessThan(.001f), path);
                    }
                    var discard = host.Actions.Panel.DiscardButton.transform;
                    var expected = authored.Profile.nodes.Single(n => n.root == "HandActionPanel" && n.pose.path == "Actions/Discard").pose;
                    Assert.That(Vector2.Distance(((RectTransform)discard).sizeDelta, expected.sizeDelta), Is.LessThan(.001f));
                }
                var liquid = All<BattleActionPointLiquid>(scene).Single(); liquid.SetActions(1);
                var energy = authored.Resolve("Battle HUD", "Concept Action Points/Energy").GetComponent<TMP_Text>(); energy.text = "1";
                var shield = host.Actions.Panel.DiscardButton.transform.Find("Discard Shield Preview/Shield Count").GetComponent<TMP_Text>(); shield.text = "+19";
                var health = authored.Resolve("Battle HUD", "Player/Health Bar/Concept Fill").GetComponent<Image>(); health.fillAmount = .25f;
                host.Actions.Panel.DiscardButton.interactable = false;
                authored.Apply();
                Assert.That(liquid.FillAmount, Is.EqualTo(1f / 3).Within(.0001f)); Assert.That(energy.text, Is.EqualTo("1"));
                Assert.That(shield.text, Is.EqualTo("+19")); Assert.That(health.fillAmount, Is.EqualTo(.25f));
                Assert.IsFalse(host.Actions.Panel.DiscardButton.interactable);
                host.Actions.Hand.StopSession();
            }
            finally { EditorSceneManager.CloseScene(scene, true); AssetDatabase.DeleteAsset(scratch); }
        }

        [Test]
        public void ProfileExcludesEnemiesAndRuntimeSafeAreaInsets()
        {
            var profile = AssetDatabase.LoadAssetAtPath<BattleUILayoutProfile>("Assets/GameAssets/SingleBattle/BattleUIWorkshop.asset");
            Assert.NotNull(profile); Assert.That(profile.nodes.Length, Is.GreaterThan(30));
            Assert.IsFalse(profile.nodes.Any(n => n.root.Contains("Enemy") || n.pose.path == "Safe Area"));
            Assert.IsFalse(BattleUILayoutProfile.Includes("BattleDemo Enemy World UI", "Health Bar"));
            Assert.IsFalse(BattleUILayoutProfile.Includes("Battle HUD", "Safe Area/Enemy/Health Bar"));
            Assert.IsFalse(BattleUILayoutProfile.Includes("Battle HUD", "Player/Health Bar/Fill"));
        }
    }
}
