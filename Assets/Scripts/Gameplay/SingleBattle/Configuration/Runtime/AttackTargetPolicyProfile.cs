using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Build a pure snapshot; policies must not read mutable Unity assets during a battle.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "AttackTargetPolicyProfile")]
    public abstract class AttackTargetPolicyProfile : ScriptableObject
    {
        public abstract IBattleTargetPolicy BuildPolicy();
    }
}
