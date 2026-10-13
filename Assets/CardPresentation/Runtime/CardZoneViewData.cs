using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;
using UnityEngine;

namespace CardPresentation
{
    /// <summary>Already-visible presentation data. A null FaceArtId represents an unknown face.</summary>
    public sealed class CardVisualData
    {
        public string ViewId { get; }
        public CardInstanceId? CardId { get; }
        public string FaceArtId { get; }
        public bool FaceUp => FaceArtId != null;
        public Color Tint { get; }
        public CardEffects Effects { get; }
        public bool IsDisabled { get; }
        public CardInteractionState Interaction { get; }
        public CardStateArtBindings ArtBindings { get; }

        public CardVisualData(string viewId, string faceArtId = null, CardInstanceId? cardId = null, Color? tint = null,
            CardEffects effects = CardEffects.None, bool isDisabled = false, CardInteractionState interaction = default(CardInteractionState), CardStateArtBindings artBindings = null)
        {
            ArtBindings = artBindings;
            ViewId = Guard.Text(viewId, nameof(viewId));
            if (cardId.HasValue && !cardId.Value.IsValid) throw new ArgumentException("Invalid card identity.", nameof(cardId));
            CardId = cardId;
            FaceArtId = faceArtId == null ? null : Guard.Text(faceArtId, nameof(faceArtId));
            Tint = tint ?? Color.white;
            CardEffectRules.Validate(effects); Effects = effects; IsDisabled = isDisabled; Interaction = interaction;
            if (!CardZoneRowLayout.Finite(Tint.r) || !CardZoneRowLayout.Finite(Tint.g) ||
                !CardZoneRowLayout.Finite(Tint.b) || !CardZoneRowLayout.Finite(Tint.a))
                throw new ArgumentException("Tint must be finite.", nameof(tint));
        }

        /// <summary>Authority/local projection. Invisible cards disclose no rule attributes and do not resolve artwork.
        /// Remote clients receive the already-filtered CardVisualData, never the authoritative CardInstance.</summary>
        public static CardVisualData FromCard(CardInstance card, string viewId, bool isVisible,
            Func<CardDefinitionId, string> resolveArt, CardInteractionState interaction = default(CardInteractionState), Color? tint = null, CardStateArtBindings artBindings = null)
        {
            if (card == null) throw new ArgumentNullException(nameof(card));
            if (!isVisible) return new CardVisualData(viewId, tint: tint, interaction: interaction, artBindings: artBindings);
            if (resolveArt == null) throw new ArgumentNullException(nameof(resolveArt));
            var art = Guard.Text(resolveArt(card.DefinitionId), nameof(resolveArt));
            return new CardVisualData(viewId, art, card.Id, tint, card.Effects, card.IsDisabled, interaction, artBindings);
        }
        /// <summary>Run-owned deck entries use the same artwork attributes without inventing battle-instance IDs.</summary>
        public static CardVisualData FromDeckEntry(DeckEntry entry, string viewId, bool isVisible,
            Func<CardDefinitionId, string> resolveArt, Color? tint = null, CardStateArtBindings artBindings = null)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (!isVisible) return new CardVisualData(viewId, tint: tint, artBindings: artBindings);
            if (resolveArt == null) throw new ArgumentNullException(nameof(resolveArt));
            return new CardVisualData(viewId, Guard.Text(resolveArt(entry.DefinitionId), nameof(resolveArt)), tint: tint,
                effects: entry.Effects, isDisabled: entry.IsDisabled, artBindings: artBindings);
        }
    }

    /// <summary>One ordered, observer-filtered zone. Contains no live engine state or ownership permissions.</summary>
    public sealed class CardZoneViewData
    {
        public ZoneId ZoneId { get; }
        public IReadOnlyList<CardVisualData> Cards { get; }

        public CardZoneViewData(ZoneId zoneId, IEnumerable<CardVisualData> cards)
        {
            if (!zoneId.IsValid) throw new ArgumentException("Invalid zone identity.", nameof(zoneId));
            var copy = Guard.Copy(cards, nameof(cards));
            if (copy.Select(c => c.ViewId).Distinct(StringComparer.Ordinal).Count() != copy.Length ||
                copy.Where(c => c.CardId.HasValue).Select(c => c.CardId.Value).Distinct().Count() != copy.Count(c => c.CardId.HasValue))
                throw new ArgumentException("Visual and disclosed card identities must be unique within a zone.", nameof(cards));
            ZoneId = zoneId; Cards = Guard.View(copy);
        }

        /// <summary>Local/authority adapter only. The caller resolves artwork and filters hidden information.</summary>
        public static CardZoneViewData FromZone(CardZone zone, Func<CardInstanceId, CardVisualData> project)
        {
            if (zone == null) throw new ArgumentNullException(nameof(zone));
            if (project == null) throw new ArgumentNullException(nameof(project));
            return new CardZoneViewData(zone.Id, zone.Cards.Select(id => {
                var item = project(id);
                if (item == null || item.CardId.HasValue && item.CardId.Value != id)
                    throw new ArgumentException("Projection returned a missing or mismatched card.", nameof(project));
                return item;
            }));
        }
    }
}
