using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
using CardArt;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Reads an explicit deck roster and the match's draw zone. Never mutates cards/zones.</summary>
    public sealed class DeckOverviewPresenter : MonoBehaviour, ICardHoverSource, ICardHoverScope
    {
        [SerializeField] private CardMatchHost match;
        [SerializeField] private DeckOverviewView view;
        [SerializeField] private string deckZoneId;
        [SerializeField] private List<string> rosterIds = new List<string>();
        private HashSet<string> knownIds;
        private CardTableState lastState;
        private DeckOverviewData data;
        private static readonly IReadOnlyDictionary<CardDefinitionId, CardDefinition> definitions = StandardDeck.Cards().ToDictionary(card => card.Id);
        public CardMatchHost Match => match;
        public DeckOverviewView View => view;
        public IReadOnlyList<string> RosterIds => rosterIds.AsReadOnly();
        public bool CapturesHover => view != null && view.IsOpen;
        public void BindArtwork(GameObject cardPrefab)
        {
            if (view == null || cardPrefab == null) throw new ArgumentNullException();
            var surface = cardPrefab.GetComponentInChildren<CardView>(true);
            if (surface == null) throw new ArgumentException("Use a card artwork prefab.");
            view.BindArtProfile(surface.ArtProfile);
        }
#if UNITY_EDITOR
        public void ClearAuthoringRoster()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Authoring cleanup is editor-only.");
            deckZoneId = null; rosterIds.Clear(); knownIds = null; lastState = null; data = null;
            if (view != null) { view.Close(false); view.Clear(); }
        }
#endif

        public bool TryGetHoveredCard(out CardHoverTarget target)
        {
            target = default;
            if (!isActiveAndEnabled || !CapturesHover || view.Data == null) return false;
            foreach (var item in view.Cards)
            {
                if (item == null || !item.isActiveAndEnabled || item.Card == null || !item.Card.FaceUp || !item.Interaction.IsHovered) continue;
                var entry = view.Data.Cards.FirstOrDefault(card => card.Visual.ViewId == item.Card.ViewId);
                if (entry == null || !entry.Visual.FaceUp || !entry.Suit.HasValue || !entry.Rank.HasValue) continue;
                long points = CardScoreRules.GetBasePoints(entry.Rank.Value);
                if (match != null && match.Commands is ICardScoreSource source &&
                    (!entry.Visual.CardId.HasValue || !source.TryGetCardBasePoints(entry.Visual.CardId.Value, out points))) continue;
                target = new CardHoverTarget(item, entry.Suit.Value, entry.Rank.Value, points);
                return true;
            }
            return false;
        }

        public void Configure(CardMatchHost owner, DeckOverviewView target, ZoneId deck, IEnumerable<CardInstanceId> roster)
        {
            if (owner == null || target == null || roster == null) throw new ArgumentNullException();
            if (owner.gameObject.scene != target.gameObject.scene) throw new ArgumentException("Bind an overview in the same scene as the match.");
            if (owner.GetZone(deck).Kind != CardZoneKinds.Deck) throw new ArgumentException("Bind a draw deck.");
            var ids = ValidateRoster(roster);
            foreach (var id in ids) owner.State.GetCard(new CardInstanceId(id));
            match = owner; view = target; deckZoneId = deck.Value; rosterIds = ids;
            knownIds = new HashSet<string>(ids, StringComparer.Ordinal); lastState = null;
            Refresh(true);
        }
        public void SetRoster(IEnumerable<CardInstanceId> roster)
        {
            var ids = ValidateRoster(roster);
            foreach (var id in ids) match.State.GetCard(new CardInstanceId(id));
            rosterIds = ids; knownIds = new HashSet<string>(ids, StringComparer.Ordinal); lastState = null; Refresh(true);
        }
        private static List<string> ValidateRoster(IEnumerable<CardInstanceId> roster)
        {
            var ids = Guard.Copy(roster, nameof(roster));
            if (ids.Any(id => !id.IsValid) || ids.Distinct().Count() != ids.Length) throw new ArgumentException("Deck roster identities must be valid and unique.");
            return ids.Select(id => id.Value).ToList();
        }
        public void Open(bool animate = true)
        {
            if (view == null) throw new InvalidOperationException("Configure the deck overview.");
            view.Show(animate); Canvas.ForceUpdateCanvases(); Refresh(true);
        }
        public void Close() { if (view != null) view.Close(); }
        public void Close(bool animate) { if (view != null) view.Close(animate); }
        public void Refresh(bool force = false)
        {
            if (match == null || view == null || string.IsNullOrEmpty(deckZoneId)) return;
            var projection = ReadData(force);
            if (force || !ReferenceEquals(view.Data,projection)) view.Present(projection);
        }
        /// <summary>Shared observer-approved projection for the summary and full page, including while the page is closed.</summary>
        public DeckOverviewData ReadData(bool force = false)
        {
            if (match == null || string.IsNullOrEmpty(deckZoneId)) return null;
            var state = match.State; if (!force && ReferenceEquals(lastState,state) && data != null) return data;
            var deck = state.GetZone(new ZoneId(deckZoneId)); var remaining = new HashSet<string>(deck.Cards.Select(id => id.Value));
            if (knownIds == null) knownIds = new HashSet<string>(rosterIds, StringComparer.Ordinal);
            // New cards introduced to this draw pile join its inspected roster; no unrelated match cards are inferred.
            var nextIds = new HashSet<string>(knownIds, StringComparer.Ordinal); nextIds.UnionWith(remaining);
            var cards = new List<DeckOverviewCard>();
            foreach (var zone in state.Zones)
            foreach (var card in match.GetCards(zone.Id))
            {
                if (!nextIds.Contains(card.Id)) continue;
                CardDefinition definition = null;
                if (card.FaceUp) definitions.TryGetValue(card.Card.DefinitionId, out definition);
                cards.Add(new DeckOverviewCard(card.Project(), definition, remaining.Contains(card.Id)));
            }
            data = new DeckOverviewData(cards); knownIds = nextIds; lastState = state; return data;
        }
        private void OnEnable() { lastState = null; if (view != null) Refresh(true); }
        private void Update() { if (Application.isPlaying && view != null && view.isActiveAndEnabled) Refresh(); }
    }
}
