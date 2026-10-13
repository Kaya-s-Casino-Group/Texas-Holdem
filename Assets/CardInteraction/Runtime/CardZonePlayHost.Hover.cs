using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;

namespace CardInteraction
{
    public sealed partial class CardZonePlayHost
    {
        private static readonly Dictionary<CardDefinitionId, CardDefinition> standardDefinitions =
            StandardDeck.Cards().ToDictionary(card => card.Id);

        /// <summary>The validation host exposes printed-face base points, never values inferred from artwork.</summary>
        public bool TryGetHoveredCard(out CardHoverTarget target)
        {
            target = default;
            if (!IsRunning || !isActiveAndEnabled || view == null || !view.isActiveAndEnabled) return false;
            foreach (var state in Cards)
            {
                if (!state.Interaction.IsHovered || !state.FaceUp || !view.TryGetView(state.Id, out var visual) ||
                    visual.Data == null || !visual.Data.FaceUp || !visual.Data.Interaction.IsHovered ||
                    visual.IsEntering || visual.IsExiting) continue;
                var card = state.Card;
                if (visual.CardId != card.Id || !standardDefinitions.TryGetValue(card.DefinitionId, out var definition)) continue;
                // Disabled/ineffective cards can still be inspected. Ineffective does not erase their printed value.
                target = new CardHoverTarget(visual, definition.Suit, definition.Rank, CardScoreRules.GetBasePoints(definition));
                return true;
            }
            return false;
        }
    }
}
