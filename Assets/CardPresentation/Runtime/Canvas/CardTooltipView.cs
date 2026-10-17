using System;
using System.Globalization;
using PokerEngine;
using TMPro;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Display supplied suit, rank and chip points. The caller owns card visibility and scoring rules.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class CardTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text suitRankText;
        [SerializeField] private TMP_Text chipsText;
        [SerializeField] private Suit suit = Suit.Clubs;
        [SerializeField] private Rank rank = Rank.Jack;
        [SerializeField] private long chipPoints = 10;
        [SerializeField] private Color blackSuitColor = new Color32(45, 30, 20, 255);
        [SerializeField] private Color redSuitColor = new Color32(199, 53, 50, 255);
        [SerializeField] private Color chipNumberColor = new Color32(0, 154, 255, 255);

        public TMP_Text SuitRankText => suitRankText;
        public TMP_Text ChipsText => chipsText;
        public bool IsConfigured => suitRankText != null && chipsText != null;
        public Suit Suit => suit;
        public Rank Rank => rank;
        public long ChipPoints => chipPoints;

        public void Configure(TMP_Text header, TMP_Text chips)
        {
            if (header == null) throw new ArgumentNullException(nameof(header));
            if (chips == null) throw new ArgumentNullException(nameof(chips));
            if (header == chips) throw new ArgumentException("The tooltip needs two separate labels.");
            suitRankText = header;
            chipsText = chips;
            Refresh();
        }

        /// <summary>Use observer-authorized card data and points resolved by the caller.</summary>
        public void SetContent(Suit cardSuit, Rank cardRank, long points)
        {
            GetSuitSymbol(cardSuit);
            if (!Enum.IsDefined(typeof(Rank), cardRank)) throw new ArgumentOutOfRangeException(nameof(cardRank));
            suit = cardSuit;
            rank = cardRank;
            chipPoints = points;
            Refresh();
        }

        public static char GetSuitSymbol(Suit value)
        {
            switch (value)
            {
                case Suit.Clubs: return '♣';
                case Suit.Diamonds: return '♦';
                case Suit.Hearts: return '♥';
                case Suit.Spades: return '♠';
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public static string GetSuitName(Suit value)
        {
            switch (value)
            {
                case Suit.Clubs: return "梅花";
                case Suit.Diamonds: return "方块";
                case Suit.Hearts: return "红桃";
                case Suit.Spades: return "黑桃";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        private void Refresh()
        {
            if (suitRankText == null || chipsText == null) return;
            string rankLabel;
            switch (rank)
            {
                case Rank.Ace: rankLabel = "A"; break;
                case Rank.King: rankLabel = "K"; break;
                case Rank.Queen: rankLabel = "Q"; break;
                case Rank.Jack: rankLabel = "J"; break;
                default: rankLabel = ((int)rank).ToString(CultureInfo.InvariantCulture); break;
            }
            suitRankText.text = GetSuitSymbol(suit) + " " + GetSuitName(suit) + " " + rankLabel;
            // Tint the entire heading, including the rank, and restore it when switching back to a black suit.
            suitRankText.color = suit == Suit.Hearts || suit == Suit.Diamonds ? redSuitColor : blackSuitColor;
            string points = (chipPoints >= 0 ? "+" : "") + chipPoints.ToString(CultureInfo.InvariantCulture);
            chipsText.color = blackSuitColor;
            chipsText.text = "<color=#" + ColorUtility.ToHtmlStringRGB(chipNumberColor) + ">" + points + "</color>筹码";
        }

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();
    }
}
