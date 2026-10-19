using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    /// <summary>Artwork projection for the existing card frontend. The shared match never depends on this adapter.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CardMatchHost))]
    public sealed class CardMatchPresentationAdapter : MonoBehaviour
    {
        [SerializeField] private CardStateArtBindings artBindings;
        [SerializeField] private List<CardMatchZoneSnapshot> initialZones = new List<CardMatchZoneSnapshot>();
        private Dictionary<string, CardZoneCardState> styles;
        private readonly ConditionalWeakTable<CardMatchUpdate, Dictionary<string, CardZoneCardState>> pending =
            new ConditionalWeakTable<CardMatchUpdate, Dictionary<string, CardZoneCardState>>();
        private CardMatchHost owner;
        private sealed class Decoration
        {
            public CardZoneCardState Style, Card;
            public CardStateArtBindings Bindings;
        }
        private readonly ConditionalWeakTable<CardMatchCardState, Decoration> decorated =
            new ConditionalWeakTable<CardMatchCardState, Decoration>();
        public CardStateArtBindings ArtBindings => artBindings;
        public static CardMatchPresentationAdapter For(CardMatchHost match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            var adapter = match.GetComponent<CardMatchPresentationAdapter>() ?? match.gameObject.AddComponent<CardMatchPresentationAdapter>();
            adapter.Subscribe(); return adapter;
        }
        private void Subscribe()
        {
            if (owner != null) return;
            owner = GetComponent<CardMatchHost>(); owner.Committed += OnCommitted;
        }
        private void OnEnable() => Subscribe();
        private void OnDestroy() { if (owner != null) owner.Committed -= OnCommitted; }
        private Dictionary<string, CardZoneCardState> Styles => styles ??
            (styles = initialZones.SelectMany(zone => zone.Cards).ToDictionary(card => card.Id, card => card.Copy()));
        public void ConfigureArtBindings(CardStateArtBindings bindings)
        {
            Subscribe();
            if (owner.IsRunning) throw new InvalidOperationException("Configure artwork before starting the card frontend.");
            artBindings = bindings;
        }
        internal void StartWithStyles(IEnumerable<CardZoneCardState> cards, Action<CardMatchCardState[]> start)
        {
            var copy = Copy(cards); var next = copy.ToDictionary(card => card.Id, card => card);
            var previous = styles; styles = next;
            try { start(copy.Select(ToState).ToArray()); }
            catch { styles = previous; throw; }
        }
        internal void Stage(CardMatchUpdate update, IEnumerable<CardZoneCardState> cards)
        {
            var next = new Dictionary<string, CardZoneCardState>(Styles);
            foreach (var card in Copy(cards)) next[card.Id] = card;
            pending.Add(update, next);
        }
        private void OnCommitted(CardMatchUpdate update)
        {
            if (pending.TryGetValue(update, out var next)) { styles = next; pending.Remove(update); }
#if UNITY_EDITOR
            if (update.IsPreview) SaveInitialStyles(owner.InitialState);
#endif
        }
        internal static CardMatchCardState ToState(CardZoneCardState card) =>
            new CardMatchCardState(card.Card, card.FaceUp, card.Interaction);
        internal static CardZoneCardState[] Copy(IEnumerable<CardZoneCardState> cards) =>
            cards?.Select(card => card?.Copy() ?? throw new ArgumentException("Missing artwork projection.")).ToArray()
                ?? throw new ArgumentNullException(nameof(cards));
        private CardZoneCardState Decorate(CardMatchCardState state, IReadOnlyDictionary<string, CardZoneCardState> source)
        {
            source.TryGetValue(state.Id, out var style);
            var bindings = artBindings != null ? artBindings : style?.ArtBindings;
            if (!decorated.TryGetValue(state, out var entry))
            { entry = new Decoration(); decorated.Add(state, entry); }
            if (entry.Card == null || !ReferenceEquals(entry.Style, style) || entry.Bindings != bindings)
            {
                entry.Style = style; entry.Bindings = bindings;
                entry.Card = new CardZoneCardState(state.Card, style?.ArtId ?? state.Card.DefinitionId.Value,
                    state.Visible, style?.Tint ?? Color.white, state.Interaction, bindings);
            }
            else entry.Card.SetInteraction(state.Interaction);
            return entry.Card;
        }
        public IReadOnlyList<CardZoneCardState> GetCards(ZoneId zone, bool initial = false) =>
            Array.AsReadOnly(owner.ReadCards(zone, initial).Select(card => Decorate(card, Styles)).ToArray());
        public CardZoneViewData Project(ZoneId zone) => new CardZoneViewData(zone, GetCards(zone).Select(card => card.Project()));
        public CardZoneViewData Project(CardMatchUpdate update, ZoneId zone)
        {
            if (update == null || update.Owner != owner) throw new ArgumentException("Foreign match update.");
            var source = pending.TryGetValue(update, out var next) ? next : Styles;
            return new CardZoneViewData(zone, update.Snapshot(zone).Cards.Select(card => Decorate(card, source).Project()));
        }
#if UNITY_EDITOR
        internal void ConfigureInitial(IEnumerable<CardMatchZoneSnapshot> zones, string identity)
        {
            var copy = zones?.ToArray() ?? throw new ArgumentNullException(nameof(zones));
            var next = copy.SelectMany(zone => zone.Cards).ToDictionary(card => card.Id, card => card.Copy(clearHover: true));
            owner.ConfigureInitial(copy.Select(zone => new CardMatchZoneData(zone.Zone, zone.Cards.Select(ToState))), identity);
            styles = next; SaveInitialStyles(owner.InitialState);
        }
        private void SaveInitialStyles(CardTableState state) => initialZones = state.Zones.Select(zone =>
            new CardMatchZoneSnapshot(zone, owner.ReadCards(zone.Id, true).Select(card => Decorate(card, Styles)))).ToList();
#endif
    }

    /// <summary>Compatibility entry points for the existing frontend. New frontends use CardMatchHost's neutral API.</summary>
    public static class CardMatchPresentationExtensions
    {
        public static void ConfigureArtBindings(this CardMatchHost host, CardStateArtBindings bindings) =>
            CardMatchPresentationAdapter.For(host).ConfigureArtBindings(bindings);
        public static IReadOnlyList<CardZoneCardState> GetCards(this CardMatchHost host, ZoneId zone, bool initial = false) =>
            CardMatchPresentationAdapter.For(host).GetCards(zone, initial);
        public static CardZoneViewData Project(this CardMatchHost host, ZoneId zone) => CardMatchPresentationAdapter.For(host).Project(zone);
        public static CardZoneViewData Project(this CardMatchUpdate update, ZoneId zone) => CardMatchPresentationAdapter.For(update.Owner).Project(update, zone);
        public static void StartMatch(this CardMatchHost host, CardTableState state, IEnumerable<CardZoneCardState> cards) =>
            CardMatchPresentationAdapter.For(host).StartWithStyles(cards, next => host.StartMatch(state, next));
        public static void StartExternalMatch(this CardMatchHost host, ICardMatchCommands authority, CardTableState state, IEnumerable<CardZoneCardState> cards) =>
            CardMatchPresentationAdapter.For(host).StartWithStyles(cards, next => host.StartExternalMatch(authority, state, next));
        public static CardMatchUpdate PrepareExternalProjection(this CardMatchHost host, ICardMatchCommands authority,
            CardTableState state, IEnumerable<CardZoneCardState> cards)
        {
            var copy = CardMatchPresentationAdapter.Copy(cards);
            var update = host.PrepareExternalProjection(authority, state, copy.Select(CardMatchPresentationAdapter.ToState));
            CardMatchPresentationAdapter.For(host).Stage(update, copy); return update;
        }
#if UNITY_EDITOR
        public static void ConfigureInitial(this CardMatchHost host, IEnumerable<CardMatchZoneSnapshot> zones, string identity = null) =>
            CardMatchPresentationAdapter.For(host).ConfigureInitial(zones, identity);
        public static CardMatchUpdate PrepareDebugEdit(this CardMatchHost host, IEnumerable<CardMatchZoneSnapshot> zones, bool preview)
        {
            var copy = zones?.ToArray() ?? throw new ArgumentNullException(nameof(zones));
            var update = host.PrepareDebugEdit(copy.Select(zone => new CardMatchZoneData(zone.Zone,
                zone.Cards.Select(CardMatchPresentationAdapter.ToState))), preview);
            CardMatchPresentationAdapter.For(host).Stage(update, copy.SelectMany(zone => zone.Cards)); return update;
        }
#endif
    }
}
