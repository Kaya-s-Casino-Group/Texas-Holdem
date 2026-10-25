#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Poker.SingleBattle;
using PokerEngine;
using TMPro;
using UnityEngine.TestTools;

namespace Poker.Adventure.Tests
{
    public sealed partial class SingleChapterAdventureSceneTests
    {
        [UnityTest]
        public IEnumerator SevenStationEliteShowsEveryCastBetweenItsActualAttacks()
        {
            yield return ReachBattle("elite");
            var view = host.BattleView; var player = host.Adventure.PlayerId;
            Assert.IsTrue(view.Playback.ApplyAdmin(host.Revision, new BattleAdminPatch(
                vitals: new[] { new BattleAdminVitals(player, health: 10000, maxHealth: 10000) },
                relics: new[] { new BattleAdminRelics(player, new BattleAdminRelic[0]) })).Accepted);
            yield return Idle(); AssertRemotePresentation();
            var actions = new[] { "cast", "strike", "cast", "strike", "cast", "strike" };
            var damage = new long[] { 4, 20, 11, 43, 22, 72 };
            var layers = new[] { 2, 1, 3, 2, 4, 3 };

            for (int i = 0; i < actions.Length; i++)
            {
                Assert.AreEqual(actions[i], view.State.Duel.Enemies.Single().BehaviorState.Intent.Id, "Upcoming action in round " + (i + 1));
                var intentRoot = FindEnemyWorldHud(view.Enemies.Single()).Find("Intents");
                var status = intentRoot.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>()
                    .Where(badge => badge.Description != null && badge.Description.Contains("施加状态") && badge.Description.Contains("流血")).ToArray();
                Assert.AreEqual(i % 2 == 0 ? 1 : 0, status.Length, "Visible bleeding cast badge in round " + (i + 1));
                if (status.Length != 0)
                    Assert.IsTrue(status.Single().GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("流血") && InformationTextIsRendered(text)),
                        "Every actual cast must render a readable Chinese bleeding caption.");
                Assert.IsTrue(intentRoot.GetComponentsInChildren<BattleDemoEnemyBadgeTooltip>()
                    .Any(badge => badge.Description != null && badge.Description.Contains("预计伤害：" + damage[i])), "Actual damage forecast in round " + (i + 1));
                if (i == 2 || i == 4) yield return Capture("elite-cast-round-" + (i + 1));

                long health = view.State.Duel.Combat.Get(player).Health;
                yield return EndAdventureRound();
                Assert.AreEqual(damage[i], health - view.State.Duel.Combat.Get(player).Health, "Actual damage in round " + (i + 1));
                Assert.AreEqual(layers[i], view.State.Duel.Combat.Statuses.Single(s => s.Target.Id == player.Value && s.DefinitionId.Value == "elite-bleeding").Stacks,
                    "Actual bleeding after round " + (i + 1));
                var applied = host.Adventure.LastBattleResult.Steps.SelectMany(step => step.Transition.RuleEvents)
                    .Where(e => e.Effect?.Source == view.Enemies.Single().ParticipantId && e.Effect.Kind == CombatEffectKind.ApplyStatus).ToArray();
                Assert.AreEqual(i % 2 == 0 ? 1 : 0, applied.Length, "Actual cast application in round " + (i + 1));
                if (applied.Length != 0) Assert.AreEqual(3, applied.Single().Effect.Status.Stacks);
                AssertRemotePresentation();
            }
        }
    }
}
#endif
