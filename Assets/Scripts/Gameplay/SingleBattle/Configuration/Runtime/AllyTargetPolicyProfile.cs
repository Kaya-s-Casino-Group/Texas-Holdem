using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "AllyTargetPolicyProfile")]
    public abstract class AllyTargetPolicyProfile : ScriptableObject
    {
        public abstract IBattleAllyTargetPolicy BuildPolicy();
    }
}
