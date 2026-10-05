using System;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Serializable local card data for a scene-authored zone; not an authoritative match save.</summary>
    [Serializable]
    public sealed class CardZoneCardState
    {
        [SerializeField] private string id, definitionId, originId, artId;
        [SerializeField] private bool faceUp = true, disabled, hovered, selected;
        [SerializeField] private CardEffects effects;
        [SerializeField] private Color tint = Color.white;
        public string Id => id;
        public string ArtId => artId;
        public bool FaceUp => faceUp;
        public Color Tint => tint;
        public CardInteractionState Interaction => new CardInteractionState(hovered, selected);
        public CardInstance Card => new CardInstance(new CardInstanceId(id), new CardDefinitionId(definitionId),
            string.IsNullOrEmpty(originId) ? null : originId, effects, disabled);

        public CardZoneCardState(CardInstance card, string artId, bool faceUp, Color tint, CardInteractionState interaction)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            id = card.Id.Value; definitionId = card.DefinitionId.Value; originId = card.OriginId;
            effects = card.Effects; disabled = card.IsDisabled; this.artId = Guard.Text(artId, nameof(artId));
            this.faceUp = faceUp; this.tint = tint; SetInteraction(interaction);
            Project();
        }
        public void SetInteraction(CardInteractionState state) { hovered = state.IsHovered; selected = state.IsSelected; }
        public CardVisualData Project() => CardVisualData.FromCard(Card, id, faceUp, _ => artId, Interaction, tint);
        public CardZoneCardState Copy(bool clearHover = false) => new CardZoneCardState(Card, artId, faceUp, tint,
            clearHover ? Interaction.WithHovered(false) : Interaction);
    }
}
