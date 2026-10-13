using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;

namespace CardPresentation
{
    /// <summary>Observer-approved inspection entry; presence in the draw zone is separate from rule effects.</summary>
    public sealed class DeckOverviewCard
    {
        public CardVisualData Visual { get; }
        public Suit? Suit { get; }
        public Rank? Rank { get; }
        public bool IsRemaining { get; }
        public DeckOverviewCard(CardVisualData visual, CardDefinition definition, bool remaining)
        {
            Visual = visual ?? throw new ArgumentNullException(nameof(visual));
            if (!visual.FaceUp && definition != null) throw new ArgumentException("Hidden cards cannot disclose suit/rank.");
            Suit = definition?.Suit; Rank = definition?.Rank; IsRemaining = remaining;
        }
    }
    public sealed class DeckSuitCount
    {
        public int Numbers { get; }
        public int Faces { get; }
        public int Aces { get; }
        public int Total => Numbers + Faces + Aces;
        internal DeckSuitCount(IEnumerable<DeckOverviewCard> cards)
        {
            var remaining = cards.Where(card => card.IsRemaining).ToArray();
            Numbers = remaining.Count(card => (int?)card.Rank >= (int)PokerEngine.Rank.Two && (int?)card.Rank <= (int)PokerEngine.Rank.Ten);
            Faces = remaining.Count(card => (int?)card.Rank >= (int)PokerEngine.Rank.Jack && (int?)card.Rank <= (int)PokerEngine.Rank.King);
            Aces = remaining.Count(card => card.Rank == PokerEngine.Rank.Ace);
        }
    }
    public sealed class DeckOverviewData
    {
        public IReadOnlyList<DeckOverviewCard> Cards { get; }
        public int RemainingCount => Cards.Count(card => card.IsRemaining);
        public int OtherCount => Cards.Count(card => !card.Suit.HasValue);
        public int OtherRemainingCount => Cards.Count(card => !card.Suit.HasValue && card.IsRemaining);
        public DeckOverviewData(IEnumerable<DeckOverviewCard> cards)
        {
            var copy = Guard.Copy(cards, nameof(cards));
            if (copy.Any(card => card == null) || copy.Select(card => card.Visual.ViewId).Distinct(StringComparer.Ordinal).Count() != copy.Length)
                throw new ArgumentException("Overview card identities must be unique.");
            Cards = Array.AsReadOnly(copy);
        }
        public IReadOnlyList<DeckOverviewCard> Row(Suit suit) => Array.AsReadOnly(Cards.Where(card => card.Suit == suit)
            .OrderByDescending(card => card.Rank).ToArray());
        public DeckSuitCount Counts(Suit suit) => new DeckSuitCount(Cards.Where(card => card.Suit == suit));
        public int Remaining(Rank rank) => Cards.Count(card => card.IsRemaining && card.Rank == rank);
        public int Remaining(Suit suit, Rank rank) => Cards.Count(card => card.IsRemaining && card.Suit == suit && card.Rank == rank);
    }
}
