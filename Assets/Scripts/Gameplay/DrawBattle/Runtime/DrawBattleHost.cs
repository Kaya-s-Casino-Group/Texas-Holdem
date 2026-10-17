using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CardInteraction;
using CardPresentation;
using PokerEngine;
using UnityEngine;

namespace Poker.DrawBattle
{
    /// <summary>A Unity driver for one Draw encounter. DrawState is authoritative; CardMatchHost is a visual mirror.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-150)]
    public sealed class DrawBattleHost : MonoBehaviour, ICardMatchCommands
    {
        [SerializeField] private CardMatchHost match;
        [SerializeField] private HandActionPresenter actions;
        [SerializeField] private DeckOverviewPresenter overview;
        [SerializeField] private DrawPilePresenter drawPile;
        [SerializeField] private bool startOnEnable = true;
        [SerializeField, Min(1)] private long targetScore = 200;
        [SerializeField, Range(1, 52)] private int handSize = 8;
        [SerializeField, Range(1, 1000)] private int hands = 4;
        [SerializeField, Range(0, 1000)] private int discards = 3;
        [SerializeField, Min(0)] private float scoreDisplaySeconds = .6f;
        private DrawEngine engine;
        private Coroutine playback;
        private bool busy;
        private Func<CardInstance, string> resolveArt;
        private Func<DrawCommand, DrawBattleTransition> submit;
        public DrawState State { get; private set; }
        public CardMatchHost Match => match;
        public HandActionPresenter Actions => actions;
        public DrawDefinition Definition => engine?.Definition;
        public bool IsPresenting => busy;
        public bool CanInteract => isActiveAndEnabled && State != null && !busy && match != null && match.IsRunning &&
            ReferenceEquals(match.Commands, this) && State.Phase == DrawPhase.Ready && State.World.ActiveDecision == null &&
            actions != null && actions.Hand != null && actions.Hand.IsRunning && !actions.Hand.GetComponent<HandZoneView>().IsTransitioning;
        public event Action<DrawState> StateChanged;
        public event Action<DrawEvent> EventPresented;
        public event Action<DrawError> CommandRejected;

        public void Configure(CardMatchHost owner, HandActionPresenter panel, DeckOverviewPresenter deckOverview = null, DrawPilePresenter pile = null)
        {
            if (owner == null || panel == null || panel.Hand == null || panel.Played == null || panel.PlayRoute == null || panel.Hand.Match != owner || panel.Played.Match != owner)
                throw new ArgumentException("Bind an existing match, hand actions and hand-to-played route.");
            if (owner.gameObject.scene != gameObject.scene) throw new ArgumentException("Bind a match in this scene.");
            match = owner; actions = panel; overview = deckOverview; drawPile = pile;
        }

        /// <summary>Start a standalone standard 52-card encounter. Run-owned encounters use StartEncounter with the current Run deck.</summary>
        public void StartBattle()
        {
            RequireBindings();
            if (submit != null) throw new InvalidOperationException("An external owner controls this encounter. Start the next encounter through that owner to preserve its deck.");
            var definitions = StandardDeck.Cards();
            var standard = definitions.ToDictionary(card => card.Id);
            var seed = match.InitialState;
            var styles = seed.Zones.SelectMany(zone => match.GetCards(zone.Id, true)).Where(card => standard.ContainsKey(card.Card.DefinitionId)).ToArray();
            var art = styles.GroupBy(card => card.Card.DefinitionId).ToDictionary(group => group.Key, group => group.First().ArtId);
            string encounterId = "draw/" + Guid.NewGuid().ToString("N");
            var cards = definitions.Select((card, i) =>
                new DrawCard(new CardInstance(new CardInstanceId(encounterId + "/card/" + i), card.Id))).ToArray();
            var catalog = new ContentCatalog("draw.scene.standard.1", standard.Values, Array.Empty<AbilityDefinitionId>(), Array.Empty<StatusDefinitionId>());
            var rules = new DrawDefinition("draw.scene.1", targetScore, handSize, hands, discards, Math.Min(5, handSize));
            var nextEngine = new DrawEngine(catalog, rules);
            var random = HmacDeckShuffler.CreateRandomState();
            var state = nextEngine.Create(encounterId, new ParticipantId("player"), cards, random);
            StartEncounter(nextEngine, state, card => art.TryGetValue(card.DefinitionId, out var found) ? found : StandardArt(standard[card.DefinitionId]));
        }

