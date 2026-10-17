using System;
using System.Collections.Generic;
using System.Linq;
using PokerEngine;

namespace CardInteraction
{
    /// <summary>Read-only rule values supplied by gameplay, independent of the card-zone presentation.</summary>
    public interface ICardScoreSource
    {
        bool TryGetCardBasePoints(CardInstanceId card, out long points);
    }

    /// <summary>Immutable input captured in observer hand order. Contains no scene objects or live selection.
    /// Match/zone/controller identities must be checked against gameplay state before accepting the cards.</summary>
    public sealed class CardSelectionRequest
    {
        public MatchId MatchId { get; }
        public ZoneId ZoneId { get; }
        public ParticipantId? ControllerId { get; }
        public IReadOnlyList<CardInstanceId> Cards { get; }

        public CardSelectionRequest(MatchId matchId, ZoneId zoneId, ParticipantId? controllerId,
            IEnumerable<CardInstanceId> cards)
        {
            if (!matchId.IsValid || !zoneId.IsValid || controllerId.HasValue && !controllerId.Value.IsValid)
                throw new ArgumentException("Invalid command identity.");
            var copy = cards?.ToArray() ?? throw new ArgumentNullException(nameof(cards));
            if (copy.Any(id => !id.IsValid) || copy.Distinct().Count() != copy.Length)
                throw new ArgumentException("Command cards must have distinct valid identities.", nameof(cards));
            MatchId = matchId; ZoneId = zoneId; ControllerId = controllerId; Cards = Array.AsReadOnly(copy);
        }
    }

    /// <summary>Gameplay command adapter. Requests contain only identities; each mode owns its presentation
    /// bindings. Animate is a playback option and never changes rule resolution.</summary>
    public interface ICardMatchCommands
    {
        bool CanInteract { get; }
        bool CanPlay(CardSelectionRequest request);
        bool CanDiscard(CardSelectionRequest request);
        int Play(CardSelectionRequest request, bool animate);
        int Discard(CardSelectionRequest request, bool animate);
    }
}
