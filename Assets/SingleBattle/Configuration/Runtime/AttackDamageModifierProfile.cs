using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Unit-owned authoring factory. Build a pure immutable rule, never retain the asset in the rule.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "AttackDamageModifierProfile")]
    public abstract class AttackDamageModifierProfile : ScriptableObject
    {
        public abstract IAttackDamageModifier BuildRule();
    }
}
