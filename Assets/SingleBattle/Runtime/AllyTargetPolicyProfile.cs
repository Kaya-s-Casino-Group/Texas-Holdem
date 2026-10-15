using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    public abstract class AllyTargetPolicyProfile : ScriptableObject
    {
        public abstract IBattleAllyTargetPolicy BuildPolicy();
    }
}
