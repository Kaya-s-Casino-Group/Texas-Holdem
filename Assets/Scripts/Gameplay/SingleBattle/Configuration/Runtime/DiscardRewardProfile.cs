using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Build an immutable, pure discard reward snapshot for the next battle.</summary>
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Poker.SingleBattle", sourceAssembly: "Poker.SingleBattle.Runtime", sourceClassName: "DiscardRewardProfile")]
    public abstract class DiscardRewardProfile : ScriptableObject
    {
        public abstract IDiscardRewardRule BuildRule();
    }
}
