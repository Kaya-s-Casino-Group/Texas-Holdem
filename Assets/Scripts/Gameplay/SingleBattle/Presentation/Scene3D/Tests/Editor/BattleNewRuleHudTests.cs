using System;
using System.Linq;
using CardPresentation;
using NUnit.Framework;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleNewRuleHudTests
    {
        private static readonly ParticipantId Hero = new ParticipantId("hero"), Enemy = new ParticipantId("enemy");

        [TestCase(CombatTimingKind.TurnStarted, PeriodicTimingSubject.Holder, "持有者回合开始")]
        [TestCase(CombatTimingKind.TurnStarted, PeriodicTimingSubject.Applier, "施加者回合开始")]
        [TestCase(CombatTimingKind.PhaseEnding, PeriodicTimingSubject.Holder, "持有者所属阵营阶段结束")]
        [TestCase(CombatTimingKind.PhaseEnding, PeriodicTimingSubject.Applier, "施加者所属阵营阶段结束")]
        [TestCase(CombatTimingKind.ActionCompleted, PeriodicTimingSubject.Holder, "持有者每次有效行动后")]
        public void PeriodicStatusUsesConfiguredClockSubjectAndDecay(CombatTimingKind timing, PeriodicTimingSubject subject, string expected)
        {
            var id = new StatusDefinitionId("poison");
            var catalog = new CombatStatusCatalog("test", new[] { new CombatStatusDefinition(id, StatusTags.None,
                new StatusBehaviorSpec[] { new PeriodicDamageSpec(timing, 1, stackDecay: 1, timingSubject: subject) }, StatusLifetimeSpec.UntilRemoved()) });
            string value = BattlePresentationRuleText.StatusName(id, catalog);
            StringAssert.Contains(expected, value); StringAssert.Contains("1 伤害/层", value); StringAssert.Contains("每次移除 1 层", value);
        }

        [Test] public void ItemRuntimeShowsChargesAndBattleCooldownWithoutHiddenRouletteRoll()
        {
            var memory = AbilityMemory.Empty.WithCounter(BattleItemRuntime.CooldownBattlesKey, 6, MemoryScope.Encounter)
                .WithCounter(BattleItemRuntime.RouletteActiveKey, 1, MemoryScope.Encounter)
                .WithCounter(BattleItemRuntime.RouletteTurnKey, 2, MemoryScope.Encounter)
                .WithCounter(BattleItemRuntime.RouletteBombKey, 5, MemoryScope.Encounter);
            var relic = new BattleRelicState("dice/1", "dice", Hero, 0, remainingCharges: 3, memory: memory);
            string value = BattlePresentationRuleText.RelicRuntime(relic);
            StringAssert.Contains("次数 3", value); StringAssert.Contains("战斗冷却 6 场", value);
            StringAssert.Contains("转盘生效中", value); StringAssert.DoesNotContain("5", value); StringAssert.DoesNotContain("bomb", value);
        }

        [Test] public void ActiveDescriptionsReadConfiguredValuesAndTargets()
        {
            var potion = new BattleRelicRules("potion", itemSpec: new BattleItemSpec(BattleItemActivation.Effects,
                new[] { new RelicTriggeredEffectSpec(BattleEffectSpec.Heal(17), CombatTargetSpec.Self()) }, removeOnDepleted: true));
            var grenade = new BattleRelicRules("grenade", itemSpec: new BattleItemSpec(BattleItemActivation.Effects,
                new[] { new RelicTriggeredEffectSpec(BattleEffectSpec.Damage(31), CombatTargetSpec.Opponents(CombatTargetSelection.AllLiving)) }));
            var dice = new BattleRelicRules("dice", itemSpec: new BattleItemSpec(BattleItemActivation.ReplaceHeld, cooldownBattles: 4));
            var roulette = new BattleRelicRules("roulette", itemSpec: new BattleItemSpec(BattleItemActivation.Roulette, rouletteBase: 7, rouletteTurns: 3, explosionDamage: 123456));
            StringAssert.Contains("自身恢复生命 17", BattlePresentationRuleText.RelicDescription(potion));
            StringAssert.Contains("所有敌人受到伤害 31", BattlePresentationRuleText.RelicDescription(grenade));
            StringAssert.Contains("冷却 4 场", BattlePresentationRuleText.RelicDescription(dice));
            StringAssert.Contains("每回合牌型最终伤害累乘 7", BattlePresentationRuleText.RelicDescription(roulette));
            StringAssert.Contains("123456", BattlePresentationRuleText.RelicDescription(roulette));
        }

        [Test] public void DisplayResourcesKeepLongGoldAndConfiguredActions()
        {
            var engine = new CardDuelEngine(new BattleRules("display", 20));
            var state = engine.Create("display", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(Hero, new CombatantDefinition("hero", 80)) },
                new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 300), 10) })
                .WithGold(new[] { new BattleAdminGold(Hero, 5000000000L) });
            var display = BattleDisplayProjector.Build(state, engine, Hero);
            Assert.AreEqual(5000000000L, display.Resource("hero", "gold").Value);
            Assert.AreEqual(state.Duel.Player(Hero).Energy.Energy, display.Resource("hero", "actions").Value);
        }

        [Test] public void ReviveAndDirectHealthLossHaveDistinctFeedback()
        {
            var names = new TeamBattleEngine(new BattleRules("names", 10)).Create("names",
                new[] { new BattlePlayerSetup(Hero, new CombatantDefinition("hero", 40)) },
                new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 100), 7) });
            var start = new CombatState("vitals", 0, new[] {
                new CombatantState(Hero, "hero", "players", 40, 10, 300), new CombatantState(Enemy, "enemy", "enemies", 100, 100) });
            var result = CombatEngine.Resolve(start, 0, new[] { new CombatEffect(CombatEffectKind.LoseHealth, Hero, Hero, 10),
                // A primary effect needs a living source; the real item revival uses an AllowDefeated reaction.
                new CombatEffect(CombatEffectKind.Revive, Enemy, Hero, 24) });
            Assert.IsTrue(result.Accepted);
            string value = string.Join("\n", BattlePresentationRuleText.Events(result.RuleEvents, names, null));
            StringAssert.Contains("失去生命 10", value); StringAssert.Contains("复活 · 生命 24 / 40", value);
            StringAssert.Contains("护盾和状态清空", value);
        }

        [Test] public void JunkCardHasVisibleCrossAndTooltipRestoresNormalCard()
        {
            var visual = new CardVisualData("junk", "hearts_02", effects: CardEffects.Unplayable);
            CollectionAssert.Contains(CardStateArtBindings.Standard.Select(visual), "cross");
            Assert.IsEmpty(CardStateArtBindings.Standard.Select(new CardVisualData("hidden", effects: CardEffects.Unplayable)));
            var root = new GameObject("Tooltip", typeof(RectTransform));
            try
            {
                var first = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI)); first.transform.SetParent(root.transform);
                var second = new GameObject("Chips", typeof(RectTransform), typeof(TextMeshProUGUI)); second.transform.SetParent(root.transform);
                var tooltip = root.AddComponent<CardTooltipView>(); tooltip.Configure(first.GetComponent<TMP_Text>(), second.GetComponent<TMP_Text>());
                tooltip.SetContent(Suit.Hearts, Rank.Two, 2, CardEffects.Unplayable);
                StringAssert.Contains("废牌", tooltip.SuitRankText.text); StringAssert.Contains("可以弃牌", tooltip.ChipsText.text);
                tooltip.SetContent(Suit.Hearts, Rank.Two, 2, CardEffects.Unplayable, disabled: true);
                StringAssert.Contains("已禁用", tooltip.ChipsText.text);
                tooltip.SetContent(Suit.Hearts, Rank.Three, 3);
                StringAssert.DoesNotContain("废牌", tooltip.SuitRankText.text); StringAssert.Contains("筹码", tooltip.ChipsText.text);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
