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
            if (index >= 0) return "玩家 " + (index + 1);
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
            var periodic = behavior as PeriodicDamageSpec;
            return periodic == null ? behavior.GetType().Name :
                (periodic.Timing == CombatTimingKind.ActionCompleted ? "流血：行动后" : "中毒：阶段末") + periodic.DamagePerStack + " 伤害/层";
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
            StatusName(status.DefinitionId, catalog) + (status.Stacks > 1 ? " ×" + status.Stacks : "") + "（" + Duration(status.Duration) + "）";

        public static string Snapshot(CardDuelState state, ParticipantId playerId, CombatStatusCatalog catalog)
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
                lines.Add(relic.DefinitionId + " Lv." + relic.Level + " · 次数 " + (relic.RemainingCharges?.ToString() ?? "不限") +
                    " · 本回合已用 " + relic.UsesThisRound + " · 冷却 " + relic.CooldownRounds);
            return string.Join("\n", lines);
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
                return owner + "：" + StatusName(status.DefinitionId, catalog) + " " + reason +
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
                    return target + origin + "伤害 " + fact.SettledDamage + (compact ? "" :
                        "（扣血 " + fact.DamageToHealth + " · 护盾吸收 " + fact.BlockAbsorbed + "）") + (fact.Died ? " · 击败" : "");
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
