using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PokerEngine;

namespace Poker.BattlePresentation
{
    /// <summary>Plain-text projection of authority facts. This never calculates effects or mutates a battle.</summary>
    public static class BattlePresentationRuleText
    {
        public static string Participant(TeamBattleState state, ParticipantId id)
        {
            int index = state.Players.ToList().FindIndex(player => player.Id == id);
            if (index >= 0)
            {
                // The seven-station single-player profile speaks to the local player directly.
                // Keep numbered names for generic demos, legacy rules and multiplayer sessions.
                var rules = state.Players[index].Rules.EffectRule as ConfiguredHandEffectRule;
                return state.Players.Count == 1 && rules != null && rules.WheelAceScoresOne && rules.PureFlushScoresAllCards
                    ? "你" : "玩家 " + (index + 1);
            }
            index = state.Enemies.ToList().FindIndex(enemy => enemy.Id == id);
            return index >= 0 ? "怪物 " + (index + 1) : id.Value;
        }

        public static string StatusName(StatusDefinitionId id, CombatStatusCatalog catalog)
        {
            var definition = catalog?.Find(id);
            if (definition == null || definition.Behaviors.Count == 0) return id.Value;
            return string.Join(" + ", definition.Behaviors.Select(BehaviorName));
        }

        private static string BehaviorName(StatusBehaviorSpec behavior)
        {
            if (behavior is BlockActionWindowSpec) return "眩晕";
            if (behavior is PreventStatusApplicationSpec) return "控制免疫";
            if (behavior is ReflectBlockedDamageSpec) return "盾反 " + Percent(((ReflectBlockedDamageSpec)behavior).Ratio);
            if (behavior is LifeStealSpec) return "吸血 " + Percent(((LifeStealSpec)behavior).Ratio);
            if (behavior is OverhealDamageSpec) return "溢出治疗转伤 " + Percent(((OverhealDamageSpec)behavior).Ratio);
            if (behavior is DamageImmunitySpec) return "伤害免疫：每层抵挡一段正数伤害（含毒和反伤，不免疫状态）";
            if (behavior is IncomingAttackDamagePerStackSpec bleeding)
                return "流血：每层使敌人攻击伤害 +" + Percent(bleeding.Rate) + "（护盾前，不影响道具自伤）";
            if (behavior is StackDecaySpec decay)
            {
                string owner = decay.TimingSubject == PeriodicTimingSubject.Applier ? "施加者" : "持有者";
                string when = decay.Timing == CombatTimingKind.ActionCompleted ? "每次有效行动后" :
                    decay.Timing == CombatTimingKind.TurnStarted ? "回合开始" : "所属阵营阶段结束";
                return owner + when + "移除 " + decay.Stacks + " 层";
            }
            var periodic = behavior as PeriodicDamageSpec;
            if (periodic == null) return behavior.GetType().Name;
            string subject = periodic.TimingSubject == PeriodicTimingSubject.Applier ? "施加者" : "持有者";
            string timing = periodic.Timing == CombatTimingKind.ActionCompleted ? "每次有效行动后" :
                periodic.Timing == CombatTimingKind.TurnStarted ? "回合开始" : "所属阵营阶段结束";
            return subject + timing + "：" + periodic.DamagePerStack + " 伤害/层" +
                (periodic.StackDecay > 0 ? " · 每次移除 " + periodic.StackDecay + " 层" : " · 层数不衰减");
        }

