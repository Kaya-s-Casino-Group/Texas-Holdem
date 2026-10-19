using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Observer data, independent of world/Canvas layout, artwork, colors and rendering assets.</summary>
    [Serializable]
    public sealed class CardMatchCardState
    {
        [SerializeField] private string id, definitionId, originId;
        [SerializeField] private bool faceUp = true, disabled, hovered, selected;
        [SerializeField] private CardEffects effects;
        public string Id => id;
        public bool Visible => faceUp;
        public CardInstance Card => new CardInstance(new CardInstanceId(id), new CardDefinitionId(definitionId),
            string.IsNullOrEmpty(originId) ? null : originId, effects, disabled);
        public CardInteractionState Interaction => new CardInteractionState(hovered, selected);
        public CardMatchCardState(CardInstance card, bool visible = true, CardInteractionState interaction = default)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            id = card.Id.Value; definitionId = card.DefinitionId.Value; originId = card.OriginId;
            effects = card.Effects; disabled = card.IsDisabled; faceUp = visible; SetInteraction(interaction);
        }
        internal void SetInteraction(CardInteractionState interaction) { hovered = interaction.IsHovered; selected = interaction.IsSelected; }
        public CardMatchCardState Copy(bool clearHover = false) => new CardMatchCardState(Card, Visible,
            clearHover ? Interaction.WithHovered(false) : Interaction);
    }

    [Serializable]
    public sealed class CardMatchZoneData
    {
        [SerializeField] private string id, kind, controllerId;
        [SerializeField] private List<CardMatchCardState> cards = new List<CardMatchCardState>();
        public CardZone Zone => new CardZone(new ZoneId(id), kind, string.IsNullOrEmpty(controllerId) ?
            (ParticipantId?)null : new ParticipantId(controllerId), cards.Select(card => card.Card.Id));
        public IReadOnlyList<CardMatchCardState> Cards => cards.AsReadOnly();
        public CardMatchZoneData(CardZone zone, IEnumerable<CardMatchCardState> states)
        {
            if (zone == null || states == null) throw new ArgumentNullException();
            cards = states.Select(card => card?.Copy() ?? throw new ArgumentException("Missing card state.")).ToList();
            if (!zone.Cards.SequenceEqual(cards.Select(card => card.Card.Id))) throw new ArgumentException("Zone and card order disagree.");
            id = zone.Id.Value; kind = zone.Kind; controllerId = zone.ControllerId?.Value;
        }
    }
}
