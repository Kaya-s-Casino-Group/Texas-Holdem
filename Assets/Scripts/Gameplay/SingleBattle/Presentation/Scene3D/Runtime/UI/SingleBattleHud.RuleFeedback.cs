using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private TMP_Text ruleFeedback;
        private ScrollRect ruleScroll;
        private CardDuelState feedbackState;
        private BattleSessionResult feedbackBatch;
        private ParticipantId feedbackPlayer;
        private CardInstanceId[] feedbackSelection;
        private bool feedbackCanPreview;
        private IReadOnlyList<BattleDiagnostic> feedbackDiagnostics;
        public string RuleFeedbackText => ruleFeedback != null ? ruleFeedback.text : "";

        private void EnsureRuleFeedback()
        {
            if (ruleFeedback != null || message == null) return;
            // Supplementary text only. Keep the authored HUD, actor views and animations in place.
            var panel = new GameObject("Rules Feedback", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            panel.layer = gameObject.layer; panel.transform.SetParent(transform, false);
            var frame = (RectTransform)panel.transform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0, 1);
            frame.anchoredPosition = new Vector2(16, -184); frame.sizeDelta = new Vector2(300, 252);
            panel.GetComponent<Image>().color = new Color32(25, 30, 35, 210);
            var title = Instantiate(message, frame); title.name = "Heading"; title.gameObject.SetActive(true);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0, 1);
            title.rectTransform.anchoredPosition = new Vector2(10, -6); title.rectTransform.sizeDelta = new Vector2(280, 22);
            title.text = "规则反馈 · 滚动查看"; title.fontSize = 13; title.alignment = TextAlignmentOptions.TopLeft; title.raycastTarget = false;
            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportObject.layer = gameObject.layer; viewportObject.transform.SetParent(frame, false);
            var viewport = (RectTransform)viewportObject.transform;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(10, 8); viewport.offsetMax = new Vector2(-10, -30);
            ruleFeedback = Instantiate(message, viewport); ruleFeedback.name = "Text"; ruleFeedback.gameObject.SetActive(true);
            ruleFeedback.fontSize = 12; ruleFeedback.enableAutoSizing = false; ruleFeedback.enableWordWrapping = true;
            ruleFeedback.richText = false; ruleFeedback.alignment = TextAlignmentOptions.TopLeft;
            ruleFeedback.overflowMode = TextOverflowModes.Overflow; ruleFeedback.raycastTarget = false;
            var content = ruleFeedback.rectTransform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            ruleScroll = panel.GetComponent<ScrollRect>(); ruleScroll.viewport = viewport; ruleScroll.content = content;
            ruleScroll.horizontal = false; ruleScroll.vertical = true; ruleScroll.scrollSensitivity = 22;
            ruleScroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private void RefreshRuleFeedback(bool force = false)
        {
            if (host == null || message == null) return;
            EnsureRuleFeedback();
            var state = host.PresentedState;
            if (state == null)
            {
                if (!force && ReferenceEquals(feedbackDiagnostics, host.ContentDiagnostics)) return;
                feedbackDiagnostics = host.ContentDiagnostics;
                SetRuleFeedback(DiagnosticText() + (host.ContentDiagnostics.Count == 0 ? "等待战斗初始化" : "无法开战，请修正 XML 后重新开始"), true);
                return;
            }
            if (host.Player == null || !host.Player.IsBound) return;
            var selected = host.Actions != null && host.Actions.Hand != null ? host.Actions.Hand.Cards.Where(card => card.Interaction.IsSelected)
                .Select(card => card.Card.Id).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray() : Array.Empty<CardInstanceId>();
            bool canPreview = host.CanReceivePlayerInput;
            if (!force && ReferenceEquals(feedbackState, state) && ReferenceEquals(feedbackBatch, host.LastResolution) &&
                ReferenceEquals(feedbackDiagnostics, host.ContentDiagnostics) &&
                feedbackPlayer == host.Player.ParticipantId && feedbackSelection != null && feedbackSelection.SequenceEqual(selected) && feedbackCanPreview == canPreview) return;
            bool newBatch = !ReferenceEquals(feedbackBatch, host.LastResolution);
            feedbackState = state; feedbackBatch = host.LastResolution; feedbackPlayer = host.Player.ParticipantId;
            feedbackSelection = selected; feedbackCanPreview = canPreview;
            feedbackDiagnostics = host.ContentDiagnostics;
            var catalog = host.Engine.Duel.Definition.StatusCatalog;
            var text = new StringBuilder();
            text.Append(DiagnosticText());
            if (host.ContentDiagnostics.Any(diagnostic => diagnostic.Severity == BattleDiagnosticSeverity.Error))
                text.AppendLine("配置未生效；当前仍是上次成功创建的战斗。\n");
            if (selected.Length > 0)
            {
                text.AppendLine("【选牌预览 · " + selected.Length + " 张】");
                if (canPreview)
                {
                    text.AppendLine("出牌：");
                    text.AppendLine(BattleHudRuleText.Transition(host.PreviewSelectedPlay(), state.Duel, catalog));
                    text.AppendLine("弃牌：");
                    text.AppendLine(BattleHudRuleText.Transition(host.PreviewSelectedDiscard(), state.Duel, catalog));
                }
                else text.AppendLine(host.IsPresenting ? "正在结算，完成后更新预览" : "等待可操作窗口");
                text.AppendLine();
            }
            text.AppendLine(BattleHudRuleText.Snapshot(state, feedbackPlayer, catalog));
            if (host.LastResolution != null)
            {
                text.AppendLine(); text.AppendLine("【最近结算】");
                // The batch is authoritative already; disclose only the steps currently projected by the host.
                foreach (var step in host.LastResolution.Steps.Where(step => step.Transition.State.Revision <= state.Revision))
                {
                    text.AppendLine(BattleHudRuleText.Participant(state.Duel, step.Actor) + " · " + ActionName(step.Action));
                    text.AppendLine(BattleHudRuleText.Transition(step.Transition, state.Duel, catalog));
                }
            }
            SetRuleFeedback(text.ToString().TrimEnd(), newBatch);
        }

        private string DiagnosticText()
        {
            if (host.ContentDiagnostics.Count == 0) return "";
            return "【XML 配置诊断】\n" + string.Join("\n", host.ContentDiagnostics.Select(diagnostic =>
                (diagnostic.Severity == BattleDiagnosticSeverity.Error ? "错误 " : "提示 ") + diagnostic.Code + " · " +
                diagnostic.Location.Line + ":" + diagnostic.Location.Column + "\n" + diagnostic.Location.Path + "\n" + diagnostic.Message)) + "\n\n";
        }

        private void SetRuleFeedback(string value, bool scrollToTop)
        {
            ruleFeedback.text = value;
            ruleFeedback.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(ruleScroll.viewport.rect.height, ruleFeedback.GetPreferredValues(ruleFeedback.text, 280, Mathf.Infinity).y + 8));
            if (scrollToTop) ruleScroll.verticalNormalizedPosition = 1;
        }

        private static string ActionName(CardDuelAction action)
        {
            switch (action)
            {
                case CardDuelAction.Play: return "出牌";
                case CardDuelAction.Discard: return "弃牌";
                case CardDuelAction.EnemyAction: return "敌人行动";
                case CardDuelAction.Start: return "开战";
                case CardDuelAction.SkipPlayerTurn: return "跳过行动";
                case CardDuelAction.EndTurn: return "结束回合";
                default: return "收牌与阶段结算";
            }
        }
    }
}
