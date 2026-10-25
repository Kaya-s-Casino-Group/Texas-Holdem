#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        [UnityTest]
        public IEnumerator EliteBleedingForecastExplainsIncludedAmplificationAndStillResolvesBlockOnce()
        {
            yield return ReachBattle("elite");
            var view = host.BattleView; var player = host.Adventure.PlayerId;
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(player, health: 10000, maxHealth: 10000) },
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle();
            for (int round = 0; round < 5; round++) yield return EndAdventureRound();
            Assert.AreEqual(5, view.State.Duel.CompletedRounds);
            Assert.AreEqual(4, view.State.Duel.Combat.Statuses.Single(status =>
                status.Target.Id == player.Value && status.DefinitionId.Value == "elite-bleeding").Stacks);
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(player, block: 20) })).Accepted);
            yield return Idle();
            var cue = Object.FindObjectsOfType<BattlePlayerBleedingCue>().Single(item => item.gameObject.scene == view.gameObject.scene);
            var label = cue.GetComponent<TMP_Text>();
            StringAssert.Contains("加伤已计入意图", label.text); StringAssert.DoesNotContain("%", label.text);
            Assert.IsTrue(InformationTextIsRendered(label));
            var enemyHud = FindEnemyWorldHud(view.Enemies.Single());
            var attack = enemyHud.Find("Intents").GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>()
                .Single(item => item.Description != null && item.Description.Contains("预计伤害"));
            StringAssert.Contains("72", attack.Description); StringAssert.Contains("格挡前", attack.Description);
            StringAssert.Contains("已含流血加伤", attack.Description);
            StringAssert.Contains("33→72", attack.Description); StringAssert.Contains("+39", attack.Description);
            var value = attack.transform.Find("Value").GetComponent<TMP_Text>(); Assert.AreEqual("72", value.text);
            var caption = attack.transform.Find("Action Caption").GetComponent<TMP_Text>();
            StringAssert.Contains("预计伤害", caption.text.Replace("\n", "")); Assert.IsTrue(InformationTextIsRendered(caption));
            caption.ForceMeshUpdate();
            Assert.LessOrEqual(caption.textBounds.size.x, caption.rectTransform.rect.width + .1f);
            value.ForceMeshUpdate();
            Assert.IsFalse(BleedingClarityGlyphRect(caption).Overlaps(BleedingClarityGlyphRect(value)),
                "The damage caption must leave the forecast number readable inside the existing badge.");
            yield return Capture("bleeding-intent-included-summary");

            long revision = host.Revision;
            var playerTooltip = cue.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>().Single();
            var pointer = EnterEnemyBadgeThroughRaycast(playerTooltip); yield return null; Canvas.ForceUpdateCanvases();
            var details = cue.transform.Find("Bleeding Status Details"); Assert.IsTrue(details.gameObject.activeInHierarchy);
            var description = details.GetComponentInChildren<TMP_Text>();
            StringAssert.Contains("+120%", description.text); StringAssert.Contains("无需再次计算", description.text);
            StringAssert.Contains("格挡", description.text); Assert.IsTrue(InformationTextIsRendered(description));
            description.ForceMeshUpdate();
            Assert.IsFalse(description.isTextTruncated); Assert.LessOrEqual(description.textBounds.size.y, description.rectTransform.rect.height + .1f);
            yield return Capture("bleeding-status-included-tooltip");
            ExecuteEvents.Execute(playerTooltip.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            pointer = EnterEnemyBadgeThroughRaycast(attack); yield return null; Canvas.ForceUpdateCanvases();
            var popup = enemyHud.Find("Enemy Badge Tooltip"); Assert.IsTrue(popup.gameObject.activeInHierarchy);
            var forecastText = popup.GetComponentInChildren<TMP_Text>();
            Assert.IsTrue(InformationTextIsRendered(forecastText)); forecastText.ForceMeshUpdate();
            Assert.LessOrEqual(forecastText.textBounds.size.y, forecastText.rectTransform.rect.height + .1f);
            yield return Capture("bleeding-attack-included-tooltip");
            ExecuteEvents.Execute(attack.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Assert.AreEqual(revision, host.Revision, "Inspecting the two explanations cannot advance combat.");
            long health = view.State.Duel.Combat.Get(player).Health;
            yield return EndAdventureRound();
            Assert.AreEqual(52, health - view.State.Duel.Combat.Get(player).Health,
                "A single resolved72damage hit must consume20block and52health, not apply bleeding again.");
            Assert.AreEqual(0, view.State.Duel.Combat.Get(player).Block);
            AssertRemotePresentation();
        }

        private static Rect BleedingClarityGlyphRect(TMP_Text text)
        {
            var min = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint(text.textBounds.min));
            var max = RectTransformUtility.WorldToScreenPoint(null, text.transform.TransformPoint(text.textBounds.max));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
#endif
