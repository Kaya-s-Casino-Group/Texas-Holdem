using System;
using System.Collections.Generic;
using System.Linq;
using CardPresentation; // Shared interaction value type; lives in CardMatch.Runtime, not the view assembly.
using PokerEngine;
using UnityEngine;

namespace CardInteraction
{
    public sealed class CardMatchUpdate
    {
        internal CardMatchHost Host;
        internal CardTableSession Session;
        internal Dictionary<string, CardMatchCardState> Presentation;
        internal bool Preview;
        internal ICardMatchCommands Authority;
        public CardTableChange Change { get; internal set; }
        public CardMatchHost Owner => Host;
        public bool IsPreview => Preview;
        public CardMatchZoneData Snapshot(ZoneId zone) => new CardMatchZoneData(Change.After.GetZone(zone),
            Change.After.GetZone(zone).Cards.Select(id => Presentation[id.Value]));
    }

    /// <summary>Shared observer table and local interaction for any frontend. No artwork, view or animation dependencies.
    /// With Commands attached this mirrors gameplay authority; otherwise it runs a standalone table.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed partial class CardMatchHost : MonoBehaviour
    {
        [SerializeField] private string matchId;
        [SerializeField] private bool runtimeManaged;
        [SerializeField] private long initialRevision;
        [SerializeField] private List<CardMatchZoneData> initialZones = new List<CardMatchZoneData>();
        private CardTableSession session;
        private Dictionary<string, CardMatchCardState> presentation;
        private CardTableState runtimeSeed;
        private Dictionary<string, CardMatchCardState> runtimeSeedPresentation;
        public bool IsRunning { get; private set; }
        public bool IsRuntimeManaged => runtimeManaged;
        public ICardMatchCommands Commands { get; private set; }
        public CardTableState InitialState => runtimeSeed ?? (runtimeManaged ? new CardTableState(new MatchId("presentation/unbound"),
            Array.Empty<CardInstance>(), Array.Empty<CardZone>()) : BuildInitial());
        public CardTableState State => IsRunning ? session.State : InitialState;
        public event Action Restarted;
        public event Action Stopped;
        public event Action<CardTableChange, IReadOnlyList<object>> Changed;
        public event Action<ZoneId, object> InteractionChanged;
        public event Action<CardMatchUpdate> Committed;

        private CardTableState BuildInitial() => new CardTableState(new MatchId(Guard.Text(matchId, nameof(matchId))),
            initialZones.SelectMany(zone => zone.Cards).Select(card => card.Card), initialZones.Select(zone => zone.Zone), initialRevision);
        public void StartMatch() { if (!IsRunning) RestartMatch(); }
        /// <summary>Start/restore a gameplay-owned table with this observer's presentation, without editor APIs.</summary>
        public void StartMatch(CardTableState state, IEnumerable<CardMatchCardState> observerCards)
        {
            RequireLocalCommands();
            var next = BuildObserverPresentation(state, observerCards);
            runtimeSeed = state; runtimeSeedPresentation = next;
            RestartMatch();
        }
        private Dictionary<string, CardMatchCardState> BuildObserverPresentation(CardTableState state, IEnumerable<CardMatchCardState> observerCards)
        {
            if (state == null || observerCards == null) throw new ArgumentNullException();
            state.Validate();
            var cards = observerCards.ToArray();
            if (cards.Any(card => card == null) || cards.Select(card => card.Id).Distinct().Count() != cards.Length ||
                !new HashSet<string>(state.Cards.Select(card => card.Id.Value)).SetEquals(cards.Select(card => card.Id)))
                throw new ArgumentException("Observer cards must match the table's identities exactly.");
            var styles = cards.ToDictionary(card => card.Id, card => card);
            return state.Cards.ToDictionary(card => card.Id.Value, card => {
                var style = styles[card.Id.Value];
                return new CardMatchCardState(card, style.Visible, style.Interaction.WithHovered(false));
            }, StringComparer.Ordinal);
        }
        public void RestartMatch()
        {
            RequireLocalCommands();
            ResetSession();
        }
        private void ResetSession()
        {
            var seed = InitialState; var next = InitialPresentation();
            session = new CardTableSession(seed); presentation = next; IsRunning = true; Restarted?.Invoke();
        }
        public void StopMatch()
        {
            bool wasRunning = IsRunning;
            IsRunning = false; session = null; presentation = null; Commands = null;
            if (wasRunning) Stopped?.Invoke();
        }
        public void StartExternalMatch(ICardMatchCommands authority, CardTableState state, IEnumerable<CardMatchCardState> observerCards)
        {
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            if (Commands != null && !ReferenceEquals(Commands, authority)) throw new InvalidOperationException("Another gameplay authority owns this mirror.");
            var next = BuildObserverPresentation(state, observerCards);
            runtimeSeed = state; runtimeSeedPresentation = next; Commands = authority; ResetSession();
        }
        private void RequireLocalCommands()
        {
            if (runtimeManaged || Commands != null) throw new InvalidOperationException("Submit commands to the gameplay authority; this table is a presentation mirror.");
        }
        private CardTableSession LocalSession { get { RequireLocalCommands(); return RunningSession; } }
        public CardMatchUpdate PrepareExternalPlay(ICardMatchCommands authority, ZoneId hand, ZoneId played, IEnumerable<CardInstanceId> selected, int capacity) =>
            External(authority, () => Wrap(RunningSession.PreparePlay(hand, played, selected, capacity), session, false, clearTransferredInput: true));
        public CardMatchUpdate PrepareExternalDiscard(ICardMatchCommands authority, ZoneId hand, ZoneId discard, IEnumerable<CardInstanceId> selected) =>
            External(authority, () => Wrap(RunningSession.PrepareDiscard(hand, discard, selected), session, false, clearTransferredInput: true));
        public CardMatchUpdate PrepareExternalProjection(ICardMatchCommands authority, CardTableState state, IEnumerable<CardMatchCardState> styles) =>
            External(authority, () => Wrap(RunningSession.PrepareZones(state.Zones, state.Cards), session, false, styles));
        private CardMatchUpdate External(ICardMatchCommands authority, Func<CardMatchUpdate> prepare)
        {
            if (authority == null || !ReferenceEquals(Commands, authority)) throw new InvalidOperationException("Foreign gameplay authority.");
            var update = prepare(); update.Authority = authority; return update;
        }
        public bool ContainsZone(ZoneId id) => (runtimeSeed != null || !string.IsNullOrEmpty(matchId)) && State.Zones.Any(zone => zone.Id == id);
        public CardZone GetZone(ZoneId id, bool initial = false) => (initial ? InitialState : State).GetZone(id);
        public CardMatchZoneData Snapshot(ZoneId zone) => new CardMatchZoneData(GetZone(zone), ReadCards(zone));
        public CardSelectionRequest CaptureSelection(ZoneId zone)
        {
            var state = RunningSession.State;
            var source = state.GetZone(zone);
            return new CardSelectionRequest(state.MatchId, source.Id, source.ControllerId,
                source.Cards.Where(id => presentation[id.Value].Interaction.IsSelected));
        }
        public IReadOnlyList<CardMatchCardState> ReadCards(ZoneId id, bool initial = false)
        {
            if (initial || !IsRunning)
            {
                var styles = InitialPresentation();
                return Array.AsReadOnly(InitialState.GetZone(id).Cards.Select(card => styles[card.Value]).ToArray());
            }
            return Array.AsReadOnly(session.State.GetZone(id).Cards.Select(card => presentation[card.Value]).ToArray());
        }
        public void SetInteraction(CardInstanceId id, CardInteractionState interaction, bool notify = true)
        {
            if (!IsRunning) throw new InvalidOperationException("Start the match before changing local input.");
            session.State.GetCard(id);
            var card = presentation[id.Value];
            if (card.Interaction.IsHovered == interaction.IsHovered && card.Interaction.IsSelected == interaction.IsSelected) return;
            card.SetInteraction(interaction);
            if (notify) foreach (var zone in State.Zones.Where(zone => zone.Cards.Contains(id))) NotifyInteraction(zone.Id);
        }
        /// <summary>A frontend may batch local input and notify after presenting its own view.</summary>
        public void NotifyInteraction(ZoneId zone, object presentedConsumer = null)
        {
            if (!IsRunning) throw new InvalidOperationException("Start the match before notifying input.");
            State.GetZone(zone); InteractionChanged?.Invoke(zone, presentedConsumer);
        }
        private Dictionary<string, CardMatchCardState> InitialPresentation() => runtimeSeedPresentation != null ?
            runtimeSeedPresentation.ToDictionary(pair => pair.Key, pair => pair.Value.Copy(clearHover: true), StringComparer.Ordinal) :
            initialZones.SelectMany(zone => zone.Cards).ToDictionary(card => card.Id, card => card.Copy(clearHover: true), StringComparer.Ordinal);

        private CardTableSession RunningSession => IsRunning ? session : throw new InvalidOperationException("Start the match before preparing commands.");

        public CardMatchUpdate PrepareDraw(ZoneId deck, ZoneId hand, int count, int capacity) =>
            Wrap(LocalSession.PrepareDraw(deck, hand, count, capacity), session, false);
        public CardMatchUpdate PreparePlay(ZoneId hand, ZoneId played, IEnumerable<CardInstanceId> selected, int capacity) =>
            Wrap(LocalSession.PreparePlay(hand, played, selected, capacity), session, false, clearTransferredInput: true);
        public CardMatchUpdate PrepareDiscard(ZoneId hand, ZoneId discard, IEnumerable<CardInstanceId> selected) =>
            Wrap(LocalSession.PrepareDiscard(hand, discard, selected), session, false, clearTransferredInput: true);
        public CardMatchUpdate PrepareReorder(ZoneId zone, IEnumerable<CardInstanceId> order) =>
            Wrap(LocalSession.PrepareReorder(zone, order), session, false);
        private CardMatchUpdate Wrap(CardTableChange change, CardTableSession owner, bool preview,
            IEnumerable<CardMatchCardState> updates = null, bool clearTransferredInput = false)
        {
            var styles = preview ? InitialPresentation() : presentation.ToDictionary(pair => pair.Key, pair => pair.Value.Copy(), StringComparer.Ordinal);
            if (updates != null) foreach (var card in updates) styles[card.Id] = card.Copy();
            var remaining = new HashSet<string>(change.After.Cards.Select(card => card.Id.Value));
            styles = styles.Where(pair => remaining.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            foreach (var card in change.After.Cards)
            {
                var style = styles[card.Id.Value];
                styles[card.Id.Value] = new CardMatchCardState(card, style.Visible, style.Interaction);
            }
            if (clearTransferredInput) foreach (var id in change.Transferred) styles[id.Value].SetInteraction(default);
            return new CardMatchUpdate { Host = this, Session = owner, Change = change, Presentation = styles, Preview = preview };
        }
        public void Commit(CardMatchUpdate update)
        {
            if (update == null || update.Host != this) throw new InvalidOperationException("Foreign match update.");
            if (Commands != null && !ReferenceEquals(update.Authority, Commands)) throw new InvalidOperationException("Only the gameplay authority can update this mirror.");
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
            Committed?.Invoke(update);
        }
        /// <summary>Already-applied snapshots suppress only their consuming views, not every observer of a zone.</summary>
        public void Notify(CardMatchUpdate update, params object[] presented) =>
            Changed?.Invoke(update.Change, Array.AsReadOnly(presented));
        public CardMatchUpdate PreparePresentationReorder(ZoneId zone, IEnumerable<CardInstanceId> order)
        {
            if (Commands != null && !Commands.CanInteract) throw new InvalidOperationException("Wait for gameplay presentation to settle before sorting.");
            var update = Wrap(RunningSession.PrepareReorder(zone, order), session, false); update.Authority = Commands; return update;
        }
        private void OnEnable() { if (Application.isPlaying && !runtimeManaged && (runtimeSeed != null || !string.IsNullOrEmpty(matchId))) StartMatch(); }
        private void OnDisable() => StopMatch();
    }
}
