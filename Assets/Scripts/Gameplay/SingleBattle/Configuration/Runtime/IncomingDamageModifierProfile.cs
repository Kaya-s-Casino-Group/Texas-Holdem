using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Unit-owned incoming-damage authoring factory, independent of shared battle rules.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "IncomingDamageModifierProfile")]
    public abstract class IncomingDamageModifierProfile : ScriptableObject
    {
        public abstract IIncomingDamageModifier BuildRule();
    }
}
