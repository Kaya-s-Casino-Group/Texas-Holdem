using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [CreateAssetMenu(menuName = "玩法/战局/出牌伤害配置（得分等额）", fileName = "ScoreDamageProfile")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "ScoreDamageProfile")]
    public sealed class ScoreDamageProfile : ScoredHandDamageProfile
    {
        [SerializeField, InspectorName("规则版本")] private string version = "score-damage.equal.1";
        public override IScoredHandDamageRule BuildRule() => new EqualScoreDamageRule(version);
    }
}
