using System;
using System.Collections.Generic;
using CardPresentation;
using PokerEngine;

namespace CardInteraction
{
    public sealed partial class CardZonePlayHost
    {
        public bool HasDeckSnapshot => match != null && !string.IsNullOrEmpty(deckZoneId) && match.ContainsZone(new ZoneId(deckZoneId));
        public IReadOnlyList<CardZoneCardState> InitialDeckCards => Read(deckZoneId, true);
        public IReadOnlyList<CardZoneCardState> DeckCards => Read(deckZoneId, false);
        public CardZone DeckZone => match.GetZone(new ZoneId(deckZoneId));
        public bool CanDraw(int count = 1) => IsRunning && match.Commands == null && isActiveAndEnabled && view is HandZoneView hand &&
            hand.isActiveAndEnabled && count > 0 && count <= DeckZone.Cards.Count &&
            (!hand.CardCapacity.HasValue || count <= hand.CardCapacity.Value - Zone.Cards.Count);
        public int DrawCards(int count = 1, bool animate = true)
        {
            if (!IsRunning || !(view is HandZoneView)) throw new InvalidOperationException("Start a match hand before drawing.");
            var update = match.PrepareDraw(DeckZone.Id, Zone.Id, count, view.CardCapacity ?? int.MaxValue);
            view.PresentCommitted(update.Project(Zone.Id), () => match.Commit(update), animate);
            match.Notify(update, this); RequestSortAfterLanding(sortOrder); return update.Change.Transferred.Count;
        }
    }
}
