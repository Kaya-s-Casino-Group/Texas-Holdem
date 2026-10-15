using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Unit-owned authoring factory. Build a pure immutable rule, never retain the asset in the rule.</summary>
    public abstract class AttackDamageModifierProfile : ScriptableObject
    {
        public abstract IAttackDamageModifier BuildRule();
    }
}
