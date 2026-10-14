using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    public enum PlayerBlockExpiry { Retain = 0, AfterEnemyPhase = 1 }

    /// <summary>Shared encounter rules only. No character stats, player energy or enemy attacks.</summary>
    public sealed class BattleRules
    {
        public string Version { get; }
        public IScoredHandDamageRule DamagePolicy { get; }
        public int RoundLimit { get; }
        public IAttackDamageModifier AttackDamage { get; }
        public IIncomingDamageModifier IncomingDamage { get; }
        public IDiscardRewardRule DiscardReward { get; }
        public PlayerBlockExpiry PlayerBlockExpiry { get; }
        public BattleRules(string version, IScoredHandDamageRule damagePolicy, int roundLimit,
            IAttackDamageModifier attackDamage = null, IIncomingDamageModifier incomingDamage = null, IDiscardRewardRule discardReward = null,
            PlayerBlockExpiry playerBlockExpiry = PlayerBlockExpiry.Retain)
        {
            Version = Guard.Text(version, nameof(version)); DamagePolicy = damagePolicy ?? throw new ArgumentNullException(nameof(damagePolicy));
            if (roundLimit < 1 || roundLimit > 1000) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            if (!Enum.IsDefined(typeof(PlayerBlockExpiry), playerBlockExpiry)) throw new ArgumentOutOfRangeException(nameof(playerBlockExpiry));
            RoundLimit = roundLimit;
            PlayerBlockExpiry = playerBlockExpiry;
            AttackDamage = attackDamage ?? new IdentityAttackDamageModifier();
            IncomingDamage = incomingDamage ?? new IdentityIncomingDamageModifier();
            DiscardReward = discardReward ?? new NoDiscardRewardRule();
        }
    }
    /// <summary>Immutable player-specific card and action budget, captured when entering the encounter.</summary>
    public sealed class BattlePlayerRules
    {
        public int MaxEnergy { get; }
        public int InitialEnergy { get; }
        public int PlayEnergyCost { get; }
        public int DiscardEnergyCost { get; }
        public int HandSize { get; }
        public int Discards { get; }
        public bool UnlimitedDiscards { get; }
        public int MaxPlayCards { get; }
        public int MaxDiscardCards { get; }
        // Selection envelope for views/legacy callers. Each action validates its own limit.
        public int MaxSelection => Math.Max(MaxPlayCards, MaxDiscardCards);
        public ICardDuelDealPolicy DealPolicy { get; }
        public BattlePlayerRules(int maxEnergy = 3, int initialEnergy = 3, int playEnergyCost = 1, int discardEnergyCost = 1,
            int handSize = 8, int discards = 3, ICardDuelDealPolicy dealPolicy = null,
            bool unlimitedDiscards = false, int maxPlayCards = 5, int maxDiscardCards = 5)
        {
            if (maxEnergy < 1 || initialEnergy < 0 || playEnergyCost < 1 || discardEnergyCost < 1 || handSize < 1 || handSize > 52 || discards < 0 || discards > 1000)
                throw new ArgumentException("Invalid player card/action budget.");
            // Poker classification accepts at most five submitted cards. Discard has no poker-pattern constraint.
            if (maxPlayCards < 1 || maxPlayCards > 5 || maxDiscardCards < 1 || maxDiscardCards > 52)
                throw new ArgumentException("Invalid play/discard card limit.");
            MaxEnergy = maxEnergy; InitialEnergy = initialEnergy; PlayEnergyCost = playEnergyCost; DiscardEnergyCost = discardEnergyCost;
            HandSize = handSize; Discards = discards; DealPolicy = dealPolicy ?? new CardDuelDealPolicy();
            UnlimitedDiscards = unlimitedDiscards;
            MaxPlayCards = Math.Min(maxPlayCards, handSize); MaxDiscardCards = Math.Min(maxDiscardCards, handSize);
        }
    }
    /// <summary>Encounter input. Deck entries belong to this player; no Unity or presentation references.</summary>
    public sealed class BattlePlayerSetup
    {
        public ParticipantId Id { get; }
        public CombatantDefinition Definition { get; }
        public int? InitialHealth { get; }
        public IReadOnlyList<DeckEntry> Deck { get; }
        public BattlePlayerRules Rules { get; }
        public BattlePlayerSetup(ParticipantId id, CombatantDefinition definition, IEnumerable<DeckEntry> deck = null, int? initialHealth = null, BattlePlayerRules rules = null)
        {
            if (!id.IsValid || definition == null || initialHealth <= 0 || initialHealth > definition.MaxHealth)
                throw new ArgumentException("Invalid player setup.");
            Id = id; Definition = definition; InitialHealth = initialHealth; Rules = rules;
            Deck = deck == null ? null : Guard.View(Guard.Copy(deck, nameof(deck)));
        }
    }
    public sealed class BattleEnemySetup
    {
        public ParticipantId Id { get; }
        public CombatantDefinition Definition { get; }
        public long Damage { get; }
        public BattleEnemySetup(ParticipantId id, CombatantDefinition definition, long damage)
        {
            if (!id.IsValid || definition == null || damage < 0) throw new ArgumentException("Invalid enemy setup.");
            Id = id; Definition = definition; Damage = damage;
        }
    }
    public sealed class BattlePlayerTurn
    {
        public ParticipantId Id { get; }
        public PlayerTurnState Energy { get; }
        public BattlePlayerRules Rules { get; }
        public bool HasEnded { get; }
        internal BattlePlayerTurn(ParticipantId id, PlayerTurnState energy, bool ended, BattlePlayerRules rules)
        { Id = id; Energy = energy; HasEnded = ended; Rules = rules; }
    }
    public sealed class BattleActionRecord
    {
        public ParticipantId Actor { get; }
        public ScoredDuelActionKind Kind { get; }
        public int? ScoreIndex { get; }
        internal BattleActionRecord(ParticipantId actor, ScoredDuelActionKind kind, int? scoreIndex = null)
        { Actor = actor; Kind = kind; ScoreIndex = scoreIndex; }
    }
    /// <summary>Immutable shared player phase, then an ordered enemy queue. No active-player cursor in rules.</summary>
    public sealed class TeamBattleState
    {
        internal BattleRules Rules { get; }
        public CombatState Combat { get; }
        public IReadOnlyList<BattlePlayerTurn> Players { get; }
        public IReadOnlyList<BattleEnemySetup> Enemies { get; }
        public IReadOnlyList<ParticipantId> PendingEnemies { get; }
        public IReadOnlyList<ScoreResult> Scores { get; }
        public IReadOnlyList<BattleActionRecord> Actions { get; }
        public ScoredDuelPhase Phase { get; }
        public long Revision { get; }
        public int CompletedRounds { get; }
        public bool IsFinished => Phase == ScoredDuelPhase.Won || Phase == ScoredDuelPhase.Lost;
        public long TotalScore => Scores.Aggregate(0L, (sum, score) => checked(sum + score.Total));
        // First-seat aliases retain the existing 1v1 callers. Multi-unit consumers must use IDs.
        public ParticipantId PlayerId => Players[0].Id;
        public ParticipantId EnemyId => Enemies[0].Id;
        public PlayerTurnState PlayerTurn => Players[0].Energy;
        public ParticipantId NextEnemy => PendingEnemies.FirstOrDefault(id => Combat.Get(id).IsAlive);
        public BattlePlayerTurn Player(ParticipantId id) => Players.Single(player => player.Id == id);
        public bool CanAct(ParticipantId id) => Phase == ScoredDuelPhase.AwaitingPlayerHand &&
            Players.Any(player => player.Id == id && !player.HasEnded && Combat.Get(id).IsAlive);
        internal TeamBattleState(BattleRules rules, CombatState combat, IEnumerable<BattlePlayerTurn> players,
            IEnumerable<BattleEnemySetup> enemies, ScoredDuelPhase phase, IEnumerable<ParticipantId> pending,
            long revision = 0, int rounds = 0, IEnumerable<ScoreResult> scores = null, IEnumerable<BattleActionRecord> actions = null)
        {
            Rules = rules; Combat = combat; Players = Guard.View(players.ToArray()); Enemies = Guard.View(enemies.ToArray());
            Phase = phase; PendingEnemies = Guard.View(pending.ToArray()); Revision = revision; CompletedRounds = rounds;
            Scores = Guard.View((scores ?? new ScoreResult[0]).ToArray()); Actions = Guard.View((actions ?? new BattleActionRecord[0]).ToArray());
        }
    }
    /// <summary>Pure authority-side targeting seam. UI may supply a request; policy decides the valid target set.
    /// Returning multiple living opponents enables area attacks without changing turn scheduling.</summary>
    public interface IBattleTargetPolicy
    {
        IReadOnlyList<ParticipantId> SelectTargets(BattleTargetContext context);
    }
    public sealed class BattleTargetContext
    {
        public TeamBattleState State { get; }
        public ParticipantId Source { get; }
        public IReadOnlyList<ParticipantId> Requested { get; }
        public IReadOnlyList<ParticipantId> Opponents { get; }
        internal BattleTargetContext(TeamBattleState state, ParticipantId source, IEnumerable<ParticipantId> requested)
        {
            State = state; Source = source; Requested = Guard.View((requested ?? new ParticipantId[0]).ToArray());
            string team = state.Combat.Get(source).TeamId;
            Opponents = Guard.View(state.Combat.Combatants.Where(unit => unit.IsAlive && unit.TeamId != team).Select(unit => unit.Id).ToArray());
        }
    }
    public sealed class FirstLivingOpponentPolicy : IBattleTargetPolicy
    {
        public IReadOnlyList<ParticipantId> SelectTargets(BattleTargetContext context)
        {
            if (context.Requested.Count > 1) throw new ArgumentException("This policy accepts one target.");
            return context.Requested.Count == 0 ? context.Opponents.Take(1).ToArray() : context.Requested;
        }
    }
    public sealed class TeamBattleTransition
    {
        public TeamBattleState State { get; }
        public CardDuelError Error { get; }
        public bool Accepted => Error == CardDuelError.None;
        public IReadOnlyList<CombatEvent> Events { get; }
        public DamagePlan Damage { get; }
        public DiscardRewardResolution DiscardReward { get; }
        internal TeamBattleTransition(TeamBattleState state, CardDuelError error = CardDuelError.None, IEnumerable<CombatEvent> events = null, DamagePlan damage = null, DiscardRewardResolution discardReward = null)
        { State = state; Error = error; Damage = damage; DiscardReward = discardReward; Events = Guard.View((events ?? new CombatEvent[0]).ToArray()); }
    }
    /// <summary>Combat and round authority. Cards/scoring legality belong to CardDuelEngine; animations belong to the host.
    /// Commands serialize by revision even though players share one phase.</summary>
    public sealed class TeamBattleEngine
    {
        public BattleRules Definition { get; }
        public IBattleTargetPolicy TargetPolicy { get; }
        public IBattleAllyTargetPolicy AllyTargetPolicy { get; }
        public TeamBattleEngine(BattleRules definition, IBattleTargetPolicy targetPolicy = null, IBattleAllyTargetPolicy allyTargetPolicy = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            TargetPolicy = targetPolicy ?? new FirstLivingOpponentPolicy();
            AllyTargetPolicy = allyTargetPolicy ?? new SelfOrRequestedAllyPolicy();
        }
        public TeamBattleState Create(string id, IEnumerable<BattlePlayerSetup> players, IEnumerable<BattleEnemySetup> enemies)
        {
            var p = players.ToArray(); var e = enemies.ToArray();
            if (p.Length == 0 || e.Length == 0 || p.Length + e.Length > 64 || p.Any(x => x == null) || e.Any(x => x == null) ||
                p.Select(x => x.Id).Concat(e.Select(x => x.Id)).Distinct().Count() != p.Length + e.Length)
                throw new ArgumentException("A battle needs distinct participants on both teams (at most 64 units).");
            return new TeamBattleState(Definition, new CombatState(id, 0,
                p.Select(x => x.Definition.Create(x.Id, "player", x.InitialHealth)).Concat(e.Select(x => x.Definition.Create(x.Id, "enemy")))),
                p.Select(x => {
                    var rules = x.Rules ?? new BattlePlayerRules();
                    return new BattlePlayerTurn(x.Id, new PlayerTurnState(rules.MaxEnergy, rules.InitialEnergy), false, rules);
                }),
                e, ScoredDuelPhase.AwaitingPlayerHand, new ParticipantId[0]);
        }
        public string NextScoreId(TeamBattleState state) => state.Combat.Id + "/score/" + state.Scores.Count;
        public IReadOnlyList<ParticipantId> Targets(TeamBattleState state, ParticipantId source, IEnumerable<ParticipantId> requested = null)
        {
            var context = new BattleTargetContext(state, source, requested);
            var targets = TargetPolicy.SelectTargets(context)?.ToArray();
            if (targets == null || targets.Length == 0 || targets.Length > 64 || targets.Distinct().Count() != targets.Length ||
                targets.Any(id => !context.Opponents.Contains(id))) throw new ArgumentException("Target policy must return distinct living opponents.");
            return Guard.View(targets);
        }
        public IReadOnlyList<ParticipantId> AllyTargets(TeamBattleState state, ParticipantId source, IEnumerable<ParticipantId> requested = null)
        {
            var context = new BattleAllyTargetContext(state, source, requested);
            if (!context.Allies.Contains(source) || context.Requested.Count > 64 ||
                context.Requested.Distinct().Count() != context.Requested.Count || context.Requested.Any(id => !context.Allies.Contains(id)))
                throw new ArgumentException("Support requests require distinct living allies.");
            var targets = AllyTargetPolicy.SelectTargets(context)?.ToArray();
            if (targets == null || targets.Length == 0 || targets.Length > 64 || targets.Distinct().Count() != targets.Length ||
                targets.Any(id => !context.Allies.Contains(id))) throw new ArgumentException("Support policy must return distinct living allies.");
            return Guard.View(targets);
        }
        public TeamBattleTransition Execute(TeamBattleState state, long revision, ParticipantId actor, ScoredDuelActionKind action,
            ScoreResult score = null, IEnumerable<ParticipantId> requestedTargets = null, HandAttackInfo hand = null, IEnumerable<DiscardCardSnapshot> discardedCards = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!ReferenceEquals(state.Rules, Definition)) return new TeamBattleTransition(state, CardDuelError.InvalidState);
            if (revision != state.Revision) return new TeamBattleTransition(state, CardDuelError.StaleRevision);
            if (hand != null && (action != ScoredDuelActionKind.ScoredHand || score == null || hand.Pattern.Category != score.Category))
                return new TeamBattleTransition(state, CardDuelError.InvalidDamagePlan);
            bool enemy = action == ScoredDuelActionKind.EnemyAction;
            if (state.IsFinished || !Enum.IsDefined(typeof(ScoredDuelActionKind), action) || (enemy
                ? state.Phase != ScoredDuelPhase.AwaitingEnemyAction || actor != state.NextEnemy : !state.CanAct(actor)))
                return new TeamBattleTransition(state, CardDuelError.InvalidPhase);
            int cost = action == ScoredDuelActionKind.ScoredHand ? state.Player(actor).Rules.PlayEnergyCost : action == ScoredDuelActionKind.Discard ? state.Player(actor).Rules.DiscardEnergyCost : 0;
            if (!enemy && state.Player(actor).Energy.Energy < cost) return new TeamBattleTransition(state, CardDuelError.InsufficientEnergy);
            try
            {
                var combat = state.Combat; var players = state.Players.ToArray(); var scores = state.Scores.ToArray();
                IReadOnlyList<CombatEvent> events = new CombatEvent[0]; DamagePlan damagePlan = null; DiscardRewardResolution discardReward = null;
                if (action == ScoredDuelActionKind.ScoredHand || enemy)
                {
                    if (!enemy)
                    {
                        ScoreEngine.Validate(score);
                        if (score.Id != NextScoreId(state)) return new TeamBattleTransition(state, CardDuelError.InvalidSelection);
                        checked { long total = state.TotalScore + score.Total; }
                        scores = scores.Concat(new[] { score }).ToArray();
                    }
                    var targets = Targets(state, actor, requestedTargets);
                    IEnumerable<DamageRequest> requests;
                    if (enemy) requests = targets.Select(target => new DamageRequest(actor, target, state.Enemies.Single(x => x.Id == actor).Damage));
                    else
                    {
                        var context = new ScoredHandDamageContext(combat, actor, score, targets, hand);
                        try
                        {
                            damagePlan = Definition.DamagePolicy.Resolve(context);
                            if (damagePlan == null) return new TeamBattleTransition(state, CardDuelError.InvalidDamagePlan);
                            damagePlan.Validate(context, Definition.DamagePolicy.Version);
                        }
                        catch (ArgumentException) { return new TeamBattleTransition(state, CardDuelError.InvalidDamagePlan); }
                        requests = damagePlan.Requests;
                    }
                    var effects = new List<CombatEffect>();
                    try
                    {
                        foreach (var request in requests)
                        {
                            // Attach authority-owned facts here; a damage rule cannot replace or drop the hand snapshot.
                            var attack = new AttackContext(actor, enemy ? AttackOrigin.EnemyAttack : hand != null ? AttackOrigin.ScoredHand : AttackOrigin.Other,
                                request.Amount, hand);
                            var adjusted = DamageModification.Validate(Definition.AttackDamage.Modify(
                                new DamageModifierContext(combat, request.Source, request.Target, request.Amount, attack)), request.Amount, attack);
                            effects.Add(new CombatEffect(CombatEffectKind.Damage, request.Source, request.Target, adjusted.OutputAmount, adjusted, attack));
                        }
                    }
                    catch (ArgumentException) { return new TeamBattleTransition(state, CardDuelError.InvalidDamageModifier); }
                    var hit = CombatEngine.Resolve(combat, combat.Revision, effects, Definition.IncomingDamage);
                    if (!hit.Accepted) return new TeamBattleTransition(state, hit.Error == CombatError.Overflow ? CardDuelError.Overflow :
                        hit.Error == CombatError.InvalidDamageModifier ? CardDuelError.InvalidDamageModifier : CardDuelError.InvalidTarget);
                    combat = hit.State; events = hit.Events;
                }
                if (action == ScoredDuelActionKind.Discard)
                {
                    // Validate friendly targets even for the default empty reward plan.
                    var targets = AllyTargets(state, actor, requestedTargets);
                    DiscardRewardPlan plan;
                    try
                    {
                        var context = new DiscardRewardContext(state, actor, discardedCards, targets);
                        plan = Definition.DiscardReward.Resolve(context);
                        if (plan == null) return new TeamBattleTransition(state, CardDuelError.InvalidDiscardReward);
                        plan.Validate(context, Definition.DiscardReward.Version);
                        discardReward = new DiscardRewardResolution(context, plan);
                    }
                    catch (ArgumentException) { return new TeamBattleTransition(state, CardDuelError.InvalidDiscardReward); }
                    if (plan.Effects.Count > 0)
                    {
                        var reward = CombatEngine.Resolve(combat, combat.Revision, plan.Effects);
                        if (!reward.Accepted) return new TeamBattleTransition(state, reward.Error == CombatError.Overflow ? CardDuelError.Overflow : CardDuelError.InvalidDiscardReward);
                        combat = reward.State; events = reward.Events;
                    }
                }
                if (!enemy)
                {
                    int index = Array.FindIndex(players, x => x.Id == actor);
                    int energy = action == ScoredDuelActionKind.EndPlayerTurn ? 0 : players[index].Energy.Energy - cost;
                    players[index] = new BattlePlayerTurn(actor, new PlayerTurnState(players[index].Energy.MaxEnergy, energy), energy == 0, players[index].Rules);
                }
                var pending = enemy ? state.PendingEnemies.Where(id => id != actor && combat.Get(id).IsAlive).ToArray() : state.PendingEnemies.ToArray();
                // Expire only after the entire enemy phase, including its final round. Enemy block is independent.
                if (enemy && pending.Length == 0 && Definition.PlayerBlockExpiry == PlayerBlockExpiry.AfterEnemyPhase)
                {
                    var clear = players.Select(x => combat.Get(x.Id)).Where(x => x.IsAlive && x.Block > 0)
                        .Select(x => new CombatEffect(CombatEffectKind.ClearBlock, x.Id, x.Id)).ToArray();
                    if (clear.Length > 0)
                    {
                        var expired = CombatEngine.Resolve(combat, combat.Revision, clear);
                        if (!expired.Accepted) return new TeamBattleTransition(state,
                            expired.Error == CombatError.Overflow ? CardDuelError.Overflow : CardDuelError.InvalidTarget);
                        combat = expired.State; events = events.Concat(expired.Events).ToArray();
                    }
                }
                int rounds = state.CompletedRounds;
                var phase = state.Phase;
                if (state.Enemies.All(x => !combat.Get(x.Id).IsAlive)) { phase = ScoredDuelPhase.Won; rounds++; }
                else if (players.All(x => !combat.Get(x.Id).IsAlive)) { phase = ScoredDuelPhase.Lost; rounds++; }
                else if (enemy && pending.Length == 0)
                {
                    rounds++;
                    phase = rounds >= Definition.RoundLimit ? ScoredDuelPhase.Lost : ScoredDuelPhase.AwaitingPlayerHand;
                    if (phase == ScoredDuelPhase.AwaitingPlayerHand)
                        players = players.Select(x => new BattlePlayerTurn(x.Id,
                            new PlayerTurnState(x.Energy.MaxEnergy, combat.Get(x.Id).IsAlive ? x.Energy.MaxEnergy : 0), !combat.Get(x.Id).IsAlive, x.Rules)).ToArray();
                }
                else if (!enemy && players.All(x => x.HasEnded || !combat.Get(x.Id).IsAlive))
                {
                    phase = ScoredDuelPhase.AwaitingEnemyAction;
                    pending = state.Enemies.Where(x => combat.Get(x.Id).IsAlive).Select(x => x.Id).ToArray();
                }
                if (phase == ScoredDuelPhase.Won || phase == ScoredDuelPhase.Lost) pending = new ParticipantId[0];
                return new TeamBattleTransition(new TeamBattleState(Definition, combat, players, state.Enemies, phase, pending,
                    checked(state.Revision + 1), rounds, scores, state.Actions.Concat(new[] { new BattleActionRecord(actor, action, action == ScoredDuelActionKind.ScoredHand ? (int?)state.Scores.Count : null) })), events: events, damage: damagePlan, discardReward: discardReward);
            }
            catch (OverflowException) { return new TeamBattleTransition(state, CardDuelError.Overflow); }
            catch (ArgumentException) { return new TeamBattleTransition(state, CardDuelError.InvalidTarget); }
        }
        public EncounterProgress ReadProgress(TeamBattleState state)
        {
            if (!state.IsFinished) return new EncounterProgress(EncounterPhase.AwaitingAction, state.Revision, state.CompletedRounds,
                state.Phase == ScoredDuelPhase.AwaitingEnemyAction ? state.NextEnemy : state.Players.First(x => state.CanAct(x.Id)).Id);
            bool won = state.Phase == ScoredDuelPhase.Won;
            var reason = won ? EncounterEndReason.ObjectiveReached : state.Players.All(x => !state.Combat.Get(x.Id).IsAlive)
                ? EncounterEndReason.ParticipantEliminated : EncounterEndReason.RoundLimit;
            return new EncounterProgress(EncounterPhase.Finished, state.Revision, state.CompletedRounds, result:
                new EncounterResult(state.Combat.Id, state.Revision, state.CompletedRounds, reason,
                    state.Players.Select(x => new EncounterParticipantResult(x.Id, won ? EncounterOutcome.Won : EncounterOutcome.Lost,
                        state.Actions.Where(a => a.Actor == x.Id && a.ScoreIndex.HasValue).Sum(a => state.Scores[a.ScoreIndex.Value].Total)))
                    .Concat(state.Enemies.Select(x => new EncounterParticipantResult(x.Id, won ? EncounterOutcome.Lost : EncounterOutcome.Won)))));
        }
    }
}
