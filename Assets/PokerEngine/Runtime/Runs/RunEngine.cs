using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    public enum RunAction { StartEncounter = 1, PlayerEncounter, SystemEncounter, ChooseReward, SkipReward, Buy, Sell, UpgradeJoker, ReorderJokers, Reroll }
    public sealed class RunCommand<TCommand> where TCommand : class
    {
        public string Id { get; }
        public long ExpectedRevision { get; }
        public RunAction Action { get; }
        public string ItemId { get; }
        public string TargetId { get; }
        public IReadOnlyList<string> Order { get; }
        public TCommand EncounterCommand { get; }
        public RunCommand(string id, long revision, RunAction action, string itemId = null, string targetId = null,
            IEnumerable<string> order = null, TCommand encounterCommand = null)
        {
            Id = Guard.Text(id, nameof(id)); ExpectedRevision = revision; Action = action; ItemId = itemId; TargetId = targetId;
            Order = Guard.View(Guard.Copy(order ?? new string[0], nameof(order))); EncounterCommand = encounterCommand;
        }
    }
    public enum RunError { None = 0, StaleRevision, WrongActor, WrongPhase, InvalidCommand, InsufficientGold, Capacity, UnknownOffer,
        InvalidTarget, EncounterRejected, AmountOverflow, InvalidState }
    public sealed class RunTransitionResult<TState, TEvent> where TState : class
    {
        private readonly Func<TState, IEnumerable<TEvent>, ParticipantId, IReadOnlyList<TEvent>> filter;
        public bool Accepted => Error == RunError.None;
        public RunError Error { get; }
        public string EncounterError { get; }
        public string CommandId { get; }
        public RunState<TState> State { get; }
        public IReadOnlyList<TEvent> EncounterEvents { get; }
        public ScoreResult ScoredRound { get; }
        internal RunTransitionResult(string id, RunError error, RunState<TState> state,
            Func<TState, IEnumerable<TEvent>, ParticipantId, IReadOnlyList<TEvent>> filter,
            IEnumerable<TEvent> events = null, ScoreResult score = null, string encounterError = null)
        {
            CommandId = id; Error = error; State = state; EncounterError = encounterError; this.filter = filter;
            EncounterEvents = Guard.View(Guard.Copy(events ?? new TEvent[0], nameof(events))); ScoredRound = score;
        }
        public IReadOnlyList<TEvent> EventsFor(ParticipantId viewer)
        {
            if (State.Encounter == null) throw new ArgumentException("No encounter has started.");
            return filter(State.Encounter, EncounterEvents, viewer);
        }
    }

    /// <summary>Authoritative build, shop, rewards and campaign progression, independent of encounter rules.</summary>
    public sealed class RunEngine<TState, TCommand, TEvent> where TState : class where TCommand : class
    {
        private readonly RunContent content;
        private readonly IRunEncounter<TState, TCommand, TEvent> encounter;
        public RunEngine(RunContent content, IRunEncounter<TState, TCommand, TEvent> encounter)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
            this.encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            if (encounter.MinimumDeckSize < 1 || encounter.MinimumDeckSize > 512 || encounter.SchemaVersion < 1)
                throw new ArgumentException("Invalid encounter contract.");
            Guard.Text(encounter.Kind, nameof(encounter.Kind)); Guard.Text(encounter.RulesVersion, nameof(encounter.RulesVersion));
        }
        public RunState<TState> Create(string id, ParticipantId player, RandomState random = null, IEnumerable<RunCard> deck = null)
        {
            var cards = deck ?? content.StartingDeck.Select((definition, i) => new RunCard(id + "/card/" + i, definition));
            var jokers = content.Definition.StartingJokers.Select((definition, i) => new RunJoker(id + "/joker/start/" + i, definition, charges: content.Joker(definition).Charges));
            var state = new RunState<TState>(id, 0, content.CardsAndAbilities.Version, content.Definition.Version, content.Definition.Scoring.Version, player,
                RunPhase.Preparing, 0, content.Definition.StartingGold, 0, 0, cards, jokers,
                Enum.GetValues(typeof(HandCategory)).Cast<HandCategory>().Select(c => new HandLevel(c, 1)), new RunOffer[0], new ScoreResult[0],
                random ?? HmacDeckShuffler.CreateRandomState(), encounter.Kind, encounter.RulesVersion, encounter.SchemaVersion, content.StartingDeckVersion);
            RunValidator.Validate(state, content, encounter); return state;
        }
        public RunTransitionResult<TState, TEvent> Execute(RunState<TState> state, RunCommand<TCommand> command, ParticipantId authenticatedActor) => ExecuteCore(state, command, authenticatedActor, false);
        /// <summary>Authority only: starts encounters and submits mode-defined system actions.</summary>
        public RunTransitionResult<TState, TEvent> ExecuteSystem(RunState<TState> state, RunCommand<TCommand> command) => ExecuteCore(state, command, default(ParticipantId), true);
        private RunTransitionResult<TState, TEvent> Result(string id, RunError error, RunState<TState> state,
            IEnumerable<TEvent> events = null, ScoreResult score = null, string encounterError = null) =>
            new RunTransitionResult<TState, TEvent>(id, error, state, encounter.FilterEvents, events, score, encounterError);
        private RunTransitionResult<TState, TEvent> ExecuteCore(RunState<TState> state, RunCommand<TCommand> command, ParticipantId actor, bool system)
        {
            if (state == null || command == null) throw new ArgumentNullException();
            try
            {
                if (state.Revision != command.ExpectedRevision) throw new Denied(RunError.StaleRevision);
                RunValidator.Validate(state, content, encounter);
                if (state.Phase == RunPhase.Completed || state.Phase == RunPhase.Failed) throw new Denied(RunError.WrongPhase);
                bool systemAction = command.Action == RunAction.StartEncounter || command.Action == RunAction.SystemEncounter;
                if (system != systemAction) throw new Denied(RunError.WrongActor);
                if (!system && command.Action != RunAction.PlayerEncounter && actor != state.PlayerId) throw new Denied(RunError.WrongActor);
                var work = new Work(this, state, checked(state.Revision + 1)); work.Apply(command, actor);
                var next = work.Build(); RunValidator.Validate(next, content, encounter);
                return Result(command.Id, RunError.None, next, work.Events, work.Score);
            }
            catch (Denied e) { return Result(command.Id, e.Error, state, encounterError: e.EncounterError); }
            catch (OverflowException) { return Result(command.Id, RunError.AmountOverflow, state); }
            catch (StateValidationException) { return Result(command.Id, RunError.InvalidState, state); }
            catch (ArgumentException) { return Result(command.Id, RunError.InvalidCommand, state); }
        }
        private sealed class Denied : Exception
        {
            public RunError Error { get; } public string EncounterError { get; }
            public Denied(RunError error, string encounterError = null) { Error = error; EncounterError = encounterError; }
        }
        private sealed class Work
        {
            private readonly RunEngine<TState, TCommand, TEvent> engine; private readonly RunState<TState> source; private readonly long revision;
            private RunPhase phase; private int stage, rounds; private long gold, score;
            private List<RunCard> deck; private List<RunJoker> jokers; private List<HandLevel> levels; private List<RunOffer> offers;
            private List<ScoreResult> scores; private RandomState random; private TState encounterState;
            public readonly List<TEvent> Events = new List<TEvent>(); public ScoreResult Score;
            private RunContent Content => engine.content;
            private RunEncounterContext Context => new RunEncounterContext(source.Id, source.PlayerId, phase, stage, Content, deck, jokers, levels, random);
            public Work(RunEngine<TState, TCommand, TEvent> engine, RunState<TState> state, long revision)
            {
                this.engine = engine; source = state; this.revision = revision; phase = state.Phase; stage = state.StageIndex;
                rounds = state.RoundsScored; gold = state.Gold; score = state.StageScore; deck = state.Deck.ToList(); jokers = state.Jokers.ToList();
                levels = state.HandLevels.ToList(); offers = state.Offers.ToList(); scores = state.Scores.ToList(); random = state.Random; encounterState = state.Encounter;
            }
            private string Id(string kind) => source.Id + "/" + kind + "/" + revision;
            private string EntityId(string kind)
            {
                string root = Id(kind), id = root; int suffix = 0;
                while (deck.Any(c => c.Id == id) || jokers.Any(j => j.Id == id)) id = root + "/" + (++suffix);
                return id;
            }
            private void Spend(long amount) { if (amount > gold) throw new Denied(RunError.InsufficientGold); gold = checked(gold - amount); }
            private void Require(RunPhase required) { if (phase != required) throw new Denied(RunError.WrongPhase); }
            private void BuildPhase() { if (phase != RunPhase.Preparing && phase != RunPhase.Shop) throw new Denied(RunError.WrongPhase); }
            public void Apply(RunCommand<TCommand> command, ParticipantId actor)
            {
                switch (command.Action)
                {
                    case RunAction.StartEncounter: Start(); break;
                    case RunAction.PlayerEncounter:
                    case RunAction.SystemEncounter: ExecuteEncounter(command, actor); break;
                    case RunAction.ChooseReward: Require(RunPhase.Reward); Claim(command, true); break;
                    case RunAction.SkipReward: Require(RunPhase.Reward); EndReward(); break;
                    case RunAction.Buy: Require(RunPhase.Shop); Claim(command, false); break;
                    case RunAction.Sell:
                        BuildPhase(); var sold = jokers.FirstOrDefault(entry => entry.Id == command.ItemId) ?? throw new Denied(RunError.InvalidTarget);
                        gold = checked(gold + Content.Joker(sold.DefinitionId).SellPrice); jokers.Remove(sold); break;
                    case RunAction.UpgradeJoker:
                        BuildPhase(); int index = jokers.FindIndex(entry => entry.Id == command.ItemId); if (index < 0) throw new Denied(RunError.InvalidTarget);
                        var j = jokers[index]; var def = Content.Joker(j.DefinitionId);
                        if (j.Level >= def.MaxLevel) throw new Denied(RunError.InvalidTarget);
                        Spend(checked(def.UpgradeCost * j.Level)); jokers[index] = Copy(j, level: j.Level + 1); break;
                    case RunAction.ReorderJokers:
                        BuildPhase(); if (command.Order.Count != jokers.Count || command.Order.Distinct().Count() != jokers.Count ||
                            !new HashSet<string>(command.Order).SetEquals(jokers.Select(x => x.Id))) throw new Denied(RunError.InvalidTarget);
                        jokers = command.Order.Select(id => jokers.First(x => x.Id == id)).ToList(); break;
                    case RunAction.Reroll: Require(RunPhase.Shop); Spend(Content.Definition.RerollCost); GenerateOffers(false); break;
                    default: throw new Denied(RunError.InvalidCommand);
                }
            }
            private static RunJoker Copy(RunJoker j, int? level = null, long? growth = null) =>
                new RunJoker(j.Id, j.DefinitionId, level ?? j.Level, growth ?? j.Growth, j.Charges, j.CooldownRounds, j.Memory);
            private void Start()
            {
                BuildPhase(); if (phase == RunPhase.Shop) stage++;
                if (stage >= Content.Definition.Stages.Count) throw new Denied(RunError.WrongPhase);
                phase = RunPhase.Encounter; rounds = 0; score = 0; offers.Clear();
                Accept(engine.encounter.Create(Context)); Harvest();
            }
            private void ExecuteEncounter(RunCommand<TCommand> command, ParticipantId actor)
            {
                Require(RunPhase.Encounter); if (command.EncounterCommand == null) throw new Denied(RunError.InvalidCommand);
                Accept(engine.encounter.Execute(Context, encounterState, command.EncounterCommand, actor, command.Action == RunAction.SystemEncounter)); Harvest();
            }
            private void Accept(RunEncounterTransition<TState, TEvent> result)
            {
                if (!result.Accepted) throw new Denied(RunError.EncounterRejected, result.Error);
                encounterState = result.State; Events.AddRange(result.Events);
                random = engine.encounter.ReadProgress(Context, encounterState).Random;
            }
            private void Harvest()
            {
                var progress = engine.encounter.ReadProgress(Context, encounterState);
                if (!progress.IsCheckpoint) return;
                if (progress.Score != null && !scores.Any(s => s.Id == progress.Score.Id))
                {
                    if (!progress.Score.Id.StartsWith(Context.EncounterId + "/", StringComparison.Ordinal)) throw new Denied(RunError.InvalidState);
                    Score = progress.Score; score = checked(score + Score.Total); scores.Add(Score); rounds++;
                    if (progress.Jokers.Select(j => j.Id).Distinct().Count() != progress.Jokers.Count || progress.Jokers.Any(j => jokers.All(owned => owned.Id != j.Id)))
                        throw new Denied(RunError.InvalidState);
                    jokers = jokers.Select(j => {
                        var runtime = progress.Jokers.FirstOrDefault(a => a.Id == j.Id);
                        long growth = checked(j.Growth + (progress.GrowJokers ? Content.Joker(j.DefinitionId).GrowthPerScoredRound : 0));
                        return new RunJoker(j.Id, j.DefinitionId, j.Level, growth, runtime == null ? j.Charges : runtime.Charges,
                            runtime == null ? j.CooldownRounds : runtime.CooldownRounds, runtime == null ? j.Memory : runtime.Memory);
                    }).ToList();
                    encounterState = engine.encounter.SyncBuild(Context, encounterState);
                }
                var rule = Content.Definition.Stages[stage];
                if (score >= rule.TargetScore)
                { gold = checked(gold + rule.ClearGold); phase = RunPhase.Reward; GenerateOffers(true); }
                else if (rounds >= rule.RoundLimit || progress.Exhausted) phase = RunPhase.Failed;
            }
            private void GenerateOffers(bool reward)
            {
                var pool = reward ? Content.Definition.RewardPool : Content.Definition.ShopPool;
                var shuffled = new HmacDeckShuffler().Shuffle(pool.Select(id => new CardInstanceId(id)).ToArray(), random); random = shuffled.Random;
                offers = shuffled.Cards.Take(Content.Definition.OfferCount).Select((id, i) => new RunOffer(Id((reward ? "reward/" : "shop/") + i), id.Value)).ToList();
            }
            private void EndReward()
            {
                offers.Clear();
                if (stage + 1 == Content.Definition.Stages.Count) phase = RunPhase.Completed;
                else { phase = RunPhase.Shop; GenerateOffers(false); }
            }
            private void Claim(RunCommand<TCommand> command, bool reward)
            {
                var offer = offers.FirstOrDefault(o => o.Id == command.ItemId) ?? throw new Denied(RunError.UnknownOffer);
                var definition = Content.Offer(offer.DefinitionId); if (!reward) Spend(definition.Price);
                switch (definition.Kind)
                {
                    case RunOfferKind.Joker:
                        var joker = Content.Joker(definition.JokerId);
                        if (jokers.Sum(j => Content.Joker(j.DefinitionId).Slots) + joker.Slots > Content.Definition.JokerCapacity ||
                            jokers.Count(j => j.DefinitionId == joker.Id) >= joker.MaxCopies) throw new Denied(RunError.Capacity);
                        jokers.Add(new RunJoker(EntityId("joker"), joker.Id, charges: joker.Charges)); break;
                    case RunOfferKind.Gold: gold = checked(gold + definition.Amount); break;
                    case RunOfferKind.UpgradeHand:
                        int levelIndex = levels.FindIndex(l => l.Category == definition.HandCategory.Value);
                        levels[levelIndex] = new HandLevel(definition.HandCategory.Value, checked(levels[levelIndex].Level + 1)); break;
                    default:
                        int index = deck.FindIndex(c => c.Id == command.TargetId); if (index < 0) throw new Denied(RunError.InvalidTarget);
                        var card = deck[index];
                        if (definition.Kind == RunOfferKind.RemoveCard)
                        { if (deck.Count <= engine.encounter.MinimumDeckSize) throw new Denied(RunError.InvalidTarget); deck.RemoveAt(index); }
                        else if (definition.Kind == RunOfferKind.DuplicateCard)
                        { if (deck.Count >= 512) throw new Denied(RunError.Capacity); deck.Add(new RunCard(EntityId("card"), card.DefinitionId, card.BonusPoints, card.BonusMultiplier, card.Retriggers)); }
                        else if (definition.Kind == RunOfferKind.TransformCard)
                            deck[index] = new RunCard(card.Id, definition.CardDefinition.Value, card.BonusPoints, card.BonusMultiplier, card.Retriggers);
                        else if (definition.Kind == RunOfferKind.EnhanceCard)
                            deck[index] = new RunCard(card.Id, card.DefinitionId, checked(card.BonusPoints + definition.Amount), checked(card.BonusMultiplier + definition.Multiplier), checked(card.Retriggers + definition.Retriggers));
                        else throw new Denied(RunError.InvalidCommand);
                        break;
                }
                offers.Remove(offer); if (reward) EndReward();
            }
            public RunState<TState> Build() => new RunState<TState>(source.Id, revision, source.ContentVersion, source.RulesVersion, source.ScoreVersion, source.PlayerId,
                phase, stage, gold, score, rounds, deck, jokers, levels, offers, scores, random, source.EncounterKind,
                source.EncounterRulesVersion, source.EncounterSchemaVersion, source.StartingDeckVersion, encounterState);
        }
    }
}
