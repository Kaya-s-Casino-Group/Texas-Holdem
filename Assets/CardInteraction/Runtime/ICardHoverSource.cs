using System;
using CardPresentation;
using PokerEngine;

namespace CardInteraction
{
    /// <summary>Observer-authorized hover information supplied by a local gameplay adapter.</summary>
    public interface ICardHoverSource
    {
        bool TryGetHoveredCard(out CardHoverTarget target);
    }

    /// <summary>An open inspection page can claim the hover layer without modifying background cards.</summary>
    public interface ICardHoverScope
    {
        bool CapturesHover { get; }
    }

    /// <summary>Display values and a live visual anchor; no authoritative card or mutable score state.</summary>
    public readonly struct CardHoverTarget
    {
        public CardInstanceView View { get; }
        public CardUIItemView UIView { get; }
        public Suit Suit { get; }
        public Rank Rank { get; }
        public long ChipPoints { get; }

        public CardHoverTarget(CardInstanceView view, Suit suit, Rank rank, long chipPoints)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (!Enum.IsDefined(typeof(Suit), suit)) throw new ArgumentOutOfRangeException(nameof(suit));
            if (!Enum.IsDefined(typeof(Rank), rank)) throw new ArgumentOutOfRangeException(nameof(rank));
            View = view; UIView = null; Suit = suit; Rank = rank; ChipPoints = chipPoints;
        }

        public CardHoverTarget(CardUIItemView view, Suit suit, Rank rank, long chipPoints)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (!Enum.IsDefined(typeof(Suit), suit)) throw new ArgumentOutOfRangeException(nameof(suit));
            if (!Enum.IsDefined(typeof(Rank), rank)) throw new ArgumentOutOfRangeException(nameof(rank));
            View = null; UIView = view; Suit = suit; Rank = rank; ChipPoints = chipPoints;
        }
    }
}
