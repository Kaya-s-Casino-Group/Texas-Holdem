using System.Collections.Generic;
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
                outcome.text = host.IsPresenting ? "结算中…" : "选择卡牌查看效果";
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
            outcome.text = summary[0]; details.text = summary[1];
            calculation.text = summary[0] + "\n" + summary[1] + "\n\n本次计分 " + candidate.Total + "\n" + candidate.Points + " × " + candidate.Multiplier +
                "\n伤害、护盾、状态和转火已计入效果预览。";
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
            if (eliminated.Length > 0) headline = "消灭 " + eliminated.Length + " 个目标";
            else if (chain) headline = "连续攻击 " + damage.Length + " 次";
            else if (damage.Select(fact => fact.Effect.Target).Distinct().Count() > 1)
                headline = "群攻 " + damage.Select(fact => fact.Effect.Target).Distinct().Count() + " 个目标";
            else if (damage.Length > 0) headline = "造成 " + damage.Sum(fact => fact.SettledDamage) + " 伤害";
            else headline = "特殊效果";
            if (kills > 0 && eliminated.Length == 0) headline += " · 可击杀 " + kills + " 个";
            var lines = new List<string>();
            for (int index = 0; index < before.Enemies.Count; index++)
            {
                var id = before.Enemies[index].Id; var old = before.Combat.Get(id); var next = after.Combat.Get(id);
                if (!old.IsAlive || old.Health == next.Health && old.Block == next.Block) continue;
                string value = !next.IsAlive ? "击杀" : old.Health != next.Health ? "生命 " + old.Health + "→" + next.Health : "";
                if (next.IsAlive && old.Block > next.Block) value += (value.Length > 0 ? "，" : "") + "护盾 -" + (old.Block - next.Block);
                lines.Add("怪物" + (index + 1) + " " + value);
            }
            long heal = after.Combat.Get(player).Health - before.Combat.Get(player).Health;
            if (heal != 0) lines.Add(heal > 0 ? "自身回复 " + heal : "自身扣血 " + -heal);
            var statuses = preview.RuleEvents.OfType<CombatStatusEvent>().Where(fact => fact.Effect.Source == player)
                .GroupBy(fact => fact.Status.DefinitionId.Value);
            foreach (var status in statuses)
            {
                string name = BattleDemoEnemyHudModel.Status(status.Key, 1).Description;
                lines.Add(name + " " + status.Select(fact => fact.Effect.Target).Distinct().Count() + " 个目标");
            }
            return new[] { headline, string.Join(" · ", lines) };
        }
        public void OnPointerEnter(PointerEventData eventData)
        { if (cached != null && cached.CanPlay && wasReady) calculationPopup.SetActive(true); }
        public void OnPointerExit(PointerEventData eventData) => calculationPopup.SetActive(false);
        private void OnDisable() { cached = null; if (calculationPopup != null) calculationPopup.SetActive(false); }
    }
}
