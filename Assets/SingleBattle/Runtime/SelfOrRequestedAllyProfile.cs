using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/角色/友方目标策略（自身或指定队友）", fileName = "SelfOrRequestedAlly")]
    public sealed class SelfOrRequestedAllyProfile : AllyTargetPolicyProfile
    {
        public override IBattleAllyTargetPolicy BuildPolicy() => new SelfOrRequestedAllyPolicy();
    }
}
