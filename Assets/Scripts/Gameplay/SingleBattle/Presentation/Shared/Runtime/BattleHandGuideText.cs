using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BattlePresentation;
using PokerEngine;

namespace Poker.BattlePresentation
{
    /// <summary>One candidate/one snapshot: highlights, sample cards and formulas must switch together.</summary>
    public sealed class BattleHandGuideText
    {
        public const string Title = "牌型指南";
        public const string FlushFilter = "只看同花组合";
        public const string SampleLabel = "补齐示例";
        public const string EmptyPrompt = "选择牌型查看组合与效果";
        public string CandidateId { get; }
        public long Revision { get; }
        public string Heading { get; }
        public string Availability { get; }
        public string Notice { get; }
        public IReadOnlyList<string> EffectTags { get; }
        public IReadOnlyList<string> FormulaLines { get; }
        public IReadOnlyList<string> SettlementLines { get; }
        public IReadOnlyList<CardDisplayData> SampleCards { get; }
        public IReadOnlyList<string> HighlightCardIds { get; }

        public static string TypeName(HandGuideType type)
        {
            if (type == HandGuideType.Any) return "不限牌型";
            if (type == HandGuideType.RoyalFlush) return "皇家同花顺";
            if (type == HandGuideType.FiveOfAKind) return "五条";
            return Enum.TryParse(type.ToString(), out HandCategory category) ? BattleDisplayProjector.HandName(category) : type.ToString();
        }
        public static string AvailabilityText(HandGuideResult result)
        {
            if (result.Error != CardDuelError.None) return "查询不可用：" + BattlePresentationRuleText.Error(result.Error);
            switch (result.Availability)
            {
                case HandGuideAvailability.Complete: return "已凑齐 · 100%";
                case HandGuideAvailability.Completable: return "可补齐 · 还差 " + result.Candidates[0].MissingCount + " 张";
                case HandGuideAvailability.Impossible: return "无法补齐";
                default: return "组合较多，尚未完成查询";
            }
        }
        public BattleHandGuideText(HandGuidePreview preview, CombatStatusCatalog catalog)
        {
            if (preview == null) throw new ArgumentNullException(nameof(preview));
            var candidate = preview.Candidate;
            CandidateId = candidate.Id; Revision = candidate.Revision;
            Heading = candidate.Category == HandCategory.StraightFlush ? "皇家同花顺" :
                candidate.Category == HandCategory.FiveOfAKind ? "五条" : BattleDisplayProjector.HandName(candidate.Category);
            if (candidate.IsFlush) Heading += " · 同花";
            Availability = candidate.MissingCount == 0 ? "已凑齐 · 100%" : "可补齐 · 还差 " + candidate.MissingCount + " 张";
            Notice = candidate.MissingCount == 0 ? "按当前组合预览" : "补齐示例 · 使用牌库或可洗回的弃牌，不代表下次抽牌";
            HighlightCardIds = Array.AsReadOnly(candidate.Cards.Where(c => !c.IsMissing).Select(c => c.Card.Id.Value).ToArray());
            // Same CardDisplayData/art/effects contract as the hand. UI can feed these through its existing card projector.
            SampleCards = Array.AsReadOnly(candidate.Cards.Where(c => c.IsMissing).Select(c => new CardDisplayData(
                "guide/" + c.Card.Id.Value, c.Card.Id.Value, c.Card.DefinitionId.Value, BattleDisplayProjector.StandardArt(c.Face), false,
                properties: new Dictionary<string, string> { ["effects"] = ((int)c.Card.Effects).ToString(CultureInfo.InvariantCulture) })).ToArray());
            var tags = new List<string>(); var formula = new List<string>(); var settlement = new List<string>();
            if (preview.Error != CardDuelError.None) formula.Add("预览不可用：" + BattlePresentationRuleText.Error(preview.Error));
            else
            {
                formula.Add("计分：" + ScoreFormula(preview.Score));
                foreach (var component in preview.Score.Components)
                    formula.Add(BattleDisplayProjector.HandName(component.Category) + "分项：" + ScoreFormula(component));
                var steps = preview.BaseEffects?.Steps ?? Array.Empty<BattleEffectStep>();
                if (steps.Count(s => s.Effect.Kind == CombatEffectKind.Damage) > 1) tags.Add("连击");
                foreach (var step in steps)
                {
                    var effect = step.Effect;
                    string label = EffectLabel(step, catalog);
                    tags.Add(label);
                    string amount = effect.Kind == CombatEffectKind.ApplyStatus ?
                        effect.Status.ResolveStacks(preview.Hand) + " 层" :
                        effect.Kind == CombatEffectKind.Cleanse || effect.Kind == CombatEffectKind.Eliminate ? "" : N(effect.UnroundedAmount ?? effect.Amount);
                    var original = preview.RuleEffects?.Steps.FirstOrDefault(s => s.Id == step.Id);
                    bool flushHeal = effect.Kind == CombatEffectKind.Heal && preview.Hand.FlushBaseTotal > 0 &&
                        original?.Effect.Kind == CombatEffectKind.Heal && original.SourceAliases.Any(a => a.Category == HandCategory.Flush);
                    if (flushHeal)
                    {
                        decimal raw = original.Effect.UnroundedAmount ?? original.Effect.Amount;
                        formula.Add("同花治疗基值：" + preview.Hand.FlushBaseTotal + " × " + N(raw / preview.Hand.FlushBaseTotal.Value) + " = " + N(raw));
                    }
                    formula.Add("基础" + label + "：" + amount + "（" + TargetLabel(step.Target) + "）");
                    if (effect.OnHitStatus != null)
                    {
                        string status = BattlePresentationRuleText.StatusName(effect.OnHitStatus.DefinitionId, catalog);
                        tags.Add(status); formula.Add("命中后：" + status + " " + effect.OnHitStatus.ResolveStacks(preview.Hand) + " 层");
                    }
                }
                if (steps.Count == 0) formula.Add("此规则未提供声明式效果；计分不等于最终伤害");
                formula.Add("基础效果包含牌型及遗物技能修正；目标减免、收发效果修正和生命上限见目标结算");
                if (!preview.SettlementRequested) settlement.Add("尚未请求目标结算");
                else if (preview.SettlementError != CardDuelError.None)
                    settlement.Add("当前无法结算：" + BattlePresentationRuleText.Error(preview.SettlementError));
                else foreach (var e in preview.Events)
                {
                    if (e.Effect.Kind == CombatEffectKind.Heal)
                        settlement.Add("治疗 " + e.SettledAmount + " · 实际恢复 " + e.HealthRestored + " · 溢出 " + e.Overheal);
                    else if (e.Effect.Kind == CombatEffectKind.Damage)
                        settlement.Add("伤害 " + e.SettledDamage + " · 实际扣血 " + e.HealthLost + " · 护盾吸收 " + e.BlockAbsorbed);
                }
            }
            EffectTags = Array.AsReadOnly(tags.Distinct().ToArray()); FormulaLines = Array.AsReadOnly(formula.ToArray()); SettlementLines = Array.AsReadOnly(settlement.ToArray());
        }
        public static string ScoreFormula(ScoreResult score) => "向下取整(" + score.Points + " × " + N(score.Multiplier) + ")" +
            (score.UnmultipliedPoints != 0 ? " + " + score.UnmultipliedPoints : "") + " = " + score.Total;

