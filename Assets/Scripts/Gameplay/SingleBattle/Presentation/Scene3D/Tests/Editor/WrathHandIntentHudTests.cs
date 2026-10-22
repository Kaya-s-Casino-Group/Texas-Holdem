using System;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Poker.SingleBattle.Editor;
using PokerEngine;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle.Tests
{
    public sealed class WrathHandIntentHudTests
    {
        private sealed class Fixture : IDisposable
        {
            private readonly string copy;
            private readonly UnityEngine.SceneManagement.Scene scene;
            private TextAsset stunnedContent;
            public readonly SingleBattleSceneAdapter Host;
            public readonly SingleBattleHud Hud;

            public Fixture(bool stunHands = false)
            {
                copy = AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/WrathHandIntentHudTest.unity");
                Assert.IsTrue(AssetDatabase.CopyAsset(WrathBossSetup.SourceScenePath, copy));
                scene = EditorSceneManager.OpenScene(copy, OpenSceneMode.Additive);
                Host = WrathBossSetup.ConfigureScene(scene);
                Hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SingleBattleHud>(true)).Single();
                if (stunHands)
                {
                    var xml = XDocument.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(WrathBossSetup.ContentPath).text);
                    foreach (string id in new[] { "wrath-blood-1", "wrath-binding" })
                        xml.Root.Element("Enemies").Elements("Enemy").Single(enemy => (string)enemy.Attribute("id") == id)
                            .Element("Behavior").AddFirst(XElement.Parse(
                                "<OpeningEffects><Effect id='hud-stun' kind='ApplyStatus' statusRef='stun' target='Self'/></OpeningEffects>"));
                    stunnedContent = new TextAsset(xml.ToString());
                    Host.ConfigureContent(stunnedContent, WrathBossSetup.EncounterId);
                }
                var canvas = Hud.GetComponent<Canvas>();
                Assert.IsNotNull(canvas);
                canvas.renderMode = RenderMode.WorldSpace;
                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.enabled = false;
                ((RectTransform)Hud.transform).sizeDelta = new Vector2(1280, 720);
                Host.StartBattle();
                Edit(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(Host.Enemies[0].ParticipantId, health: 1400) },
                    reason: "Visible hand healing HUD fixture"));
            }

            public Button Target(string id) => Hud.GetComponentsInChildren<Button>(true).Single(button => button.name == "Wrath Target " + id);
            public string Text(string id) => Target(id).GetComponentInChildren<TMP_Text>().text;
            public void Edit(BattleAdminPatch patch)
            {
                var result = Host.Session.ApplyAdmin(Host.AuthorityState.Revision, patch);
                Assert.IsTrue(result.Accepted, result.Error.ToString());
                Refresh();
            }
            public void EndRound()
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
            private void Refresh()
            {
                Host.SynchronizeBattle();
                Host.Actions.Hand.GetComponent<CardPresentation.HandZoneView>().CompleteTransitions();
                Hud.Refresh();
                Canvas.ForceUpdateCanvases();
            }
            public void AssertReadable(string id)
            {
                var text = Target(id).GetComponentInChildren<TMP_Text>();
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextOverflowing, "The complete hand state and intent must fit its 720p target control: " + text.text +
                    " rect=" + text.rectTransform.rect + " font=" + text.fontSize + " preferred=" + text.preferredHeight);
                Assert.GreaterOrEqual(text.fontSize, 9);
            }
            public void Dispose()
            {
                Host.SnapToState();
                Host.Actions.Hand.StopSession();
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(copy);
                if (stunnedContent != null) UnityEngine.Object.DestroyImmediate(stunnedContent);
            }
        }

        [Test]
        public void LivingBloodHandsExposeTheirHealingIntentBesideEachTargetAt720p()
        {
            using (var fixture = new Fixture())
                foreach (string id in new[] { "hand-1", "hand-2", "hand-3", "hand-4" })
                {
                    StringAssert.Contains("回复5", fixture.Text(id), "Each living blood hand must expose its current healing action.");
                    fixture.AssertReadable(id);
                }
        }

        [Test]
        public void BindingHandDistinguishesWaitingFromTheRoundFiveStun()
        {
            using (var fixture = new Fixture())
            {
                StringAssert.Contains("等待束缚", fixture.Text("hand-5"));
                for (int round = 0; round < 4; round++) fixture.EndRound();
                Assert.AreEqual(4, fixture.Host.Display.CompletedRounds);
                StringAssert.Contains("眩晕玩家", fixture.Text("hand-5"));
                fixture.AssertReadable("hand-5");
            }
        }

        [Test]
        public void StunnedLivingHandsExposeTheSkippedActionInsteadOfTheirUsualIntent()
        {
            using (var fixture = new Fixture(stunHands: true))
                foreach (string id in new[] { "hand-1", "hand-5" })
                {
                    Assert.IsTrue(fixture.Host.Display.Unit(id).IsAlive);
                    StringAssert.Contains("眩晕", fixture.Text(id));
                    StringAssert.Contains("跳过", fixture.Text(id));
                    fixture.AssertReadable(id);
                }
        }

        [Test]
        public void AdministratorEntryDoesNotCoverAnyRegularIntentAt720p()
        {
            using (var fixture = new Fixture())
            {
                var button = RectInHud(fixture.Hud, fixture.Hud.AdministratorButton.GetComponent<RectTransform>());
                for (int i = 0; i < 8; i++)
                {
                    var slot = fixture.Hud.transform.Find("Wrath Boss/Regular Intent " + i).GetComponent<RectTransform>();
                    Assert.IsFalse(button.Overlaps(RectInHud(fixture.Hud, slot)), "Administrator covers intent " + i);
                }
            }
        }

        private static Rect RectInHud(SingleBattleHud hud, RectTransform rect)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(hud.transform, rect);
            return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
        }

        [Test]
        public void DeadHandKeepsItsCountdownWithoutAnActiveHealingIntent()
        {
            using (var fixture = new Fixture())
            {
                fixture.Edit(new BattleAdminPatch(vitals: new[] { new BattleAdminVitals(new ParticipantId("hand-1"), health: 0) },
                    reason: "Dead hand HUD fixture"));
                StringAssert.Contains("再生 8", fixture.Text("hand-1"));
                StringAssert.DoesNotContain("回复5", fixture.Text("hand-1"));
                Assert.IsFalse(fixture.Target("hand-1").interactable);
                fixture.AssertReadable("hand-1");
            }
        }
    }
}
