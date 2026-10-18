using System.Linq;
using global::BattlePresentation;
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
        [SerializeField] private SingleBattleSceneAdapter host;
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
                if (host.Display?.Resource(players[(current + offset) % players.Count].ParticipantId.Value, "canAct")?.Value == 1)
                { host.SelectPlayer(players[(current + offset) % players.Count]); return; }
        }
        public Button RestartButton => restart;
        public Button EndTurnButton => endTurn;
        public string EnergyText => energy.text;
        public GameObject ResultPanel => resultPanel;
        public string TurnText => turn.text;
        public void Configure(SingleBattleSceneAdapter owner, TMP_Text playerText, TMP_Text enemyText, TMP_Text turnText, TMP_Text intentText,
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
            if (host == null) return;
            var snapshot = host.Display;
            if (snapshot == null)
            {
                if (message != null) message.text = host.Message;
                RefreshRuleFeedback(true); return;
            }
            if (player == null) return;
            var hero = snapshot.Unit(snapshot.ObserverId);
            var enemies = snapshot.Units.Where(unit => unit.Team == "enemy").ToArray();
            if (hero == null || enemies.Length == 0) return;
            var players = snapshot.Units.Where(unit => unit.Team == "player").ToList();
            EnsurePlayerSwitch();
            player.text = "玩家 " + (players.FindIndex(unit => unit.Id == hero.Id) + 1) + "   " + hero.Health + " / " + hero.MaxHealth + "\n格挡 " + hero.Block;
            enemy.text = string.Join("\n", enemies.Select((unit, i) => "怪物 " + (i + 1) + "  " + unit.Health + " / " + unit.MaxHealth));
            if (energy != null) energy.text = "能量 " + hero.Energy + " / " + hero.MaxEnergy +
                "\n出牌 " + ResourceValue(snapshot, hero.Id, "playCost") + " · 弃牌 " + ResourceValue(snapshot, hero.Id, "discardCost");
            if (endTurn != null) endTurn.interactable = host.CanEndTurn;
            playerBar.rectTransform.anchorMax = new Vector2(hero.MaxHealth > 0 ? Mathf.Clamp01((float)hero.Health / hero.MaxHealth) : 0, 1);
            double enemyMaximum = enemies.Sum(unit => (double)unit.MaxHealth);
            enemyBar.rectTransform.anchorMax = new Vector2(enemyMaximum > 0 ? Mathf.Clamp01((float)(enemies.Sum(unit => (double)unit.Health) / enemyMaximum)) : 0, 1);
            long round = snapshot.IsFinished ? snapshot.CompletedRounds : snapshot.CompletedRounds + 1;
            turn.text = "回合 " + round + " / " + ResourceValue(snapshot, "", "roundLimit") + "\n" + (snapshot.IsFinished ? "战斗结束" :
                snapshot.Phase == "player" ? "玩家行动 · 剩余 " + players.Count(unit => ResourceValue(snapshot, unit.Id, "canAct") == 1) : "敌人行动");
            RefreshEnemyIntents();
            message.text = "玩家 " + players.Count(unit => unit.IsAlive) + " · 怪物 " + enemies.Count(unit => unit.IsAlive);
            long discardBudget = snapshot.Resource(hero.Id, "discards")?.Value ?? -1;
            counts.text = "牌库 " + ZoneCount(snapshot, hero.Id, "deck") + " · 弃牌堆 " + ZoneCount(snapshot, hero.Id, "discard") +
                "\n弃牌次数 " + (discardBudget < 0 ? "不限" : discardBudget.ToString());
            score.text = snapshot.Labels.TryGetValue("lastScorePattern", out var lastPattern) && snapshot.Labels.TryGetValue("lastScoreTotal", out var lastTotal)
                ? "上次 " + lastPattern + " · " + lastTotal + " 分\n效果见规则反馈" : "总得分 " + ResourceValue(snapshot, "", "totalScore");
            RefreshRuleFeedback(true);
            bool showResult = snapshot.IsFinished && !host.IsPresenting;
            resultPanel.SetActive(showResult); restart.interactable = !host.IsPresenting; resultRestart.interactable = !host.IsPresenting;
            if (endTurn != null) endTurn.gameObject.SetActive(!showResult);
            restart.gameObject.SetActive(!showResult);
            if (switchPlayer != null) switchPlayer.gameObject.SetActive(!showResult);
            if (showResult)
            {
                bool won = snapshot.Phase == "won";
                resultTitle.text = won ? "战斗胜利" : "战斗失败";
                resultTitle.color = won ? new Color32(233, 193, 112, 255) : new Color32(237, 125, 123, 255);
                bool roundLimit = snapshot.Labels.TryGetValue("resultReason", out var reason) && reason == "RoundLimit";
                resultDetails.text = (won ? "全部怪物已被击败" : roundLimit ? "回合已用尽" : "全部玩家已阵亡") +
                    "\n完成回合 " + snapshot.CompletedRounds + "\n总得分 " + ResourceValue(snapshot, "", "totalScore");
            }
        }
        private static long ResourceValue(BattleDisplaySnapshot snapshot, string owner, string key) => snapshot.Resource(owner, key)?.Value ?? 0;
        private static int ZoneCount(BattleDisplaySnapshot snapshot, string owner, string kind) => snapshot.Zones.FirstOrDefault(zone => zone.OwnerId == owner && zone.Kind == kind)?.Cards.Count ?? 0;
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
            RefreshRuleFeedback();
            if (host != null && endTurn != null) endTurn.interactable = host.CanEndTurn;
            if (switchPlayer != null) switchPlayer.interactable = host.CanSwitchPlayer && host.Display != null && host.Display.Units.Count(unit => unit.Team == "player" && ResourceValue(host.Display, unit.Id, "canAct") == 1) > 1;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) SelectNextPlayer();
        }
    }
}
