using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Unit-owned incoming-damage authoring factory, independent of shared battle rules.</summary>
    public abstract class IncomingDamageModifierProfile : ScriptableObject
    {
        public abstract IIncomingDamageModifier BuildRule();
    }
}
