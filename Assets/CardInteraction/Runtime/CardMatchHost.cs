using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation;
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

    public sealed class CardMatchUpdate
    {
        internal CardMatchHost Host;
        internal CardTableSession Session;
        internal Dictionary<string, CardZoneCardState> Presentation;
        internal bool Preview;
        public CardTableChange Change { get; internal set; }
        public CardZoneViewData Project(ZoneId zone) => new CardZoneViewData(zone,
            Change.After.GetZone(zone).Cards.Select(id => Presentation[id.Value].Project()));
    }

    /// <summary>One scene match owns all cards/zones. Local observer art/visibility/interaction decorate its card identities.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed partial class CardMatchHost : MonoBehaviour
    {
        [SerializeField] private string matchId;
        [SerializeField] private long initialRevision;
        [SerializeField] private List<CardMatchZoneSnapshot> initialZones = new List<CardMatchZoneSnapshot>();
        private CardTableSession session;
        private Dictionary<string, CardZoneCardState> presentation;
        private CardTableState runtimeSeed;
        private Dictionary<string, CardZoneCardState> runtimeSeedPresentation;
        public bool IsRunning { get; private set; }
        public CardTableState InitialState => runtimeSeed ?? BuildInitial();
        public CardTableState State => IsRunning ? session.State : InitialState;
        public event Action Restarted;
        public event Action<CardTableChange, CardZonePlayHost[]> Changed;

        private CardTableState BuildInitial() => new CardTableState(new MatchId(Guard.Text(matchId, nameof(matchId))),
            initialZones.SelectMany(zone => zone.Cards).Select(card => card.Card), initialZones.Select(zone => zone.Zone), initialRevision);
        public void StartMatch() { if (!IsRunning) RestartMatch(); }
        /// <summary>Start/restore a gameplay-owned table with this observer's presentation, without editor APIs.</summary>
        public void StartMatch(CardTableState state, IEnumerable<CardZoneCardState> observerCards)
        {
            if (state == null || observerCards == null) throw new ArgumentNullException();
            state.Validate();
            var cards = observerCards.ToArray();
            if (cards.Any(card => card == null) || cards.Select(card => card.Id).Distinct().Count() != cards.Length ||
                !new HashSet<string>(state.Cards.Select(card => card.Id.Value)).SetEquals(cards.Select(card => card.Id)))
                throw new ArgumentException("Observer cards must match the table's identities exactly.");
            var styles = cards.ToDictionary(card => card.Id, card => card);
            var next = state.Cards.ToDictionary(card => card.Id.Value, card => {
                var style = styles[card.Id.Value];
                return new CardZoneCardState(card, style.ArtId, style.FaceUp, style.Tint, style.Interaction.WithHovered(false));
            }, StringComparer.Ordinal);
            runtimeSeed = state; runtimeSeedPresentation = next;
            RestartMatch();
        }
        public void RestartMatch()
        {
            var seed = InitialState; var next = InitialPresentation();
            session = new CardTableSession(seed); presentation = next; IsRunning = true; Restarted?.Invoke();
        }
        public void StopMatch() { IsRunning = false; session = null; presentation = null; }
        public bool ContainsZone(ZoneId id) => (runtimeSeed != null || !string.IsNullOrEmpty(matchId)) && State.Zones.Any(zone => zone.Id == id);
        public CardZone GetZone(ZoneId id, bool initial = false) => (initial ? InitialState : State).GetZone(id);
        public IReadOnlyList<CardZoneCardState> GetCards(ZoneId id, bool initial = false)
        {
            if (initial || !IsRunning)
            {
                var styles = InitialPresentation();
                return Array.AsReadOnly(InitialState.GetZone(id).Cards.Select(card => styles[card.Value]).ToArray());
            }
            return Array.AsReadOnly(session.State.GetZone(id).Cards.Select(card => presentation[card.Value]).ToArray());
        }
        public void SetInteraction(CardInstanceId id, CardInteractionState interaction)
        {
            if (!IsRunning) throw new InvalidOperationException("Start the match before changing local input.");
            session.State.GetCard(id); presentation[id.Value].SetInteraction(interaction);
        }
        private Dictionary<string, CardZoneCardState> InitialPresentation() => runtimeSeedPresentation != null ?
            runtimeSeedPresentation.ToDictionary(pair => pair.Key, pair => pair.Value.Copy(clearHover: true), StringComparer.Ordinal) :
            initialZones.SelectMany(zone => zone.Cards).ToDictionary(card => card.Id, card => card.Copy(clearHover: true), StringComparer.Ordinal);

        private CardTableSession RunningSession => IsRunning ? session : throw new InvalidOperationException("Start the match before preparing commands.");

        public CardMatchUpdate PrepareDraw(ZoneId deck, ZoneId hand, int count, int capacity) =>
            Wrap(RunningSession.PrepareDraw(deck, hand, count, capacity), session, false);
        public CardMatchUpdate PreparePlay(ZoneId hand, ZoneId played, IEnumerable<CardInstanceId> selected, int capacity) =>
            Wrap(RunningSession.PreparePlay(hand, played, selected, capacity), session, false, clearTransferredInput: true);
        public CardMatchUpdate PrepareDiscard(ZoneId hand, ZoneId discard, IEnumerable<CardInstanceId> selected) =>
            Wrap(RunningSession.PrepareDiscard(hand, discard, selected), session, false, clearTransferredInput: true);
        public CardMatchUpdate PrepareReorder(ZoneId zone, IEnumerable<CardInstanceId> order) =>
            Wrap(RunningSession.PrepareReorder(zone, order), session, false);
        private CardMatchUpdate Wrap(CardTableChange change, CardTableSession owner, bool preview,
            IEnumerable<CardZoneCardState> updates = null, bool clearTransferredInput = false)
        {
            var styles = preview ? InitialPresentation() : presentation.ToDictionary(pair => pair.Key, pair => pair.Value.Copy(), StringComparer.Ordinal);
            if (updates != null) foreach (var card in updates) styles[card.Id] = card.Copy();
            var remaining = new HashSet<string>(change.After.Cards.Select(card => card.Id.Value));
            styles = styles.Where(pair => remaining.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var card in change.After.Cards)
            {
                var style = styles[card.Id.Value];
                styles[card.Id.Value] = new CardZoneCardState(card, style.ArtId, style.FaceUp, style.Tint, style.Interaction);
            }
            if (clearTransferredInput) foreach (var id in change.Transferred) styles[id.Value].SetInteraction(default);
            return new CardMatchUpdate { Host = this, Session = owner, Change = change, Presentation = styles, Preview = preview };
        }
        public void Commit(CardMatchUpdate update)
        {
            if (update == null || update.Host != this) throw new InvalidOperationException("Foreign match update.");
            if (update.Preview)
            {
#if UNITY_EDITOR
                if (Application.isPlaying || InitialState.Revision != update.Change.Before.Revision)
                    throw new InvalidOperationException("Stale authoring match update.");
                update.Session.Commit(update.Change); SaveInitial(update.Change.After, update.Presentation);
#else
                throw new InvalidOperationException("Authoring changes are unavailable in Player.");
#endif
            }
            else
            {
                if (!IsRunning || session != update.Session) throw new InvalidOperationException("The match session changed.");
                session.Commit(update.Change); presentation = update.Presentation;
            }
        }
        public void Notify(CardMatchUpdate update, params CardZonePlayHost[] presented) => Changed?.Invoke(update.Change, presented);
        private void OnEnable() { if (Application.isPlaying && (runtimeSeed != null || !string.IsNullOrEmpty(matchId))) StartMatch(); }
        private void OnDisable() => StopMatch();
    }
}
