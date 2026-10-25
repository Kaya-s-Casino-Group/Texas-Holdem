using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PokerEngine;
using Poker.BattlePresentation;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Scene-local read-only icon projection of the resolver's forecast; no text parsing or combat calculation.</summary>
    public static class BattleDemoEnemyHudModel
    {
        public sealed class Badge
        {
            public BattleDemoEnemySymbol Symbol { get; }
            public Color Color { get; }
            public string Value { get; }
            public string Description { get; }
            public Badge(BattleDemoEnemySymbol symbol, Color color, string value, string description)
            { Symbol = symbol; Color = color; Value = value; Description = description; }
        }
        public static readonly Color AttackColor = new Color32(245, 71, 72, 255);
        public static readonly Color ShieldColor = new Color32(224, 231, 240, 255);
        public static readonly Color PoisonColor = new Color32(144, 221, 91, 255);
        private static readonly Color Gold = new Color32(255, 208, 88, 255), Green = new Color32(102, 221, 158, 255), Purple = new Color32(193, 136, 239, 255);
        private static string Number(long amount) => amount.ToString(CultureInfo.InvariantCulture);
        public static Badge Status(string id, long stacks)
        {
            BattleDemoEnemySymbol symbol; Color color; string name;
            switch (id)
            {
                case "bleed": symbol = BattleDemoEnemySymbol.Poison; color = PoisonColor; name = "中毒"; break;
                case "elite-bleeding": symbol = BattleDemoEnemySymbol.Bleeding; color = AttackColor; name = "流血"; break;
                case "stun": symbol = BattleDemoEnemySymbol.Stun; color = Gold; name = "眩晕"; break;
                case "control-immunity": symbol = BattleDemoEnemySymbol.Immunity; color = new Color32(111, 203, 247, 255); name = "控制免疫"; break;
                case "shield-reflect": symbol = BattleDemoEnemySymbol.Reflect; color = ShieldColor; name = "盾反"; break;
                case "life-steal": symbol = BattleDemoEnemySymbol.LifeSteal; color = AttackColor; name = "吸血"; break;
                case "overheal-damage": symbol = BattleDemoEnemySymbol.Overheal; color = Green; name = "溢出治疗转伤"; break;
                case "layer-armor": symbol = BattleDemoEnemySymbol.Armor; color = Gold; name = "多段护甲"; break;
                case "wrath-shell": symbol = BattleDemoEnemySymbol.Immunity; color = ShieldColor; name = "龟缩·伤害免疫"; break;
                default: symbol = BattleDemoEnemySymbol.Unknown; color = Purple; name = id; break;
            }
            return new Badge(symbol, color, Number(stacks), name);
        }
        public static IReadOnlyList<Badge> Intents(EnemyActionPreview preview, long? wrathChargeIncrease = null, EnemyBehaviorAction plannedAction = null)
        {
            if (preview == null || !preview.Accepted) return new[] { new Badge(BattleDemoEnemySymbol.Unknown, Gold, "?", "意图不可用") };
            if (preview.IsSkipped) return new[] { new Badge(BattleDemoEnemySymbol.Stun, Gold, "", "眩晕：跳过本次行动") };
            var badges = new List<Badge>();
            // Reapplying a status is an announced effect; its later consumption and decay are bookkeeping.
            var effects = BattleDisplayProjector.IntentEvents(preview, plannedAction).Where(item => item.Effect?.Source == preview.Source &&
                item.Provenance?.Origin == CombatEffectOrigin.Primary && (!(item is CombatStatusChangedEvent changed) ||
                    changed.Reason == CombatStatusChangeReason.Refreshed && item.Effect.Kind == CombatEffectKind.ApplyStatus)).ToArray();
            foreach (var group in effects.GroupBy(item => new { item.Effect.Kind, Status = item.Effect.Status?.DefinitionId.Value,
                Amount = item is CombatEvent hit ? hit.SettledAmount : item.Effect.Status?.Stacks ?? item.Effect.Amount, item.Effect.Target }))
            {
                long amount = group.Key.Amount;
                var value = Number(amount) + (group.Count() > 1 ? "×" + group.Count() : "");
                Badge badge;
                switch (group.Key.Kind)
                {
                    case CombatEffectKind.Damage:
                        var bleeding = group.OfType<CombatEvent>().Where(hit => hit.SettledAmount > 0 && hit.IncomingModification != null)
                            .SelectMany(hit => hit.IncomingModification.Triggers)
                            .Where(trigger => trigger.SourceId == "incoming.attack-per-stack")
                            .Select(trigger => trigger.Reason).Distinct().ToArray();
                        badge = new Badge(BattleDemoEnemySymbol.Sword, AttackColor, value, "预计伤害：" + value + "（格挡前）" +
                            (bleeding.Length == 0 ? "" : "\n已含流血加伤：" + string.Join("；", bleeding)));
                        break;
                    case CombatEffectKind.GainBlock:
                        badge = wrathChargeIncrease.HasValue && amount == 0 && group.Key.Target == preview.Source
                            ? new Badge(BattleDemoEnemySymbol.Buff, Gold, "+" + Number(wrathChargeIncrease.Value),
                                "蓄力：攻击力永久增加 " + Number(wrathChargeIncrease.Value))
                            : new Badge(BattleDemoEnemySymbol.Shield, ShieldColor, value, "防御：获得 " + value + " 护盾");
                        break;
                    case CombatEffectKind.Heal: case CombatEffectKind.Revive: badge = new Badge(BattleDemoEnemySymbol.Heal, Green, value, "治疗 / 复活：" + value); break;
                    case CombatEffectKind.ApplyStatus:
                        var status = Status(group.Key.Status, amount); badge = new Badge(status.Symbol, status.Color, status.Value, "施加状态：" + status.Description + " ×" + status.Value); break;
                    case CombatEffectKind.Cleanse: badge = new Badge(BattleDemoEnemySymbol.Cleanse, Green, "", "净化负面状态"); break;
                    case CombatEffectKind.ClearBlock: badge = new Badge(BattleDemoEnemySymbol.Debuff, Purple, "", "移除护盾"); break;
                    case CombatEffectKind.Eliminate: badge = new Badge(BattleDemoEnemySymbol.Poison, AttackColor, "!", "消灭目标"); break;
                    case CombatEffectKind.DrawCards: badge = new Badge(BattleDemoEnemySymbol.Cards, new Color32(109, 195, 246, 255), value, "抽牌：" + value); break;
                    case CombatEffectKind.LoseHealth: badge = new Badge(BattleDemoEnemySymbol.LifeSteal, AttackColor, value, "失去生命：" + value); break;
                    default: badge = new Badge(BattleDemoEnemySymbol.Unknown, Purple, "?", group.Key.Kind.ToString()); break;
                }
                badges.Add(badge);
            }
            if (badges.Count == 0) badges.Add(new Badge(BattleDemoEnemySymbol.Unknown, Gold, "…", "特殊行动"));
            return badges;
        }
    }
}
