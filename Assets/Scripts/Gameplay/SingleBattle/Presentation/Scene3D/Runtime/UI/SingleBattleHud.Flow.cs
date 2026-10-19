using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    public sealed partial class SingleBattleHud
    {
        private RectTransform flowPanel;
        private TMP_Text flowHint;
        private Button drawButton, skipDrawButton, discardDrawButton, confirmOverflowButton, lessDrawButton, moreDrawButton;
        private int chosenDrawCount = 5;
        private void EnsureTurnFlow()
        {
            if (flowPanel != null || restart == null || energy == null || !Application.isPlaying) return;
            flowPanel = new GameObject("Turn Flow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            flowPanel.SetParent(restart.transform.parent, false);
            flowPanel.anchorMin = flowPanel.anchorMax = flowPanel.pivot = new Vector2(1, 0);
            flowPanel.anchoredPosition = new Vector2(-16, 16); flowPanel.sizeDelta = new Vector2(350, 136);
            flowPanel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.1f, 0.96f);
            flowHint = Instantiate(energy, flowPanel); flowHint.name = "Flow Hint"; flowHint.fontSize = 15;
            Place(flowHint.rectTransform, 10, -8, 330, 54);
            lessDrawButton = FlowButton("−", 10, () => chosenDrawCount = Mathf.Max(1, chosenDrawCount - 1));
            drawButton = FlowButton("摸牌", 64, () => host.DrawCards(chosenDrawCount));
            moreDrawButton = FlowButton("+", 174, () => chosenDrawCount++);
            skipDrawButton = FlowButton("跳过", 230, () => host.SkipDraw());
            discardDrawButton = FlowButton("先弃所选牌", 10, () => host.DiscardBeforeDraw(), 102);
            confirmOverflowButton = FlowButton("确认强制弃牌", 160, () => host.ConfirmOverflow(), 102);
        }
        private Button FlowButton(string title, float left, UnityEngine.Events.UnityAction click, float top = 68)
        {
            var button = Instantiate(restart, flowPanel); button.name = title;
            button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(click);
            Place(button.GetComponent<RectTransform>(), left, -top, title == "−" || title == "+" ? 44 : 104, 28);
            var label = button.GetComponentInChildren<TMP_Text>(); label.text = title; label.fontSize = 14;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(2, 1); label.rectTransform.offsetMax = new Vector2(-2, -1);
            return button;
        }
        private static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, top); rect.sizeDelta = new Vector2(width, height);
        }
        private void RefreshTurnFlow()
        {
            if (host == null || host.AuthorityState == null || host.Player == null) return;
            var state = host.AuthorityState; var actor = host.Player.ParticipantId; var player = state.Duel.Player(actor);
            if (!player.Rules.Flow.ManualDraw) { if (flowPanel != null) flowPanel.gameObject.SetActive(false); return; }
            EnsureTurnFlow(); if (flowPanel == null) return;
            bool active = !state.Duel.IsFinished && state.Duel.HasPlayerWindow(actor);
            flowPanel.gameObject.SetActive(active); if (!active) return;
            var flow = state.FlowFor(actor); bool drawing = flow.Window == BattlePlayerWindow.Draw;
            bool forced = flow.Window == BattlePlayerWindow.ForcedDiscard; bool input = host.Player.CanInteract;
            int limit = host.Engine.DrawLimit(state, actor);
            chosenDrawCount = Mathf.Clamp(chosenDrawCount, 1, Mathf.Max(1, limit));
            foreach (var button in new[] { drawButton, lessDrawButton, moreDrawButton, skipDrawButton, discardDrawButton }) button.gameObject.SetActive(drawing);
            confirmOverflowButton.gameObject.SetActive(forced);
            drawButton.GetComponentInChildren<TMP_Text>().text = limit == 0 ? "手牌已满" : "摸 " + chosenDrawCount + " 张";
            drawButton.interactable = input && limit > 0; lessDrawButton.interactable = input && chosenDrawCount > 1;
            moreDrawButton.interactable = input && chosenDrawCount < limit; skipDrawButton.interactable = input;
            int selected = host.Actions.Hand.CaptureSelection().Cards.Count;
            discardDrawButton.interactable = input && selected > 0 && host.Engine.RequiresDrawWarning(state, actor);
            confirmOverflowButton.interactable = input && selected == host.Engine.RequiredDiscards(state, actor);
            flowHint.text = forced ? "手牌超限：请选择恰好 " + host.Engine.RequiredDiscards(state, actor) + " 张弃牌" : drawing ?
                (host.Engine.RequiresDrawWarning(state, actor) ? "手牌较多：少摸，或先弃掉所选手牌再摸。" : "行动前可摸牌一次，也可以跳过。") :
                flow.Window == BattlePlayerWindow.Ending ? "出牌阶段结束：可弃牌防御，或确认结束回合。" : "剩余行动 " + flow.ActionsRemaining;
            if (endTurn != null) endTurn.GetComponentInChildren<TMP_Text>().text = flow.Window == BattlePlayerWindow.Ending ? "确认结束回合" : "结束出牌阶段";
        }
    }
}
