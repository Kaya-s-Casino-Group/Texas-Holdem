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
        public static string ScoreFormula(ScoreResult score) => "⌊" + score.Points + " × " + N(score.Multiplier) + "⌋" +
            (score.UnmultipliedPoints != 0 ? " + " + score.UnmultipliedPoints : "") + " = " + score.Total;
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
