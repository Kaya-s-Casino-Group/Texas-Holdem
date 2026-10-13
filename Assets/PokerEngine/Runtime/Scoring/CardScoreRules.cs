using System;

namespace PokerEngine
{
    /// <summary>Shared base-point rules for scoring and read-only card inspection. No UI or match state.</summary>
    public static class CardScoreRules
    {
        /// <summary>2–10 use their rank, J/Q/K score 10, A scores 11. Pass the effective rank when rules alter the face.</summary>
        public static long GetBasePoints(Rank rank)
        {
            if (!Enum.IsDefined(typeof(Rank), rank)) throw new ArgumentOutOfRangeException(nameof(rank));
            return rank == Rank.Ace ? 11 : Math.Min(10, (int)rank);
        }

        /// <summary>Printed-face value only; resolving temporary face changes belongs to the gameplay host.</summary>
        public static long GetBasePoints(CardDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return GetBasePoints(definition.Rank);
        }
    }
}
