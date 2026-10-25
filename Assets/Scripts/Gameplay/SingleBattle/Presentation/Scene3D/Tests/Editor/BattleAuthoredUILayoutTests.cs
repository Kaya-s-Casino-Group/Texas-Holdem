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
                // Automatic idle frames must retain the authored result without restoring
                // the desk defaults and immediately writing the profile over them again.
                for (int frame = 0; frame < 4; frame++) { host.RefreshCandidate(); host.Actions.Refresh(); desk.RefreshLayout(); authored.RefreshLayout(); }
                int deskVersion = desk.LayoutVersion, authoredVersion = authored.LayoutVersion;
                var stablePlayer = authored.Resolve("Battle HUD", "Player"); stablePlayer.hasChanged = false;
                for (int frame = 0; frame < 30; frame++)
                {
                    host.RefreshCandidate(); host.Actions.Refresh(); // Runtime presenters run before the layouts.
                    Assert.IsFalse(desk.RefreshLayout(), "Idle desk layout rebuilt");
                    Assert.IsFalse(authored.RefreshLayout(), "Idle authored layout rebuilt");
                    AssertSortPresentation(host, host.Actions.Hand.SortOrder == CardInteraction.CardZoneSortOrder.SuitFirst);
                }
                Assert.That(desk.LayoutVersion, Is.EqualTo(deskVersion));
                Assert.That(authored.LayoutVersion, Is.EqualTo(authoredVersion));
                Assert.IsFalse(stablePlayer.hasChanged, "Two layout owners still rewrote the player's pose");
                foreach (bool suitFirst in new[] { true, false })
                {
                    if (suitFirst) host.Actions.SortBySuit(); else host.Actions.SortByRank();
                    Assert.AreEqual(suitFirst ? CardInteraction.CardZoneSortOrder.SuitFirst : CardInteraction.CardZoneSortOrder.RankFirst,
                        host.Actions.Hand.SortOrder);
                    for (int frame = 0; frame < 30; frame++)
                    {
                        // Observe both the sorting animation and the return to input-ready state.
                        // Each can replace Candidate without changing the zero discard preview.
                        if (frame == 15) host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                        host.RefreshCandidate(); host.Actions.Refresh();
                        Assert.IsFalse(desk.RefreshLayout(), "Sorting should only update its own panel state");
                        Assert.IsFalse(authored.RefreshLayout(), "Sorting does not change authored geometry");
                        AssertSortPresentation(host, suitFirst);
                    }
                    Assert.That(desk.LayoutVersion, Is.EqualTo(deskVersion));
                    Assert.That(authored.LayoutVersion, Is.EqualTo(authoredVersion));
                }
                var discardCards = host.Actions.Hand.Cards.Where(card => card.Card.CanDiscard).Take(2).ToArray();
                Assert.AreEqual(2, discardCards.Length);
                foreach (var card in discardCards)
                {
                    host.Actions.Hand.SetInteraction(card.Id, new CardPresentation.CardInteractionState(isSelected: true));
                    host.Actions.Hand.Publish();
                    host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                    host.RefreshCandidate(); host.Actions.Refresh();
                    Assert.IsTrue(host.Actions.Panel.DiscardButton.interactable);
                    Assert.IsTrue(desk.RefreshLayout(), "An actionable changed discard candidate must refresh its shield preview");
                    authored.RefreshLayout();
                    long gain = BattleResourceWidgets.DiscardShieldGain(host.PreviewSelectedDiscard(), host.Player.ParticipantId);
                    Assert.That(host.Actions.Panel.DiscardButton.transform.Find("Discard Shield Preview/Shield Count")
                        .GetComponent<TMP_Text>().text, Is.EqualTo("格挡+" + gain));
                    Assert.IsFalse(desk.RefreshLayout(), "The same actionable candidate must stay cached");
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

        private static void AssertSortPresentation(SingleBattleSceneAdapter host, bool suitFirst)
        {
            var rank = host.Actions.Panel.RankSortButton.GetComponentInChildren<TMP_Text>();
            var suit = host.Actions.Panel.SuitSortButton.GetComponentInChildren<TMP_Text>();
            Assert.AreEqual("点数", rank.text); Assert.AreEqual("花色", suit.text);
            Assert.AreEqual(suitFirst ? ConceptBattleSkin.Paper : ConceptBattleSkin.Gold, rank.color);
            Assert.AreEqual(suitFirst ? ConceptBattleSkin.Gold : ConceptBattleSkin.Paper, suit.color);
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
