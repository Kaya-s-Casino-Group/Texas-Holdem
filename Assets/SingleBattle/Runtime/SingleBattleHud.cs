using PokerEngine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Poker.SingleBattle
{
    /// <summary>Read-only battle projection; buttons delegate to the host.</summary>
    public sealed class SingleBattleHud : MonoBehaviour
    {
        [SerializeField] private SingleBattleHost host;
        [SerializeField] private TMP_Text player, enemy, turn, intent, message, counts, score, resultTitle, resultDetails;
        [SerializeField] private Image playerBar, enemyBar;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Button restart, resultRestart;
        [SerializeField] private TMP_Text energy;
        [SerializeField] private Button endTurn;
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
            if (host == null || host.State == null || player == null) return;
            var state = host.State; var duel = state.Duel; var rules = host.Engine.Duel.Definition;
            var hero = host.Player.Unit; var monster = host.Enemy.Unit;
            if (hero == null || monster == null) return;
            player.text = "你   " + hero.Health + " / " + hero.MaxHealth + "\n格挡 " + hero.Block;
            enemy.text = "守卫   " + monster.Health + " / " + monster.MaxHealth + "\n格挡 " + monster.Block;
            if (energy != null) energy.text = "能量 " + duel.PlayerTurn.Energy + " / " + duel.PlayerTurn.MaxEnergy +
                "\n出牌 " + rules.PlayEnergyCost + " · 弃牌 " + rules.DiscardEnergyCost;
            if (endTurn != null) endTurn.interactable = host.CanEndTurn;
            playerBar.rectTransform.anchorMax = new Vector2((float)hero.Health / hero.MaxHealth, 1);
            enemyBar.rectTransform.anchorMax = new Vector2((float)monster.Health / monster.MaxHealth, 1);
            int round = duel.IsFinished ? duel.CompletedRounds : duel.CompletedRounds + 1;
            turn.text = "第 " + round + " / " + rules.RoundLimit + " 回合 · " + (duel.IsFinished ? "战斗结束" :
                duel.Phase == ScoredDuelPhase.AwaitingPlayerHand ? "玩家行动" : "敌人行动");
            intent.text = duel.IsFinished ? "" : "敌人意图：攻击 " + host.Enemy.ReadIntent().Damage;
            message.text = host.Message;
            counts.text = "抽牌堆 " + state.Zone(CardZoneKinds.Deck).Cards.Count + "  ·  弃牌堆 " + state.Zone(CardZoneKinds.Discard).Cards.Count +
                "  ·  剩余弃牌 " + state.DiscardsRemaining;
            score.text = host.LastScore == null ? "牌型得分 ÷ " + rules.DamagePolicy.PointsPerDamage + " = 伤害（向下取整）" :
                "上次：" + HandName(host.LastScore.Category) + "  " + host.LastScore.Total + " 分 / " + host.LastDamage + " 伤害";
            bool showResult = duel.IsFinished && !host.IsPresenting;
            resultPanel.SetActive(showResult); restart.interactable = !host.IsPresenting; resultRestart.interactable = !host.IsPresenting;
            if (showResult)
            {
                var result = host.Engine.Duel.ReadProgress(duel).Result;
                bool won = duel.Phase == ScoredDuelPhase.Won;
                resultTitle.text = won ? "战斗胜利" : "战斗失败";
                resultTitle.color = won ? new Color32(233, 193, 112, 255) : new Color32(237, 125, 123, 255);
                resultDetails.text = (won ? "你击败了守卫" : result.Reason == EncounterEndReason.RoundLimit ? "回合已用尽" : "你的生命值已耗尽") +
                    "\n完成 " + duel.CompletedRounds + " 回合 · 总得分 " + duel.TotalScore;
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
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() { Unsubscribe(); }
        // Card landing can finish without a rules transition (e.g. opening deal or local sort).
        private void LateUpdate()
        {
            if (host != null && endTurn != null) endTurn.interactable = host.CanEndTurn;
        }
    }
}
