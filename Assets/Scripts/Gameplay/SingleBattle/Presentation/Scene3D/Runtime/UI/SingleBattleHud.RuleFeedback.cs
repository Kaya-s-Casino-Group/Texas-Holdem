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
        private RectTransform relicIcons;
        private readonly System.Collections.Generic.List<Image> relicIconViews = new System.Collections.Generic.List<Image>();
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
                ruleFeedback.fontSize = 12;
                ruleFeedback.lineSpacing = 3;
            }
            if (ritualLayout)
            {
                title.text = "遗物"; title.fontSize = 18;
                title.rectTransform.anchoredPosition = new Vector2(18, -18);
                relicIcons = new GameObject("Relic Icons", typeof(RectTransform)).GetComponent<RectTransform>();
                relicIcons.SetParent(frame, false); relicIcons.gameObject.layer = gameObject.layer; Place(relicIcons, 16, -52, 180, 28);
                viewport.offsetMin = new Vector2(16, 18); viewport.offsetMax = new Vector2(-16, -92);
                GetComponent<BattleDemoUITheme>()?.StylePanel(panel.GetComponent<Image>());
            }
            // A raycastable background gives the ScrollRect a wheel/drag target even though text and icons are read-only.
            panel.GetComponent<Image>().raycastTarget = true;
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
            if (ritualLayout)
            {
                if (!force && ReferenceEquals(feedbackState, snapshot)) return;
                feedbackState = snapshot;
                RefreshRelicIcons();
                string relics = Poker.BattlePresentation.BattlePresentationRuleText.Relics(host.State, host.Player.ParticipantId, host.ItemName);
                string multiplier = Poker.BattlePresentation.BattlePresentationRuleText.HandMultiplier(host.State.Duel.Player(host.Player.ParticipantId).Relics);
                SetRuleFeedback(relics + (multiplier.Length == 0 ? "" : "\n\n" + multiplier), false);
                // Content errors remain visible without mixing battle state into the relic rail.
                if (!string.IsNullOrEmpty(diagnostics))
                { message.text = diagnostics; message.gameObject.SetActive(true); }
                return;
            }
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
            if (host.State != null && host.Player != null)
                text.AppendLine(Poker.BattlePresentation.BattlePresentationRuleText.Snapshot(host.State, host.Player.ParticipantId,
                    host.Engine.Duel.Definition.StatusCatalog, host.ItemName));
            if (!string.IsNullOrEmpty(revealed))
            {
                text.AppendLine(); text.AppendLine("【最近结算】");
                // This string contains only facts revealed at the display cursor, never future authority data.
                text.AppendLine(revealed);
            }
            SetRuleFeedback(text.ToString().TrimEnd(), newBatch);
        }

        private void RefreshRelicIcons()
        {
            if (relicIcons == null || host?.State == null || host.Player == null) return;
            var relics = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics;
            while (relicIconViews.Count < relics.Count)
            {
                var icon = new GameObject("Relic", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(relicIcons, false); icon.gameObject.layer = gameObject.layer; icon.preserveAspect = true; icon.raycastTarget = false; relicIconViews.Add(icon);
            }
            float width = Mathf.Max(28, ruleScroll.viewport.rect.width);
            int columns = Mathf.Max(1, Mathf.FloorToInt(width / 28));
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)relics.Count / columns));
            ruleScroll.viewport.offsetMax = new Vector2(-16, -(64 + rows * 28));
            for (int i = 0; i < relicIconViews.Count; i++)
            {
                var icon = relicIconViews[i]; icon.gameObject.SetActive(i < relics.Count);
                if (i >= relics.Count) continue;
                icon.name = host.ItemName(relics[i].DefinitionId); icon.sprite = BattleRelicArt.Get(relics[i].DefinitionId);
                icon.enabled = icon.sprite != null;
                Place(icon.rectTransform, i % columns * 28, -(i / columns) * 28, 24, 24);
            }
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
