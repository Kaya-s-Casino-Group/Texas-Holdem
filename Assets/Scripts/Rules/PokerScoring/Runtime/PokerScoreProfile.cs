using System;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace Poker.Scoring
{
    /// <summary>Authoring only. Build produces an immutable, Unity-free snapshot shared by Run and battle consumers.</summary>
    [CreateAssetMenu(menuName = "玩法/牌型计分配置", fileName = "PokerScoreProfile")]
    public sealed class PokerScoreProfile : ScriptableObject
    {
        [Serializable]
        private sealed class HandEntry
        {
            public HandCategory category;
            [Min(0)] public long chips;
            [Min(0)] public float multiplier;
            [Min(0)] public long chipsPerLevel;
            [Min(0)] public float multiplierPerLevel;
            public HandEntry(HandScoreRule rule)
            { category = rule.Category; chips = rule.BasePoints; multiplier = (float)rule.BaseMultiplier; chipsPerLevel = rule.PointsPerLevel; multiplierPerLevel = (float)rule.MultiplierPerLevel; }
            public HandScoreRule Build() => new HandScoreRule(category, chips, (decimal)multiplier, chipsPerLevel, (decimal)multiplierPerLevel);
        }
        [Serializable]
        private sealed class RankEntry
        {
            public Rank rank;
            [Min(0)] public long chips;
            public RankEntry(Rank rank) { this.rank = rank; chips = CardPointProfile.Default.GetBasePoints(rank); }
        }
        [Serializable]
        private sealed class FaceEntry
        {
            public Suit suit = Suit.Spades;
            public Rank rank = Rank.Ace;
            [Min(0)] public long chips = 11;
        }
        [SerializeField, InspectorName("规则版本")] private string version = "score.balatro.1";
        [SerializeField] private HandEntry[] hands = DefaultHands();
        [SerializeField] private RankEntry[] ranks = Enum.GetValues(typeof(Rank)).Cast<Rank>().Select(rank => new RankEntry(rank)).ToArray();
        [SerializeField] private FaceEntry[] overrides = Array.Empty<FaceEntry>();
        private static HandEntry[] DefaultHands()
        {
            var defaults = ScoreProfile.Default;
            return Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>().Select(category => new HandEntry(defaults.Rule(category))).ToArray();
        }
        public ScoreProfile Build() => new ScoreProfile(version, hands.Select(hand => hand.Build()),
            new CardPointProfile(ranks.Select(rank => new RankPointRule(rank.rank, rank.chips)),
                overrides.Select(face => new FacePointRule(new CardFace(face.rank, face.suit), face.chips))));
    }
}