        public static string BasicRuleSummary(HandGuideType type, bool requireFlush, BattlePlayerTurn player)
        {
            var resolver = new BattleRuleResolver();
            var query = new BattleLimitQuery(player.Rules, player.Relics);
            int straight = resolver.ResolveValue(query, BattleRuleValueKey.MinStraightCards).Value;
            int flush = resolver.ResolveValue(query, BattleRuleValueKey.MinFlushCards).Value;
            string summary;
            switch (type)
            {
                case HandGuideType.HighCard: summary = "高牌：按最高单张计分，组合不能形成更高牌型。"; break;
                case HandGuideType.OnePair: summary = "一对：2 张点数相同的牌，按这一对计分。"; break;
                case HandGuideType.TwoPair: summary = "两对：2 组不同点数的对子，按两对计分。"; break;
                case HandGuideType.ThreeOfAKind: summary = "三条：3 张点数相同的牌，按这三张计分。"; break;
                case HandGuideType.Straight: summary = "顺子：至少 " + straight + " 张点数连续的牌；A 可作最低点或最高点，不能跨端连接。"; break;
                case HandGuideType.FullHouse: summary = "葫芦：3 张相同点数的牌，加另一个点数的对子；三条与对子分别计分。"; break;
                case HandGuideType.FourOfAKind: summary = "四条：4 张点数相同的牌，按这四张计分。"; break;
                case HandGuideType.FiveOfAKind: summary = "五条：5 张点数相同的牌，需要牌组中实际存在足够的同点数牌。"; break;
                case HandGuideType.RoyalFlush: summary = "皇家同花顺：以 A 为最高点的同花顺，普通五张规则下为同花色的 10、J、Q、K、A。"; break;
                default: summary = "不限牌型：按最高可结算牌型计分，同花可作为额外属性叠加。"; break;
            }
            return summary + (requireFlush || type == HandGuideType.RoyalFlush ? "\n同花条件：至少 " + flush + " 张牌花色相同。" : "");
        }

