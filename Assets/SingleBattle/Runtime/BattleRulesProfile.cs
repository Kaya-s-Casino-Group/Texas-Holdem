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
        [SerializeField, InspectorName("出牌伤害配置")] private ScoredHandDamageProfile damage;
        public ScoredHandDamageProfile Damage => damage;
        public void ConfigureDamage(ScoredHandDamageProfile profile) => damage = profile != null ? profile : throw new System.ArgumentNullException(nameof(profile));
        [SerializeField, InspectorName("牌型计分配置")] private PokerScoreProfile scoring;
        public PokerScoreProfile Scoring => scoring;
        public ScoreProfile BuildScoring() => scoring != null ? scoring.Build() : ScoreProfile.Default;
        [SerializeField, InspectorName("弃牌收益配置（空为无收益）")] private DiscardRewardProfile discardReward;
        public DiscardRewardProfile DiscardReward => discardReward;
        public void ConfigureDiscardReward(DiscardRewardProfile profile) => discardReward = profile;
        public BattleRules BuildRules() => new BattleRules(version, damage != null ? damage.BuildRule() : throw new System.InvalidOperationException("请先绑定独立的出牌伤害配置。"), roundLimit,
            discardReward: discardReward != null ? discardReward.BuildRule() ?? throw new System.InvalidOperationException("弃牌收益配置返回了空规则。") : new NoDiscardRewardRule(),
            playerBlockExpiry: playerBlockExpiry);
    }
}
