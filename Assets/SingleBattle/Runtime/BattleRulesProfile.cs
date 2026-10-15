using PokerEngine;
using Poker.Scoring;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/公共规则", fileName = "BattleRulesProfile")]
    public sealed class BattleRulesProfile : ScriptableObject
    {
        [SerializeField, InspectorName("规则版本")] private string version = "team-battle.demo.1";
        [SerializeField, Range(1, 1000), InspectorName("回合上限")] private int roundLimit = 8;
        [SerializeField, InspectorName("玩家格挡清除时机")] private PlayerBlockExpiry playerBlockExpiry = PlayerBlockExpiry.Retain;
        [SerializeField, InspectorName("牌型计分配置")] private PokerScoreProfile scoring;
        public PokerScoreProfile Scoring => scoring;
        public ScoreProfile BuildScoring() => scoring != null ? scoring.Build() : ScoreProfile.Default;
        public BattleRules BuildRules() => new BattleRules(version, roundLimit,
            playerBlockExpiry: playerBlockExpiry);
    }
}
