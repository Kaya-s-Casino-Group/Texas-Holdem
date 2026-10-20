using System;
using NUnit.Framework;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleRitualHudTests
    {
        [Test]
        public void StatusBadgeHidesWhenEmptyAndReappearsForActiveStatus()
        {
            var root = new GameObject("status", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var label = root.GetComponent<TMP_Text>();
                SingleBattleHud.PresentActorStatuses(label, "");
                Assert.IsFalse(root.activeSelf);
                SingleBattleHud.PresentActorStatuses(label, "眩晕（剩余 1 次）");
                Assert.IsTrue(root.activeSelf);
                Assert.AreEqual("眩晕（剩余 1 次）", label.text);
                SingleBattleHud.PresentActorStatuses(label, null);
                Assert.IsFalse(root.activeSelf);
                Assert.IsEmpty(label.text);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ProjectionSeparatesRelicsFromActorStatusesWithoutChangingAuthority()
        {
            var hero = new ParticipantId("hero"); var enemy = new ParticipantId("enemy");
            var relic = new BattleRelicSetup(new RelicDefinition("bag", Array.Empty<ScoreEffect>(), charges: 3),
                new RunRelic("bag/1", "bag", level: 2, charges: 2, cooldownRounds: 1),
                new BattleRelicRules("bag-rules"));
            var engine = new CardDuelEngine(new BattleRules("ui-test", 10));
            var state = engine.Create("ui-test", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(hero, new CombatantDefinition("hero", 40), relics: new[] { relic }) },
                new[] { new BattleEnemySetup(enemy, new CombatantDefinition("enemy", 100), 7) });
            var revision = state.Revision;
            var display = BattleDisplayProjector.Build(state, engine, hero);
            Assert.IsEmpty(display.Labels["status/hero"]);
            Assert.IsEmpty(display.Labels["status/enemy"]);
            StringAssert.Contains("bag  Lv.2", display.Labels["relics"]);
            StringAssert.Contains("次数 2", display.Labels["relics"]);
            StringAssert.DoesNotContain("当前状态", display.Labels["relics"]);
            StringAssert.DoesNotContain("玩家", display.Labels["relics"]);
            Assert.AreEqual(revision, state.Revision);
            Assert.IsEmpty(state.Duel.Combat.Statuses);
        }
    }
}
