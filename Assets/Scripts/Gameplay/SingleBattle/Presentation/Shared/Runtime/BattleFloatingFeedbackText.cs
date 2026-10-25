using System;
using System.Globalization;
using System.Linq;
using PokerEngine;

namespace Poker.BattlePresentation
{
    /// <summary>Short labels for already committed facts; the detailed rule log remains separate.</summary>
    internal static class BattleFloatingFeedbackText
    {
        internal static bool IsDamageImmune(CombatEvent fact) => fact?.Effect.Kind == CombatEffectKind.Damage &&
            fact.FinalDamageModification?.RuleVersion == "incoming.damage-immunity.1";

        internal static string Event(CombatRuleEvent fact, TeamBattleState state, CombatStatusCatalog catalog)
        {
            if (fact is CombatActionWindowSkippedEvent) return "眩晕 · 跳过行动";
            if (fact is CombatStatusChangedEvent changed)
            {
                var status = changed.StatusAfter ?? changed.StatusBefore;
                var name = StatusName(status.DefinitionId, catalog);
                var owner = Owner(status, state);
                long remaining = changed.StatusAfter?.Stacks ?? 0;
                if (changed.Reason == CombatStatusChangeReason.Consumed &&
                    catalog?.Find(status.DefinitionId)?.Behaviors.Any(behavior => behavior is DamageImmunitySpec) == true)
                    return owner + "：免疫伤害 · " + (remaining == 0 ? "耗尽" : "余" + Number(remaining) + "层");
                if (changed.StatusBefore.Stacks != remaining)
                    return owner + "：" + name + " " + Number(changed.StatusBefore.Stacks) + "→" + Number(remaining) + "层";
                return owner + "：" + name + " " + (changed.Reason == CombatStatusChangeReason.Cleansed ? "净化" :
                    changed.Reason == CombatStatusChangeReason.Expired ? "结束" : changed.Reason == CombatStatusChangeReason.Refreshed ? "刷新" : "消耗");
            }
            if (fact is CombatStatusBlockedEvent blocked) return "免疫 " + StatusName(blocked.Effect.Status.DefinitionId, catalog);
            if (fact is CombatStatusEvent applied)
                return Owner(applied.Status, state) + "：获得 " + StatusName(applied.Status.DefinitionId, catalog) +
                    (applied.Status.Stacks > 1 ? " ×" + Number(applied.Status.Stacks) : "");
            return null;
        }

        private static string Owner(StatusInstance status, TeamBattleState state) => status.Target.Kind == EntityKind.Participant ?
            BattlePresentationRuleText.Participant(state, new ParticipantId(status.Target.Id)) : status.Target.Id;
        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
        private static string StatusName(StatusDefinitionId id, CombatStatusCatalog catalog)
        {
            var behaviors = catalog?.Find(id)?.Behaviors;
            if (behaviors == null) return id.Value;
            var names = behaviors.Select(behavior => behavior is BlockActionWindowSpec ? "眩晕" :
                behavior is PreventStatusApplicationSpec ? "控制免疫" : behavior is DamageImmunitySpec ? "伤害免疫" :
                behavior is IncomingAttackDamagePerStackSpec ? "流血" : behavior is PeriodicDamageSpec ? "中毒" :
                behavior is ReflectBlockedDamageSpec ? "盾反" : behavior is LifeStealSpec ? "吸血" :
                behavior is OverhealDamageSpec ? "溢出转伤" : null).Where(name => name != null).Distinct().ToArray();
            return names.Length > 0 ? string.Join("·", names) : id.Value;
        }
    }
}
