using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    public enum CombatEffectKind { Damage = 1, Heal = 2, GainBlock = 3, ClearBlock = 4, ApplyStatus = 5, Eliminate = 6 }
    public enum CombatError { None, InvalidState, StaleRevision, InvalidEffect, Overflow, InvalidDamageModifier, InvalidReaction, ReactionLimitExceeded }

    /// <summary>Authority-resolved effect, not a player command. The mode validates action costs, targets and amounts first.</summary>
    public sealed class CombatEffect
    {
        public CombatEffectKind Kind { get; }
        public ParticipantId Source { get; }
        public ParticipantId Target { get; }
        public long Amount { get; }
        public DamageModification AttackModification { get; }
        public AttackContext Attack { get; }
        /// <summary>Required only for ApplyStatus. Identity, attribution, stacks and duration are authority-resolved.</summary>
        public StatusInstance Status { get; }
        public CombatEffect(CombatEffectKind kind, ParticipantId source, ParticipantId target, long amount = 0, DamageModification attackModification = null, AttackContext attack = null, StatusInstance status = null)
        {
            if (!Enum.IsDefined(typeof(CombatEffectKind), kind) || !source.IsValid || !target.IsValid || amount < 0 ||
                (kind == CombatEffectKind.ClearBlock || kind == CombatEffectKind.ApplyStatus || kind == CombatEffectKind.Eliminate) && amount != 0)
                throw new ArgumentException("Invalid combat effect.");
            if ((kind == CombatEffectKind.ApplyStatus) != (status != null))
                throw new ArgumentException("Only ApplyStatus requires a status payload.");
            if (status != null && (status.Source == null || status.Target == null ||
                status.Source.Kind != EntityKind.Participant || status.Source.Id != source.Value ||
                status.Target.Kind != EntityKind.Participant || status.Target.Id != target.Value))
                throw new ArgumentException("Status attribution must match the effect's source and target participants.");
            if (attackModification != null && (kind != CombatEffectKind.Damage || attackModification.OutputAmount != amount))
                throw new ArgumentException("Attack report must match the outgoing damage.");
            if (kind != CombatEffectKind.Damage && attack != null) throw new ArgumentException("Only damage effects carry attack facts.");
            Attack = kind == CombatEffectKind.Damage ? attack ?? new AttackContext(source, AttackOrigin.Other, attackModification?.InputAmount ?? amount) : null;
            if (Attack != null && (Attack.Source != source || Attack.BaseDamage != (attackModification?.InputAmount ?? amount)))
                throw new ArgumentException("Attack facts must match the original damage request.");
            if (attackModification != null) DamageModification.Validate(attackModification, Attack.BaseDamage, Attack);
            Kind = kind; Source = source; Target = target; Amount = amount; AttackModification = attackModification; Status = status;
        }
        public static CombatEffect ApplyStatus(StatusInstance status)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            if (status.Source == null || status.Target == null || status.Source.Kind != EntityKind.Participant || status.Target.Kind != EntityKind.Participant)
                throw new ArgumentException("Combat status effects require participant references.");
            return new CombatEffect(CombatEffectKind.ApplyStatus, new ParticipantId(status.Source.Id), new ParticipantId(status.Target.Id), status: status);
        }
        public static CombatEffect Eliminate(ParticipantId source, ParticipantId target) => new CombatEffect(CombatEffectKind.Eliminate, source, target);
    }
    public sealed class CombatEvent : CombatRuleEvent
    {
        public int BlockAbsorbed { get; }
        /// <summary>Actual health decrease, including elimination. Damage reactions must use DamageToHealth instead.</summary>
        public int HealthLost { get; }
        public int DamageToHealth => Effect.Kind == CombatEffectKind.Damage ? HealthLost : 0;
        public int HealthRestored { get; }
        /// <summary>Accepted healing that exceeded missing health. Skipped heals produce no fact or overheal.</summary>
        public long Overheal => Effect.Kind == CombatEffectKind.Heal ? Effect.Amount - HealthRestored : 0;
        public long Overkill { get; }
        public DamageModification IncomingModification { get; }
        public long SettledDamage => IncomingModification?.OutputAmount ?? (Effect.Kind == CombatEffectKind.Damage ? Effect.Amount : 0);
        public CombatantState Before { get; }
        public CombatantState After { get; }
        public bool Died => Before.IsAlive && !After.IsAlive;
        public CombatDeathCause DeathCause => !Died ? CombatDeathCause.None :
            Effect.Kind == CombatEffectKind.Eliminate ? CombatDeathCause.Eliminate : CombatDeathCause.Damage;
        internal CombatEvent(long revision, int index, CombatEffect effect, CombatantState before, CombatantState after, int absorbed, long overkill,
            DamageModification incomingModification = null, int sequenceIndex = -1, CombatEffectProvenance provenance = null)
            : base(revision, index, effect, sequenceIndex, provenance)
        {
            IncomingModification = incomingModification; Before = before; After = after; BlockAbsorbed = absorbed; Overkill = overkill;
            HealthLost = Math.Max(0, before.Health - after.Health); HealthRestored = Math.Max(0, after.Health - before.Health);
        }
    }
    public sealed class CombatTransition
    {
        public CombatState State { get; }
        public CombatError Error { get; }
        public bool Accepted => Error == CombatError.None;
        public IReadOnlyList<CombatRuleEvent> RuleEvents { get; }
        /// <summary>Compatibility projection of vitals events; Index still identifies the original effect position.</summary>
        public IReadOnlyList<CombatEvent> Events { get; }
        internal CombatTransition(CombatState state, CombatError error = CombatError.None, IEnumerable<CombatRuleEvent> events = null)
        {
            State = state; Error = error;
            RuleEvents = Guard.View(Guard.Copy(events ?? Array.Empty<CombatRuleEvent>(), nameof(events)));
            Events = Guard.View(RuleEvents.OfType<CombatEvent>().ToArray());
        }
    }

    /// <summary>Shared deterministic authority resolver. No turn, AI, card score, faction targeting or reward policy.</summary>
    public static class CombatEngine
    {
        public const string RulesVersion = "combat.6";
        public static CombatTransition Resolve(CombatState state, long expectedRevision, IEnumerable<CombatEffect> effects, IIncomingDamageModifier incomingDamage = null,
            CombatStatusCatalog statusCatalog = null, ICombatReactionProvider reactionProvider = null, CombatReactionLimits reactionLimits = null)
        {
            if (state == null || effects == null) throw new ArgumentNullException();
            try { Validate(state); CombatStatusRules.ValidateCatalog(state, statusCatalog); }
            catch (ArgumentException) { return new CombatTransition(state, CombatError.InvalidState); }
            if (state.Revision != expectedRevision) return new CombatTransition(state, CombatError.StaleRevision);
            incomingDamage = incomingDamage ?? new IdentityIncomingDamageModifier();
            reactionLimits = reactionLimits ?? new CombatReactionLimits();
            try
            {
                var batch = Guard.Copy(effects, nameof(effects));
                if (batch.Length < 1 || batch.Length > 64) throw new ArgumentException("Resolve between 1 and 64 effects atomically.");
                var units = state.Combatants.ToDictionary(unit => unit.Id); var events = new List<CombatRuleEvent>();
                var statuses = state.Statuses.ToList();
                var unitIds = new HashSet<ParticipantId>(units.Keys);
                // Validate the entire primary declaration against the action-start state. Later deaths cancel
                // active effects, but must never hide an initially dead source or an unknown participant.
                foreach (var effect in batch)
                    if (effect == null || !units.ContainsKey(effect.Source) || !units.ContainsKey(effect.Target) || !units[effect.Source].IsAlive)
                        throw new ArgumentException("Primary effects require known units and an initially living source.");
                long revision = checked(state.Revision + 1);
                var queue = new Stack<QueuedEffect>();
                for (int index = batch.Length - 1; index >= 0; index--)
                    queue.Push(new QueuedEffect(batch[index], new CombatEffectProvenance(index)));
                int reactionCount = 0;
                while (queue.Count > 0)
                {
                    var queued = queue.Pop(); var effect = queued.Effect; var provenance = queued.Provenance;
                    int effectIndex = provenance.RootEffectIndex;
                    // Identities are checked before enqueueing. Death is a normal settlement result, not an error.
                    if (!units[effect.Source].IsAlive && provenance.SourcePolicy == CombatReactionSourcePolicy.RequireLiving) continue;
                    if (!units[effect.Target].IsAlive) continue;
                    int firstEvent = events.Count;
                    var before = units[effect.Target]; int health = before.Health, block = before.Block, absorbed = 0; long overkill = 0;
                    DamageModification incoming = null;
                    try
                    {
                        switch (effect.Kind)
                        {
                            case CombatEffectKind.Damage:
                                try
                                {
                                    // Earlier primary and derived effects are visible to target-side modifiers.
                                    var current = new CombatState(state.Id, state.Revision, state.Combatants.Select(unit => units[unit.Id]), statuses);
                                    incoming = DamageModification.Validate(incomingDamage.Modify(new DamageModifierContext(current,
                                        effect.Source, effect.Target, effect.Amount, effect.Attack,
                                        provenance.SourcePolicy == CombatReactionSourcePolicy.AllowDefeated)), effect.Amount, effect.Attack);
                                }
                                catch (ArgumentException) { return new CombatTransition(state, CombatError.InvalidDamageModifier); }
                                absorbed = (int)Math.Min(incoming.OutputAmount, block); block -= absorbed;
                                long remaining = incoming.OutputAmount - absorbed; int lost = (int)Math.Min(remaining, health);
                                health -= lost; overkill = remaining - lost; break;
                            case CombatEffectKind.Heal: health += (int)Math.Min(effect.Amount, before.MaxHealth - health); break;
                            case CombatEffectKind.GainBlock: block = checked(block + checked((int)effect.Amount)); break;
                            case CombatEffectKind.ClearBlock: block = 0; break;
                            case CombatEffectKind.ApplyStatus:
                                ValidateStatus(effect.Status, state, unitIds);
                                CombatStatusRules.Apply(state, statuses, effect, statusCatalog, revision, effectIndex, events, provenance);
                                break;
                            case CombatEffectKind.Eliminate: health = 0; break;
                            default: throw new ArgumentException("Unknown combat effect.");
                        }
                        if (effect.Kind != CombatEffectKind.ApplyStatus)
                        {
                            var after = before.WithVitals(health, block); units[after.Id] = after;
                            events.Add(new CombatEvent(revision, effectIndex, effect, before, after, absorbed, overkill, incoming, events.Count, provenance));
                        }
                    }
                    catch (ArgumentException)
                    { return new CombatTransition(state, provenance.Depth == 0 ? CombatError.InvalidEffect : CombatError.InvalidReaction); }

                    if (reactionProvider == null || !provenance.CanTriggerReactions) continue;
                    // Complete all writes/facts for this effect before taking one reaction snapshot. For example,
                    // an immunity Blocked fact and its Consumed fact both see the already-consumed charge.
                    var settled = new CombatState(state.Id, state.Revision, state.Combatants.Select(unit => units[unit.Id]), statuses);
                    var derived = new List<QueuedEffect>();
                    try
                    {
                        for (int eventIndex = firstEvent; eventIndex < events.Count; eventIndex++)
                        {
                            var parent = events[eventIndex];
                            var reactions = reactionProvider.React(new CombatReactionContext(settled, parent));
                            if (reactions == null) return new CombatTransition(state, CombatError.InvalidReaction);
                            // Bound enumeration before materializing. Even skipped reactions spend the budget.
                            foreach (var reaction in reactions)
                            {
                                if (++reactionCount > reactionLimits.MaxEffects || provenance.Depth >= reactionLimits.MaxDepth)
                                    return new CombatTransition(state, CombatError.ReactionLimitExceeded);
                                if (reaction == null || !unitIds.Contains(reaction.Owner) ||
                                    !unitIds.Contains(reaction.Effect.Source) || !unitIds.Contains(reaction.Effect.Target))
                                    return new CombatTransition(state, CombatError.InvalidReaction);
                                derived.Add(new QueuedEffect(reaction.Effect, new CombatEffectProvenance(reaction, parent)));
                            }
                        }
                    }
                    catch (ArgumentException) { return new CombatTransition(state, CombatError.InvalidReaction); }
                    catch (InvalidOperationException) { return new CombatTransition(state, CombatError.InvalidReaction); }
                    // Depth-first immediate reactions: A -> A's descendants -> B -> next primary effect.
                    // Reverse insertion preserves the rule's stable sibling order without recursive calls.
                    for (int index = derived.Count - 1; index >= 0; index--) queue.Push(derived[index]);
                }
                return new CombatTransition(new CombatState(state.Id, revision, state.Combatants.Select(unit => units[unit.Id]), statuses), events: events);
            }
            catch (ArgumentException) { return new CombatTransition(state, CombatError.InvalidEffect); }
            catch (OverflowException) { return new CombatTransition(state, CombatError.Overflow); }
        }
        private sealed class QueuedEffect
        {
            internal CombatEffect Effect { get; }
            internal CombatEffectProvenance Provenance { get; }
            internal QueuedEffect(CombatEffect effect, CombatEffectProvenance provenance)
            { Effect = effect; Provenance = provenance; }
        }
        public static void Validate(CombatState state)
        {
            try
            {
                if (state == null || string.IsNullOrWhiteSpace(state.Id) || state.Revision < 0 || state.RulesVersion != RulesVersion ||
                    state.Combatants.Count < 1 || state.Combatants.Count > 64 || state.Statuses.Count > 256)
                    throw new ArgumentException("Invalid combat state.");
                var ids = new HashSet<ParticipantId>();
                foreach (var unit in state.Combatants)
                {
                    if (unit == null || !ids.Add(unit.Id)) throw new ArgumentException("Missing or duplicate combatant.");
                    new CombatantState(unit.Id, unit.DefinitionId, unit.TeamId, unit.MaxHealth, unit.Health, unit.Block);
                }
                var statuses = new HashSet<StatusInstanceId>();
                foreach (var status in state.Statuses)
                {
                    if (status == null || !statuses.Add(status.Id)) throw new ArgumentException("Missing or duplicate combat status.");
                    ValidateStatus(status, state, ids);
                }
            }
            catch (NullReferenceException error) { throw new ArgumentException("Missing combat state data.", error); }
        }
        private static void ValidateStatus(StatusInstance status, CombatState state, HashSet<ParticipantId> ids)
        {
            if (status == null) throw new ArgumentException("Missing status payload.");
            new StatusInstance(status.Id, status.DefinitionId, status.Source, status.Target, status.Stacks, status.Duration);
            ValidateEntity(status.Source, state, ids); ValidateEntity(status.Target, state, ids);
            var d = status.Duration;
            switch (d.Kind)
            {
                case DurationKind.UntilRemoved:
                    if (d.ScopeId != null || d.PhaseKey != null || d.RemainingTriggers.HasValue) throw new ArgumentException("Invalid unbounded status."); break;
                case DurationKind.UntilRoundEnd:
                    if (string.IsNullOrWhiteSpace(d.ScopeId) || d.PhaseKey != null || d.RemainingTriggers.HasValue) throw new ArgumentException("Invalid round status."); break;
                case DurationKind.UntilPhaseEnd:
                    if (string.IsNullOrWhiteSpace(d.ScopeId) || string.IsNullOrWhiteSpace(d.PhaseKey) || d.RemainingTriggers.HasValue) throw new ArgumentException("Invalid phase status."); break;
                case DurationKind.TriggerCount:
                    if (!d.RemainingTriggers.HasValue || d.RemainingTriggers <= 0 || d.ScopeId != null || d.PhaseKey != null) throw new ArgumentException("Invalid trigger status."); break;
                default: throw new ArgumentException("Unknown duration.");
            }
        }
        private static void ValidateEntity(EntityRef reference, CombatState state, HashSet<ParticipantId> ids)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.Id) ||
                !(reference.Kind == EntityKind.Match && reference.Id == state.Id || reference.Kind == EntityKind.Participant && ids.Contains(new ParticipantId(reference.Id))))
                throw new ArgumentException("This combat state supports match and participant status references only.");
        }
    }
}
