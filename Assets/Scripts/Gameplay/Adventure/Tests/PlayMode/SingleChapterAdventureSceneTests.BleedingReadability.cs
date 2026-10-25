#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        [UnityTest]
        public IEnumerator SevenStationEliteBleedingCueRendersRealStacksAmplificationAndFullHouseCleanse()
        {
            yield return ReachBattle("elite");
            var view = host.BattleView; var player = host.Adventure.PlayerId;
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(player, health: 10000, maxHealth: 10000) },
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle(); AssertRemotePresentation();
            var intent = FindEnemyWorldHud(view.Enemies.Single()).GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>()
                .Single(badge => badge.Description != null && badge.Description.Contains("施加状态"));
            StringAssert.Contains("流血", intent.Description); StringAssert.DoesNotContain("elite-bleeding", intent.Description);
            Assert.IsTrue(intent.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("流血") && InformationTextIsRendered(text)),
                "The real enemy intent must render the Chinese bleeding caption.");

            yield return EndAdventureRound();
            AssertRenderedBleeding(2, 60);
            yield return Capture("elite-bleeding-two-stacks");
            yield return EndAdventureRound();
            AssertRenderedBleeding(1, 30);
            yield return Capture("elite-bleeding-one-stack");

            // Resolve an actual full house to exercise the existing cleanse and its separate self-immunity.
            // Proof images above are captured while real hand views are still present.
            yield return InformationHand("standard:1:2", "standard:2:2", "standard:3:2", "standard:1:3", "standard:2:3");
            Assert.IsTrue(view.Actions.Panel.PlayButton.interactable);
            yield return ClickButton(view.Actions.Panel.PlayButton); yield return Idle();
            Assert.IsFalse(view.State.Duel.Combat.Statuses.Any(status => status.Target.Id == player.Value && status.DefinitionId.Value == "elite-bleeding"));
            var cue = view.GetComponentsInChildren<BattlePlayerBleedingCue>(true).SingleOrDefault();
            if (cue == null) cue = Object.FindObjectsOfType<BattlePlayerBleedingCue>(true).Single(item => item.gameObject.scene == view.gameObject.scene);
            var icon = cue.GetComponentsInChildren<Image>(true).Single(image => image.name == "Bleeding Status Icon");
            Assert.IsFalse(icon.gameObject.activeInHierarchy);
            var label = cue.GetComponent<TMP_Text>();
            StringAssert.DoesNotContain("流血", label.text);
            StringAssert.Contains("控制免疫", label.text);
            Assert.IsTrue(InformationTextIsRendered(label), "Cleansing bleeding must retain and render the separate real self-immunity.");
            AssertRemotePresentation();
        }

        private void AssertRenderedBleeding(int stacks, int percent)
        {
            var view = host.BattleView; var player = host.Adventure.PlayerId;
            Assert.AreEqual(stacks, view.State.Duel.Combat.Statuses.Single(status => status.Target.Id == player.Value && status.DefinitionId.Value == "elite-bleeding").Stacks);
            var cue = Object.FindObjectsOfType<BattlePlayerBleedingCue>().Single(item => item.gameObject.scene == view.gameObject.scene);
            var label = cue.GetComponent<TMP_Text>();
            string text = Regex.Replace(Regex.Replace(label.text, "<[^>]+>", ""), @"\s+", "");
            StringAssert.Contains("流血" + stacks + "层", text);
            StringAssert.Contains("加伤已计入意图", text); StringAssert.DoesNotContain("%", text);
            Assert.IsTrue(InformationTextIsRendered(label), "The current bleeding values need real visible CJK glyphs.");
            Assert.LessOrEqual(label.textBounds.size.x, label.rectTransform.rect.width + .01f);
            var icon = cue.GetComponentsInChildren<Image>().Single(image => image.name == "Bleeding Status Icon");
            Assert.IsTrue(icon.isActiveAndEnabled); Assert.GreaterOrEqual(icon.canvasRenderer.GetAlpha(), .75f);
            Assert.AreSame(ConceptBattleSkin.Get("Icons/icon_bleeding"), icon.sprite);
            StringAssert.Contains("+" + percent + "%", icon.GetComponent<BattleDemoEnemyBadgeTooltip>().Description);
            var canvas = icon.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; icon.rectTransform.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                Assert.That(point.x, Is.InRange(0, Screen.width)); Assert.That(point.y, Is.InRange(0, Screen.height));
            }
        }
    }
}
#endif
