namespace PokerEngine
{
    /// <summary>An immutable fact in a Combat batch. Index is the original effect position; skipped targets leave gaps.</summary>
    public abstract class CombatRuleEvent
    {
        public long Revision { get; }
        public int Index { get; }
        public CombatEffect Effect { get; }

        internal CombatRuleEvent(long revision, int index, CombatEffect effect)
        { Revision = revision; Index = index; Effect = effect; }
    }

    /// <summary>A newly applied status. It is not a zero-damage or other vitals event.</summary>
    public sealed class CombatStatusEvent : CombatRuleEvent
    {
        public StatusInstance Status => Effect.Status;

        internal CombatStatusEvent(long revision, int index, CombatEffect effect) : base(revision, index, effect) { }
    }

    public enum CombatDeathCause { None = 0, Damage = 1, Eliminate = 2 }
}
