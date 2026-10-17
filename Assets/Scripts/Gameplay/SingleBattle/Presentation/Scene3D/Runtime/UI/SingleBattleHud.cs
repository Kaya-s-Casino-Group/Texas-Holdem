using System.Linq;
using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UIFoundation;

namespace Poker.SingleBattle
{
    /// <summary>Read-only battle projection; buttons delegate to the host.</summary>
    public sealed partial class SingleBattleHud : MonoBehaviour
    {
        [SerializeField] private SingleBattleHost host;
        [SerializeField] private TMP_Text player, enemy, turn, intent, message, counts, score, resultTitle, resultDetails;
        [SerializeField] private Image playerBar, enemyBar;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button restart, resultRestart;
        [SerializeField] private TMP_Text energy;
        [SerializeField] private Button endTurn;
        private Button switchPlayer;
        public Button SwitchPlayerButton => switchPlayer;
        private void EnsurePlayerSwitch()
        {
            if (!Application.isPlaying || switchPlayer != null || host.Players.Count < 2 || restart == null) return;
            switchPlayer = Instantiate(restart, restart.transform.parent);
            switchPlayer.name = "Switch Player";
            switchPlayer.onClick = new Button.ButtonClickedEvent();
            switchPlayer.onClick.AddListener(SelectNextPlayer);
            var rect = switchPlayer.GetComponent<RectTransform>();
            rect.anchoredPosition += new Vector2(0, -34);
            var label = switchPlayer.GetComponentInChildren<TMP_Text>(); label.text = "切换玩家 · Tab";
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4, 2); label.rectTransform.offsetMax = new Vector2(-4, -2);
        }
        private void SelectNextPlayer()
        {
            if (host == null || !host.CanSwitchPlayer) return;
            var players = host.Players.ToList(); int current = players.IndexOf(host.Player);
            for (int offset = 1; offset < players.Count; offset++)
                if (host.PresentedState.Duel.CanAct(players[(current + offset) % players.Count].ParticipantId))
                { host.SelectPlayer(players[(current + offset) % players.Count]); return; }
        }
        public Button RestartButton => restart;
        public Button EndTurnButton => endTurn;
        public string EnergyText => energy.text;
        public GameObject ResultPanel => resultPanel;
        public string TurnText => turn.text;
        public void Configure(SingleBattleHost owner, TMP_Text playerText, TMP_Text enemyText, TMP_Text turnText, TMP_Text intentText,
            TMP_Text messageText, TMP_Text countsText, TMP_Text scoreText, Image playerFill, Image enemyFill,
            GameObject result, TMP_Text title, TMP_Text details, Button restartButton, Button resultButton, TMP_Text energyText, Button endTurnButton)
        {
            Unsubscribe(); host = owner; player = playerText; enemy = enemyText; turn = turnText; intent = intentText;
            message = messageText; counts = countsText; score = scoreText; playerBar = playerFill; enemyBar = enemyFill;
            resultPanel = result; resultTitle = title; resultDetails = details; restart = restartButton; resultRestart = resultButton;
            energy = energyText; endTurn = endTurnButton;
            if (isActiveAndEnabled) Subscribe(); Refresh();
        }
        public void Refresh()
        {
            if (host == null || host.PresentedState == null || player == null) return;
            var state = host.PresentedState; var duel = state.Duel; var rules = host.Engine.Duel.Definition;
            var hero = host.Player.Unit; var monster = host.Enemy.Unit;
            if (hero == null || monster == null) return;
            EnsurePlayerSwitch();
            player.text = "玩家 " + (host.Players.ToList().IndexOf(host.Player) + 1) + "   " + hero.Health + " / " + hero.MaxHealth + "\n格挡 " + hero.Block;
            enemy.text = string.Join("\n", host.Enemies.Select((actor, i) => "怪物 " + (i + 1) + "  " + actor.Unit.Health + " / " + actor.Unit.MaxHealth));
            if (energy != null) energy.text = "能量 " + host.Player.Turn.Energy + " / " + host.Player.Turn.MaxEnergy +
                "\n出牌 " + host.Player.Rules.PlayEnergyCost + " · 弃牌 " + host.Player.Rules.DiscardEnergyCost;
            if (endTurn != null) endTurn.interactable = host.CanEndTurn;
            playerBar.rectTransform.anchorMax = new Vector2((float)hero.Health / hero.MaxHealth, 1);
            enemyBar.rectTransform.anchorMax = new Vector2((float)host.Enemies.Sum(actor => actor.Unit.Health) / host.Enemies.Sum(actor => actor.Unit.MaxHealth), 1);
            int round = duel.IsFinished ? duel.CompletedRounds : duel.CompletedRounds + 1;
            turn.text = "回合 " + round + " / " + rules.RoundLimit + "\n" + (duel.IsFinished ? "战斗结束" :
                duel.Phase == ScoredDuelPhase.AwaitingPlayerHand ? "玩家行动 · 剩余 " + duel.Players.Count(p => duel.CanAct(p.Id)) : "敌人行动");
            RefreshEnemyIntents();
            message.text = "玩家 " + host.Players.Count(actor => actor.IsAlive) + " · 怪物 " + host.Enemies.Count(actor => actor.IsAlive);
            counts.text = "牌库 " + host.PlayerZone(CardZoneKinds.Deck).Cards.Count + " · 弃牌堆 " + host.PlayerZone(CardZoneKinds.Discard).Cards.Count +
                "\n弃牌次数 " + (host.PresentedState.RemainingDiscardBudgetFor(host.Player.ParticipantId)?.ToString() ?? "不限");
            score.text = host.LastScore == null ? "总得分 " + duel.TotalScore :
                "上次 " + HandName(host.LastScore.Category) + " · " + host.LastScore.Total + " 分\n伤害 " + host.LastDamage;
            bool showResult = duel.IsFinished && !host.IsPresenting;
            resultPanel.SetActive(showResult); restart.interactable = !host.IsPresenting; resultRestart.interactable = !host.IsPresenting;
            if (endTurn != null) endTurn.gameObject.SetActive(!showResult);
            restart.gameObject.SetActive(!showResult);
            if (switchPlayer != null) switchPlayer.gameObject.SetActive(!showResult);
            if (showResult)
            {
                var result = host.Engine.Duel.ReadProgress(duel).Result;
                bool won = duel.Phase == ScoredDuelPhase.Won;
                resultTitle.text = won ? "战斗胜利" : "战斗失败";
                resultTitle.color = won ? new Color32(233, 193, 112, 255) : new Color32(237, 125, 123, 255);
                resultDetails.text = (won ? "全部怪物已被击败" : result.Reason == EncounterEndReason.RoundLimit ? "回合已用尽" : "全部玩家已阵亡") +
                    "\n完成回合 " + duel.CompletedRounds + "\n总得分 " + duel.TotalScore;
            }
        }
        public static string HandName(HandCategory category)
        {
            switch (category)
            {
                case HandCategory.HighCard: return "高牌";
                case HandCategory.OnePair: return "一对";
                case HandCategory.TwoPair: return "两对";
                case HandCategory.ThreeOfAKind: return "三条";
                case HandCategory.Straight: return "顺子";
                case HandCategory.Flush: return "同花";
                case HandCategory.FullHouse: return "葫芦";
                case HandCategory.FourOfAKind: return "四条";
                case HandCategory.StraightFlush: return "同花顺";
                case HandCategory.FiveOfAKind: return "五条";
                default: return category.ToString();
            }
        }
        private void Restart() { if (host != null) host.StartBattle(); }
        private void EndTurn() { if (host != null && host.Player != null) host.Player.EndTurn(); }
        private void Subscribe()
        {
            if (host != null) host.StateChanged += Refresh;
            if (restart != null) restart.onClick.AddListener(Restart);
            if (resultRestart != null) resultRestart.onClick.AddListener(Restart);
            if (endTurn != null) endTurn.onClick.AddListener(EndTurn);
        }
        private void Unsubscribe()
        {
            if (host != null) host.StateChanged -= Refresh;
            if (restart != null) restart.onClick.RemoveListener(Restart);
            if (resultRestart != null) resultRestart.onClick.RemoveListener(Restart);
            if (endTurn != null) endTurn.onClick.RemoveListener(EndTurn);
        }
        private void OnEnable() { Subscribe(); Refresh(); if (Application.isPlaying) ScreenSafeArea.Ensure(GetComponent<Canvas>()); }
        private void OnDisable() { Unsubscribe(); HideEnemyIntents(); }
        private void OnDestroy() => DestroyEnemyIntents();
        // Card landing can finish without a rules transition (e.g. opening deal or local sort).
        private void LateUpdate()
        {
            UpdateEnemyIntentBillboards();
            if (host != null && endTurn != null) endTurn.interactable = host.CanEndTurn;
            if (switchPlayer != null) switchPlayer.interactable = host.CanSwitchPlayer && host.PresentedState.Duel.Players.Count(p => host.PresentedState.Duel.CanAct(p.Id)) > 1;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) SelectNextPlayer();
        }
    }
}
