using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PokerEngine;

namespace Poker.SingleBattle.Tests
{
    public sealed class BattleHudRuleTextTests
    {
        private static readonly ParticipantId Player = new ParticipantId("hud-player"), Enemy = new ParticipantId("hud-enemy");
        private static TeamBattleState Names() => new TeamBattleEngine(new BattleRules("hud-test", 10)).Create("hud",
            new[] { new BattlePlayerSetup(Player, new CombatantDefinition("hero", 40)) },
            new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 10000), 7) });
        private static StatusInstance Status(string id, string definition, ParticipantId target, StatusDuration duration) =>
            new StatusInstance(new StatusInstanceId(id), new StatusDefinitionId(definition), EntityRef.Participant(Player),
                EntityRef.Participant(target), 1, duration);
        private static CombatStatusDefinition Definition(string id, StatusBehaviorSpec behavior) =>
            new CombatStatusDefinition(new StatusDefinitionId(id), behavior is BlockActionWindowSpec ? StatusTags.Control : StatusTags.None,
                new[] { behavior }, StatusLifetimeSpec.UntilRemoved());
        private static string Text(CombatTransition transition, CombatStatusCatalog catalog = null) =>
            string.Join("\n", BattleHudRuleText.Events(transition.RuleEvents, Names(), catalog));

        [Test] public void MixedVitalsKeepDamageHealingBlockAndEliminationSeparate()
        {
            var state = new CombatState("hud", 0, new[] {
                new CombatantState(Player, "hero", "players", 40, 30), new CombatantState(Enemy, "enemy", "enemies", 30, 30, 3)
            });
            var result = CombatEngine.Resolve(state, 0, new[] {
                new CombatEffect(CombatEffectKind.Damage, Player, Enemy, 10),
                new CombatEffect(CombatEffectKind.Heal, Player, Player, 15),
                new CombatEffect(CombatEffectKind.GainBlock, Player, Player, 6), CombatEffect.Eliminate(Player, Enemy)
            });
            Assert.IsTrue(result.Accepted);
            string text = Text(result);
            StringAssert.Contains("怪物 1：伤害 10（扣血 7 · 护盾吸收 3）", text);
            StringAssert.Contains("玩家 1：治疗 +10（溢出 5）", text);
            StringAssert.Contains("玩家 1：护盾 +6", text);
            StringAssert.Contains("怪物 1：消灭", text);
            Assert.AreEqual(30, state.Get(Player).Health, "Formatting must not mutate authority inputs.");
        }

        [Test] public void StatusFeedbackIncludesAppliedImmunityPreventionSkipConsumptionAndExpiry()
        {
            var catalog = new CombatStatusCatalog("hud-status", new[] {
                Definition("arbitrary-stop", new BlockActionWindowSpec()),
                Definition("arbitrary-guard", new PreventStatusApplicationSpec(StatusTags.Control))
            });
            var state = Names().Combat;
            var applied = CombatEngine.Resolve(state, state.Revision, new[] {
                CombatEffect.ApplyStatus(Status("stop", "arbitrary-stop", Enemy, StatusDuration.ForTriggers(1))),
                CombatEffect.ApplyStatus(Status("guard", "arbitrary-guard", Player, StatusDuration.UntilPhaseEnd("round/1", "enemy"))),
                CombatEffect.ApplyStatus(Status("blocked", "arbitrary-stop", Player, StatusDuration.ForTriggers(1)))
            }, statusCatalog: catalog);
            Assert.IsTrue(applied.Accepted);
            StringAssert.Contains("怪物 1：获得 眩晕（剩余 1 次）", Text(applied, catalog));
            StringAssert.Contains("玩家 1：获得 控制免疫（至敌人阶段结束）", Text(applied, catalog));
            StringAssert.Contains("玩家 1：免疫 眩晕", Text(applied, catalog));
            var skipped = CombatStatusLifecycle.SkipActionWindow(applied.State, applied.State.Revision, Enemy, catalog);
            StringAssert.Contains("怪物 1：眩晕，跳过行动", Text(skipped, catalog));
            StringAssert.Contains("眩晕 耗尽", Text(skipped, catalog));
            var expired = CombatStatusLifecycle.EndPhase(skipped.State, skipped.State.Revision, "round/1", "enemy");
            StringAssert.Contains("控制免疫 到期", Text(expired, catalog));
        }

        [Test] public void ReactionsAndPeriodicDamageRetainTheirOriginInsteadOfBecomingPrimaryDamage()
        {
            var catalog = new CombatStatusCatalog("hud-reactions", new[] {
                Definition("reflect", new ReflectBlockedDamageSpec(1)), Definition("leech", new LifeStealSpec(1)),
                Definition("poison", new PeriodicDamageSpec(CombatTimingKind.PhaseEnding, 3))
            });
            var state = new CombatState("hud", 0, new[] {
                new CombatantState(Player, "hero", "player", 40, 20), new CombatantState(Enemy, "enemy", "enemy", 40, 40, 3)
            }, new[] { Status("reflect/i", "reflect", Enemy, StatusDuration.UntilRemoved()),
                Status("leech/i", "leech", Player, StatusDuration.UntilRemoved()),
                Status("poison/i", "poison", Enemy, StatusDuration.ForTriggers(2)) });
            var damage = CombatEngine.Resolve(state, 0, new[] { new CombatEffect(CombatEffectKind.Damage, Player, Enemy, 8) }, statusCatalog: catalog);
            Assert.IsTrue(damage.Accepted);
            StringAssert.Contains("玩家 1：盾反 · 伤害 3", Text(damage, catalog));
            StringAssert.Contains("玩家 1：吸血 · 治疗 +5", Text(damage, catalog));
            var periodic = CombatEngine.ResolveTiming(damage.State, damage.State.Revision,
                CombatTimingSignal.PhaseEnding("hud/end", new[] { Enemy }, "enemy"), statusCatalog: catalog);
            Assert.IsTrue(periodic.Accepted);
            StringAssert.Contains("怪物 1：周期 · 伤害 3", Text(periodic, catalog));
            StringAssert.Contains("剩余 1 次", Text(periodic, catalog));
        }

        [Test] public void SnapshotDisplaysEffectiveHandLimitAndActualRelicRuntime()
        {
            var relic = new BattleRelicSetup(new RelicDefinition("bag", Array.Empty<ScoreEffect>(), charges: 3),
                new RunRelic("bag/1", "bag", level: 2, charges: 2, cooldownRounds: 1),
                new BattleRelicRules("bag-rules", ruleValues: new[] { new RelicRuleValueSpec("space", BattleRuleValueKey.HandLimit, 2) }));
            var engine = new CardDuelEngine(new BattleRules("hud", 10));
            var state = engine.Create("hud", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(Player, new CombatantDefinition("hero", 40), relics: new[] { relic }) },
                new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 40, 5), 7) });
            string text = BattleHudRuleText.Snapshot(state, Player, null);
            StringAssert.Contains("怪物 1 · 护盾 5", text);
            StringAssert.Contains("手牌 0 / 10", text);
            StringAssert.Contains("bag Lv.2 · 次数 2 · 本回合已用 0 · 冷却 1", text);
        }

        [Test] public void FullHousePreviewReportsControlAndSelfImmunityWithoutDamageOrStateMutation()
        {
            var profile = new PokerHandEffectProfile("hud-mapping", Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>()
                .Where(category => category != HandCategory.FullHouse).Select(category =>
                    new KeyValuePair<HandCategory, PokerHandDamageSettings>(category, new PokerHandDamageSettings(1, category == HandCategory.Straight ? 3 : 1))), 0.5m);
            var catalog = profile.CreateStatusCatalog("hud-catalog");
            var engine = new CardDuelEngine(new BattleRules("hud", 10, statusCatalog: catalog));
            var ranks = new[] { Rank.Two, Rank.Two, Rank.Two, Rank.Three, Rank.Three };
            var deck = ranks.Select((rank, index) => new DeckEntry("card/" + index, StandardDeck.CardId((Suit)(index % 4 + 1), rank)));
            var initial = engine.Create("hud", HmacDeckShuffler.FromSeed(new byte[32]),
                new[] { new BattlePlayerSetup(Player, new CombatantDefinition("hero", 40), deck,
                    rules: new BattlePlayerRules(handSize: 5, effectRule: new PokerHandEffectRule(profile))) },
                new[] { new BattleEnemySetup(Enemy, new CombatantDefinition("enemy", 40), 7) });
            var session = new BattleSession(engine, initial);
            Assert.IsTrue(session.Execute(initial.Revision, Player, CardDuelAction.Start).Accepted);
            var before = session.State;
            var preview = engine.PreviewPlay(before, before.Revision, Player, before.Zone(CardZoneKinds.Hand, Player).Cards);
            Assert.IsTrue(preview.Accepted);
            string text = BattleHudRuleText.Transition(preview, before.Duel, catalog);
            StringAssert.Contains("获得 眩晕", text); StringAssert.Contains("获得 控制免疫", text);
            StringAssert.DoesNotContain("伤害", text);
            Assert.AreSame(before, session.State); Assert.IsEmpty(before.Duel.Combat.Statuses);
        }

        [Test] public void UnknownStatusUsesItsConfiguredIdentityAndErrorsRemainExplicit()
        {
            var status = Status("custom/1", "custom-special", Player, StatusDuration.ForTriggers(3));
            Assert.AreEqual("custom-special（剩余 3 次）", BattleHudRuleText.Status(status, null));
            Assert.AreEqual("能量不足", BattleHudRuleText.Error(CardDuelError.InsufficientEnergy));
            Assert.AreEqual("弃牌次数已用尽", BattleHudRuleText.Error(CardDuelError.NoDiscards));
        }
    }
}
