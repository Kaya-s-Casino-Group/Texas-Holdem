using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    /// <summary>
    /// One ordered, authority-resolved action batch. Targets and legacy actor outgoing damage are resolved;
    /// passive amount stages run in Combat. Source rules still own validation. Empty plans produce no combat mutation or synthetic event.
    /// This plan does not select targets, repeat effects, schedule status lifecycles or spend resources.
    /// </summary>
    public sealed class BattleEffectPlan
    {
        public string RuleVersion { get; }
        public string Explanation { get; }
        public IReadOnlyList<CombatEffect> Effects { get; }

        public BattleEffectPlan(string ruleVersion, string explanation, IEnumerable<CombatEffect> effects)
        {
            RuleVersion = Guard.Text(ruleVersion, nameof(ruleVersion)); Explanation = Guard.Text(explanation, nameof(explanation));
            var copy = Guard.Copy(effects, nameof(effects));
            if (copy.Length > 64 || copy.Any(effect => effect == null)) throw new ArgumentException("Invalid battle effect plan.");
            Effects = Guard.View(copy);
        }
    }

    /// <summary>Internal settlement result. Public action transitions retain their original error mapping and reports.</summary>
    internal sealed class BattleEffectResolution
    {
        public CombatState State { get; }
        public CombatError Error { get; }
        public bool Accepted => Error == CombatError.None;
        public IReadOnlyList<CombatRuleEvent> RuleEvents { get; }
        public IReadOnlyList<CombatEvent> Events { get; }

        internal BattleEffectResolution(CombatState state, CombatError error = CombatError.None, IEnumerable<CombatRuleEvent> events = null)
        {
            State = state; Error = error;
            RuleEvents = Guard.View(Guard.Copy(events ?? Array.Empty<CombatRuleEvent>(), nameof(events)));
            Events = Guard.View(RuleEvents.OfType<CombatEvent>().ToArray());
        }
    }

    /// <summary>
    /// Common settlement for validated attack and discard plans. Nonempty plans remain one Combat batch;
    /// phase maintenance (such as block expiry) is a separate batch owned by TeamBattleEngine.
    /// </summary>
    internal static class BattleEffectResolver
    {
        internal static BattleEffectResolution Resolve(CombatState state, BattleEffectPlan plan, IIncomingDamageModifier incomingDamage = null,
            CombatStatusCatalog statusCatalog = null, ICombatReactionProvider reactionProvider = null, CombatReactionLimits reactionLimits = null,
            ICombatAmountModifier amountModifier = null)
        {
            if (state == null || plan == null) throw new ArgumentNullException();
            if (plan.Effects.Count == 0) return new BattleEffectResolution(state);
            var result = CombatEngine.Resolve(state, state.Revision, plan.Effects, incomingDamage, statusCatalog, reactionProvider, reactionLimits, amountModifier);
            return new BattleEffectResolution(result.State, result.Error, result.RuleEvents);
        }
    }
}
