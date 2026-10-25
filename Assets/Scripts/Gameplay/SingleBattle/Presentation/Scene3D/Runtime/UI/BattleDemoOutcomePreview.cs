using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using global::BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Poker.SingleBattle
{
    /// <summary>Scene-local outcome-first selection display. The existing rule preview owns every predicted result.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class BattleDemoOutcomePreview : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SingleBattleSceneAdapter host;
        [SerializeField] private TMP_Text handName, outcome, details, calculation;
        [SerializeField] private GameObject calculationPopup;
        private CandidatePresentationModel cached;
        private bool wasReady;
        public string OutcomeText => outcome != null ? outcome.text : "";
        public string DetailsText => details != null ? details.text : "";
        public void Configure(SingleBattleSceneAdapter owner, TMP_Text name, TMP_Text result, TMP_Text info, TMP_Text math, GameObject popup)
        { host = owner; handName = name; outcome = result; details = info; calculation = math; calculationPopup = popup; }
        private void LateUpdate()
        {
            if (host == null || outcome == null || host.State == null) return;
            var candidate = host.Candidate;
            bool ready = host.CanChoosePlayTarget;
            if (ReferenceEquals(cached, candidate) && wasReady == ready) return;
            cached = candidate; wasReady = ready;
            calculationPopup.SetActive(false);
            var window = host.State.FlowFor(host.Player.ParticipantId).Window;
            if (host.Player.Rules.Flow.ManualDraw && (window == BattlePlayerWindow.Draw || window == BattlePlayerWindow.ForcedDiscard))
            {
                handName.text = "弃牌阶段"; outcome.text = "已选 " + host.Actions.Hand.CaptureSelection().Cards.Count + " 张";
                details.text = "选择要弃置的手牌"; return;
            }
            if (candidate == null || candidate.State == CandidateState.Empty || candidate.State == CandidateState.Closed || candidate.State == CandidateState.Committing)
            {
                handName.text = "选牌预览";
                outcome.text = host.IsPresenting ? "结算中…" : "选择手牌查看效果";
                details.text = ""; return;
            }
            handName.text = candidate.PatternName ?? "选牌预览";
            if (!ready) { outcome.text = "正在整理手牌…"; details.text = ""; return; }
            var preview = host.PreviewSelectedPlay();
            if (preview == null || !preview.Accepted)
            {
                bool requested = host.PlayTargetChoices.Any(choice => choice.Required && choice.SelectedId == null);
                outcome.text = requested ? "请选择目标" : "当前无法出牌";
                details.text = requested ? "点击怪物指定追加伤害目标" : candidate.CanDiscard ? "可弃置这些牌" : candidate.Reason ?? "请调整选牌";
                return;
            }
            var summary = Summarize(host.State, preview, host.Player.ParticipantId);
            var rule = host.Player.Rules.EffectRule as ConfiguredHandEffectRule;
            // The seven-station profile has contextual scoring; keep existing demo presentations intact.
            if (rule?.WheelAceScoresOne == true && rule.PureFlushScoresAllCards)
            {
                PresentSelection(preview, summary, rule);
                return;
            }
            outcome.text = handName.text + "  " + summary[0]; details.text = summary[1];
            calculation.text = outcome.text + "\n" + summary[1] + "\n\n本次计分 " + candidate.Total + "\n" + candidate.Points + " × " + candidate.Multiplier +
                "\n伤害、护盾、状态和转火已计入效果预览。";
        }

        private void PresentSelection(CardDuelTransition preview, string[] summary, ConfiguredHandEffectRule rule)
        {
            var score = preview.Score;
            bool royal = preview.Pattern.Category == HandCategory.StraightFlush && preview.Pattern.TieBreakRanks[0] == (int)Rank.Ace;
            bool flush = score.Steps.Any(step => step.SourceId == "hand/flush");
            string name = royal ? "皇家同花顺" : flush && score.Category == HandCategory.HighCard ? "同花" :
                flush && score.Category == HandCategory.Straight ? "同花顺" : handName.text + (flush ? "·同花" : "");
            string formula = score.Components.Count > 0 && rule.FullHouseComponentDamage
                ? string.Join(" / ", score.Components.OrderByDescending(component => component.Category).Select(component => ComponentFormula(score, component, rule)))
                : (royal ? "计分 " : "") + ScoreFormula(score);
            string visibleFormula = score.Components.Count > 0 && rule.FullHouseComponentDamage
                ? string.Join("/", score.Components.OrderByDescending(component => component.Category).Select(component => ComponentFormula(score, component, rule, compact: true)))
                : (royal ? "计分" : "") + ScoreFormula(score, compact: true);
            var player = host.Player.ParticipantId;
            var enemies = host.State.Duel.Enemies.Select(enemy => enemy.Id).ToArray();
            var damage = preview.Events.Where(fact => fact.Effect.Source == player &&
                fact.Provenance?.Origin == CombatEffectOrigin.Primary && fact.Effect.Kind == CombatEffectKind.Damage && enemies.Contains(fact.Effect.Target)).ToArray();
            var effects = new List<string>();
            int eliminated = preview.Events.Count(fact => fact.Effect.Source == player && fact.Effect.Kind == CombatEffectKind.Eliminate && enemies.Contains(fact.Effect.Target));
            if (eliminated > 0) effects.Add("消灭 " + eliminated);
            else if (damage.Length > 0)
            {
                string kind = score.Category == HandCategory.Straight || score.Category == HandCategory.StraightFlush ? "连击" :
                    damage.Select(fact => fact.Effect.Target).Distinct().Count() > 1 ? "群攻" : "伤害";
                effects.Add(kind + " " + damage.Sum(fact => fact.SettledDamage));
            }
            var extraDetails = new List<string>();
            var healing = preview.Events.Where(fact => fact.Effect.Target == player && fact.Effect.Kind == CombatEffectKind.Heal).ToArray();
            if (healing.Length > 0)
            {
                effects.Add("治疗 " + healing.Sum(fact => fact.SettledAmount));
                extraDetails.Add("治疗 " + healing.Sum(fact => fact.SettledAmount) + " · 实际恢复 " + healing.Sum(fact => fact.HealthRestored) + " · 溢出 " + healing.Sum(fact => fact.Overheal));
            }
            long block = preview.Events.Where(fact => fact.Effect.Target == player && fact.Effect.Kind == CombatEffectKind.GainBlock)
                .Sum(fact => Math.Max(0, fact.After.Block - fact.Before.Block));
            if (block > 0) effects.Add("格挡+" + block);
            var statuses = preview.RuleEvents.Where(fact => fact.Effect?.Source == player).Select(fact =>
                fact is CombatStatusEvent applied ? applied.Status : fact is CombatStatusChangedEvent changed && changed.Reason == CombatStatusChangeReason.Refreshed
                    ? changed.StatusAfter : null).Where(status => status != null);
            effects.AddRange(statuses.Select(status => BattleDemoEnemyHudModel.Status(status.DefinitionId.Value, status.Stacks).Description).Distinct());
            if (preview.EffectPlan?.Effects.Any(effect => effect.Source == player && effect.Kind == CombatEffectKind.Cleanse) == true)
                effects.Add("净化");
            int immuneHits = damage.Count(fact => fact.FinalDamageModification?.RuleVersion == "incoming.damage-immunity.1");
            if (immuneHits > 0)
            {
                effects.Add("免疫 " + immuneHits + " 次");
                extraDetails.Add("伤害免疫 " + immuneHits + " 次 · 已按免疫后结算");
            }
            foreach (var blocked in preview.RuleEvents.OfType<CombatStatusBlockedEvent>().Where(fact => fact.Effect.Source == player))
            {
                string status = BattleDemoEnemyHudModel.Status(blocked.Status.DefinitionId.Value, blocked.Status.Stacks).Description;
                effects.Add("免疫" + status); extraDetails.Add("目标免疫" + status);
            }
            if (effects.Count == 0) effects.Add("无效果 0");
            var distinctEffects = effects.Distinct().ToArray();
            string effectLine = string.Join(" · ", distinctEffects);
            handName.text = name;
            // The skin fits the frame art to its native aspect, making the text area shorter at
            // narrow aspect ratios. Keep readable glyph sizes and move secondary tags to the
            // existing popup instead of shrinking every glyph to force two rows into that area.
            outcome.lineSpacing = outcome.paragraphSpacing = 0;
            if (outcome.rectTransform.rect.height >= 36.5f)
                outcome.text = "<line-height=14px><size=16>" + name + " " + visibleFormula + "\n</size><size=13>" +
                    CompactEffects(distinctEffects) + "</size></line-height>";
            else
            {
                string prefix = "<size=14>" + (score.Components.Count > 0 ? "" : name + " ") + visibleFormula + "</size><size=13> ";
                string compact = CompactEffects(distinctEffects, prefix);
                if (compact == null)
                {
                    // Keep both component factors and the first result readable at the supported
                    // short-footer size. Equal final factors can be shared without losing points.
                    string[] parts = visibleFormula.Split('/');
                    if (score.Components.Count == 2 && parts.Length == 2)
                    {
                        int first = parts[0].LastIndexOf('×'), second = parts[1].LastIndexOf('×');
                        if (first > 0 && second > 0 && parts[0].Substring(first) == parts[1].Substring(second))
                            visibleFormula = "(" + parts[0].Substring(0, first) + "/" + parts[1].Substring(0, second) + ")" + parts[0].Substring(first);
                    }
                    prefix = "<size=13>" + (score.Components.Count > 0 ? "" : name + " ") + visibleFormula + " ";
                    compact = distinctEffects[0].Replace(" ", "");
                }
                outcome.text = prefix + compact + "</size>";
            }
            details.text = string.Join(" · ", new[] { summary[1] }.Concat(extraDetails).Where(line => !string.IsNullOrEmpty(line)));
            calculation.enableAutoSizing = true; calculation.fontSizeMin = 10;
            calculation.fontSizeMax = Mathf.Max(13, calculation.fontSizeMax); calculation.enableWordWrapping = true;
            calculation.text = name + "\n" + formula + "\n" + effectLine + "\n" + details.text +
                "\n伤害、护盾、状态和转火已计入效果预览。";
        }

        private static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
        private string CompactEffects(IReadOnlyList<string> effects, string prefix = "")
        {
            float width = outcome.rectTransform.rect.width - outcome.margin.x - outcome.margin.z;
            for (int count = effects.Count; count > 0; count--)
            {
                string value = string.Join(" ", effects.Take(count).Select(effect => effect.Replace(" ", ""))) +
                    (count < effects.Count ? " +" + (effects.Count - count) : "");
                string measured = prefix.Length > 0 ? prefix + value + "</size>" : "<size=13>" + value + "</size>";
                if (outcome.GetPreferredValues(measured, float.PositiveInfinity, float.PositiveInfinity).x <= width)
                    return value;
            }
            return null;
        }
        private static string ScoreFormula(ScoreResult score, bool compact = false) => score.Points + (compact ? "×" : " × ") + Number(score.Multiplier) +
            (score.UnmultipliedPoints != 0 ? (compact ? "+" : " + ") + score.UnmultipliedPoints : "") + (compact ? "" : " = " + score.Total);
        private static string ComponentFormula(ScoreResult score, ScoreResult component, ConfiguredHandEffectRule rule, bool compact = false)
        {
            var kind = component.Category == HandCategory.ThreeOfAKind ? EffectAmountKind.ThreeOfAKindTotalScale : EffectAmountKind.OnePairTotalScale;
            var step = rule.Entries.Single(entry => entry.Category == HandCategory.FullHouse).Effects.FirstOrDefault(effect => effect.Kind == CombatEffectKind.Damage && effect.Amount.Kind == kind);
            string name = (component.Category == HandCategory.ThreeOfAKind ? "三条" : "对子") + (compact ? "" : " ");
            if (step == null) return name + ScoreFormula(component, compact);
            // Combining factors is exact only when the component's own multiplication did not round.
            // Otherwise retain the engine's two floor operations in the readable formula.
            decimal product = component.Points * component.Multiplier;
            if (component.UnmultipliedPoints == 0 && product == decimal.Floor(product))
                return name + component.Points + (compact ? "×" : " × ") + Number(component.Multiplier * step.Amount.Value) +
                    (compact ? "" : " = " + step.Amount.Resolve(score));
            return name + "(" + ScoreFormula(component, compact) + ")" + (compact ? "×" : " × ") + Number(step.Amount.Value) +
                (compact ? "" : " = " + step.Amount.Resolve(score));
        }
        public static string[] Summarize(CardDuelState current, CardDuelTransition preview, ParticipantId player)
        {
            var before = current.Duel; var after = preview.State.Duel;
            var enemies = before.Enemies.Select(enemy => before.Combat.Get(enemy.Id)).Where(unit => unit.IsAlive).ToArray();
            var damage = preview.Events.Where(fact => fact.Effect.Source == player &&
                fact.Provenance?.Origin == CombatEffectOrigin.Primary && fact.Effect.Kind == CombatEffectKind.Damage &&
                enemies.Any(enemy => enemy.Id == fact.Effect.Target)).ToArray();
            int kills = enemies.Count(enemy => !after.Combat.Get(enemy.Id).IsAlive);
            var eliminated = preview.Events.Where(fact => fact.Effect.Kind == CombatEffectKind.Eliminate &&
                enemies.Any(enemy => enemy.Id == fact.Effect.Target)).ToArray();
            string headline;
            bool chain = preview.Score?.Category == HandCategory.Straight || preview.Score?.Category == HandCategory.StraightFlush;
            long totalDamage = damage.Sum(fact => fact.SettledDamage);
            if (eliminated.Length > 0) headline = "消灭  " + eliminated.Length;
            else if (chain && damage.Length > 0) headline = "连击  " + totalDamage;
            else if (damage.Select(fact => fact.Effect.Target).Distinct().Count() > 1)
                headline = "群伤  " + totalDamage;
            else if (damage.Length > 0) headline = "伤害  " + totalDamage;
            else headline = "";
            var lines = new List<string>();
            if (damage.Length > 0)
                lines.Add("总伤害 " + totalDamage + " · " + damage.Length + " 次命中 · " + damage.Select(fact => fact.Effect.Target).Distinct().Count() + " 个目标");
            if (kills > 0 && eliminated.Length == 0) lines.Add("可击杀 " + kills + " 个");
            for (int index = 0; index < before.Enemies.Count; index++)
            {
                var id = before.Enemies[index].Id; var old = before.Combat.Get(id); var next = after.Combat.Get(id);
                if (!old.IsAlive || old.Health == next.Health && old.Block == next.Block) continue;
                string value = !next.IsAlive ? "击杀" : old.Health != next.Health ? "生命 " + old.Health + "→" + next.Health : "";
                if (next.IsAlive && old.Block > next.Block) value += (value.Length > 0 ? "，" : "") + "护盾 -" + (old.Block - next.Block);
                lines.Add("怪物" + (index + 1) + " " + value);
            }
            long heal = after.Combat.Get(player).Health - before.Combat.Get(player).Health;
            if (heal != 0)
            {
                string healthEffect = heal > 0 ? "回复  " + heal : "扣血  " + -heal;
                headline += (headline.Length > 0 ? " / " : "") + healthEffect;
                lines.Add(heal > 0 ? "自身回复 " + heal : "自身扣血 " + -heal);
            }
            var statuses = preview.RuleEvents.OfType<CombatStatusEvent>().Where(fact => fact.Effect.Source == player)
                .GroupBy(fact => fact.Status.DefinitionId.Value);
            var statusHeadlines = new List<string>();
            foreach (var status in statuses)
            {
                string name = BattleDemoEnemyHudModel.Status(status.Key, 1).Description;
                int targets = status.Select(fact => fact.Effect.Target).Distinct().Count();
                statusHeadlines.Add(name + "  " + targets);
                lines.Add(name + " " + targets + " 个目标");
            }
            if (headline.Length == 0) headline = statusHeadlines.Count > 0 ? string.Join(" / ", statusHeadlines) : "无效果  0";
            return new[] { headline, string.Join(" · ", lines) };
        }
        public void OnPointerEnter(PointerEventData eventData)
        { if (cached != null && cached.CanPlay && wasReady) calculationPopup.SetActive(true); }
        public void OnPointerExit(PointerEventData eventData) => calculationPopup.SetActive(false);
        private void OnDisable() { cached = null; if (calculationPopup != null) calculationPopup.SetActive(false); }
    }
}
