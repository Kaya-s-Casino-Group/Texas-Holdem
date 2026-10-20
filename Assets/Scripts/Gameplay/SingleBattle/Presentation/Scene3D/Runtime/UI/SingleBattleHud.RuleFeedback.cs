using System;
using System.Text;
using global::BattlePresentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private TMP_Text ruleFeedback;
        private ScrollRect ruleScroll;
        private BattleDisplaySnapshot feedbackState;
        private CandidatePresentationModel feedbackCandidate;
        private string feedbackBatchId, feedbackFacts, feedbackDiagnostics;
        public string RuleFeedbackText => ruleFeedback != null ? ruleFeedback.text : "";

        private void EnsureRuleFeedback()
        {
            if (ruleFeedback != null || message == null) return;
            // Supplementary text only. Keep the authored HUD, actor views and animations in place.
            var panel = new GameObject("Rules Feedback", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            panel.layer = gameObject.layer; panel.transform.SetParent(feedbackContainer != null ? feedbackContainer : transform, false);
            var frame = (RectTransform)panel.transform;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1, 1);
            frame.anchoredPosition = new Vector2(-24, -115); frame.sizeDelta = new Vector2(210, 250);
            panel.GetComponent<Image>().color = new Color32(25, 30, 35, 210);
            var title = Instantiate(message, frame); title.name = "Heading"; title.gameObject.SetActive(true);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0, 1);
            title.rectTransform.anchoredPosition = new Vector2(10, -6); title.rectTransform.sizeDelta = new Vector2(190, 22);
            title.text = "状态 / 遗物"; title.fontSize = 13; title.alignment = TextAlignmentOptions.TopLeft; title.raycastTarget = false;
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
            if (feedbackContainer != null)
            {
                frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one;
                frame.offsetMin = frame.offsetMax = Vector2.zero;
                panel.GetComponent<Image>().color = new Color32(29, 25, 37, 225);
                title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = Vector2.one;
                title.rectTransform.anchoredPosition = new Vector2(6, -12); title.rectTransform.sizeDelta = new Vector2(-12, 42);
                title.text = "状态 / 遗物"; title.fontSize = 12; title.enableWordWrapping = true;
                viewport.offsetMin = new Vector2(6, 10); viewport.offsetMax = new Vector2(-6, -54);
                ruleFeedback.fontSize = 11;
            }
        }

        private void RefreshRuleFeedback(bool force = false)
        {
            if (host == null || message == null) return;
            EnsureRuleFeedback();
            var snapshot = host.Display;
            string diagnostics = host.ContentDiagnosticsText ?? "";
            if (snapshot == null)
            {
                if (!force && feedbackDiagnostics == diagnostics) return;
                feedbackDiagnostics = diagnostics;
                SetRuleFeedback(diagnostics + (diagnostics.Length == 0 ? "等待战斗初始化" : "无法开战，请修正 XML 后重新开始"), true);
                return;
            }
            var candidate = host.Candidate;
            string revealed = host.DisplayFeedback ?? "";
            if (!force && ReferenceEquals(feedbackState, snapshot) && ReferenceEquals(feedbackCandidate, candidate) &&
                feedbackFacts == revealed && feedbackDiagnostics == diagnostics) return;
            bool newBatch = feedbackBatchId != snapshot.Cursor.BatchId;
            feedbackState = snapshot; feedbackCandidate = candidate; feedbackFacts = revealed;
            feedbackDiagnostics = diagnostics; feedbackBatchId = snapshot.Cursor.BatchId;
            var text = new StringBuilder(); text.Append(diagnostics);
            if (candidate != null && candidate.SelectedIds.Count > 0 && candidate.State != CandidateState.Closed && candidate.State != CandidateState.Committing)
            {
                text.AppendLine("【选牌预览 · " + candidate.SelectedIds.Count + " 张】");
                text.AppendLine("出牌：");
                text.AppendLine(candidate.Labels.TryGetValue("playPreview", out var playPreview) ? playPreview : candidate.Reason ?? "当前选择不可用");
                text.AppendLine("弃牌：");
                text.AppendLine(candidate.Labels.TryGetValue("discardPreview", out var discardPreview) ? discardPreview : candidate.Reason ?? "当前选择不可用");
                text.AppendLine();
            }
            if (snapshot.Labels.TryGetValue("statusSummary", out var statusText)) text.AppendLine(statusText);
            if (!string.IsNullOrEmpty(revealed))
            {
                text.AppendLine(); text.AppendLine("【最近结算】");
                // This string contains only facts revealed at the display cursor, never future authority data.
                text.AppendLine(revealed);
            }
            SetRuleFeedback(text.ToString().TrimEnd(), newBatch);
        }

        private void SetRuleFeedback(string value, bool scrollToTop)
        {
            ruleFeedback.text = value;
            ruleFeedback.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(ruleScroll.viewport.rect.height, ruleFeedback.GetPreferredValues(ruleFeedback.text, Mathf.Max(1,ruleScroll.viewport.rect.width), Mathf.Infinity).y + 8));
            if (scrollToTop) ruleScroll.verticalNormalizedPosition = 1;
        }

    }
}
