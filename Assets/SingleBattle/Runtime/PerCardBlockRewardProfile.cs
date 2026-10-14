using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/弃牌收益配置（每张格挡）", fileName = "PerCardBlockRewardProfile")]
    public sealed class PerCardBlockRewardProfile : DiscardRewardProfile
    {
        [SerializeField, InspectorName("规则版本")] private string version = "discard.block-per-card.1";
        [SerializeField, Min(0), InspectorName("每张弃牌获得格挡")] private int blockPerCard = 8;
        public override IDiscardRewardRule BuildRule() => new PerCardBlockRewardRule(blockPerCard, version);
    }
}
