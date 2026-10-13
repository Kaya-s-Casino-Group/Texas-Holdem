using PokerEngine;
using UnityEngine;

namespace Poker.SingleBattle
{
    /// <summary>Build an immutable, pure discard reward snapshot for the next battle.</summary>
    public abstract class DiscardRewardProfile : ScriptableObject
    {
        public abstract IDiscardRewardRule BuildRule();
    }
}