        public static IReadOnlyList<string> BasicRuleLines(HandGuideType type, bool requireFlush, BattlePlayerTurn player,
            ScoreProfile scoring, CombatStatusCatalog catalog)
        {
            var lines = new List<string>();
            var rule = player.Rules.EffectRule as ConfiguredHandEffectRule;
            var category = type == HandGuideType.RoyalFlush ? HandCategory.StraightFlush :
                Enum.TryParse(type.ToString(), out HandCategory parsed) ? parsed : HandCategory.HighCard;
            if (type == HandGuideType.Any) lines.Add("计分由最高可结算牌型决定；选择具体牌型可查看对应公式与效果。");
            else if (type == HandGuideType.FullHouse && rule?.FullHouseComponentDamage == true)
            { AddScore(HandCategory.ThreeOfAKind, "三条分项"); AddScore(HandCategory.OnePair, "对子分项"); }
            else if (type != HandGuideType.RoyalFlush) AddScore(category, "牌型计分");
            if (rule != null)
            {
                if (type != HandGuideType.Any)
                {
                    var entry = rule.Entries.FirstOrDefault(e => e.Category == category &&
                        (e.Variant == HandEffectVariant.Any || e.Variant == HandEffectVariant.Royal));
                    if (entry != null) foreach (var step in entry.Effects) AddEffect(step);
                }
                bool flush = requireFlush || type == HandGuideType.RoyalFlush;
                if (flush && rule.ModernScoring && type != HandGuideType.RoyalFlush)
                    lines.Add("同花计分加成：计分倍率额外乘 " + N(rule.FlushDamageMultiplier));
                if (flush) foreach (var step in rule.FlushBonus) AddEffect(step);
                else lines.Add("满足同花条件时，还会叠加同花效果。");
            }
            else lines.Add("具体效果由当前战斗规则决定。");
            lines.Add("卡牌强化、遗物和目标状态会改变实际结算。");
            return lines.AsReadOnly();

            void AddScore(HandCategory hand, string label)
            {
                var score = scoring.Rule(hand); int level = player.Rules.HandLevels.LevelFor(hand);
                decimal points = score.BasePoints + (decimal)(level - 1) * score.PointsPerLevel;
                decimal multiplier = score.BaseMultiplier + (level - 1) * score.MultiplierPerLevel;
                lines.Add(label + "（等级 " + level + "）：向下取整((计分牌点数" + (points != 0 ? " + " + N(points) : "") + ") × " + N(multiplier) + ")");
            }
            void AddEffect(ConfiguredEffectStep step)
            {
                string label = step.Kind == CombatEffectKind.Damage ? step.Target.Selection == EffectTargetSelection.AllLiving ? "群体伤害" : "单体伤害" :
                    step.Kind == CombatEffectKind.Heal ? "治疗" : step.Kind == CombatEffectKind.GainBlock ? "护盾" :
                    step.Kind == CombatEffectKind.DrawCards ? "抽牌" : step.Kind == CombatEffectKind.Cleanse ? "净化" :
                    step.Kind == CombatEffectKind.Eliminate ? "消灭" : "施加状态";
                string amount = "";
                if (step.Amount != null)
                {
                    switch (step.Amount.Kind)
                    {
                        case EffectAmountKind.Constant: amount = N(step.Amount.Value); break;
                        case EffectAmountKind.ScoreTotalScale: amount = "牌型计分 × " + N(step.Amount.Value); break;
                        case EffectAmountKind.ScorePointsScale: amount = "计分点数 × " + N(step.Amount.Value); break;
                        case EffectAmountKind.ThreeOfAKindTotalScale: amount = "三条分项 × " + N(step.Amount.Value); break;
                        case EffectAmountKind.OnePairTotalScale: amount = "对子分项 × " + N(step.Amount.Value); break;
                        case EffectAmountKind.FlushBaseTotalScale: amount = "同花牌基础点数合计 × " + N(step.Amount.Value); break;
                        case EffectAmountKind.CardScoreScale: amount = "每张牌的计分 × " + N(step.Amount.Value); break;
                        default: amount = "按规则计算"; break;
                    }
                }
                if (step.Status != null) amount = StatusRule(step.Status);
                lines.Add((step.PerSubmittedCard ? "逐张结算，" : "") + label + (amount.Length > 0 ? "：" + amount : "") + "（" + TargetLabel(step.Target) + "）");
                if (step.OnHitStatus != null) lines.Add("每次命中后：" + StatusRule(step.OnHitStatus));
            }
            string StatusRule(StatusApplicationSpec status) => BattlePresentationRuleText.StatusName(status.DefinitionId, catalog) + " " + status.Stacks + " 层" +
                (status.StacksPerSubmittedCard != 0 ? "，每张出牌额外 " + status.StacksPerSubmittedCard + " 层" : "");
        }
        private static string N(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
        private static string TargetLabel(EffectTargetSpec target) => target.Group == EffectTargetGroup.Self ? "自身" :
            target.Selection == EffectTargetSelection.AllLiving ? target.Group == EffectTargetGroup.Opponents ? "所有敌人，各自结算" : "所有友方，各自结算" : "单个目标";
        private static string EffectLabel(BattleEffectStep step, CombatStatusCatalog catalog)
        {
            switch (step.Effect.Kind)
            {
                case CombatEffectKind.Damage: return step.Target.Selection == EffectTargetSelection.AllLiving && step.Target.Group != EffectTargetGroup.Self ? "群体伤害" : "单体伤害";
                case CombatEffectKind.Heal: return "治疗";
                case CombatEffectKind.GainBlock: return "护盾";
                case CombatEffectKind.ApplyStatus: return BattlePresentationRuleText.StatusName(step.Effect.Status.DefinitionId, catalog);
                case CombatEffectKind.Cleanse: return "净化";
                case CombatEffectKind.Eliminate: return "消灭";
                case CombatEffectKind.DrawCards: return "抽牌";
                default: return step.Effect.Kind.ToString();
            }
        }
    }
}
