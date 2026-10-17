using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Authoring factory only. Implementations must build immutable pure rules for one battle.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "ScoredHandDamageProfile")]
    public abstract class ScoredHandDamageProfile : ScriptableObject
    {
        public abstract IScoredHandDamageRule BuildRule();
    }
}