        /// <summary>Start/restore an encounter. A Run owner can supply submission through its RunEngine and return the Draw transition.</summary>
        public void StartEncounter(DrawEngine rules, DrawState state, Func<CardInstance, string> artwork,
            Func<DrawCommand, DrawBattleTransition> commandSubmit = null)
        {
            RequireBindings();
            if (rules == null || state == null || artwork == null) throw new ArgumentNullException();
            rules.Validate(state); actions.Hand.GetComponent<HandZoneView>().ValidateCardCount(rules.Definition.HandSize);
            var table = Table(state);
            var oldStyles = match.State.Zones.SelectMany(zone => match.GetCards(zone.Id)).ToDictionary(card => card.Id);
            var decorations = state.World.Cards.Select(card => new CardZoneCardState(card, artwork(card), true,
                oldStyles.TryGetValue(card.Id.Value, out var old) ? old.Tint : Color.white, default)).ToArray();
            CancelPlayback(); engine = rules; resolveArt = artwork; submit = commandSubmit; State = state;
            actions.Hand.StopSession(); actions.Played.StopSession();
            match.StartExternalMatch(this, table, decorations);
            var deck = DrawEngine.Zone(state.Id, CardZoneKinds.Deck); var discard = DrawEngine.Zone(state.Id, CardZoneKinds.Discard);
            actions.Hand.Bind(match, DrawEngine.Zone(state.Id, CardZoneKinds.Hand), deck, discard, actions.Hand.GetComponent<HandZoneView>().FacingCamera);
            actions.Played.Bind(match, DrawEngine.Zone(state.Id, CardZoneKinds.Played), deck, discard);
            actions.Hand.BeginSession(); actions.Played.BeginSession();
            if (overview != null) { overview.BindArtwork(actions.Hand.GetComponent<HandZoneView>().CardPrefab); overview.Configure(match, overview.View, deck, state.World.Cards.Select(card => card.Id)); }
            if (drawPile != null) { drawPile.Configure(actions.Hand, drawPile.View); if (overview != null) drawPile.BindOverview(overview); }
            StateChanged?.Invoke(State); actions.Refresh();
            if (State.Phase == DrawPhase.Scored && State.World.ActiveDecision == null) ContinueBattle(false);
        }

        private void RequireBindings()
        {
            if (match == null || actions == null || actions.Hand == null || actions.Played == null || actions.PlayRoute == null)
                throw new InvalidOperationException("Configure the existing hand, played route and action panel before starting Draw.");
        }
        private static CardTableState Table(DrawState state) => new CardTableState(new MatchId(state.Id), state.World.Cards, state.World.Zones, state.Revision);
        public static string StandardArt(CardDefinition card)
        {
            var rank = card.Rank == Rank.Ace ? "a" : card.Rank == Rank.King ? "k" : card.Rank == Rank.Queen ? "q" : card.Rank == Rank.Jack ? "j" : ((int)card.Rank).ToString("00", CultureInfo.InvariantCulture);
            return card.Suit.ToString().ToLowerInvariant() + "_" + rank;
        }
        private bool CanSubmit(CardSelectionRequest request, bool discard)
        {
            if (!CanInteract || request == null || request.MatchId != new MatchId(State.Id) ||
                request.ControllerId != State.PlayerId || request.ZoneId != DrawEngine.Zone(State.Id, CardZoneKinds.Hand) ||
                (discard && State.DiscardsRemaining == 0)) return false;
            var selected = request.Cards;
            return selected.Count > 0 && selected.Count <= Definition.MaxSelection && selected.All(id =>
                State.World.Zones.Single(zone => zone.Kind == CardZoneKinds.Hand).Cards.Contains(id) &&
                (discard ? State.World.Cards.Single(card => card.Id == id).CanDiscard : State.World.Cards.Single(card => card.Id == id).CanPlay));
        }
        public bool CanPlay(CardSelectionRequest request)
        {
            if (!CanSubmit(request, false)) return false;
            // Route readiness belongs to this mode's presentation binding, never to the command payload.
            var route = actions.PlayRoute; var destination = actions.Played;
            return route != null && route.isActiveAndEnabled && !route.IsTransferring &&
                route.Source == actions.Hand.GetComponent<HandZoneView>() && destination != null && destination.IsRunning &&
                destination.isActiveAndEnabled && route.Destination.isActiveAndEnabled &&
                route.Source.CardPrefab == route.Destination.CardPrefab &&
                request.Cards.Count <= PlayedZoneView.SlotCount - destination.Cards.Count;
        }
        public bool CanDiscard(CardSelectionRequest request) => CanSubmit(request, true);

