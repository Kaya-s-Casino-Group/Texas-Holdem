using System;
using System.Collections.Generic;
using System.Linq;
using Poker.BattlePresentation;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private RectTransform battleToolsPanel, battleToolsBody, battleToolsContent;
        private Button battleToolsToggle;
        private ScrollRect battleToolsScroll;
        private readonly List<GameObject> battleToolsRows = new List<GameObject>();
        private readonly List<KeyValuePair<Button, Func<bool>>> battleToolsAvailability = new List<KeyValuePair<Button, Func<bool>>>();
        private CardDuelState battleToolsState;
        private global::BattlePresentation.CandidatePresentationModel battleToolsCandidate;
        private string battleToolsKey, selectedItem, selectedItemTarget, itemFeedback;
        private string battleToolsMatchId, battleToolsObserverId;
        private bool battleToolsExpanded;
        private float battleToolsTop;
        public Button BattleToolsButton => battleToolsToggle;
        public string SelectedItemId => selectedItem;
        internal DeskBattleLayout DeskLayout { get; set; }
        private bool UsesDeskLayout => DeskLayout != null && DeskLayout.isActiveAndEnabled;

        private void EnsureBattleTools()
        {
            if (!Application.isPlaying || battleToolsPanel != null || restart == null || message == null) return;
            battleToolsPanel = new GameObject("Battle Tools", typeof(RectTransform)).GetComponent<RectTransform>();
            battleToolsPanel.SetParent(transform, false); battleToolsPanel.gameObject.layer = gameObject.layer;
            battleToolsPanel.anchorMin = battleToolsPanel.anchorMax = battleToolsPanel.pivot = new Vector2(0, 1);
            battleToolsPanel.anchoredPosition = new Vector2(24, -158);
            battleToolsPanel.sizeDelta = new Vector2(440, 36);
            battleToolsToggle = CreateToolsButton(battleToolsPanel, GetComponent<BattleDemoRelicArt>() != null ? "选敌 · 结算预览" : "选敌 · 道具 · 结算预览", () => {
                battleToolsExpanded = !battleToolsExpanded; RefreshBattleTools(true);
            }, 0, 36);
            battleToolsBody = new GameObject("Details", typeof(RectTransform), typeof(Image), typeof(ScrollRect)).GetComponent<RectTransform>();
            battleToolsBody.SetParent(battleToolsPanel, false); battleToolsBody.gameObject.layer = gameObject.layer; Place(battleToolsBody, 0, -40, 440, 490);
            battleToolsBody.GetComponent<Image>().color = new Color32(25, 30, 35, 245);
            GetComponent<BattleDemoUITheme>()?.StylePanel(battleToolsBody.GetComponent<Image>());
            battleToolsBody.GetComponent<Image>().raycastTarget = true;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(battleToolsBody, false); viewport.gameObject.layer = gameObject.layer; viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12, 8); viewport.offsetMax = new Vector2(-12, -8);
            battleToolsContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            battleToolsContent.SetParent(viewport, false); battleToolsContent.gameObject.layer = gameObject.layer;
            battleToolsContent.anchorMin = new Vector2(0, 1); battleToolsContent.anchorMax = Vector2.one;
            battleToolsContent.pivot = new Vector2(0, 1); battleToolsContent.sizeDelta = Vector2.zero;
            battleToolsScroll = battleToolsBody.GetComponent<ScrollRect>();
            battleToolsScroll.viewport = viewport; battleToolsScroll.content = battleToolsContent;
            battleToolsScroll.horizontal = false; battleToolsScroll.scrollSensitivity = 26;
            battleToolsScroll.movementType = ScrollRect.MovementType.Clamped;
        }

        private Button CreateToolsButton(Transform parent, string title, UnityEngine.Events.UnityAction click, float top, float height = 36)
        {
            var button = Instantiate(restart, parent); button.name = title; button.gameObject.SetActive(true);
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(click);
            var rect = button.GetComponent<RectTransform>(); Place(rect, 0, -top, parent == battleToolsPanel ? 440 : 412, height);
            var label = button.GetComponentInChildren<TMP_Text>(); label.text = title; label.fontSize = 15;
            label.enableAutoSizing = false; label.enableWordWrapping = true; label.richText = false;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8, 3); label.rectTransform.offsetMax = new Vector2(-8, -3);
            GetComponent<BattleDemoUITheme>()?.StyleButton(button, false);
            return button;
        }

        private void ToolsText(string value, int size = 14)
        {
            if (string.IsNullOrEmpty(value)) return;
            var label = Instantiate(message, battleToolsContent); label.name = "Detail"; label.gameObject.SetActive(true);
            label.text = value; label.fontSize = size; label.enableAutoSizing = false; label.enableWordWrapping = true;
            label.richText = false; label.raycastTarget = false; label.alignment = TextAlignmentOptions.TopLeft;
            label.overflowMode = TextOverflowModes.Overflow;
            float height = Mathf.Max(24, label.GetPreferredValues(value, 412, Mathf.Infinity).y + 8);
            Place(label.rectTransform, 0, -battleToolsTop, 412, height); battleToolsTop += height + 4;
            battleToolsRows.Add(label.gameObject);
        }

        private Button ToolsButton(string title, UnityEngine.Events.UnityAction click, Func<bool> available)
        {
            var button = CreateToolsButton(battleToolsContent, title, click, battleToolsTop, 42);
            battleToolsTop += 46; battleToolsRows.Add(button.gameObject);
            battleToolsAvailability.Add(new KeyValuePair<Button, Func<bool>>(button, available));
            button.interactable = available(); return button;
        }

        private void ItemIcon(Button button, string definitionId)
        {
            var sprite = ResolveRelicIcon(definitionId); if (sprite == null) return;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(button.transform, false); icon.gameObject.layer = button.gameObject.layer; icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
            Place(icon.rectTransform, 6, -5, 32, 32);
            button.GetComponentInChildren<TMP_Text>().rectTransform.offsetMin = new Vector2(44, 3);
        }

        public bool SelectItem(string instanceId)
        {
            if (host?.State == null || host.Player == null || !host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics.Any(r => r.InstanceId == instanceId)) return false;
            if (GetComponent<BattleDemoRelicArt>() != null)
            {
                RefreshRelicIcons();
                var relics = host.State.Duel.Player(host.Player.ParticipantId).Relics.Relics;
                int index = relics.ToList().FindIndex(item => item.InstanceId == instanceId);
                selectedItem = instanceId; ShowRelicHover(instanceId, relicIconViews[index].rectTransform, forUse: true); return true;
            }
            ResetToolsContext();
            selectedItem = instanceId; selectedItemTarget = null; itemFeedback = null; battleToolsExpanded = true;
            RefreshBattleTools(true); return true;
        }

        private bool ResetToolsContext()
        {
            string matchId = host.State.Table.MatchId.ToString(), observerId = host.Player.ParticipantId.Value;
            if (battleToolsMatchId == matchId && battleToolsObserverId == observerId) return false;
            battleToolsMatchId = matchId; battleToolsObserverId = observerId;
            selectedItem = selectedItemTarget = itemFeedback = battleToolsKey = null;
            battleToolsState = null; battleToolsCandidate = null;
            if (battleToolsScroll != null) battleToolsScroll.StopMovement();
            if (battleToolsContent != null) battleToolsContent.anchoredPosition = Vector2.zero;
            return true;
        }

        private void RefreshBattleTools(bool force = false)
        {
            if (host?.State == null || host.Player == null || host.Display == null) return;
            bool contextChanged = ResetToolsContext();
            EnsureBattleTools(); if (battleToolsPanel == null) return;
            var state = host.State; var candidate = host.Candidate;
            var choices = host.PlayTargetChoices;
            string key = host.Player.ParticipantId.Value + "/" + string.Join("|", choices.Select(choice => choice.Slot + "=" + choice.SelectedId)) + "/" + selectedItem + "/" + selectedItemTarget + "/" + (GetComponent<BattleDemoRelicArt>()?.Version ?? 0);
            bool changed = force || contextChanged || !ReferenceEquals(battleToolsState, state) || !ReferenceEquals(battleToolsCandidate, candidate) || key != battleToolsKey;
            bool needsTarget = choices.Any(choice => choice.Required && string.IsNullOrEmpty(choice.SelectedId));
            if (!UsesDeskLayout)
                battleToolsToggle.GetComponentInChildren<TMP_Text>().text = "行动点 " + ResourceValue(host.Display, host.Player.ParticipantId.Value, "actions") +
                    " · 金币 " + state.GoldFor(host.Player.ParticipantId) + " · " + (battleToolsExpanded ? "收起操作" : needsTarget ? "请选择目标" :
                        GetComponent<BattleDemoRelicArt>() != null ? "选敌 / 预览" : "选敌 / 道具 / 预览");
            battleToolsBody.gameObject.SetActive(battleToolsExpanded);
            if (!battleToolsExpanded) return;
            float parentHeight = ((RectTransform)transform).rect.height;
            if (!UsesDeskLayout) battleToolsBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(parentHeight - 100, 240, 490));
            if (changed)
            {
                foreach (var row in battleToolsRows) { row.SetActive(false); Destroy(row); }
                battleToolsRows.Clear(); battleToolsAvailability.Clear(); battleToolsTop = 0;
                battleToolsState = state; battleToolsCandidate = candidate; battleToolsKey = key;
                foreach (var choice in choices)
                {
                    ToolsText(choice.Label + (choice.Required ? "（必选）" : "（不选时按规则自动）"), 16);
                    foreach (var target in choice.Candidates)
                    {
                        var slot = choice.Slot; var id = target.Id;
                        ToolsButton((choice.SelectedId == id ? "✓ " : "") + target.Label + " · 生命 " + target.Health,
                            () => { host.SelectPlayTarget(slot, id); RefreshBattleTools(true); }, () => host.CanChoosePlayTarget);
                    }
                }
                BuildToolsItems(state);
                ToolsText("结算预览", 17);
                if (candidate != null && candidate.SelectedIds.Count > 0 && candidate.State != global::BattlePresentation.CandidateState.Closed && candidate.State != global::BattlePresentation.CandidateState.Committing)
                {
                    ToolsText("出牌：\n" + (candidate.Labels.TryGetValue("playPreview", out var play) ? play : candidate.Reason ?? "当前选择不可用"));
                    ToolsText("弃牌：\n" + (candidate.Labels.TryGetValue("discardPreview", out var discard) ? discard : candidate.Reason ?? "当前选择不可用"));
                }
                else ToolsText("选择手牌后，这里分别展示出牌与弃牌的实际结算。废牌可选择弃掉，不能出牌。");
                if (!string.IsNullOrEmpty(host.DisplayFeedback)) ToolsText("最近结算：\n" + host.DisplayFeedback);
                battleToolsContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, battleToolsTop);
            }
            foreach (var entry in battleToolsAvailability) if (entry.Key != null) entry.Key.interactable = entry.Value();
        }

        private void BuildToolsItems(CardDuelState state)
        {
            if (GetComponent<BattleDemoRelicArt>() != null) return;
            var loadout = state.Duel.Player(host.Player.ParticipantId).Relics;
            var items = loadout.Relics.Where(relic => loadout.RulesFor(relic.InstanceId).Item?.Activation != null &&
                loadout.RulesFor(relic.InstanceId).Item.Activation != BattleItemActivation.None).ToArray();
            ToolsText("主动道具 · 使用不消耗行动点", 17);
            if (items.Length == 0) { selectedItem = null; selectedItemTarget = null; ToolsText("当前没有主动道具。"); return; }
            foreach (var relic in items)
            {
                string id = relic.InstanceId;
                var itemButton = ToolsButton((selectedItem == id ? "✓ " : "") + host.ItemName(relic.DefinitionId) + " · 次数 " + (relic.RemainingCharges?.ToString() ?? "不限"),
                    () => SelectItem(id), () => !host.IsPresenting);
                ItemIcon(itemButton, relic.DefinitionId);
            }
            var item = items.FirstOrDefault(relic => relic.InstanceId == selectedItem);
            if (item == null) { selectedItem = null; selectedItemTarget = null; ToolsText("选择道具查看详情和使用。"); return; }
            ToolsText(BattlePresentationRuleText.RelicDescription(loadout.RulesFor(item.InstanceId)));
            ToolsText(BattlePresentationRuleText.RelicRuntime(item));
            string multiplier = BattlePresentationRuleText.HandMultiplier(loadout); if (multiplier.Length > 0) ToolsText(multiplier);
            var spec = loadout.RulesFor(item.InstanceId).Item;
            if (spec.Activation == BattleItemActivation.ReplaceHeld)
            {
                ToolsText("选择要替换的另一件持有遗物：");
                foreach (var relic in loadout.Relics.Where(relic => relic.InstanceId != item.InstanceId))
                {
                    string targetId = relic.InstanceId;
                    ToolsButton((selectedItemTarget == targetId ? "✓ " : "") + host.ItemName(relic.DefinitionId),
                        () => { selectedItemTarget = targetId; itemFeedback = null; RefreshBattleTools(true); }, () => host.CanUseItem(item.InstanceId));
                }
                if (!loadout.Relics.Any(relic => relic.InstanceId == selectedItemTarget && relic.InstanceId != item.InstanceId)) selectedItemTarget = null;
            }
            // Never reveal a preview of a future random replacement or roulette explosion.
            if (spec.Activation == BattleItemActivation.Effects && host.CanUseItem(item.InstanceId))
            {
                var preview = host.PreviewItem(item.InstanceId);
                if (preview != null && preview.Accepted) foreach (var step in preview.Steps)
                    ToolsText(BattlePresentationRuleText.Transition(step.Transition, state.Duel, host.Engine.Duel.Definition.StatusCatalog));
            }
            if (!string.IsNullOrEmpty(itemFeedback)) ToolsText(itemFeedback);
            ToolsButton("使用 " + host.ItemName(item.DefinitionId), () => {
                var response = host.UseItem(item.InstanceId, selectedItemTarget);
                itemFeedback = response != null && response.Accepted ? "使用成功" : "当前无法使用该道具";
                RefreshBattleTools(true);
            }, () => host.CanUseItem(item.InstanceId) && (spec.Activation != BattleItemActivation.ReplaceHeld || selectedItemTarget != null));
        }
    }
}
