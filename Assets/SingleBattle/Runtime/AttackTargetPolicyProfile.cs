using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Build a pure snapshot; policies must not read mutable Unity assets during a battle.</summary>
    public abstract class AttackTargetPolicyProfile : ScriptableObject
    {
        public abstract IBattleTargetPolicy BuildPolicy();
    }
}
