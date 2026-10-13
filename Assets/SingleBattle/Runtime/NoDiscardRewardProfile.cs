using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/弃牌收益配置（无收益）", fileName = "NoDiscardRewardProfile")]
    public sealed class NoDiscardRewardProfile : DiscardRewardProfile
    {
        [SerializeField, InspectorName("规则版本")] private string version = "discard.none.1";
        public override IDiscardRewardRule BuildRule() => new NoDiscardRewardRule(version);
    }
}
