using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Authoring factory only. Implementations must build immutable pure rules for one battle.</summary>
    public abstract class ScoredHandDamageProfile : ScriptableObject
    {
        public abstract IScoredHandDamageRule BuildRule();
    }
}
