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
        [Header("Compact battle layout")]
        [SerializeField] private bool compactLayout;
        [SerializeField] private RectTransform flowContainer, feedbackContainer;
        [Header("BattleDemo themed UI")]
        [SerializeField] private bool ritualLayout;
        [SerializeField] private TMP_Text playerStatuses, enemyStatuses;
        [SerializeField] private GameObject intentPanel;
        public void ConfigureRitualLayout(TMP_Text heroStatus, TMP_Text monsterStatus, GameObject intentFrame)
        { ritualLayout = true; playerStatuses = heroStatus; enemyStatuses = monsterStatus; intentPanel = intentFrame; }
        public static void PresentActorStatuses(TMP_Text label, string value)
        {
            if (label == null) return;
            label.text = value ?? "";
            label.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
        }
        public void ConfigureCompactLayout(RectTransform flow, RectTransform feedback)
        { compactLayout = true; flowContainer = flow; feedbackContainer = feedback; }
        private Button switchPlayer;
        private readonly BattleResultDelay resultDelay = new BattleResultDelay();
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
            Unsubscribe(); resultDelay.Reset(); host = owner; player = playerText; enemy = enemyText; turn = turnText; intent = intentText;
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
                resultDelay.Reset();
                if (resultPanel != null) resultPanel.SetActive(false);
                if (message != null) message.text = host.Message;
                RefreshRuleFeedback(true); return;
            }
            if (player == null) return;
            var hero = snapshot.Unit(snapshot.ObserverId);
            var enemies = snapshot.Units.Where(unit => unit.Team == "enemy").ToArray();
            if (hero == null || enemies.Length == 0) return;
            var bossBody = UsesWrathDemoPresentation ? WrathBody(snapshot) : null;
            var primaryEnemy = bossBody ?? enemies[0];
            var players = snapshot.Units.Where(unit => unit.Team == "player").ToList();
            EnsurePlayerSwitch();
            player.text = compactLayout ? "生命 " + hero.Health + " / " + hero.MaxHealth + "   护盾 " + hero.Block :
                "玩家 " + (players.FindIndex(unit => unit.Id == hero.Id) + 1) + "   " + hero.Health + " / " + hero.MaxHealth + "\n格挡 " + hero.Block;
            enemy.text = string.Join("\n", enemies.Select((unit, i) => "怪物 " + (i + 1) + "  " + unit.Health + " / " + unit.MaxHealth));
            if (ritualLayout || bossBody != null)
            {
                enemy.text = (bossBody != null ? "暴怒  " : "生命 ") + primaryEnemy.Health + " / " + primaryEnemy.MaxHealth +
                    (primaryEnemy.Block > 0 ? " · 护盾 " + primaryEnemy.Block : "");
                snapshot.Labels.TryGetValue("status/" + hero.Id, out var heroStatus);
                snapshot.Labels.TryGetValue("status/" + primaryEnemy.Id, out var monsterStatus);
                PresentActorStatuses(playerStatuses, heroStatus);
                PresentActorStatuses(enemyStatuses, monsterStatus);
            }
            if (energy != null) energy.text = "剩余行动点 " + ResourceValue(snapshot, hero.Id, "actions") +
                "\n出牌 " + ResourceValue(snapshot, hero.Id, "playCost") + " · 弃牌 " + ResourceValue(snapshot, hero.Id, "discardCost");
            if (endTurn != null) endTurn.interactable = host.CanEndTurn;
            playerBar.rectTransform.anchorMax = new Vector2(hero.MaxHealth > 0 ? Mathf.Clamp01((float)hero.Health / hero.MaxHealth) : 0, 1);
            double enemyMaximum = bossBody != null ? bossBody.MaxHealth : enemies.Sum(unit => (double)unit.MaxHealth);
            double enemyHealth = bossBody != null ? bossBody.Health : enemies.Sum(unit => (double)unit.Health);
            enemyBar.rectTransform.anchorMax = new Vector2(enemyMaximum > 0 ? Mathf.Clamp01((float)(enemyHealth / enemyMaximum)) : 0, 1);
            long round = snapshot.IsFinished ? snapshot.CompletedRounds : snapshot.CompletedRounds + 1;
            turn.text = "回合 " + round + " / " + ResourceValue(snapshot, "", "roundLimit") + "\n" + (snapshot.IsFinished ? "战斗结束" :
                snapshot.Phase == "player" ? "玩家行动 · 剩余 " + players.Count(unit => ResourceValue(snapshot, unit.Id, "canAct") == 1) : "敌人行动");
            if (ritualLayout)
            {
                var window = host.State.FlowFor(host.Player.ParticipantId).Window;
                bool discarding = window == BattlePlayerWindow.Draw || window == BattlePlayerWindow.ForcedDiscard;
                turn.text = "回合 " + round + " / " + ResourceValue(snapshot, "", "roundLimit") +
                    "\n" + (snapshot.IsFinished ? "战斗结束" : snapshot.Phase == "player" ?
                        (discarding ? "弃牌阶段" : "出牌阶段") : "敌人行动");
            }
            RefreshEnemyIntents();
            message.text = "玩家 " + players.Count(unit => unit.IsAlive) + " · 怪物 " + enemies.Count(unit => unit.IsAlive) + " · 金币 " + ResourceValue(snapshot, hero.Id, "gold");
            long discardBudget = snapshot.Resource(hero.Id, "discards")?.Value ?? -1;
            counts.text = "牌库 " + ZoneCount(snapshot, hero.Id, "deck") + " · 弃牌堆 " + ZoneCount(snapshot, hero.Id, "discard") +
                (compactLayout ? "" : "\n弃牌次数 " + (discardBudget < 0 ? "不限" : discardBudget.ToString()));
            score.text = snapshot.Labels.TryGetValue("lastScorePattern", out var lastPattern) && snapshot.Labels.TryGetValue("lastScoreTotal", out var lastTotal)
                ? "上次 " + lastPattern + " · " + lastTotal + " 分" + (compactLayout ? "" : "\n效果见规则反馈")
                : "总得分 " + ResourceValue(snapshot, "", "totalScore");
            RefreshRuleFeedback(true);
            bool showResult = resultDelay.Ready(snapshot.IsFinished && !host.UsesExternalCommands,
                host.IsPresenting, Time.unscaledTimeAsDouble);
            resultPanel.SetActive(showResult);
            restart.interactable = !host.IsPresenting && (!snapshot.IsFinished || showResult);
            resultRestart.interactable = showResult;
            if (endTurn != null) endTurn.gameObject.SetActive(!showResult);
            restart.gameObject.SetActive(!showResult && !host.UsesExternalCommands);
            if (switchPlayer != null) switchPlayer.gameObject.SetActive(!showResult);
            if (showResult)
            {
                bool won = snapshot.Phase == "won";
                resultTitle.text = won ? "战斗胜利" : "战斗失败";
                resultTitle.color = won ? new Color32(233, 193, 112, 255) : new Color32(237, 125, 123, 255);
                bool roundLimit = snapshot.Labels.TryGetValue("resultReason", out var reason) && reason == "RoundLimit";
                resultDetails.text = (won ? bossBody != null ? "暴怒已被击败" : "全部怪物已被击败" : roundLimit ? "回合已用尽" : "全部玩家已阵亡") +
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
        private void OnDisable() { Unsubscribe(); resultDelay.Reset(); HideEnemyIntents(); HideRelicHover(); HideWrathHud(); }
        private void OnDestroy() => DestroyEnemyIntents();
        // Card landing can finish without a rules transition (e.g. opening deal or local sort).
        private void LateUpdate()
        {
            if (host != null && !host.UsesExternalCommands && host.Display?.IsFinished == true &&
                resultPanel != null && !resultPanel.activeSelf) Refresh();
            UpdateRitualIntentLayout();
            UpdateEnemyIntentBillboards();
            RefreshTurnFlow();
            RefreshRuleFeedback();
            RefreshBattleTools();
            RefreshRelicHover();
            RefreshWrathTargetAvailability();
            UpdateWrathPromptViews();
            RefreshAdminEntry();
            if (host != null && endTurn != null) endTurn.interactable = host.CanEndTurn;
            if (switchPlayer != null) switchPlayer.interactable = host.CanSwitchPlayer && host.Display != null && host.Display.Units.Count(unit => unit.Team == "player" && ResourceValue(host.Display, unit.Id, "canAct") == 1) > 1;
            if (adminPanel?.IsOpen != true && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) SelectNextPlayer();
        }
    }
}
