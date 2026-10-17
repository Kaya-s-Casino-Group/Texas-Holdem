using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;

namespace CardInteraction
{
    /// <summary>Local-zone command preparation. Full match turn/ownership rules remain with the gameplay engine.</summary>
    public sealed class CardZoneDiscardBatch
    {
        public CardZone Source { get; }
        public CardZone Target { get; }
        public IReadOnlyList<CardZoneCardState> Remaining { get; }
        public IReadOnlyList<CardZoneCardState> Discarded { get; }

        public CardZoneDiscardBatch(CardZone source, IEnumerable<CardZoneCardState> cards, CardZone discardZone)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (discardZone == null) throw new ArgumentNullException(nameof(discardZone));
            var copy = cards.Select(card => card != null ? card.Copy() : throw new ArgumentException("Missing card state.")).ToArray();
            if (!source.Cards.SequenceEqual(copy.Select(card => card.Card.Id)))
                throw new ArgumentException("Cards must match the source zone's identity order.", nameof(cards));
            var selected = copy.Where(card => card.Interaction.IsSelected).ToArray();
            if (selected.Any(card => !card.Card.CanDiscard))
                throw new InvalidOperationException("选中集合含禁用牌，不能弃牌；请先取消这些牌的选中。");
            var transfer = source.TransferRangeTo(discardZone, selected.Select(card => card.Card.Id), discardZone.Cards.Count);
            Source = transfer.Source; Target = transfer.Target;
            Remaining = Array.AsReadOnly(copy.Where(card => !card.Interaction.IsSelected).ToArray());
            foreach (var card in selected) card.SetInteraction(default(CardPresentation.CardInteractionState));
            Discarded = Array.AsReadOnly(selected);
        }
    }
}
