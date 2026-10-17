using System;
using System.Collections.Generic;
using System.Linq;

namespace PokerEngine
{
    /// <summary>Unbound status data. The authority creates a separate instance and attribution for each resolved target.</summary>
    public sealed class StatusApplicationSpec
    {
        public StatusDefinitionId DefinitionId { get; }
        public int Stacks { get; }
        public StatusDuration Duration { get; }
        public StatusApplicationSpec(StatusDefinitionId definitionId, int stacks, StatusDuration duration)
        {
            if (!definitionId.IsValid || stacks <= 0) throw new ArgumentException("Invalid status application specification.");
            DefinitionId = definitionId; Stacks = stacks; Duration = duration ?? throw new ArgumentNullException(nameof(duration));
        }
    }

    /// <summary>What an effect does, independent of source, target count, targeting and attack reports.</summary>
    public sealed class BattleEffectSpec
    {
        public CombatEffectKind Kind { get; }
        public long Amount { get; }
        public StatusApplicationSpec Status { get; }
        private BattleEffectSpec(CombatEffectKind kind, long amount = 0, StatusApplicationSpec status = null)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Kind = kind; Amount = amount; Status = status;
        }
        public static BattleEffectSpec Damage(long amount) => new BattleEffectSpec(CombatEffectKind.Damage, amount);
        public static BattleEffectSpec Heal(long amount) => new BattleEffectSpec(CombatEffectKind.Heal, amount);
        public static BattleEffectSpec GainBlock(long amount) => new BattleEffectSpec(CombatEffectKind.GainBlock, amount);
        public static BattleEffectSpec ApplyStatus(StatusApplicationSpec status) =>
            new BattleEffectSpec(CombatEffectKind.ApplyStatus, status: status ?? throw new ArgumentNullException(nameof(status)));
        public static BattleEffectSpec Eliminate() => new BattleEffectSpec(CombatEffectKind.Eliminate);
    }

    public sealed class BattleEffectStep
    {
        public BattleEffectSpec Effect { get; }
        public EffectTargetSpec Target { get; }
        public BattleEffectStep(BattleEffectSpec effect, EffectTargetSpec target)
        { Effect = effect ?? throw new ArgumentNullException(nameof(effect)); Target = target ?? throw new ArgumentNullException(nameof(target)); }
    }

    /// <summary>Ordered declarations before targeting. All steps bind against the same action-start snapshot.</summary>
    public sealed class BattleEffectSequence
    {
        public string RuleVersion { get; }
        public string Explanation { get; }
        public IReadOnlyList<BattleEffectStep> Steps { get; }
        public BattleEffectSequence(string ruleVersion, string explanation, IEnumerable<BattleEffectStep> steps)
        {
            RuleVersion = Guard.Text(ruleVersion, nameof(ruleVersion)); Explanation = Guard.Text(explanation, nameof(explanation));
            var copy = Guard.Copy(steps, nameof(steps));
            if (copy.Length == 0 || copy.Length > 64 || copy.Any(step => step == null)) throw new ArgumentException("Invalid effect sequence.");
            Steps = Guard.View(copy);
        }
    }
}
