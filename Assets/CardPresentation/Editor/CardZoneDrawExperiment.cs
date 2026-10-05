using System;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace CardPresentation.Editor
{
    /// <summary>Serializable editor-only deck and last draw audit; independent of the live presentation clock.</summary>
    [Serializable]
    public sealed class CardZoneDrawExperiment
    {
        [SerializeField] private CardZonePreviewModel deck = new CardZonePreviewModel();
        [SerializeField] private bool initialized;
        [SerializeField] private string[] deckBefore = new string[0], deckAfter = new string[0];
        [SerializeField] private string[] handBefore = new string[0], handAfter = new string[0], drawn = new string[0];
        [SerializeField] private bool generatedDraw;
        public CardZonePreviewModel Deck => deck;
        public bool GeneratedDraw => generatedDraw;
        public System.Collections.Generic.IReadOnlyList<PreviewCard> DeckCards => deck.Cards;
        public System.Collections.Generic.IReadOnlyList<string> DeckBefore => Array.AsReadOnly(deckBefore);
        public System.Collections.Generic.IReadOnlyList<string> DeckAfter => Array.AsReadOnly(deckAfter);
        public System.Collections.Generic.IReadOnlyList<string> HandBefore => Array.AsReadOnly(handBefore);
        public System.Collections.Generic.IReadOnlyList<string> HandAfter => Array.AsReadOnly(handAfter);
        public System.Collections.Generic.IReadOnlyList<string> Drawn => Array.AsReadOnly(drawn);
        public bool HasDraw => drawn.Length > 0;
        public CardZone BuildDeckZone() => deck.BuildZone(new ZoneId("editor/draw-deck"));

        public void EnsureDeck(string[] artIds)
        {
            if (!initialized && artIds != null && artIds.Length > 0) PrepareDeck(artIds);
        }

        public void PrepareDeck(string[] artIds, int count = 12)
        {
            if (artIds == null || artIds.Length == 0) throw new ArgumentException("请先配置卡牌图集。", nameof(artIds));
            if (count <= 0 || count > CardZonePreviewModel.MaximumCards) throw new ArgumentOutOfRangeException(nameof(count));
            var next = new CardZonePreviewModel();
            for (int i = 0; i < count; i++) next.Add(artIds[i % artIds.Length]);
            foreach (var card in next.Cards) card.ToCard();
            deck = next; initialized = true; ClearAudit();
        }

        public void Draw(CardZonePreviewModel hand, int count, int targetIndex = -1)
            => Record(hand, () => hand.DrawFromDeck(deck, count, targetIndex), false);

        public void DrawCard(CardZonePreviewModel hand, CardInstanceId id, int targetIndex = -1)
            => Record(hand, () => hand.TakeFromDeck(deck, new[] { id }, targetIndex), false);

        /// <summary>Create explicitly chosen test cards, then transfer via Core without consuming the configured deck.</summary>
        public void DrawSpecified(CardZonePreviewModel hand, string artId, int count, int targetIndex = -1, bool faceUp = true)
        {
            var source = new CardZonePreviewModel(); source.Insert(0, artId, count, faceUp);
            Record(hand, () => hand.DrawFromDeck(source, count, targetIndex), true);
        }

        private void Record(CardZonePreviewModel hand, Func<CardZoneTransfer> transferCards, bool generated)
        {
            if (hand == null) throw new ArgumentNullException(nameof(hand));
            var beforeHand = hand.BuildZone().Cards.Select(id => id.Value).ToArray();
            var beforeDeck = BuildDeckZone().Cards.Select(id => id.Value).ToArray();
            var transfer = transferCards();
            handBefore = beforeHand; deckBefore = beforeDeck;
            handAfter = transfer.Target.Cards.Select(id => id.Value).ToArray();
            deckAfter = BuildDeckZone().Cards.Select(id => id.Value).ToArray();
            drawn = transfer.Cards.Select(id => id.Value).ToArray();
            generatedDraw = generated;
        }

        private void ClearAudit()
        {
            deckBefore = deckAfter = handBefore = handAfter = drawn = new string[0];
            generatedDraw = false;
        }
    }
}
