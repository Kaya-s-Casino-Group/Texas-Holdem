using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    public enum CardDuelAction { Play = 1, Discard, EnemyAction, EndTurn, CollectPlayed, Start }
    public enum CardDuelError { None, StaleRevision, InvalidPhase, InvalidSelection, NoDiscards, InsufficientEnergy, Overflow }
    public enum CardDuelRefillTiming { NextPlayerTurn = 0, AfterAction = 1 }
    public enum CardDuelDealTrigger { OpeningTurn, AfterPlay, AfterDiscard, PlayerTurnStart }

    /// <summary>Read-only input to a pure draw policy. It decides how many cards, never moves or creates cards.</summary>
    public sealed class CardDuelDealContext
    {
        public CardDuelDealTrigger Trigger { get; }
        public CardTableState Table { get; }
        public int HandLimit { get; }
        public int HandCount => Table.Zones.Single(zone => zone.Kind == CardZoneKinds.Hand).Cards.Count;
        public int MissingCards => Math.Max(0, HandLimit - HandCount);
        internal CardDuelDealContext(CardDuelDealTrigger trigger, CardTableState table, int handLimit)
        { Trigger = trigger; Table = table; HandLimit = handLimit; }
    }
    public interface ICardDuelDealPolicy
    {
        int GetDrawCount(CardDuelDealContext context);
    }
    /// <summary>Opening/turn-start fill the hand. Play and discard can each refill immediately or wait for the next turn.</summary>
    public sealed class CardDuelDealPolicy : ICardDuelDealPolicy
    {
        public CardDuelRefillTiming AfterPlay { get; }
        public CardDuelRefillTiming AfterDiscard { get; }
        public CardDuelDealPolicy(CardDuelRefillTiming afterPlay = CardDuelRefillTiming.NextPlayerTurn,
            CardDuelRefillTiming afterDiscard = CardDuelRefillTiming.AfterAction)
        {
            if (!Enum.IsDefined(typeof(CardDuelRefillTiming), afterPlay) || !Enum.IsDefined(typeof(CardDuelRefillTiming), afterDiscard))
                throw new ArgumentException("Unknown refill timing.");
            AfterPlay = afterPlay; AfterDiscard = afterDiscard;
        }
        public int GetDrawCount(CardDuelDealContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return (context.Trigger == CardDuelDealTrigger.AfterPlay && AfterPlay != CardDuelRefillTiming.AfterAction) ||
                (context.Trigger == CardDuelDealTrigger.AfterDiscard && AfterDiscard != CardDuelRefillTiming.AfterAction) ? 0 : context.MissingCards;
        }
    }

    /// <summary>One authority owns card locations and the health duel. Views only project this state.</summary>
    public sealed class CardDuelState
    {
        public CardTableState Table { get; }
        public ScoredDuelState Duel { get; }
        public RandomState Random { get; }
        public int DiscardsRemaining { get; }
        public bool HasStarted { get; }
        public long Revision => Table.Revision;
        public CardZone Zone(string kind) => Table.Zones.Single(zone => zone.Kind == kind);
        internal CardDuelState(CardTableState table, ScoredDuelState duel, RandomState random, int discards, bool hasStarted)
        { Table = table; Duel = duel; Random = random; DiscardsRemaining = discards; HasStarted = hasStarted; }
    }
    public sealed class CardDuelTransition
    {
        public CardDuelState State { get; }
        public CardDuelError Error { get; }
        public bool Accepted => Error == CardDuelError.None;
        public ScoreResult Score { get; }
        public IReadOnlyList<CombatEvent> Events { get; }
        internal CardDuelTransition(CardDuelState state, CardDuelError error = CardDuelError.None,
            ScoreResult score = null, IEnumerable<CombatEvent> events = null)
        { State = state; Error = error; Score = score; Events = Guard.View(Guard.Copy(events ?? new CombatEvent[0], nameof(events))); }
    }

    /// <summary>
    /// Card/health coordinator: energy-funded player actions, optional limited discards, then one enemy attack.
    /// No Draw target-score victory, AbilityWorld, Run rewards or Unity dependency.
    /// </summary>
    public sealed class CardDuelEngine
    {
        private readonly Dictionary<CardDefinitionId, CardDefinition> faces = StandardDeck.Cards().ToDictionary(card => card.Id);
        private readonly HmacDeckShuffler shuffler = new HmacDeckShuffler();
        public ScoredDuelEngine Duel { get; }
        public ScoreProfile Scoring { get; }
        public int HandSize { get; }
        public int Discards { get; }
        public ICardDuelDealPolicy DealPolicy { get; }
        public int MaxSelection => Math.Min(5, HandSize);
        public CardDuelEngine(ScoredDuelDefinition definition, int handSize = 8, int discards = 3, ScoreProfile scoring = null,
            ICardDuelDealPolicy dealPolicy = null)
        {
            if (handSize < 1 || handSize > 52 || discards < 0 || discards > 1000) throw new ArgumentException("Invalid card duel budget.");
            Duel = new ScoredDuelEngine(definition); HandSize = handSize; Discards = discards; Scoring = scoring ?? ScoreProfile.Default;
            DealPolicy = dealPolicy ?? new CardDuelDealPolicy();
        }
        public CardDefinition Face(CardInstance card) => faces[card.DefinitionId];
        public static ZoneId ZoneId(string encounterId, string kind) => new ZoneId(encounterId + "/" + kind);

        /// <summary>Create an undealt battle. Start enters the first player turn and deals the hand for free.</summary>
        public CardDuelState Create(string id, RandomState random, IEnumerable<DeckEntry> deck = null, int? initialHealth = null)
        {
            var entries = Guard.Copy(deck ?? StandardDeck.Cards().Select((card, i) => new DeckEntry("standard/" + i, card.Id)), nameof(deck));
            if (entries.Length < HandSize || entries.Length > 512 || entries.Any(entry => entry == null || !faces.ContainsKey(entry.DefinitionId)) ||
                entries.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() != entries.Length || entries.Any(entry => entry.IsDisabled))
                throw new ArgumentException("Demo requires a playable deck of standard faces with unique origins, at least the hand size.");
            var duel = Duel.Create(id, new ParticipantId("player"), new ParticipantId(id + "/enemy"), initialHealth);
            var cards = entries.Select((entry, i) => new CardInstance(new CardInstanceId(id + "/card/" + i), entry.DefinitionId, entry.Id, entry.Effects)).ToArray();
            var shuffled = shuffler.Shuffle(cards.Select(card => card.Id).ToArray(), random);
            var zones = new[] { CardZoneKinds.Deck, CardZoneKinds.Hand, CardZoneKinds.Played, CardZoneKinds.Discard }
                .Select(kind => new CardZone(ZoneId(id, kind), kind, duel.PlayerId, kind == CardZoneKinds.Deck ? shuffled.Cards : new CardInstanceId[0])).ToArray();
            return new CardDuelState(new CardTableState(new MatchId(id), cards, zones), duel, shuffled.Random, Discards, false);
        }
        public CardDuelTransition Execute(CardDuelState state, long expectedRevision, CardDuelAction action, IEnumerable<CardInstanceId> selected = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (expectedRevision != state.Revision) return new CardDuelTransition(state, CardDuelError.StaleRevision);
            if (action == CardDuelAction.Start ? state.HasStarted : !state.HasStarted)
                return new CardDuelTransition(state, CardDuelError.InvalidPhase);
            if (state.Duel.IsFinished || !Enum.IsDefined(typeof(CardDuelAction), action) ||
                (action == CardDuelAction.EnemyAction ? state.Duel.Phase != ScoredDuelPhase.AwaitingEnemyAction : state.Duel.Phase != ScoredDuelPhase.AwaitingPlayerHand))
                return new CardDuelTransition(state, CardDuelError.InvalidPhase);
            // The scored hand occupies the presentation checkpoint; collect it before another player command.
            bool hasPlayed = state.Zone(CardZoneKinds.Played).Cards.Count > 0;
            if (action == CardDuelAction.CollectPlayed ? !hasPlayed : action != CardDuelAction.EnemyAction && hasPlayed)
                return new CardDuelTransition(state, CardDuelError.InvalidPhase);
            var ids = (selected ?? new CardInstanceId[0]).ToArray();
            if (action != CardDuelAction.Play && action != CardDuelAction.Discard ? ids.Length != 0 : ids.Length < 1 || ids.Length > MaxSelection ||
                ids.Distinct().Count() != ids.Length || ids.Any(id => !state.Zone(CardZoneKinds.Hand).Cards.Contains(id)))
                return new CardDuelTransition(state, CardDuelError.InvalidSelection);
            if (action == CardDuelAction.Discard && state.DiscardsRemaining == 0) return new CardDuelTransition(state, CardDuelError.NoDiscards);
            if ((action == CardDuelAction.Play && state.Duel.PlayerTurn.Energy < Duel.Definition.PlayEnergyCost) ||
                (action == CardDuelAction.Discard && state.Duel.PlayerTurn.Energy < Duel.Definition.DiscardEnergyCost))
                return new CardDuelTransition(state, CardDuelError.InsufficientEnergy);
            try
            {
                var zones = state.Table.Zones.ToArray(); var random = state.Random; var duel = state.Duel;
                int discards = state.DiscardsRemaining; ScoreResult score = null; IReadOnlyList<CombatEvent> events = new CombatEvent[0];
                if (action == CardDuelAction.Start)
                {
                    Deal(state.Table, zones, CardDuelDealTrigger.OpeningTurn, ref random);
                }
                else if (action == CardDuelAction.Play)
                {
                    // Authority determines both submitted membership and score, never accepting a UI-provided numeric score.
                    var ordered = state.Zone(CardZoneKinds.Hand).Cards.Where(ids.Contains).Select(state.Table.GetCard).ToArray();
                    var pattern = PokerHandClassifier.Classify(ordered.Select(card => new EffectiveCard(card.Id, Face(card).Rank, Face(card).Suit)));
                    score = ScoreEngine.EvaluatePattern(Duel.NextScoreId(duel), Scoring, pattern, 1,
                        ordered.Select(card => new ScoringCard(card.Id.Value, new CardFace(Face(card).Rank, Face(card).Suit), effects: card.Effects)), new ScoringJoker[0]);
                    var hit = Duel.ResolveScoredHand(duel, duel.Revision, score);
                    if (!hit.Accepted) return new CardDuelTransition(state, hit.Error == ScoredDuelError.Overflow ? CardDuelError.Overflow : CardDuelError.InvalidPhase);
                    duel = hit.State; events = hit.Events; Move(zones, CardZoneKinds.Hand, CardZoneKinds.Played, ordered.Select(card => card.Id));
                    if (!duel.IsFinished) Deal(state.Table, zones, CardDuelDealTrigger.AfterPlay, ref random);
                }
                else if (action == CardDuelAction.Discard)
                {
                    var spent = Duel.ResolveDiscard(duel, duel.Revision);
                    if (!spent.Accepted) return new CardDuelTransition(state, CardDuelError.InvalidPhase);
                    duel = spent.State;
                    Move(zones, CardZoneKinds.Hand, CardZoneKinds.Discard, state.Zone(CardZoneKinds.Hand).Cards.Where(ids.Contains));
                    discards--; Deal(state.Table, zones, CardDuelDealTrigger.AfterDiscard, ref random);
                }
                else if (action == CardDuelAction.EndTurn)
                {
                    var ended = Duel.EndPlayerTurn(duel, duel.Revision);
                    if (!ended.Accepted) return new CardDuelTransition(state, CardDuelError.InvalidPhase);
                    duel = ended.State;
                }
                else if (action == CardDuelAction.CollectPlayed)
                {
                    Move(zones, CardZoneKinds.Played, CardZoneKinds.Discard, state.Zone(CardZoneKinds.Played).Cards);
                }
                else
                {
                    var response = Duel.ResolveEnemyAction(duel, duel.Revision);
                    if (!response.Accepted) return new CardDuelTransition(state, CardDuelError.InvalidPhase);
                    duel = response.State; events = response.Events;
                    Move(zones, CardZoneKinds.Played, CardZoneKinds.Discard, state.Zone(CardZoneKinds.Played).Cards);
                    if (!duel.IsFinished) Deal(state.Table, zones, CardDuelDealTrigger.PlayerTurnStart, ref random);
                }
                var table = new CardTableState(state.Table.MatchId, state.Table.Cards, zones, checked(state.Revision + 1));
                return new CardDuelTransition(new CardDuelState(table, duel, random, discards, true), score: score, events: events);
            }
            catch (OverflowException) { return new CardDuelTransition(state, CardDuelError.Overflow); }
        }
        private static void Move(CardZone[] zones, string from, string to, IEnumerable<CardInstanceId> ids)
        {
            int a = Array.FindIndex(zones, zone => zone.Kind == from), b = Array.FindIndex(zones, zone => zone.Kind == to);
            var transfer = zones[a].TransferRangeTo(zones[b], ids, zones[b].Cards.Count); zones[a] = transfer.Source; zones[b] = transfer.Target;
        }
        private void Deal(CardTableState before, CardZone[] zones, CardDuelDealTrigger trigger, ref RandomState random)
        {
            var context = new CardDuelDealContext(trigger, new CardTableState(before.MatchId, before.Cards, zones, before.Revision), HandSize);
            int remaining = DealPolicy.GetDrawCount(context);
            if (remaining < 0 || remaining > context.MissingCards) throw new ArgumentException("Draw policy must respect the hand limit.");
            int d = Array.FindIndex(zones, zone => zone.Kind == CardZoneKinds.Deck), h = Array.FindIndex(zones, zone => zone.Kind == CardZoneKinds.Hand),
                x = Array.FindIndex(zones, zone => zone.Kind == CardZoneKinds.Discard);
            while (remaining > 0)
            {
                if (zones[d].Cards.Count == 0)
                {
                    if (zones[x].Cards.Count == 0) break;
                    var shuffled = shuffler.Shuffle(zones[x].Cards, random); random = shuffled.Random;
                    zones[d] = new CardZone(zones[d].Id, zones[d].Kind, zones[d].ControllerId, shuffled.Cards);
                    zones[x] = new CardZone(zones[x].Id, zones[x].Kind, zones[x].ControllerId, new CardInstanceId[0]);
                }
                int count = Math.Min(remaining, zones[d].Cards.Count);
                var transfer = zones[d].DrawTo(zones[h], count, zones[h].Cards.Count); zones[d] = transfer.Source; zones[h] = transfer.Target;
                remaining -= count;
            }
        }
    }
}
