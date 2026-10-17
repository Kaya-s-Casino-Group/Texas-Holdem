using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;

namespace CardInteraction
{
    /// <summary>All-or-nothing local command; turn and match permissions belong to the gameplay engine.</summary>
    public sealed class CardZonePlayBatch
    {
        public CardZone Source { get; }
        public CardZone Target { get; }
        public IReadOnlyList<CardZoneCardState> Remaining { get; }
        public IReadOnlyList<CardZoneCardState> Destination { get; }
        public IReadOnlyList<CardZoneCardState> Played { get; }
        public CardZonePlayBatch(CardZone source, IEnumerable<CardZoneCardState> sourceCards,
            CardZone target, IEnumerable<CardZoneCardState> targetCards, int capacity)
        {
            var from = Copy(source, sourceCards); var to = Copy(target, targetCards);
            var selected = from.Where(card => card.Interaction.IsSelected).ToArray();
            if (capacity < 0 || to.Length > capacity || selected.Length > capacity - to.Length)
                throw new InvalidOperationException("出牌区剩余槽位不足，整批出牌未提交。");
            if (selected.Any(card => !card.Card.CanPlay)) throw new InvalidOperationException("选中集合含禁用牌，不能出牌。");
            var transfer = source.TransferRangeTo(target, selected.Select(card => card.Card.Id), to.Length);
            Source = transfer.Source; Target = transfer.Target;
            Remaining = Array.AsReadOnly(from.Where(card => !card.Interaction.IsSelected).ToArray());
            foreach (var card in selected) card.SetInteraction(default(CardInteractionState));
            Played = Array.AsReadOnly(selected); Destination = Array.AsReadOnly(to.Concat(selected).ToArray());
        }
        private static CardZoneCardState[] Copy(CardZone zone, IEnumerable<CardZoneCardState> cards)
        {
            if (zone == null || cards == null) throw new ArgumentNullException("Zone cards");
            var copy = cards.Select(card => card != null ? card.Copy() : throw new ArgumentException("Missing card state.")).ToArray();
            if (!zone.Cards.SequenceEqual(copy.Select(card => card.Card.Id))) throw new ArgumentException("Cards must match their zone's full identity order.");
            return copy;
        }
    }
}
