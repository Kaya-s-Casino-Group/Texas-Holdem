using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/角色/攻击目标策略", fileName = "OpponentTargetPolicy")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "OpponentTargetPolicyProfile")]
    public sealed class OpponentTargetPolicyProfile : AttackTargetPolicyProfile
    {
        public enum Selection
        {
            [InspectorName("首个存活对手")] FirstLiving,
            [InspectorName("当前生命最低的对手")] LowestHealth,
            [InspectorName("当前生命最高的对手")] HighestHealth,
            [InspectorName("全部存活对手")] AllLiving
        }
        [SerializeField, InspectorName("目标选择方式")] private Selection selection;
        [SerializeField, InspectorName("允许命令指定目标")] private bool allowRequestedTargets = true;
        public override IBattleTargetPolicy BuildPolicy() => new OpponentTargetPolicy((OpponentTargetSelection)selection, allowRequestedTargets);
    }
}
