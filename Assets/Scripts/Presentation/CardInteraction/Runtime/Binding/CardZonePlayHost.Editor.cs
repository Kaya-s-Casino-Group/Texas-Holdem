#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    public sealed partial class CardZonePlayHost
    {
        // Migration only. These legacy scene fields are cleared once the match takes ownership; absent from Player.
        [SerializeField, HideInInspector] private bool hasSnapshot, hasDeckSnapshot;
        [SerializeField, HideInInspector] private string zoneKind, controllerId;
        [SerializeField, HideInInspector] private List<CardZoneCardState> initialCards = new List<CardZoneCardState>();
        [SerializeField, HideInInspector] private List<CardZoneCardState> initialDiscardedCards = new List<CardZoneCardState>();
        [SerializeField, HideInInspector] private List<CardZoneCardState> initialDeckCards = new List<CardZoneCardState>();

        public bool TryReadLegacy(out CardMatchZoneSnapshot zone, out IReadOnlyList<CardZoneCardState> deck, out IReadOnlyList<CardZoneCardState> discarded)
        {
            zone = null; deck = hasDeckSnapshot ? initialDeckCards : (IReadOnlyList<CardZoneCardState>)Array.Empty<CardZoneCardState>(); discarded = initialDiscardedCards;
            if (match != null || !hasSnapshot) return false;
            var kind = zoneKind == "editor.preview" ? GetComponent<CardZoneView>().DefaultZoneKind : zoneKind;
            zone = new CardMatchZoneSnapshot(new CardZone(new ZoneId(zoneId), kind, string.IsNullOrEmpty(controllerId) ?
                (ParticipantId?)null : new ParticipantId(controllerId), initialCards.Select(card => card.Card.Id)), initialCards);
            return true;
        }
        public void ClearLegacy()
        {
            hasSnapshot = hasDeckSnapshot = false; zoneKind = controllerId = null;
            initialCards.Clear(); initialDiscardedCards.Clear(); initialDeckCards.Clear();
        }
        public void ConfigureRuntimeBinding(CardMatchHost owner)
        {
            if (Application.isPlaying || owner == null || !owner.IsRuntimeManaged || owner.gameObject.scene != gameObject.scene)
                throw new InvalidOperationException("Configure a runtime-managed match in this scene in Edit mode.");
            StopSession(); match = owner; zoneId = deckZoneId = discardZoneId = null; ClearLegacy();
            GetComponent<CardZoneView>().Clear();
        }
        public void Capture(CardZone zone, IEnumerable<CardZoneCardState> cards, Camera camera = null,
            IEnumerable<CardZoneCardState> discarded = null, IEnumerable<CardZoneCardState> deck = null, CardMatchHost owner = null)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Startup configuration is editor-only.");
            if (zone == null || cards == null) throw new ArgumentNullException();
            var copied = cards.Select(card => card != null ? card.Copy(clearHover: true) : throw new ArgumentException("Missing card state.")).ToArray();
            GetComponent<CardZoneView>().ValidateCardCount(copied.Length);
            if (!zone.Cards.SequenceEqual(copied.Select(card => card.Card.Id))) throw new ArgumentException("Snapshot order does not match its zone.");
            if (zone.Kind == "editor.preview") zone = new CardZone(zone.Id, GetComponent<CardZoneView>().DefaultZoneKind, zone.ControllerId, zone.Cards);
            owner = owner != null ? owner : match != null ? match : CardMatchHost.FindOrCreate(gameObject.scene);
            string deckId = string.IsNullOrEmpty(deckZoneId) ? zone.Id.Value + "/deck" : deckZoneId;
            string discardId = string.IsNullOrEmpty(discardZoneId) ? zone.Id.Value + "/discard" : discardZoneId;
            var deckCards = (deck ?? (owner.ContainsZone(new ZoneId(deckId)) ? owner.GetCards(new ZoneId(deckId), true) : Array.Empty<CardZoneCardState>()))
                .Select(card => card != null ? card.Copy(clearHover: true) : throw new ArgumentException("Missing deck state.")).ToArray();
            var discards = (discarded ?? (owner.ContainsZone(new ZoneId(discardId)) ? owner.GetCards(new ZoneId(discardId), true) : Array.Empty<CardZoneCardState>()))
                .Select(card => card != null ? card.Copy() : throw new ArgumentException("Missing discard state.")).ToArray();
            foreach (var card in discards) card.SetInteraction(default);
            var update = owner.PrepareDebugEdit(new[] {
                new CardMatchZoneSnapshot(zone, copied),
                new CardMatchZoneSnapshot(new CardZone(new ZoneId(deckId), CardZoneKinds.Deck, zone.ControllerId, deckCards.Select(card => card.Card.Id)), deckCards),
                new CardMatchZoneSnapshot(new CardZone(new ZoneId(discardId), CardZoneKinds.Discard, zone.ControllerId, discards.Select(card => card.Card.Id)), discards)
            }, preview: true);
            StopSession(); owner.Commit(update); Bind(owner, zone.Id, new ZoneId(deckId), new ZoneId(discardId), camera); ClearLegacy();
        }
        public void ReplaceCards(IEnumerable<CardZoneCardState> cards, bool animate = true) => ReplaceCardsAndDeck(cards, DeckCards, animate);
        public void ReplaceCardsAndDeck(IEnumerable<CardZoneCardState> cards, IEnumerable<CardZoneCardState> deck, bool animate = true)
        {
            if (!IsRunning) throw new InvalidOperationException("Start a match before debug editing.");
            var current = cards?.ToArray() ?? throw new ArgumentNullException(nameof(cards));
            var drawPile = deck?.ToArray() ?? throw new ArgumentNullException(nameof(deck));
            var update = match.PrepareDebugEdit(new[] {
                new CardMatchZoneSnapshot(new CardZone(Zone.Id, Zone.Kind, Zone.ControllerId, current.Select(card => card.Card.Id)), current),
                new CardMatchZoneSnapshot(new CardZone(DeckZone.Id, DeckZone.Kind, DeckZone.ControllerId, drawPile.Select(card => card.Card.Id)), drawPile)
            }, preview: false);
            view.PresentCommitted(update.Project(Zone.Id), () => match.Commit(update), animate); match.Notify(update, view.Data);
        }
    }
}
#endif
