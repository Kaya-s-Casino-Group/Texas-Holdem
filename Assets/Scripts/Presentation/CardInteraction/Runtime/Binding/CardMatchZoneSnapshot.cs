using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    [Serializable]
    public sealed class CardMatchZoneSnapshot
    {
        [SerializeField] private string id, kind, controllerId;
        [SerializeField] private List<CardZoneCardState> cards = new List<CardZoneCardState>();
        public CardZone Zone => new CardZone(new ZoneId(id), kind, string.IsNullOrEmpty(controllerId) ?
            (ParticipantId?)null : new ParticipantId(controllerId), cards.Select(card => card.Card.Id));
        public IReadOnlyList<CardZoneCardState> Cards => cards.AsReadOnly();
        public CardMatchZoneSnapshot(CardZone zone, IEnumerable<CardZoneCardState> states)
        {
            if (zone == null || states == null) throw new ArgumentNullException();
            cards = states.Select(card => card != null ? card.Copy() : throw new ArgumentException("Missing card projection.")).ToList();
            if (!zone.Cards.SequenceEqual(cards.Select(card => card.Card.Id))) throw new ArgumentException("Zone and card order disagree.");
            id = zone.Id.Value; kind = zone.Kind; controllerId = zone.ControllerId?.Value;
        }
    }

}
