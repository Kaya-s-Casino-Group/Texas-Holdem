using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    public enum CombatEffectKind { Damage = 1, Heal, GainBlock, ClearBlock }
    public enum CombatError { None, InvalidState, StaleRevision, InvalidEffect, Overflow, InvalidDamageModifier }

    /// <summary>Authority-resolved effect, not a player command. The mode validates action costs, targets and amounts first.</summary>
    public sealed class CombatEffect
    {
        public CombatEffectKind Kind { get; }
        public ParticipantId Source { get; }
        public ParticipantId Target { get; }
        public long Amount { get; }
        public DamageModification AttackModification { get; }
        public AttackContext Attack { get; }
        public CombatEffect(CombatEffectKind kind, ParticipantId source, ParticipantId target, long amount = 0, DamageModification attackModification = null, AttackContext attack = null)
        {
            if (!Enum.IsDefined(typeof(CombatEffectKind), kind) || !source.IsValid || !target.IsValid || amount < 0 || kind == CombatEffectKind.ClearBlock && amount != 0)
                throw new ArgumentException("Invalid combat effect.");
            if (attackModification != null && (kind != CombatEffectKind.Damage || attackModification.OutputAmount != amount))
                throw new ArgumentException("Attack report must match the outgoing damage.");
            if (kind != CombatEffectKind.Damage && attack != null) throw new ArgumentException("Only damage effects carry attack facts.");
            Attack = kind == CombatEffectKind.Damage ? attack ?? new AttackContext(source, AttackOrigin.Other, attackModification?.InputAmount ?? amount) : null;
            if (Attack != null && (Attack.Source != source || Attack.BaseDamage != (attackModification?.InputAmount ?? amount)))
                throw new ArgumentException("Attack facts must match the original damage request.");
            if (attackModification != null) DamageModification.Validate(attackModification, Attack.BaseDamage, Attack);
            Kind = kind; Source = source; Target = target; Amount = amount; AttackModification = attackModification;
        }
    }
    public sealed class CombatEvent
    {
        public long Revision { get; }
        public int Index { get; }
        public CombatEffect Effect { get; }
        public int BlockAbsorbed { get; }
        public int HealthLost { get; }
        public int HealthRestored { get; }
        public long Overkill { get; }
        public DamageModification IncomingModification { get; }
        public long SettledDamage => IncomingModification?.OutputAmount ?? (Effect.Kind == CombatEffectKind.Damage ? Effect.Amount : 0);
        public CombatantState Before { get; }
        public CombatantState After { get; }
        public bool Died => Before.IsAlive && !After.IsAlive;
        internal CombatEvent(long revision, int index, CombatEffect effect, CombatantState before, CombatantState after, int absorbed, long overkill, DamageModification incomingModification = null)
        {
            IncomingModification = incomingModification; Revision = revision; Index = index; Effect = effect; Before = before; After = after; BlockAbsorbed = absorbed; Overkill = overkill;
            HealthLost = Math.Max(0, before.Health - after.Health); HealthRestored = Math.Max(0, after.Health - before.Health);
        }
    }
    public sealed class CombatTransition
    {
        public CombatState State { get; }
        public CombatError Error { get; }
        public bool Accepted => Error == CombatError.None;
        public IReadOnlyList<CombatEvent> Events { get; }
        internal CombatTransition(CombatState state, CombatError error = CombatError.None, IEnumerable<CombatEvent> events = null)
        { State = state; Error = error; Events = Guard.View(Guard.Copy(events ?? new CombatEvent[0], nameof(events))); }
    }

    /// <summary>Shared deterministic authority resolver. No turn, AI, card score, faction targeting or reward policy.</summary>
    public static class CombatEngine
    {
        public const string RulesVersion = "combat.2";
        public static CombatTransition Resolve(CombatState state, long expectedRevision, IEnumerable<CombatEffect> effects, IIncomingDamageModifier incomingDamage = null)
        {
            if (state == null || effects == null) throw new ArgumentNullException();
            try { Validate(state); } catch (ArgumentException) { return new CombatTransition(state, CombatError.InvalidState); }
            if (state.Revision != expectedRevision) return new CombatTransition(state, CombatError.StaleRevision);
            incomingDamage = incomingDamage ?? new IdentityIncomingDamageModifier();
            try
            {
                var batch = Guard.Copy(effects, nameof(effects));
                if (batch.Length < 1 || batch.Length > 64) throw new ArgumentException("Resolve between 1 and 64 effects atomically.");
                var units = state.Combatants.ToDictionary(unit => unit.Id); var events = new List<CombatEvent>();
                long revision = checked(state.Revision + 1);
                foreach (var effect in batch)
                {
                    if (effect == null || !units.ContainsKey(effect.Source) || !units.ContainsKey(effect.Target) ||
                        !units[effect.Source].IsAlive || !units[effect.Target].IsAlive) throw new ArgumentException("Effects require living known units.");
                    var before = units[effect.Target]; int health = before.Health, block = before.Block, absorbed = 0; long overkill = 0;
                    DamageModification incoming = null;
                    switch (effect.Kind)
                    {
                        case CombatEffectKind.Damage:
                            try
                            {
                                // Earlier effects in this atomic batch are visible to the next target-side modifier.
                                var current = new CombatState(state.Id, state.Revision, state.Combatants.Select(unit => units[unit.Id]), state.Statuses);
                                incoming = DamageModification.Validate(incomingDamage.Modify(
                                    new DamageModifierContext(current, effect.Source, effect.Target, effect.Amount, effect.Attack)), effect.Amount, effect.Attack);
                            }
                            catch (ArgumentException) { return new CombatTransition(state, CombatError.InvalidDamageModifier); }
                            absorbed = (int)Math.Min(incoming.OutputAmount, block); block -= absorbed;
                            long remaining = incoming.OutputAmount - absorbed; int lost = (int)Math.Min(remaining, health);
                            health -= lost; overkill = remaining - lost; break;
                        case CombatEffectKind.Heal: health += (int)Math.Min(effect.Amount, before.MaxHealth - health); break;
                        case CombatEffectKind.GainBlock: block = checked(block + checked((int)effect.Amount)); break;
                        case CombatEffectKind.ClearBlock: block = 0; break;
                        default: throw new ArgumentException("Unknown combat effect.");
                    }
                    var after = before.WithVitals(health, block); units[after.Id] = after;
                    events.Add(new CombatEvent(revision, events.Count, effect, before, after, absorbed, overkill, incoming));
                }
                return new CombatTransition(new CombatState(state.Id, revision, state.Combatants.Select(unit => units[unit.Id]), state.Statuses), events: events);
            }
            catch (ArgumentException) { return new CombatTransition(state, CombatError.InvalidEffect); }
            catch (OverflowException) { return new CombatTransition(state, CombatError.Overflow); }
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
            }
            catch (NullReferenceException error) { throw new ArgumentException("Missing combat state data.", error); }
        }
        private static void ValidateEntity(EntityRef reference, CombatState state, HashSet<ParticipantId> ids)
        {
            if (reference == null || string.IsNullOrWhiteSpace(reference.Id) ||
                !(reference.Kind == EntityKind.Match && reference.Id == state.Id || reference.Kind == EntityKind.Participant && ids.Contains(new ParticipantId(reference.Id))))
                throw new ArgumentException("This combat state supports match and participant status references only.");
        }
    }
}