        private static string Percent(decimal ratio) => ratio <= decimal.MaxValue / 100
            ? (ratio * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%"
            : ratio.ToString("0.##", CultureInfo.InvariantCulture) + " 倍";

        public static string Duration(StatusDuration duration)
        {
            switch (duration.Kind)
            {
                case DurationKind.TriggerCount: return "剩余 " + duration.RemainingTriggers + " 次";
                case DurationKind.UntilRoundEnd: return "至回合结束";
                case DurationKind.UntilPhaseEnd:
                    return "至" + (duration.PhaseKey == BattleStatusTiming.PlayerPhase ? "玩家" :
                        duration.PhaseKey == BattleStatusTiming.EnemyPhase ? "敌人" : duration.PhaseKey) + "阶段结束";
                default: return "持续生效";
            }
        }

        public static string Status(StatusInstance status, CombatStatusCatalog catalog) =>
            StatusName(status.DefinitionId, catalog) + (status.Stacks > 1 ||
                catalog?.Find(status.DefinitionId)?.Behaviors.Any(b => b is IncomingAttackDamagePerStackSpec || b is DamageImmunitySpec) == true ? " ×" + status.Stacks : "") +
            "（" + Duration(status.Duration) + "）";

        public static string Snapshot(CardDuelState state, ParticipantId playerId, CombatStatusCatalog catalog, Func<string, string> name = null)
        {
            var lines = new List<string> { "【当前状态】" };
            foreach (var unit in state.Duel.Combat.Combatants)
            {
                var statuses = state.Duel.Combat.Statuses.Where(status => status.Target.Kind == EntityKind.Participant && status.Target.Id == unit.Id.Value);
                lines.Add(Participant(state.Duel, unit.Id) + " · 护盾 " + unit.Block + (unit.IsAlive ? "" : " · 已阵亡"));
                lines.Add("  " + (statuses.Any() ? string.Join("；", statuses.Select(status => Status(status, catalog))) : "无状态"));
            }
            var player = state.Duel.Player(playerId);
            lines.Add("手牌 " + state.Zone(CardZoneKinds.Hand, playerId).Cards.Count + " / " + player.Limits.HandLimit.Value);
            lines.Add("【当前玩家遗物】");
            if (player.Relics.Relics.Count == 0) lines.Add("无遗物");
            foreach (var relic in player.Relics.Relics)
                lines.Add((name?.Invoke(relic.DefinitionId) ?? relic.DefinitionId) + " Lv." + relic.Level + " · 次数 " + (relic.RemainingCharges?.ToString() ?? "不限") +
                    " · 本回合已用 " + relic.UsesThisRound + " · 冷却 " + relic.CooldownRounds);
            return string.Join("\n", lines);
        }

        public static string Relics(CardDuelState state, ParticipantId playerId, Func<string, string> name = null)
        {
            var relics = state.Duel.Player(playerId).Relics.Relics;
            return relics.Count == 0 ? "无遗物" : string.Join("\n\n", relics.Select(relic =>
                (name?.Invoke(relic.DefinitionId) ?? relic.DefinitionId) + "  Lv." + relic.Level + "\n" + RelicRuntime(relic) + "\n" +
                RelicDescription(state.Duel.Player(playerId).Relics.RulesFor(relic.InstanceId))));
        }

        public static string RelicDescription(BattleRelicRules rules)
        {
            if (rules == null) return "";
            var lines = new List<string>(); var item = rules.Item;
            if (item != null)
            {
                if (item.Activation == BattleItemActivation.Effects)
                    foreach (var step in item.Effects)
                    {
                        string group = step.Target.Group == CombatTargetGroup.Self ? "自身" :
                            (step.Target.Selection == CombatTargetSelection.AllLiving ? "所有" :
                            step.Target.Selection == CombatTargetSelection.LowestHealth ? "生命最低的" :
                            step.Target.Selection == CombatTargetSelection.HighestHealth ? "生命最高的" : "首个") +
                            (step.Target.Group == CombatTargetGroup.Opponents ? "敌人" : "友方");
                        string effect = step.Effect.Kind == CombatEffectKind.Heal ? "恢复生命 " :
                            step.Effect.Kind == CombatEffectKind.GainBlock ? "获得护盾 " : "受到伤害 ";
                        lines.Add(group + effect + step.Effect.Amount + "。");
                    }
                if (item.Activation == BattleItemActivation.ReplaceHeld)
                    lines.Add("替换另一件持有遗物。所有等级中等概率抽取；排除原道具、六面骰及已达持有上限的道具。");
                if (item.Activation == BattleItemActivation.Roulette)
                    lines.Add("使用时固定随机抽取 1–" + item.RouletteTurns + " 回合中的爆炸时机；可能立刻爆炸。每回合牌型最终伤害累乘 " +
                        item.RouletteBase.ToString("0.##", CultureInfo.InvariantCulture) + "；爆炸对自己造成 " + item.ExplosionDamage + " 伤害。状态结束后可再用。");
                if (item.CooldownBattles > 0) lines.Add("使用后冷却 " + item.CooldownBattles + " 场战斗，当前场计第 1 场。");
                if (item.RemoveOnDepleted) lines.Add("剩余次数跨关保留，用完移除。");
                if (item.ReviveRatio > 0) lines.Add("死亡时自动消耗，复活至最大生命的 " + Percent(item.ReviveRatio) + "（向下取整），清空护盾和全部状态。");
                if (item.ExecuteChance > 0) lines.Add("出牌每段对每个命中敌人独立判定 " + Percent(item.ExecuteChance) + " 击杀概率；道具和毒伤不触发。");
                if (item.ReflectRatio > 0) lines.Add("将护盾实际挡住伤害的 " + Percent(item.ReflectRatio) + " 反给攻击者。");
                if (item.LocationEnterHeal > 0) lines.Add("进入每个地点恢复 " + item.LocationEnterHeal + " 生命。");
                if (item.LocationLeaveGoldRatio > 0) lines.Add("离开每个地点时，金币增加 " + Percent(item.LocationLeaveGoldRatio) + "。");
            }
            foreach (var program in rules.Programs)
            {
                string scope = program.Hand.HasValue ? (program.ExcludeHand ? "非" : "") + BattleDisplayProjector.HandName(program.Hand.Value) : "所有牌型";
                if (program.Kind == RelicProgramKind.ScoreMultiplier)
                    lines.Add(scope + "倍率 " + (program.Amount >= 0 ? "+" : "") + program.Amount.ToString("0.##", CultureInfo.InvariantCulture) + "。");
                else if (program.Kind == RelicProgramKind.HealRatioAdd)
                    lines.Add(scope + "治疗比例增加 " + (program.Amount * 100).ToString("0.##", CultureInfo.InvariantCulture) + " 个百分点。");
            }
            return string.Join("\n", lines);
        }

        public static string RelicRuntime(BattleRelicState relic)
        {
            var value = "次数 " + (relic.RemainingCharges?.ToString() ?? "不限") + " · 本回合已用 " + relic.UsesThisRound;
            if (relic.CooldownRounds > 0) value += "\n回合冷却 " + relic.CooldownRounds;
            long battles = relic.Memory.CounterValue(BattleItemRuntime.CooldownBattlesKey);
            if (battles > 0) value += "\n战斗冷却 " + battles + " 场（当前场计入）";
            if (relic.Memory.CounterValue(BattleItemRuntime.RouletteActiveKey) != 0) value += "\n转盘生效中";
            return value;
        }

        public static string HandMultiplier(BattleRelicLoadout relics)
        {
            if (!relics.Relics.Any(relic => relic.Memory.CounterValue(BattleItemRuntime.RouletteActiveKey) != 0)) return "";
            try { return "转盘 · 当前牌型伤害 ×" + BattleItemRuntime.HandMultiplier(relics).ToString("0.##", CultureInfo.InvariantCulture); }
            catch (OverflowException) { return "转盘倍率超出可显示范围"; }
        }

        public static IReadOnlyList<string> Events(IEnumerable<CombatRuleEvent> events, TeamBattleState state,
            CombatStatusCatalog catalog, bool compact = false)
        {
            var lines = new List<string>();
            foreach (var item in events)
            {
                string text = Event(item, state, catalog, compact);
                if (!string.IsNullOrEmpty(text)) lines.Add(text);
            }
            return lines;
        }

        private static string Event(CombatRuleEvent item, TeamBattleState state, CombatStatusCatalog catalog, bool compact)
        {
            var skipped = item as CombatActionWindowSkippedEvent;
            if (skipped != null) return Participant(state, skipped.Actor) + "：眩晕，跳过行动";
            var changed = item as CombatStatusChangedEvent;
            if (changed != null)
            {
                var status = changed.StatusAfter ?? changed.StatusBefore;
                string reason = changed.Reason == CombatStatusChangeReason.Cleansed ? "净化" : changed.Reason == CombatStatusChangeReason.Expired ? "到期" :
                    changed.Reason == CombatStatusChangeReason.Refreshed ? "刷新" : changed.StatusAfter == null ? "耗尽" : "消耗";
                string owner = status.Target.Kind == EntityKind.Participant ? Participant(state, new ParticipantId(status.Target.Id)) : status.Target.Id;
                string layers = catalog?.Find(status.DefinitionId)?.Behaviors.Any(b => b is StackDecaySpec) == true &&
                    changed.StatusBefore.Stacks != (changed.StatusAfter?.Stacks ?? 0) ?
                    "（" + changed.StatusBefore.Stacks + "→" + (changed.StatusAfter?.Stacks ?? 0) + " 层）" : "";
                return owner + "：" + StatusName(status.DefinitionId, catalog) + " " + reason +
                    layers +
                    (changed.StatusAfter == null ? "" : "（" + Duration(changed.StatusAfter.Duration) + "）");
            }
            var effect = item.Effect;
            if (effect == null) return null;
            string target = Participant(state, effect.Target) + "：";
            if (item is CombatStatusBlockedEvent) return target + "免疫 " + StatusName(effect.Status.DefinitionId, catalog);
            if (item is CombatStatusEvent) return target + "获得 " + Status(effect.Status, catalog);
            var fact = item as CombatEvent;
            if (fact == null) return null;
            string origin = Origin(item.Provenance?.Origin ?? CombatEffectOrigin.Primary);
            switch (effect.Kind)
            {
                case CombatEffectKind.Damage:
                    string bleeding = string.Join("；", fact.IncomingModification?.Triggers
                        .Where(t => t.SourceId == "incoming.attack-per-stack").Select(t => t.Reason) ?? Enumerable.Empty<string>());
                    return target + origin + "伤害 " + fact.SettledDamage + (compact ? "" :
                        "（扣血 " + fact.DamageToHealth + " · 护盾吸收 " + fact.BlockAbsorbed + "）") +
                        (bleeding.Length == 0 ? "" : " · 流血增伤：" + bleeding) + (fact.Died ? " · 击败" : "");
                case CombatEffectKind.Revive: return target + "复活 · 生命 " + fact.After.Health + " / " + fact.After.MaxHealth + " · 护盾和状态清空";
                case CombatEffectKind.LoseHealth: return target + "失去生命 " + (fact.Before.Health - fact.After.Health) + (fact.Died ? " · 阵亡" : "");
                case CombatEffectKind.Heal:
                    return target + origin + "治疗 +" + fact.HealthRestored + (fact.Overheal > 0 ? "（溢出 " + fact.Overheal + "）" : "");
                case CombatEffectKind.GainBlock: return target + origin + "护盾 +" + (fact.After.Block - fact.Before.Block);
                case CombatEffectKind.ClearBlock: return target + "护盾清空";
                case CombatEffectKind.Eliminate: return target + "消灭";
                default: return null;
            }
        }

        private static string Origin(CombatEffectOrigin origin)
        {
            switch (origin)
            {
                case CombatEffectOrigin.Reflection: return "盾反 · ";
                case CombatEffectOrigin.Lifesteal: return "吸血 · ";
                case CombatEffectOrigin.Overheal: return "溢出转伤 · ";
                case CombatEffectOrigin.Periodic: return "周期 · ";
                case CombatEffectOrigin.Triggered: return "触发 · ";
                default: return "";
            }
        }

        public static string Transition(CardDuelTransition result, TeamBattleState names, CombatStatusCatalog catalog)
        {
            if (result == null) return "等待可操作窗口";
            if (!result.Accepted) return "不可执行：" + Error(result.Error);
            var lines = new List<string>();
            var hand = result.ActionCompletion?.Context.Hand;
            if (hand != null && hand.UsesCompatibleEffects && hand.CompatibleCategories.Count > 1)
                lines.Add("兼容牌型：" + string.Join("、", hand.CompatibleCategories.Select(BattleDisplayProjector.HandName)));
            if (result.EffectPlan != null && !string.IsNullOrEmpty(result.EffectPlan.Explanation)) lines.Add(result.EffectPlan.Explanation);
            if (result.ActionCost != null) lines.Add("消耗能量 " + result.ActionCost.Cost);
            lines.AddRange(Events(result.RuleEvents, names, catalog));
            foreach (var activation in result.RelicActivations)
                lines.Add("遗物触发：" + activation.Before.DefinitionId + " / " + activation.TriggerId);
            foreach (var draw in result.Draws.Where(draw => draw.DrawnCount > 0))
                lines.Add(Participant(names, draw.Plan.PlayerId) + "：补牌 " + draw.DrawnCount);
            return lines.Count == 0 ? "无额外效果" : string.Join("\n", lines);
        }

        public static string Error(CardDuelError error)
        {
            switch (error)
            {
                case CardDuelError.InsufficientEnergy: return "能量不足";
                case CardDuelError.NoDiscards: return "弃牌次数已用尽";
                case CardDuelError.InvalidSelection: return "选牌数量或卡牌不符合要求";
                case CardDuelError.ActionBlocked: return "当前行动被控制";
                case CardDuelError.InvalidPhase: return "当前阶段不可行动";
                case CardDuelError.Overflow: return "数值超限";
                default: return error.ToString();
            }
        }
    }
}