        public int Play(CardSelectionRequest request, bool animate)
        {
            if (!CanPlay(request)) return 0;
            var hand = actions.Hand; var destination = actions.Played; var route = actions.PlayRoute;
            if (route.Source.Data == null || route.Destination.Data == null ||
                !route.Source.Data.Cards.Select(card => card.ViewId).SequenceEqual(hand.Cards.Select(card => card.Id)) ||
                !route.Destination.Data.Cards.Select(card => card.ViewId).SequenceEqual(destination.Cards.Select(card => card.Id)))
                throw new InvalidOperationException("Refresh stale zone presentations before transfer.");
            var ids = request.Cards.ToArray();
            // Prepare the visual handoff before the command; its callback only assigns this already validated mirror update.
            var frame = match.PrepareExternalPlay(this, hand.Zone.Id, destination.Zone.Id, ids, PlayedZoneView.SlotCount);
            if (!Execute(new DrawCommand(Guid.NewGuid().ToString("N"), State.Revision, DrawAction.Play, ids), out var events)) return 0;
            busy = true;
            try
            {
                // The command preserves captured input order; the visual handoff follows the current mirror order.
                route.PresentTransfer(frame.Project(hand.Zone.Id), frame.Project(destination.Zone.Id),
                    frame.Change.Transferred.Select(id => id.Value), animate, () => match.Commit(frame));
                match.Notify(frame, route.Source.Data, route.Destination.Data);
                StartPlayback(events, animate, true);
            }
            catch { SnapToState(); throw; }
            return ids.Length;
        }
        public int Discard(CardSelectionRequest request, bool animate)
        {
            if (!CanDiscard(request)) return 0;
            var hand = actions.Hand;
            var ids = request.Cards.ToArray(); var frame = match.PrepareExternalDiscard(this, hand.Zone.Id, hand.DiscardZone.Id, ids);
            if (!Execute(new DrawCommand(Guid.NewGuid().ToString("N"), State.Revision, DrawAction.Discard, ids), out var events)) return 0;
            busy = true;
            try
            {
                hand.GetComponent<HandZoneView>().PresentDiscardCommitted(frame.Project(hand.Zone.Id), ids.Select(id => id.Value), () => match.Commit(frame), animate);
                match.Notify(frame, hand.GetComponent<HandZoneView>().Data); StartPlayback(events, animate, false);
            }
            catch { SnapToState(); throw; }
            return ids.Length;
        }
        private bool Execute(DrawCommand command, out IReadOnlyList<DrawEvent> events)
        {
            var result = submit != null ? submit(command) : new DrawBattleTransition(engine.Execute(State, command, State.PlayerId));
            if (result == null) throw new InvalidOperationException("Draw submission returned no transition.");
            events = result.Events;
            if (!result.Accepted) { CommandRejected?.Invoke(result.Error); return false; }
            engine.Validate(result.State);
            if (result.State.Id != State.Id || result.State.PlayerId != State.PlayerId || result.State.Revision <= State.Revision)
                throw new InvalidOperationException("Draw submission returned a stale or foreign encounter.");
            State = result.State; return true;
        }
        private void StartPlayback(IReadOnlyList<DrawEvent> events, bool animate, bool scoring)
        {
            if (animate && Application.isPlaying && isActiveAndEnabled) playback = StartCoroutine(Present(events, scoring));
            else { PresentEvents(events); FinishCommand(false); }
            actions.Refresh();
        }
        private IEnumerator Present(IReadOnlyList<DrawEvent> events, bool scoring)
        {
            try
            {
                while (actions.PlayRoute.IsTransferring || actions.Hand.GetComponent<HandZoneView>().IsTransitioning) yield return null;
                PresentEvents(events);
                if (scoring && events.Any(item => item.Kind == DrawEventKind.Scored) && scoreDisplaySeconds > 0) yield return new WaitForSecondsRealtime(scoreDisplaySeconds);
                FinishCommand(true);
                while (actions.Hand.GetComponent<HandZoneView>().IsTransitioning) yield return null;
            }
            finally { playback = null; busy = false; actions.Refresh(); }
        }
        private void PresentEvents(IEnumerable<DrawEvent> events) { foreach (var item in events) EventPresented?.Invoke(item); }
        private void FinishCommand(bool animate)
        {
            Project(animate);
            if (State.Phase == DrawPhase.Scored && State.World.ActiveDecision == null) ContinueBattle(animate);
            StateChanged?.Invoke(State);
            if (!animate) busy = false;
        }
        private void ContinueBattle(bool animate)
        {
            if (!Execute(new DrawCommand(Guid.NewGuid().ToString("N"), State.Revision, DrawAction.Continue), out var events)) return;
            Project(animate); PresentEvents(events);
        }
        /// <summary>Ability UI can submit explicit choices; no implicit skip or timeout is performed.</summary>
        public bool Submit(DrawCommand command, bool animate = true)
        {
            if (State == null || busy || command == null || (command.Action != DrawAction.ActivateAbility && command.Action != DrawAction.SelectAbilityTargets && command.Action != DrawAction.Continue)) return false;
            if (!Execute(command, out var events)) return false;
            busy = true; StartPlayback(events, animate, events.Any(item => item.Kind == DrawEventKind.Scored)); return true;
        }
        private void Project(bool animate)
        {
            var oldHand = actions.Hand.Cards.ToDictionary(card => card.Card.Id);
            var zones = State.World.Zones.Select(zone => {
                if (zone.Kind != CardZoneKinds.Hand) return zone;
                var present = new HashSet<CardInstanceId>(zone.Cards);
                var order = actions.Hand.Cards.Select(card => card.Card.Id).Where(present.Contains).Concat(zone.Cards.Where(id => !oldHand.ContainsKey(id)));
                return new CardZone(zone.Id, zone.Kind, zone.ControllerId, order);
            }).ToArray();
            var styles = State.World.Cards.Select(card => new CardZoneCardState(card, resolveArt(card), true,
                oldHand.TryGetValue(card.Id, out var old) ? old.Tint : Color.white,
                old != null && zones.Single(zone => zone.Kind == CardZoneKinds.Hand).Cards.Contains(card.Id) ? old.Interaction : default)).ToArray();
            var update = match.PrepareExternalProjection(this, new CardTableState(new MatchId(State.Id), State.World.Cards, zones, State.Revision), styles);
            // Explicit presentation handles animate=false as well as card removals/refills; other observers read the same mirror.
            actions.Hand.GetComponent<HandZoneView>().PresentCommitted(update.Project(actions.Hand.Zone.Id), () => match.Commit(update), animate);
            actions.Played.GetComponent<PlayedZoneView>().Present(update.Project(actions.Played.Zone.Id), animate);
            match.Notify(update, actions.Hand.GetComponent<HandZoneView>().Data, actions.Played.GetComponent<PlayedZoneView>().Data); overview?.Refresh(); drawPile?.Refresh();
            if (zones.Single(zone => zone.Kind == CardZoneKinds.Hand).Cards.Any(id => !oldHand.ContainsKey(id)))
                actions.Hand.RequestSortAfterLanding(actions.Hand.SortOrder);
        }
        public void SnapToState()
        {
            CancelPlayback();
            if (State != null && match != null && ReferenceEquals(match.Commands, this)) { Project(false); StateChanged?.Invoke(State); actions.Refresh(); }
        }
        private void CancelPlayback()
        {
            if (playback != null) { StopCoroutine(playback); playback = null; }
            actions?.PlayRoute?.CompleteTransfers(); actions?.Hand?.GetComponent<HandZoneView>()?.CompleteTransitions();
            busy = false;
        }
        private void OnEnable() { if (Application.isPlaying && startOnEnable && match != null && actions != null) { if (State == null) StartBattle(); else StartEncounter(engine, State, resolveArt, submit); } }
        private void OnDisable() { if (State != null) SnapToState(); }
    }

    /// <summary>Submission result for a standalone engine or a Run-owned encounter.</summary>
    public sealed class DrawBattleTransition
    {
        public DrawState State { get; }
        public DrawError Error { get; }
        public bool Accepted => Error == DrawError.None;
        public IReadOnlyList<DrawEvent> Events { get; }
        public DrawBattleTransition(DrawState state, IEnumerable<DrawEvent> events, DrawError error = DrawError.None)
        { State = state ?? throw new ArgumentNullException(nameof(state)); Events = Array.AsReadOnly((events ?? Array.Empty<DrawEvent>()).ToArray()); Error = error; }
        public DrawBattleTransition(DrawTransitionResult result) : this(result.State, result.Events, result.Error) { }
    }
}
